' Gama Print Agent — Transport Spike (Step 1)
'
' Tujuan: membuktikan browser <-> http://localhost:9111 berfungsi (CORS / mixed-content)
' TANPA mencetak apa pun. POST /print hanya menyimpan body JSON yang diterima ke file.
' Printing nyata (PowerPacks) menyusul di Step 2.
'
' Build/run di Windows (.NET Framework 4.8). Lihat README.md.
'
' Namespace: project memakai <RootNamespace>GamaPrintAgent.SpikeTransport</RootNamespace>,
' jadi Module Program di sini otomatis berada di namespace itu. JANGAN menambahkan deklarasi
' 'Namespace' eksplisit di file ini — VB akan menambahkan RootNamespace ke depannya (jadi ganda)
' sehingga StartupObject tidak ketemu dan build gagal.

Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Velopack

Module Program

    Private Const Prefix As String = "http://localhost:9111/"
    Private Const AgentVersion As String = "1.0.0"
    Private Const SchemaVersion As Integer = 1

    Private _listener As HttpListener = Nothing
    Private _instanceMutex As Mutex = Nothing

    <STAThread()>
    Sub Main()
        ' Velopack: tangani lifecycle install/update — HARUS paling awal (saat dipanggil installer/updater
        ' ia memproses arg lalu keluar). Tanpa baris ini, install/auto-update tak berjalan benar.
        VelopackApp.Build().Run()

        ' Single-instance (per sesi user): cegah dua agent rebutan port 9111 + file log.
        Dim createdNew As Boolean = False
        _instanceMutex = New Mutex(True, "GamaPrintAgent_SingleInstance_9111", createdNew)
        If Not createdNew Then Return   ' sudah ada instance → keluar diam-diam

        AppPaths.SetupLogging()   ' Console.WriteLine → file log (WinExe tak punya jendela console)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Start server DI SINI (bukan di constructor TrayContext) → kalau gagal, keluar BERSIH sebelum
        ' masuk message loop. (ExitThread di constructor tak ter-hook ThreadExit → zombie.)
        If Not StartServer() Then
            MessageBox.Show(
                "Gagal memulai Gama Print Agent." & Environment.NewLine &
                "Port 9111 mungkin dipakai aplikasi lain, atau perlu izin." & Environment.NewLine &
                "Lihat log: " & AppPaths.LogDir(),
                "Gama Print Agent", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Application.Run(New TrayContext())   ' message loop + ikon tray; server HTTP di thread BG
    End Sub

    ' Mulai HttpListener. Return True bila sukses. Tanpa Console.ReadLine (WinExe tak punya console);
    ' kegagalan dilaporkan via return → TrayContext tampilkan MessageBox + keluar.
    Friend Function StartServer() As Boolean
        Try
            _listener = New HttpListener()
            _listener.Prefixes.Add(Prefix)
            _listener.Start()
        Catch ex As Exception
            Console.WriteLine("Gagal start HttpListener: " & ex.Message)
            Console.WriteLine("Jika 'Access is denied', jalankan sekali sebagai Administrator:")
            Console.WriteLine("  netsh http add urlacl url=" & Prefix & " user=" & Environment.UserName)
            Return False
        End Try

        Console.WriteLine("Gama Print Agent v" & AgentVersion & " listening on " & Prefix)
        Console.WriteLine("Endpoints: GET /health | GET /printers | POST /print | POST /print/test | POST /printers/config")
        Console.WriteLine(Printers.ConfigSummary())
        ' Bila proses sebelumnya mati saat cetak (default printer Windows belum dikembalikan), pulihkan.
        Printers.RestoreDefaultPrinterIfNeeded()
        Return True
    End Function

    ' Loop accept request — dijalankan TrayContext di thread background.
    Friend Sub RunAcceptLoop()
        While _listener IsNot Nothing AndAlso _listener.IsListening
            Dim ctx As HttpListenerContext = Nothing
            Try
                ctx = _listener.GetContext()
            Catch
                Exit While
            End Try

            ' GET/OPTIONS ringan → inline (selalu responsif). POST (berpotensi lambat/hang) → thread pool;
            ' pencetakan nyata tetap di-serialize via lock di Printers.
            If ctx.Request.HttpMethod.ToUpperInvariant() = "POST" Then
                ThreadPool.QueueUserWorkItem(Sub() SafeHandle(ctx))
            Else
                SafeHandle(ctx)
            End If
        End While
    End Sub

    Friend Sub StopServer()
        Try
            If _listener IsNot Nothing Then
                _listener.Stop()
                _listener.Close()
            End If
        Catch
        End Try
    End Sub

    ' Ringkasan status untuk menu tray (Status).
    Friend Function StatusSummary() As String
        Return "Gama Print Agent v" & AgentVersion & Environment.NewLine &
               "Listening: " & Prefix & Environment.NewLine &
               "Data: " & AppPaths.DataDir() & Environment.NewLine & Environment.NewLine &
               Printers.ConfigSummary()
    End Function

    ' Bungkus HandleRequest + penangan error (dipakai inline & di thread pool).
    Private Sub SafeHandle(ctx As HttpListenerContext)
        Try
            HandleRequest(ctx)
        Catch ex As Exception
            Console.WriteLine("ERROR: " & ex.Message)
            Try
                WriteJson(ctx, 500, "{""ok"":false,""error"":""INTERNAL"",""message"":" & JsonString(ex.Message) & "}")
            Catch
                ' abaikan kegagalan saat menulis response error
            End Try
        End Try
    End Sub

    Private Sub HandleRequest(ctx As HttpListenerContext)
        Dim req As HttpListenerRequest = ctx.Request
        Dim method As String = req.HttpMethod.ToUpperInvariant()
        Dim path As String = req.Url.AbsolutePath.TrimEnd("/"c).ToLowerInvariant()
        If path.Length = 0 Then path = "/"

        Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {method} {req.Url.AbsolutePath}")

        ' CORS preflight
        If method = "OPTIONS" Then
            WriteResponse(ctx, 204, "", "")
            Return
        End If

        ' Gerbang Origin SISI-SERVER: CORS hanya memblokir browser MEMBACA respons, bukan mencegah
        ' request diproses → endpoint state-changing (POST) rentan CSRF lintas-origin. Tolak origin terlarang.
        If method = "POST" Then
            Dim reqOrigin As String = req.Headers("Origin")
            If Not String.IsNullOrEmpty(reqOrigin) AndAlso Not IsOriginAllowed(reqOrigin) Then
                WriteJson(ctx, 403, "{""ok"":false,""error"":""FORBIDDEN_ORIGIN""}")
                Return
            End If
        End If

        Select Case method & " " & path

            Case "GET /health"
                WriteJson(ctx, 200, $"{{""ok"":true,""agentVersion"":""{AgentVersion}"",""schemaVersion"":{SchemaVersion},""mode"":""spike""}}")

            Case "GET /printers"
                ' Printer terpasang + peta role (printers.json) + default Windows.
                WriteJson(ctx, 200, Printers.StatusJson())

            Case "POST /print"
                Dim body As String
                Try
                    body = ReadBody(req)
                Catch ex As Exception
                    WriteJson(ctx, 413, "{""ok"":false,""error"":""PAYLOAD_TOO_LARGE""}")
                    Return
                End Try
                ' Payload berisi PII tenant → JANGAN persist by default. Hanya bila debug di-opt-in.
                If DebugJobsEnabled() Then
                    Try
                        SaveJob(body)   ' debug-only; gagal-tulis tak menggagalkan cetak
                    Catch
                    End Try
                End If
                WriteJson(ctx, 200, Dispatch(body))

            Case "POST /print/test"
                Dim pdf As String = Nothing
                Printers.Serialize(Sub() pdf = PrintTestPage())
                Console.WriteLine("   printed -> " & pdf)
                WriteJson(ctx, 200, $"{{""ok"":true,""printed"":{JsonString(pdf)}}}")

            Case "POST /printers/config"
                WriteJson(ctx, 200, Printers.SaveConfig(ReadBody(req)))

            Case Else
                WriteJson(ctx, 404, "{""ok"":false,""error"":""NOT_FOUND""}")

        End Select
    End Sub

    ' Deserialize amplop job JSON lalu dispatch sesuai jobType. Mengembalikan body JSON response.
    Private Function Dispatch(body As String) As String
        Dim job As PrintJob = Nothing
        Try
            job = JsonConvert.DeserializeObject(Of PrintJob)(body)
        Catch ex As Exception
            Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":" & JsonString(ex.Message) & "}"
        End Try

        If job Is Nothing OrElse String.IsNullOrEmpty(job.jobType) Then
            Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""jobType kosong""}"
        End If

        Select Case job.jobType.ToLowerInvariant()

            Case "cashier_receipt"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As CashierReceiptPayload = job.payload.ToObject(Of CashierReceiptPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintCashierReceipt(job.store, p))
                    Console.WriteLine("   printed cashier_receipt " & If(p.rcptNo, ""))
                    Return "{""ok"":true,""jobType"":""cashier_receipt"",""rcptNo"":" & JsonString(If(p.rcptNo, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "kasbon_receipt"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As KasbonReceiptPayload = job.payload.ToObject(Of KasbonReceiptPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintKasbonReceipt(job.store, p))
                    Console.WriteLine("   printed kasbon_receipt " & If(p.rcptNo, ""))
                    Return "{""ok"":true,""jobType"":""kasbon_receipt"",""rcptNo"":" & JsonString(If(p.rcptNo, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "split_receipt"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As SplitReceiptPayload = job.payload.ToObject(Of SplitReceiptPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintSplitReceipt(job.store, p))
                    Console.WriteLine("   printed split_receipt " & If(p.rcptNo, ""))
                    Return "{""ok"":true,""jobType"":""split_receipt"",""rcptNo"":" & JsonString(If(p.rcptNo, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "return_note"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReturnReceiptPayload = job.payload.ToObject(Of ReturnReceiptPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReturnReceipt(p))
                    Console.WriteLine("   printed return_note " & If(p.rcptNo, ""))
                    Return "{""ok"":true,""jobType"":""return_note"",""rcptNo"":" & JsonString(If(p.rcptNo, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "receivable_proof"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReceivableProofPayload = job.payload.ToObject(Of ReceivableProofPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReceivableProof(job.store, p))
                    Console.WriteLine("   printed receivable_proof " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_proof"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "salary_slip", "bpjs_slip"
                ' Template slip daftar-nominal identik; beda hanya teks header (dikirim web di payload).
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As AmountListSlipPayload = job.payload.ToObject(Of AmountListSlipPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintAmountListSlip(p))
                    Console.WriteLine("   printed " & job.jobType)
                    Return "{""ok"":true,""jobType"":" & JsonString(job.jobType) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "receivable_selected"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReceivableSelectedPayload = job.payload.ToObject(Of ReceivableSelectedPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReceivableSelected(job.store, p))
                    Console.WriteLine("   printed receivable_selected " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_selected"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "receivable_selected_card"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReceivableSelectedCardPayload = job.payload.ToObject(Of ReceivableSelectedCardPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReceivableSelectedCard(job.store, p))
                    Console.WriteLine("   printed receivable_selected_card " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_selected_card"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "receivable_paidoff"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReceivablePaidOffPayload = job.payload.ToObject(Of ReceivablePaidOffPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReceivablePaidOff(job.store, p))
                    Console.WriteLine("   printed receivable_paidoff " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_paidoff"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "receivable_redeem"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As ReceivableRedeemPayload = job.payload.ToObject(Of ReceivableRedeemPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintReceivableRedeem(job.store, p))
                    Console.WriteLine("   printed receivable_redeem " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_redeem"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "delivery_order"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As DeliveryOrderPayload = job.payload.ToObject(Of DeliveryOrderPayload)()
                    WithRolePrinter(job.printerRole, Sub() PrintDeliveryOrder(job.store, p))
                    Console.WriteLine("   printed delivery_order")
                    Return "{""ok"":true,""jobType"":""delivery_order""}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "qr_item_label"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As QrItemLabelPayload = job.payload.ToObject(Of QrItemLabelPayload)()
                    Printers.Serialize(Sub() PrintQrItemLabel(p))   ' serialize cetak; targeting via PrinterSettings.PrinterName (QRLABEL)
                    Console.WriteLine("   printed qr_item_label " & If(p.itemId, ""))
                    Return "{""ok"":true,""jobType"":""qr_item_label"",""itemId"":" & JsonString(If(p.itemId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case "qr_invoice"
                If job.payload Is Nothing Then
                    Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""payload kosong""}"
                End If
                Try
                    Dim p As QrInvoicePayload = job.payload.ToObject(Of QrInvoicePayload)()
                    Printers.Serialize(Sub() PrintQrInvoice(p))
                    Console.WriteLine("   printed qr_invoice " & If(p.invoiceNo, ""))
                    Return "{""ok"":true,""jobType"":""qr_invoice"",""invoiceNo"":" & JsonString(If(p.invoiceNo, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case Else
                Return "{""ok"":false,""error"":""UNSUPPORTED_JOBTYPE"",""message"":" & JsonString(job.jobType) & "}"

        End Select
    End Function

    Private Const MaxBodyBytes As Long = 1024L * 1024L   ' 1 MB — cukup utk nota terbesar; cegah OOM/DoS

    ' Baca body dengan BATAS ukuran (buffered) → tolak body raksasa walau Content-Length berbohong.
    Private Function ReadBody(req As HttpListenerRequest) As String
        If Not req.HasEntityBody Then Return ""
        If req.ContentLength64 > MaxBodyBytes Then Throw New Exception("PAYLOAD_TOO_LARGE")
        Dim buf(8191) As Byte
        Dim total As Long = 0
        Using ms As New MemoryStream()
            Dim n As Integer
            Do
                n = req.InputStream.Read(buf, 0, buf.Length)
                If n <= 0 Then Exit Do
                total += n
                If total > MaxBodyBytes Then Throw New Exception("PAYLOAD_TOO_LARGE")
                ms.Write(buf, 0, n)
            Loop
            Return req.ContentEncoding.GetString(ms.ToArray())
        End Using
    End Function

    ' Penyimpanan payload mentah ke jobs/ berisi PII tenant → DEFAULT OFF. Aktifkan HANYA utk
    ' diagnostik via env GAMA_AGENT_DEBUG_JOBS=1 atau file 'debug.flag' di folder data (%LOCALAPPDATA%).
    Private Function DebugJobsEnabled() As Boolean
        If String.Equals(Environment.GetEnvironmentVariable("GAMA_AGENT_DEBUG_JOBS"), "1", StringComparison.Ordinal) Then Return True
        Return File.Exists(AppPaths.DebugFlagPath())
    End Function

    Private Function SaveJob(body As String) As String
        Dim dir As String = AppPaths.JobsDir()
        PruneJobs(dir, 200)   ' rotasi: sisakan ~200 file debug terbaru (cegah tumbuh tanpa batas)
        Dim name As String = "job-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4) & ".json"
        Dim full As String = Path.Combine(dir, name)
        File.WriteAllText(full, body, New UTF8Encoding(False))
        Return full
    End Function

    ' Hapus file job lama, sisakan 'keep' terbaru.
    Private Sub PruneJobs(dir As String, keep As Integer)
        Try
            Dim files() As FileInfo = New DirectoryInfo(dir).GetFiles("job-*.json")
            If files.Length <= keep Then Return
            Array.Sort(files, Function(a As FileInfo, b As FileInfo) b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc))   ' terbaru dulu
            For i As Integer = keep To files.Length - 1
                Try
                    files(i).Delete()
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

    Private Sub WriteJson(ctx As HttpListenerContext, status As Integer, json As String)
        WriteResponse(ctx, status, json, "application/json")
    End Sub

    Private Sub WriteResponse(ctx As HttpListenerContext, status As Integer, body As String, contentType As String)
        Dim res As HttpListenerResponse = ctx.Response
        ' CORS: HANYA izinkan origin domain toko (gamapos.id) + localhost (dev) — BUKAN '*' →
        ' cegah situs web sembarang men-drive agent (drive-by print / enumerasi printer).
        Dim origin As String = ctx.Request.Headers("Origin")
        If IsOriginAllowed(origin) Then
            res.Headers("Access-Control-Allow-Origin") = origin
            res.Headers("Access-Control-Allow-Methods") = "GET, POST, OPTIONS"
            res.Headers("Access-Control-Allow-Headers") = "Content-Type"
            ' Private Network Access (Chrome): origin PUBLIK HTTPS → localhost butuh opt-in ini.
            res.Headers("Access-Control-Allow-Private-Network") = "true"
            res.Headers("Vary") = "Origin"
        End If
        res.StatusCode = status

        ' 204 (mis. CORS preflight): tanpa body / Content-Type.
        If status = 204 Then
            res.OutputStream.Close()
            Return
        End If

        res.ContentType = contentType
        Dim buffer As Byte() = Encoding.UTF8.GetBytes(body)
        res.ContentLength64 = buffer.LongLength
        res.OutputStream.Write(buffer, 0, buffer.Length)
        res.OutputStream.Close()
    End Sub

    ' Origin yang boleh akses agent: subdomain gamapos.id (HTTPS) + localhost/127.0.0.1 (dev).
    Private Function IsOriginAllowed(origin As String) As Boolean
        If String.IsNullOrEmpty(origin) Then Return False
        Dim u As Uri = Nothing
        If Not Uri.TryCreate(origin.Trim(), UriKind.Absolute, u) Then Return False
        Dim host As String = u.Host.ToLowerInvariant()
        ' Cocokkan HOST persis (bukan StartsWith) → cegah bypass 'localhost.evil.com' / 'gamapos.id.evil.com'.
        If u.Scheme = "https" AndAlso (host = "gamapos.id" OrElse host.EndsWith(".gamapos.id", StringComparison.Ordinal)) Then Return True
        If host = "localhost" OrElse host = "127.0.0.1" Then Return True
        Return False
    End Function

    ' JSON string escaper minimal (cukup untuk path / pesan).
    Private Function JsonString(s As String) As String
        If s Is Nothing Then Return ChrW(34) & ChrW(34)
        Dim sb As New StringBuilder()
        sb.Append(ChrW(34))
        For Each ch As Char In s
            Select Case AscW(ch)
                Case 34 : sb.Append(ChrW(92)).Append(ChrW(34))   ' \"
                Case 92 : sb.Append(ChrW(92)).Append(ChrW(92))   ' \\
                Case 8 : sb.Append("\b")
                Case 9 : sb.Append("\t")
                Case 10 : sb.Append("\n")
                Case 12 : sb.Append("\f")
                Case 13 : sb.Append("\r")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u" & AscW(ch).ToString("x4"))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        sb.Append(ChrW(34))
        Return sb.ToString()
    End Function

End Module
