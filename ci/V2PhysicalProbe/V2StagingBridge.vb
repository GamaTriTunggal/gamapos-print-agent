' P-604 / DR-08: jalur HTTP sementara untuk batch cetakan staging terbatas di PC uji.
' D-024 adendum 8 Okt 2026: batch kasbon (nota kasbon v2, tiap nota satu asli + satu cetak ulang).
' Tidak menjalankan Program.Main agent, mengubah pemetaan, atau menyimpan payload.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Friend Class BridgeReply
    Friend ReadOnly Status As Integer
    Friend ReadOnly Body As String
    Friend Sub New(status As Integer, body As String)
        Me.Status = status
        Me.Body = body
    End Sub
End Class

Friend Class V2StagingBridge
    Friend Const StagingOrigin As String = "https://staging.gamapos.id"
    Friend Const MaxBodyBytes As Integer = 1048576
    Private ReadOnly _parser As Func(Of String, JObject)
    Private ReadOnly _render As Action(Of String)
    Private ReadOnly _printerReady As Func(Of Boolean)
    Private ReadOnly _edcSplitBatch As Boolean
    Private ReadOnly _receivableBatch As Boolean
    Private ReadOnly _kasbonBatch As Boolean
    Private ReadOnly _version As String
    Private ReadOnly _now As Func(Of DateTime)
    Private ReadOnly _expires As DateTime
    Private ReadOnly _jobs As New Dictionary(Of String, KeyValuePair(Of String, Boolean))(StringComparer.Ordinal)
    ' Batch piutang/kasbon: snapshot asli per transaksi (piutang) atau per nota (kasbon;
    ' kasbon DP0 tidak punya nomor transaksi kas) dan yang sudah dicetak ulang.
    Private ReadOnly _batchOriginals As New Dictionary(Of String, JObject)(StringComparer.Ordinal)
    Private ReadOnly _batchCopies As New HashSet(Of String)(StringComparer.Ordinal)
    Private _snapshot As JObject
    Private _uncertain As Boolean
    Private _printing As Integer
    Private _closed As Integer

    Friend Sub CloseSession()
        SyncLock _jobs
            Volatile.Write(_closed, 1)
        End SyncLock
    End Sub

    Friend ReadOnly Property IsPrinting As Boolean
        Get
            Return Volatile.Read(_printing) <> 0
        End Get
    End Property

    Friend Sub New(parser As Func(Of String, JObject), render As Action(Of String), printerReady As Func(Of Boolean),
                   version As String, Optional now As Func(Of DateTime) = Nothing, Optional edcSplitBatch As Boolean = False,
                   Optional receivableBatch As Boolean = False, Optional kasbonBatch As Boolean = False)
        If (If(edcSplitBatch, 1, 0) + If(receivableBatch, 1, 0) + If(kasbonBatch, 1, 0)) > 1 Then Throw New ArgumentException("Satu sesi uji hanya satu batch.")
        _parser = parser
        _render = render
        _printerReady = printerReady
        _version = version
        _edcSplitBatch = edcSplitBatch
        _receivableBatch = receivableBatch
        _kasbonBatch = kasbonBatch
        _now = If(now, Function() DateTime.UtcNow)
        _expires = _now().AddMinutes(30)
    End Sub

    Friend Shared Function OriginAllowed(origin As String) As Boolean
        Return String.Equals(origin, StagingOrigin, StringComparison.Ordinal)
    End Function

    Friend Function Handle(method As String, path As String, origin As String, contentType As String,
                           body As String, isLoopback As Boolean) As BridgeReply
        If Not isLoopback Then Return Failure(403, "LOOPBACK_ONLY")
        If Volatile.Read(_closed) <> 0 Then Return Failure(410, "TEST_SESSION_ENDED")
        If Not String.IsNullOrEmpty(origin) AndAlso Not OriginAllowed(origin) Then Return Failure(403, "FORBIDDEN_ORIGIN")
        If _now() >= _expires Then Return Failure(410, "TEST_SESSION_EXPIRED")
        If method = "OPTIONS" AndAlso (path = "/health" OrElse path = "/print") Then
            If Not OriginAllowed(origin) Then Return Failure(403, "FORBIDDEN_ORIGIN")
            Return New BridgeReply(204, "")
        End If
        If method = "GET" AndAlso path = "/health" Then
            If _receivableBatch Then
                Return New BridgeReply(200, JsonConvert.SerializeObject(New With {
                    .ok = True, .agentVersion = _version, .schemaVersion = 1, .mode = "staging-test",
                    .testBatch = "receivable", .supportedPrintSchemas = New Integer() {2},
                    .supportedPrintJobTypesV2 = ReceivableJobTypes}))
            End If
            If _kasbonBatch Then
                Return New BridgeReply(200, JsonConvert.SerializeObject(New With {
                    .ok = True, .agentVersion = _version, .schemaVersion = 1, .mode = "staging-test",
                    .testBatch = "kasbon", .supportedPrintSchemas = New Integer() {2},
                    .supportedPrintJobTypesV2 = New String() {"kasbon_receipt"}}))
            End If
            If _edcSplitBatch Then
                Return New BridgeReply(200, JsonConvert.SerializeObject(New With {
                    .ok = True, .agentVersion = _version, .schemaVersion = 1, .mode = "staging-test",
                    .testBatch = "edc-split", .supportedPrintSchemas = New Integer() {2},
                    .supportedPrintJobTypesV2 = New String() {"cashier_receipt", "split_receipt"}}))
            End If
            Return New BridgeReply(200, JsonConvert.SerializeObject(New With {
                .ok = True, .agentVersion = _version, .schemaVersion = 1, .mode = "staging-test",
                .supportedPrintSchemas = New Integer() {2},
                .supportedPrintJobTypesV2 = New String() {"cashier_receipt"}}))
        End If
        If method <> "POST" OrElse path <> "/print" Then Return Failure(404, "NOT_FOUND")
        If Not OriginAllowed(origin) Then Return Failure(403, "FORBIDDEN_ORIGIN")
        If contentType Is Nothing OrElse Not String.Equals(contentType.Split(";"c)(0).Trim(), "application/json", StringComparison.OrdinalIgnoreCase) Then
            Return Failure(415, "JSON_REQUIRED")
        End If
        If body Is Nothing OrElse Encoding.UTF8.GetByteCount(body) > MaxBodyBytes Then Return Failure(413, "PAYLOAD_TOO_LARGE")
        Dim root As JObject
        Try
            root = _parser(body)
        Catch
            Return Failure(400, "BAD_PAYLOAD")
        End Try
        If _receivableBatch Then
            If Array.IndexOf(ReceivableJobTypes, CStr(root("jobType"))) < 0 Then Return Failure(422, "TEST_RECEIVABLE_ONLY")
        ElseIf _kasbonBatch Then
            If CStr(root("jobType")) <> "kasbon_receipt" OrElse CStr(root("payload")("paymentMethod")) <> "CREDIT" Then
                Return Failure(422, "TEST_KASBON_ONLY")
            End If
        ElseIf _edcSplitBatch Then
            Dim kind = CStr(root("jobType"))
            Dim payment = CStr(root("payload")("paymentMethod"))
            If CStr(root("payload")("noncashMethod")) <> "EDC" OrElse
               Not ((kind = "cashier_receipt" AndAlso payment = "EDC") OrElse
                    (kind = "split_receipt" AndAlso payment = "SPLIT")) Then
                Return Failure(422, "TEST_EDC_SPLIT_ONLY")
            End If
        ElseIf CStr(root("jobType")) <> "cashier_receipt" OrElse CStr(root("payload")("paymentMethod")) <> "CASH" Then
            Return Failure(422, "TEST_CASHIER_CASH_ONLY")
        End If
        ' Kasbon tidak boleh berdiskon (D-024); batch kasbon menguji pembulatan/DP, bukan diskon.
        Dim perReceipt As Boolean = _receivableBatch OrElse _kasbonBatch
        If Not perReceipt AndAlso CStr(root("payload")("amounts")("discountSen")) = "0" Then Return Failure(422, "TEST_DISCOUNT_REQUIRED")
        Dim jobId As String = CStr(root("jobId"))
        Dim fingerprint As String
        Using hash As SHA256 = SHA256.Create()
            fingerprint = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(body)))
        End Using
        SyncLock _jobs
            If Volatile.Read(_closed) <> 0 Then Return Failure(410, "TEST_SESSION_ENDED")
            If _now() >= _expires Then Return Failure(410, "TEST_SESSION_EXPIRED")
            Dim previous As New KeyValuePair(Of String, Boolean)()
            If _jobs.TryGetValue(jobId, previous) Then
                If previous.Key <> fingerprint Then Return Failure(409, "JOB_ID_CONFLICT")
                If Not previous.Value Then Return Failure(409, "PRINT_OUTCOME_UNKNOWN")
                Return New BridgeReply(200, "{""ok"":true,""duplicate"":true}")
            End If
            If _uncertain Then Return Failure(409, "PRINT_OUTCOME_UNKNOWN")
            If _jobs.Count >= If(perReceipt, 6, If(_edcSplitBatch, 4, 2)) Then Return Failure(409, "TEST_PRINT_LIMIT")
            Dim copy As JObject = TryCast(root("payload")("reprint"), JObject)
            Dim snapshot As JObject = SnapshotFor(root)
            Dim originalRequired = _jobs.Count = 0 OrElse (_edcSplitBatch AndAlso _jobs.Count = 2)
            Dim batchKey As String = If(_kasbonBatch, CStr(snapshot("payload")("receiptNo")), CStr(snapshot("payload")("transactionId")))
            If perReceipt Then
                ' Maksimal tiga pembayaran/nota: masing-masing satu asli lalu satu cetak ulang identik.
                originalRequired = copy Is Nothing
                Dim original As JObject = Nothing
                If originalRequired Then
                    If _batchOriginals.ContainsKey(batchKey) Then Return Failure(409, "ORIGINAL_ALREADY_PRINTED")
                    If _batchOriginals.Count >= 3 Then Return Failure(409, "TEST_PRINT_LIMIT")
                    If _snapshot IsNot Nothing AndAlso Not JToken.DeepEquals(snapshot("store"), _snapshot("store")) Then
                        Return Failure(409, "SAME_STORE_REQUIRED")
                    End If
                ElseIf Not _batchOriginals.TryGetValue(batchKey, original) Then
                    Return Failure(409, "ORIGINAL_REQUIRED")
                ElseIf _batchCopies.Contains(batchKey) Then
                    Return Failure(409, "TEST_PRINT_LIMIT")
                ElseIf Not JToken.DeepEquals(snapshot, original) Then
                    Return Failure(409, "SAME_RECEIPT_REPRINT_REQUIRED")
                End If
            ElseIf _edcSplitBatch Then
                Dim expectedKind = If(_jobs.Count < 2, "cashier_receipt", "split_receipt")
                If CStr(root("jobType")) <> expectedKind Then Return Failure(409, "TEST_BATCH_ORDER")
                If _jobs.Count = 2 Then
                    If CStr(snapshot("payload")("receiptNo")) = CStr(_snapshot("payload")("receiptNo")) OrElse
                       CStr(snapshot("payload")("transactionId")) = CStr(_snapshot("payload")("transactionId")) OrElse
                       CStr(snapshot("payload")("eventId")) = CStr(_snapshot("payload")("eventId")) Then
                        Return Failure(409, "DIFFERENT_RECEIPT_REQUIRED")
                    End If
                    If Not JToken.DeepEquals(snapshot("store"), _snapshot("store")) Then Return Failure(409, "SAME_STORE_REQUIRED")
                End If
            End If
            If Not perReceipt AndAlso originalRequired AndAlso copy IsNot Nothing Then Return Failure(409, "ORIGINAL_REQUIRED")
            If Not perReceipt AndAlso Not originalRequired AndAlso (copy Is Nothing OrElse Not JToken.DeepEquals(snapshot, _snapshot)) Then
                Return Failure(409, "SAME_RECEIPT_REPRINT_REQUIRED")
            End If
            If Not _printerReady() Then Return Failure(409, "PRINTER_NOT_CONFIRMED")
            ' Reservasi sebelum renderer: timeout/galat tidak boleh mencetak ulang otomatis.
            _jobs.Add(jobId, New KeyValuePair(Of String, Boolean)(fingerprint, False))
            If originalRequired AndAlso (Not perReceipt OrElse _snapshot Is Nothing) Then _snapshot = snapshot
            If perReceipt Then
                If originalRequired Then _batchOriginals.Add(batchKey, snapshot) Else _batchCopies.Add(batchKey)
            End If
            Volatile.Write(_printing, 1)
            Try
                _render(body)
                _jobs(jobId) = New KeyValuePair(Of String, Boolean)(fingerprint, True)
                Console.WriteLine("Staging test print command completed; inspect the paper.")
                Return New BridgeReply(200, "{""ok"":true}")
            Catch
                _uncertain = True
                Console.WriteLine("Staging test print outcome unknown; no automatic retry.")
                Return Failure(409, "PRINT_OUTCOME_UNKNOWN")
            Finally
                Volatile.Write(_printing, 0)
            End Try
        End SyncLock
    End Function

    Friend Shared ReadOnly ReceivableJobTypes As String() = {"receivable_selected", "receivable_selected_card", "receivable_proof"}

    Private Shared Function SnapshotFor(root As JObject) As JObject
        Dim payload As JObject = CType(root("payload").DeepClone(), JObject)
        payload.Remove("reprint")
        Return New JObject(New JProperty("store", root("store").DeepClone()), New JProperty("payload", payload))
    End Function

    Friend Shared Function Failure(status As Integer, code As String) As BridgeReply
        Return New BridgeReply(status, JsonConvert.SerializeObject(New With {.ok = False, .error = code}))
    End Function
End Class

Friend Class V2StagingHttpServer
    Private ReadOnly _policy As V2StagingBridge
    Private ReadOnly _listener As New HttpListener()
    Private ReadOnly _requests As New SemaphoreSlim(4, 4)
    Private _thread As Thread

    Friend Sub New(policy As V2StagingBridge, prefix As String)
        _policy = policy
        _listener.Prefixes.Add(prefix)
    End Sub

    Friend Sub Start()
        _listener.Start()
        _thread = New Thread(AddressOf Listen) With {.IsBackground = True}
        _thread.Start()
    End Sub

    Friend Sub [Stop]()
        ' Tunggu renderer yang sedang berjalan; jangan hentikan proses di tengah EndDoc.
        _policy.CloseSession()
        _listener.Close()
        If _thread IsNot Nothing Then _thread.Join(2000)
    End Sub

    Private Sub Listen()
        Try
            While _listener.IsListening
                Dim context As HttpListenerContext = _listener.GetContext()
                If Not _requests.Wait(0) Then
                    Try
                        WriteReply(context, V2StagingBridge.Failure(503, "TEST_BUSY"))
                    Catch
                        ' Klien sudah menutup koneksi; listener tetap hidup.
                    End Try
                    Continue While
                End If
                ThreadPool.QueueUserWorkItem(Sub(state As Object)
                                                Try
                                                    Serve(CType(state, HttpListenerContext))
                                                Finally
                                                    _requests.Release()
                                                End Try
                                            End Sub, context)
            End While
        Catch ex As HttpListenerException
            ' Stop menutup listener; tidak memanggil renderer lagi.
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub Serve(context As HttpListenerContext)
        Try
            Dim request As HttpListenerRequest = context.Request
            Dim loopback As Boolean = request.RemoteEndPoint IsNot Nothing AndAlso IPAddress.IsLoopback(request.RemoteEndPoint.Address)
            Dim origin As String = request.Headers("Origin")
            Dim body As String = Nothing
            If request.HttpMethod = "POST" Then
                ' Tolak sebelum membaca body dari origin/peer yang tidak berwenang.
                If Not loopback OrElse Not V2StagingBridge.OriginAllowed(origin) Then
                    WriteReply(context, V2StagingBridge.Failure(403, "FORBIDDEN_ORIGIN"))
                    Return
                End If
                If request.ContentLength64 > V2StagingBridge.MaxBodyBytes Then
                    WriteReply(context, V2StagingBridge.Failure(413, "PAYLOAD_TOO_LARGE"))
                    Return
                End If
                Using buffer As New MemoryStream()
                    Dim bytes(4095) As Byte
                    Dim count As Integer
                    Do
                        count = request.InputStream.Read(bytes, 0, bytes.Length)
                        If buffer.Length + count > V2StagingBridge.MaxBodyBytes Then
                            WriteReply(context, V2StagingBridge.Failure(413, "PAYLOAD_TOO_LARGE"))
                            Return
                        End If
                        buffer.Write(bytes, 0, count)
                    Loop While count > 0
                    body = New UTF8Encoding(False, True).GetString(buffer.ToArray())
                End Using
            End If
            WriteReply(context, _policy.Handle(request.HttpMethod, request.Url.AbsolutePath, origin,
                request.ContentType, body, loopback))
        Catch
            ' Jangan keluarkan pesan parser/driver, identitas, atau isi body ke log.
            Try
                WriteReply(context, V2StagingBridge.Failure(400, "BAD_REQUEST"))
            Catch
                ' Klien sudah menutup koneksi; jangan mencoba renderer/print lagi.
            End Try
        End Try
    End Sub

    Private Shared Sub WriteReply(context As HttpListenerContext, reply As BridgeReply)
        Dim response As HttpListenerResponse = context.Response
        response.StatusCode = reply.Status
        response.Headers("Cache-Control") = "no-store"
        response.Headers("Vary") = "Origin"
        If V2StagingBridge.OriginAllowed(context.Request.Headers("Origin")) Then
            response.Headers("Access-Control-Allow-Origin") = V2StagingBridge.StagingOrigin
            response.Headers("Access-Control-Allow-Methods") = "GET, POST, OPTIONS"
            response.Headers("Access-Control-Allow-Headers") = "Content-Type"
            If context.Request.HttpMethod = "OPTIONS" AndAlso context.Request.Headers("Access-Control-Request-Private-Network") = "true" Then
                response.Headers("Access-Control-Allow-Private-Network") = "true"
            End If
        End If
        Dim bytes As Byte() = Encoding.UTF8.GetBytes(reply.Body)
        response.ContentType = "application/json; charset=utf-8"
        response.ContentLength64 = bytes.Length
        If bytes.Length > 0 Then response.OutputStream.Write(bytes, 0, bytes.Length)
        response.Close()
    End Sub
End Class
