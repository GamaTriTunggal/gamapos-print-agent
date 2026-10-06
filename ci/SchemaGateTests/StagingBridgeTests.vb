' P-604: policy dan HTTP nyata diuji tanpa Windows/printer; renderer adalah spy.
Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module StagingBridgeTests
    Friend Sub Run(directory As String)
        Dim original = ParseSaleV2(File.ReadAllText(Path.Combine(directory, "sale_cash.sample.json")))
        ' Satu nota sintetis berdiskon: 19.752 - 500 - 252 = 19.000.
        ' Fixture asal tidak diubah, dan alat tidak menghitung ulang uang.
        original("payload")("amounts")("discountSen") = "50000"
        For Each field In {"netSen", "principalAppliedSen", "cashSen", "customerPaysSen", "merchantReceivesSen"}
            original("payload")("amounts")(field) = "1900000"
        Next
        Dim body = ParseSaleV2(original.ToString(Formatting.None)).ToString(Formatting.None)
        Dim copy = CType(JObject.Parse(body).DeepClone(), JObject)
        copy("jobId") = "bridge-reprint"
        copy("payload")("reprint") = JObject.Parse("{""date"":""2026-10-03"",""time"":""13:30:00"",""processor"":{""userId"":""43"",""name"":""KASIR ULANG""},""corrections"":[]}")
        Dim copyBody = copy.ToString(Formatting.None)
        Dim calls As Integer = 0
        Dim ready As Boolean = True
        Dim policy = NewPolicy(Sub(value As String) calls += 1, Function() ready)
        Dim health = JObject.Parse(policy.Handle("GET", "/health", V2StagingBridge.StagingOrigin, Nothing, Nothing, True).Body)
        If CStr(health("mode")) <> "staging-test" OrElse
           Not JToken.DeepEquals(health("supportedPrintSchemas"), New JArray(2)) OrElse
           Not JToken.DeepEquals(health("supportedPrintJobTypesV2"), New JArray("cashier_receipt")) Then
            Throw New InvalidOperationException("Bridge health membuka keluarga lain atau menyamar sebagai agent terpasang.")
        End If
        For Each origin In {Nothing, "", "null", "https://app.gamapos.id", "https://staging.gamapos.id.evil", "https://staging.gamapos.id/"}
            Expect(Send(policy, body, origin), 403, "FORBIDDEN_ORIGIN")
        Next
        Expect(policy.Handle("POST", "/print", V2StagingBridge.StagingOrigin, "application/json", body, False), 403, "LOOPBACK_ONLY")
        Expect(policy.Handle("GET", "/setup/status", Nothing, Nothing, Nothing, True), 404, "NOT_FOUND")
        Expect(policy.Handle("POST", "/print", V2StagingBridge.StagingOrigin, "text/plain", body, True), 415, "JSON_REQUIRED")
        Expect(Send(policy, New String("x"c, V2StagingBridge.MaxBodyBytes + 1)), 413, "PAYLOAD_TOO_LARGE")
        Expect(Send(policy, body & "{}"), 400, "BAD_PAYLOAD")
        Expect(Send(policy, body.Replace("""schemaVersion"":2", """schemaVersion"":1")), 400, "BAD_PAYLOAD")
        Expect(Send(policy, body.Replace("""schemaVersion"":2", """schemaVersion"":2,""schemaVersion"":2")), 400, "BAD_PAYLOAD")
        Expect(Send(policy, File.ReadAllText(Path.Combine(directory, "sale_cash.sample.json"))), 422, "TEST_DISCOUNT_REQUIRED")
        For Each name In {"sale_edc_only.sample.json", "sale_split_wire.sample.json", "sale_kasbon_dp.sample.json"}
            Expect(Send(policy, File.ReadAllText(Path.Combine(directory, name))), 422, "TEST_CASHIER_CASH_ONLY")
        Next
        Expect(Send(policy, copyBody), 409, "ORIGINAL_REQUIRED")
        ready = False
        Expect(Send(policy, body), 409, "PRINTER_NOT_CONFIRMED")
        If calls <> 0 Then Throw New InvalidOperationException("Permintaan yang ditolak mencapai printer.")
        ready = True
        Expect(Send(policy, body), 200, "")
        Dim duplicate = Send(policy, body)
        Expect(duplicate, 200, "")
        If CBool(JObject.Parse(duplicate.Body)("duplicate")) <> True OrElse calls <> 1 Then Throw New InvalidOperationException("Retry mencetak dua kali.")
        Dim conflict = JObject.Parse(body)
        conflict("store")("name") = "DIFFERENT"
        Expect(Send(policy, conflict.ToString(Formatting.None)), 409, "JOB_ID_CONFLICT")
        conflict("jobId") = "another-original"
        Expect(Send(policy, conflict.ToString(Formatting.None)), 409, "SAME_RECEIPT_REPRINT_REQUIRED")
        Dim wrongCopy = JObject.Parse(copyBody)
        wrongCopy("payload")("customer")("name") = "OTHER"
        Expect(Send(policy, wrongCopy.ToString(Formatting.None)), 409, "SAME_RECEIPT_REPRINT_REQUIRED")
        Expect(Send(policy, copyBody), 200, "")
        Expect(Send(policy, copyBody), 200, "")
        wrongCopy("jobId") = "third-job"
        Expect(Send(policy, wrongCopy.ToString(Formatting.None)), 409, "TEST_PRINT_LIMIT")
        If calls <> 2 Then Throw New InvalidOperationException("Bridge tidak membatasi dua cetakan berbeda.")

        calls = 0
        Dim uncertain = NewPolicy(Sub(value As String)
                                      calls += 1
                                      Throw New InvalidOperationException("SECRET PAYLOAD MUST NOT LEAK")
                                  End Sub, Function() True)
        Dim failed = Send(uncertain, body)
        Expect(failed, 409, "PRINT_OUTCOME_UNKNOWN")
        If failed.Body.Contains("SECRET") Then Throw New InvalidOperationException("Pesan renderer bocor ke respons.")
        Expect(Send(uncertain, body), 409, "PRINT_OUTCOME_UNKNOWN")
        Expect(Send(uncertain, copyBody), 409, "PRINT_OUTCOME_UNKNOWN")
        If calls <> 1 Then Throw New InvalidOperationException("Cetak ambigu diulang.")
        Dim clock As New DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)
        Dim expired = New V2StagingBridge(AddressOf ParseSaleV2, Sub(value As String) calls += 1, Function() True,
                                           "1.2.0", Function() clock)
        clock = clock.AddMinutes(30)
        Expect(Send(expired, body), 410, "TEST_SESSION_EXPIRED")
        expired.CloseSession()
        Expect(Send(expired, body), 410, "TEST_SESSION_ENDED")

        calls = 0
        Dim concurrent = NewPolicy(Sub(value As String) Interlocked.Increment(calls), Function() True)
        Dim tasks As Task() = {
            Task.Run(Sub() Expect(Send(concurrent, body), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, body), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, body), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, body), 200, ""))}
        If Not Task.WaitAll(tasks, 5000) OrElse calls <> 1 Then Throw New InvalidOperationException("Retry paralel menggandakan cetak.")
        CheckHttp(body, copyBody)
        CheckEDCSplitBatch(directory)
        Console.WriteLine("Staging bridge policy + loopback HTTP passed; printer spy only, not Windows/physical/browser UAT.")
    End Sub

    Private Sub CheckEDCSplitBatch(directory As String)
        ' Salinan RAM: sumber fixture tidak diubah, fee tetap seperti snapshot parser.
        Dim edc = DiscountedCardSample(directory, "sale_edc_only.sample.json")
        Dim split = DiscountedCardSample(directory, "sale_split_edc.sample.json")
        Dim edcCopy = Reprint(edc, "batch-edc-copy")
        Dim splitCopy = Reprint(split, "batch-split-copy")
        Dim calls As Integer = 0
        Dim ready As Boolean = True
        Dim policy = NewPolicy(Sub(value As String) calls += 1, Function() ready, True)
        Dim health = JObject.Parse(policy.Handle("GET", "/health", Nothing, Nothing, Nothing, True).Body)
        If CStr(health("mode")) <> "staging-test" OrElse CStr(health("testBatch")) <> "edc-split" OrElse
           Not JToken.DeepEquals(health("supportedPrintSchemas"), New JArray(2)) OrElse
           Not JToken.DeepEquals(health("supportedPrintJobTypesV2"), New JArray("cashier_receipt", "split_receipt")) Then
            Throw New InvalidOperationException("Batch health tidak sesuai batas EDC/Campuran.")
        End If
        For Each name In {"sale_cash.sample.json", "sale_split_wire.sample.json", "sale_kasbon_dp.sample.json"}
            Expect(Send(policy, File.ReadAllText(Path.Combine(directory, name))), 422, "TEST_EDC_SPLIT_ONLY")
        Next
        For Each name In {"sale_edc_only.sample.json", "sale_split_edc.sample.json"}
            Expect(Send(policy, File.ReadAllText(Path.Combine(directory, name))), 422, "TEST_DISCOUNT_REQUIRED")
        Next
        Expect(Send(policy, split), 409, "TEST_BATCH_ORDER")
        Expect(Send(policy, edcCopy), 409, "ORIGINAL_REQUIRED")
        ready = False
        Expect(Send(policy, edc), 409, "PRINTER_NOT_CONFIRMED")
        If calls <> 0 Then Throw New InvalidOperationException("Batch ditolak tetapi mencapai printer.")
        ready = True
        Expect(Send(policy, edc), 200, "")
        Expect(Send(policy, split), 409, "TEST_BATCH_ORDER")
        Dim changed = JObject.Parse(edc)
        changed("payload")("customer")("name") = "OTHER"
        Expect(Send(policy, changed.ToString(Formatting.None)), 409, "JOB_ID_CONFLICT")
        changed("jobId") = "second-original"
        Expect(Send(policy, changed.ToString(Formatting.None)), 409, "SAME_RECEIPT_REPRINT_REQUIRED")
        For Each source In {edcCopy, splitCopy}
            ' Snapshot ulang pertama dan kedua harus identik, termasuk fee dan identitas.
            For Each field In {"name", "fee"}
                changed = JObject.Parse(source)
                If field = "name" Then
                    changed("payload")("customer")("name") = "OTHER"
                Else
                    Dim amounts = changed("payload")("amounts")
                    amounts("customerFeeSen") = "600"
                    amounts("customerPaysSen") = "1900600"
                    amounts("merchantReceivesSen") = "1900350"
                End If
                Expect(Send(policy, changed.ToString(Formatting.None)), 409, "SAME_RECEIPT_REPRINT_REQUIRED")
            Next
            Expect(Send(policy, source), 200, "")
            If source = edcCopy Then
                Expect(Send(policy, splitCopy), 409, "ORIGINAL_REQUIRED")
                changed = JObject.Parse(edc)
                changed("jobId") = "extra-edc"
                Expect(Send(policy, changed.ToString(Formatting.None)), 409, "TEST_BATCH_ORDER")
                For Each field In {"receiptNo", "transactionId", "eventId"}
                    changed = JObject.Parse(split)
                    changed("payload")(field) = JObject.Parse(edc)("payload")(field).DeepClone()
                    Expect(Send(policy, changed.ToString(Formatting.None)), 409, "DIFFERENT_RECEIPT_REQUIRED")
                Next
                changed = JObject.Parse(split)
                changed("store")("name") = "OTHER STORE"
                Expect(Send(policy, changed.ToString(Formatting.None)), 409, "SAME_STORE_REQUIRED")
                If calls <> 2 Then Throw New InvalidOperationException("Batch melampaui pasangan EDC.")
                Expect(Send(policy, split), 200, "")
            End If
        Next
        For Each body In {edc, edcCopy, split, splitCopy}
            Dim duplicate = Send(policy, body)
            Expect(duplicate, 200, "")
            If Not CBool(JObject.Parse(duplicate.Body)("duplicate")) Then Throw New InvalidOperationException("Batch retry bukan replay.")
        Next
        changed = JObject.Parse(splitCopy)
        changed("jobId") = "fifth-job"
        Expect(Send(policy, changed.ToString(Formatting.None)), 409, "TEST_PRINT_LIMIT")
        If calls <> 4 Then Throw New InvalidOperationException("Batch harus tepat empat cetakan.")

        calls = 0
        Dim uncertain = NewPolicy(Sub(value As String)
                                      calls += 1
                                      Throw New InvalidOperationException("SECRET")
                                  End Sub, Function() True, True)
        Dim failed = Send(uncertain, edc)
        Expect(failed, 409, "PRINT_OUTCOME_UNKNOWN")
        If failed.Body.Contains("SECRET") Then Throw New InvalidOperationException("Batch membocorkan galat renderer.")
        For Each body In {edc, edcCopy, split}
            Expect(Send(uncertain, body), 409, "PRINT_OUTCOME_UNKNOWN")
        Next
        If calls <> 1 Then Throw New InvalidOperationException("Batch ambigu diulang.")
        calls = 0
        Dim concurrent = NewPolicy(Sub(value As String) Interlocked.Increment(calls), Function() True, True)
        Dim tasks As Task() = {
            Task.Run(Sub() Expect(Send(concurrent, edc), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, edc), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, edc), 200, "")),
            Task.Run(Sub() Expect(Send(concurrent, edc), 200, ""))}
        If Not Task.WaitAll(tasks, 5000) OrElse calls <> 1 Then Throw New InvalidOperationException("Batch retry paralel menggandakan cetak.")
        Dim clock As New DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc)
        Dim expired = New V2StagingBridge(AddressOf ParseSaleV2, Sub(value As String) calls += 1, Function() True,
                                         "1.2.0", Function() clock, True)
        clock = clock.AddMinutes(30)
        Expect(Send(expired, edc), 410, "TEST_SESSION_EXPIRED")
        expired.CloseSession()
        Expect(Send(expired, edc), 410, "TEST_SESSION_ENDED")
        If calls <> 1 Then Throw New InvalidOperationException("Batch kedaluwarsa mencapai printer.")
        CheckHttp(edc, edcCopy, split, splitCopy)
        Console.WriteLine("EDC/split four-job batch policy + loopback HTTP passed; renderer spy only.")
    End Sub

    Private Function DiscountedCardSample(directory As String, filename As String) As String
        Dim root = ParseSaleV2(File.ReadAllText(Path.Combine(directory, filename)))
        Dim amounts = root("payload")("amounts")
        amounts("discountSen") = "50000"
        amounts("netSen") = "1900000"
        amounts("principalAppliedSen") = "1900000"
        amounts("customerPaysSen") = "1900500"
        amounts("merchantReceivesSen") = "1900250"
        If CStr(root("payload")("paymentMethod")) = "EDC" Then
            amounts("noncashSen") = "1900000"
        Else
            amounts("cashSen") = "950000"
        End If
        Return ParseSaleV2(root.ToString(Formatting.None)).ToString(Formatting.None)
    End Function

    Private Function Reprint(body As String, jobId As String) As String
        Dim root = JObject.Parse(body)
        root("jobId") = jobId
        root("payload")("reprint") = JObject.Parse("{""date"":""2026-10-06"",""time"":""13:30:00"",""processor"":{""userId"":""43"",""name"":""KASIR ULANG""},""corrections"":[]}")
        Return ParseSaleV2(root.ToString(Formatting.None)).ToString(Formatting.None)
    End Function

    Private Function NewPolicy(render As Action(Of String), ready As Func(Of Boolean), Optional edcSplitBatch As Boolean = False) As V2StagingBridge
        Return New V2StagingBridge(AddressOf ParseSaleV2, render, ready, "1.2.0", edcSplitBatch:=edcSplitBatch)
    End Function

    Private Function Send(policy As V2StagingBridge, body As String, Optional origin As String = V2StagingBridge.StagingOrigin) As BridgeReply
        Return policy.Handle("POST", "/print", origin, "application/json; charset=utf-8", body, True)
    End Function

    Private Sub Expect(reply As BridgeReply, status As Integer, code As String)
        If reply.Status <> status OrElse (code <> "" AndAlso CStr(JObject.Parse(reply.Body)("error")) <> code) Then
            Throw New InvalidOperationException("Bridge result differs: " & status & "/" & code)
        End If
        If status = 200 AndAlso Not CBool(JObject.Parse(reply.Body)("ok")) Then Throw New InvalidOperationException("Bridge ok=false.")
    End Sub

    Private Sub CheckHttp(body As String, copy As String, Optional split As String = Nothing, Optional splitCopy As String = Nothing)
        Dim reservation As New TcpListener(IPAddress.Loopback, 0)
        reservation.Start()
        Dim port = CType(reservation.LocalEndpoint, IPEndPoint).Port
        reservation.Stop()
        Dim prefix = "http://127.0.0.1:" & port & "/"
        Dim calls As Integer = 0
        Dim server As New V2StagingHttpServer(NewPolicy(Sub(value As String) calls += 1, Function() True, split IsNot Nothing), prefix)
        server.Start()
        Try
            Using client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(5)}
                Using request As New HttpRequestMessage(HttpMethod.Options, prefix & "print")
                    request.Headers.Add("Origin", V2StagingBridge.StagingOrigin)
                    request.Headers.Add("Access-Control-Request-Private-Network", "true")
                    Using response = client.SendAsync(request).GetAwaiter().GetResult()
                        If response.StatusCode <> HttpStatusCode.NoContent OrElse
                           String.Join("", response.Headers.GetValues("Access-Control-Allow-Origin")) <> V2StagingBridge.StagingOrigin OrElse
                           String.Join("", response.Headers.GetValues("Access-Control-Allow-Private-Network")) <> "true" Then
                            Throw New InvalidOperationException("CORS/PNA preflight bridge gagal.")
                        End If
                    End Using
                End Using
                CheckHttpReply(client, prefix & "health", HttpMethod.Get, V2StagingBridge.StagingOrigin, Nothing, 200, "")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, "https://app.gamapos.id", body, 403, "FORBIDDEN_ORIGIN")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, Nothing, body, 403, "FORBIDDEN_ORIGIN")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, "{", 400, "BAD_PAYLOAD")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin,
                               New String("x"c, V2StagingBridge.MaxBodyBytes + 1), 413, "PAYLOAD_TOO_LARGE")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, body, 200, "")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, body, 200, "")
                CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, copy, 200, "")
                If split IsNot Nothing Then
                    CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, split, 200, "")
                    CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, splitCopy, 200, "")
                    CheckHttpReply(client, prefix & "print", HttpMethod.Post, V2StagingBridge.StagingOrigin, body, 200, "")
                End If
                If calls <> If(split Is Nothing, 2, 4) Then Throw New InvalidOperationException("Jumlah HTTP print berbeda.")
            End Using
        Finally
            server.Stop()
        End Try
    End Sub

    Private Sub CheckHttpReply(client As HttpClient, url As String, method As HttpMethod, origin As String,
                              body As String, status As Integer, code As String)
        Using request As New HttpRequestMessage(method, url)
            If origin IsNot Nothing Then request.Headers.Add("Origin", origin)
            If body IsNot Nothing Then request.Content = New StringContent(body, Encoding.UTF8, "application/json")
            Using response = client.SendAsync(request).GetAwaiter().GetResult()
                Expect(New BridgeReply(CInt(response.StatusCode), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()), status, code)
                If origin <> V2StagingBridge.StagingOrigin AndAlso response.Headers.Contains("Access-Control-Allow-Origin") Then
                    Throw New InvalidOperationException("CORS memberikan akses ke origin bukan staging.")
                End If
            End Using
        End Using
    End Sub
End Module
