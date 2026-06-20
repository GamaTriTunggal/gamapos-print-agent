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
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module Program

    Private Const Prefix As String = "http://localhost:9111/"
    Private Const AgentVersion As String = "0.1.0-spike"
    Private Const SchemaVersion As Integer = 1

    Sub Main()
        Dim listener As New HttpListener()
        listener.Prefixes.Add(Prefix)

        Try
            listener.Start()
        Catch ex As HttpListenerException
            Console.WriteLine("Gagal start HttpListener: " & ex.Message)
            Console.WriteLine("Jika 'Access is denied', jalankan sekali sebagai Administrator:")
            Console.WriteLine("  netsh http add urlacl url=" & Prefix & " user=" & Environment.UserName)
            Console.WriteLine("...atau jalankan agent sebagai Administrator.")
            Console.WriteLine("Tekan Enter untuk keluar.")
            Console.ReadLine()
            Return
        End Try

        Console.WriteLine("Gama Print Agent (transport spike) listening on " & Prefix)
        Console.WriteLine("Endpoints: GET /health | GET /printers | POST /print | POST /print/test")
        Console.WriteLine("Ctrl+C untuk berhenti.")

        While listener.IsListening
            Dim ctx As HttpListenerContext = Nothing
            Try
                ctx = listener.GetContext()
            Catch
                Exit While
            End Try

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
        End While
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

        Select Case method & " " & path

            Case "GET /health"
                WriteJson(ctx, 200, $"{{""ok"":true,""agentVersion"":""{AgentVersion}"",""schemaVersion"":{SchemaVersion},""mode"":""spike""}}")

            Case "GET /printers"
                ' SPIKE: enumerasi di-stub agar tak butuh System.Drawing.
                ' Versi nyata (uncomment + tambah <Reference Include="System.Drawing" /> di .vbproj):
                '   Dim sb As New StringBuilder()
                '   For Each p As String In System.Drawing.Printing.PrinterSettings.InstalledPrinters
                '       If sb.Length > 0 Then sb.Append(",")
                '       sb.Append(JsonString(p))
                '   Next
                '   WriteJson(ctx, 200, "{""ok"":true,""installed"":[" & sb.ToString() & "]}")
                WriteJson(ctx, 200, "{""ok"":true,""installed"":[],""roles"":{""CASHIER"":null,""DELIVERY"":null,""QRLABEL"":null,""REPORT"":null},""note"":""spike: printer enumeration deferred""}")

            Case "POST /print"
                Dim body As String = ReadBody(req)
                SaveJob(body)   ' simpan body mentah utk debug (jobs/)
                WriteJson(ctx, 200, Dispatch(body))

            Case "POST /print/test"
                Dim pdf As String = PrintTestPage()
                Console.WriteLine("   printed -> " & pdf)
                WriteJson(ctx, 200, $"{{""ok"":true,""printed"":{JsonString(pdf)}}}")

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
                    PrintCashierReceipt(job.store, p)
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
                    PrintKasbonReceipt(job.store, p)
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
                    PrintSplitReceipt(job.store, p)
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
                    PrintReturnReceipt(p)
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
                    PrintReceivableProof(job.store, p)
                    Console.WriteLine("   printed receivable_proof " & If(p.custId, ""))
                    Return "{""ok"":true,""jobType"":""receivable_proof"",""custId"":" & JsonString(If(p.custId, "")) & "}"
                Catch ex As Exception
                    Console.WriteLine("   PRINT_FAILED: " & ex.Message)
                    Return "{""ok"":false,""error"":""PRINT_FAILED"",""message"":" & JsonString(ex.Message) & "}"
                End Try

            Case Else
                Return "{""ok"":false,""error"":""UNSUPPORTED_JOBTYPE"",""message"":" & JsonString(job.jobType) & "}"

        End Select
    End Function

    Private Function ReadBody(req As HttpListenerRequest) As String
        If Not req.HasEntityBody Then Return ""
        Using reader As New StreamReader(req.InputStream, req.ContentEncoding)
            Return reader.ReadToEnd()
        End Using
    End Function

    Private Function SaveJob(body As String) As String
        Dim dir As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jobs")
        Directory.CreateDirectory(dir)
        Dim name As String = "job-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".json"
        Dim full As String = Path.Combine(dir, name)
        File.WriteAllText(full, body, New UTF8Encoding(False))
        Return full
    End Function

    Private Sub WriteJson(ctx As HttpListenerContext, status As Integer, json As String)
        WriteResponse(ctx, status, json, "application/json")
    End Sub

    Private Sub WriteResponse(ctx As HttpListenerContext, status As Integer, body As String, contentType As String)
        Dim res As HttpListenerResponse = ctx.Response
        ' CORS — spike: izinkan semua origin. Di produksi, batasi ke domain toko (lihat README).
        res.Headers("Access-Control-Allow-Origin") = "*"
        res.Headers("Access-Control-Allow-Methods") = "GET, POST, OPTIONS"
        res.Headers("Access-Control-Allow-Headers") = "Content-Type"
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
