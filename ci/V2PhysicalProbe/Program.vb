' Probe manual nota v2, terpisah dari startup agent. Tidak memasang agent,
' menulis autostart, mengganti default printer, atau merilisnya.
' --serve membuka loopback sementara khusus kasir staging setelah konfirmasi.
Option Strict On
Option Explicit On

Imports System
Imports System.Drawing.Printing
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports Newtonsoft.Json.Linq

Module Program
    Private Const AgentNamespace As String = "GamaPrintAgent.SpikeTransport."

    Function Main(args As String()) As Integer
        If args.Length = 3 AndAlso args(0) = "--serve" Then Return ServeStaging(args(1), args(2))
        If args.Length <> 4 OrElse
           (args(0) <> "--verify" AndAlso args(0) <> "--preflight" AndAlso args(0) <> "--print") Then
            Console.Error.WriteLine("Pakai: V2PhysicalProbe.exe --verify|--preflight|--print AGENT_EXE FIXTURE_JSON NAMA_PRINTER_DEFAULT; atau --serve AGENT_EXE NAMA_PRINTER_DEFAULT")
            Return 2
        End If
        Try
            Dim agentPath As String = Path.GetFullPath(args(1))
            Dim fixturePath As String = Path.GetFullPath(args(2))
            If Not File.Exists(agentPath) OrElse Not File.Exists(fixturePath) Then
                Throw New ArgumentException("Agent atau fixture tidak ditemukan.")
            End If
            Dim expectedPrinter As String = args(3).Trim()
            If expectedPrinter = "" Then Throw New ArgumentException("Nama printer uji wajib diisi.")

            Dim body As String = File.ReadAllText(fixturePath)
            Dim root As JObject = JObject.Parse(body, New JsonLoadSettings With {
                .DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error})
            Dim jobType As String = CStr(root("jobType"))
            Dim family As String = FamilyFor(jobType)
            If family = "" Then Throw New ArgumentException("Job v2 bukan keluarga nota uji.")

            Dim agent As Assembly = Assembly.LoadFrom(agentPath)
            Dim parser As MethodInfo = FindMethod(agent, family & "V2Parser", "Parse" & family & "V2")
            parser.Invoke(Nothing, New Object() {body})
            Console.WriteLine("Payload v2 valid: " & jobType & ". Isi fixture tidak ditampilkan.")

            Dim settings As New PrinterSettings()
            If Not settings.IsValid Then Throw New InvalidOperationException("Printer default Windows tidak tersedia.")
            Dim actualPrinter As String = settings.PrinterName
            If Not String.Equals(actualPrinter, expectedPrinter, StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("Printer default tidak sama dengan nama yang dikonfirmasi operator.")
            End If
            Console.WriteLine("Printer default terverifikasi: " & actualPrinter)

            If args(0) = "--verify" Then
                Console.WriteLine("Verifikasi selesai; belum ada kertas dicetak.")
                Return 0
            End If
            If args(0) = "--preflight" Then
                If family <> "Sale" Then Throw New ArgumentException("Preflight diagnostik baru tersedia untuk nota penjualan.")
                Dim preflight As MethodInfo = FindMethod(agent, "SaleV2Receipt", "PreflightSaleV2Receipt")
                preflight.Invoke(Nothing, New Object() {body})
                Console.WriteLine("Preflight layout dan pergantian font penjualan v2 selesai; Printer.Print dan EndDoc tidak dipanggil.")
                Return 0
            End If
            Console.Write("Untuk mencetak SATU fixture sintetis, ketik CETAK: ")
            If Console.ReadLine() <> "CETAK" Then
                Console.WriteLine("Dibatalkan; tidak ada perintah cetak.")
                Return 2
            End If
            Dim renderer As MethodInfo = FindMethod(agent, family & "V2Receipt", "Print" & family & "V2Receipt")
            renderer.Invoke(Nothing, New Object() {body})
            Console.WriteLine("Perintah cetak selesai. Periksa kertas fisik; ini bukan bukti tata letak otomatis.")
            Return 0
        Catch ex As TargetInvocationException
            Dim failure As Exception = If(ex.InnerException, ex)
            Dim safeReason As String = If(args(0) = "--preflight", SafePreflightReason(failure), "")
            If args(0) = "--print" AndAlso failure.Message.StartsWith("SALE_V2_STAGE:", StringComparison.Ordinal) Then
                safeReason = " — tahap " & failure.Message.Substring("SALE_V2_STAGE:".Length) &
                             ", penyebab " & If(failure.InnerException, failure).GetType().Name
            End If
            Console.Error.WriteLine("Gagal: " & failure.GetType().Name & safeReason)
        Catch ex As Exception
            ' Parser/driver dapat memasukkan isi fixture ke pesan galat.
            Console.Error.WriteLine("Gagal: " & ex.GetType().Name & ". Periksa path, printer, dan fixture uji.")
        End Try
        Return 1
    End Function

    Private Function ServeStaging(agentPath As String, expectedPrinter As String) As Integer
        Dim server As V2StagingHttpServer = Nothing
        Try
            If Not File.Exists(agentPath) OrElse String.IsNullOrWhiteSpace(expectedPrinter) Then Throw New ArgumentException()
            expectedPrinter = expectedPrinter.Trim()
            Dim agent As Assembly = Assembly.LoadFrom(Path.GetFullPath(agentPath))
            Dim parser = FindMethod(agent, "SaleV2Parser", "ParseSaleV2")
            Dim renderer = FindMethod(agent, "SaleV2Receipt", "PrintSaleV2Receipt")
            Dim printerReady As Func(Of Boolean) = Function()
                                                      Dim settings As New PrinterSettings()
                                                      Return settings.IsValid AndAlso String.Equals(settings.PrinterName, expectedPrinter, StringComparison.OrdinalIgnoreCase)
                                                  End Function
            If Not printerReady() Then Throw New InvalidOperationException()
            Console.WriteLine("Uji browser staging: maksimal satu nota tunai v2 dan satu cetak ulang nota yang sama, selama 30 menit.")
            Console.WriteLine("Tidak memasang agent/autostart, mengubah printer default, atau menyimpan isi nota.")
            Console.WriteLine("Jangan ubah printer default selama uji. Jika cetak gagal/timeout, jangan mengulang pembayaran atau memulai sesi baru.")
            Console.Write("Untuk membuka localhost:9111 pada PC uji, ketik UJI STAGING: ")
            If Console.ReadLine() <> "UJI STAGING" Then Return 2
            Dim policy As New V2StagingBridge(
                Function(body As String) CType(parser.Invoke(Nothing, New Object() {body}), JObject),
                Sub(body As String) renderer.Invoke(Nothing, New Object() {body}),
                printerReady, agent.GetName().Version.ToString(3))
            server = New V2StagingHttpServer(policy, "http://localhost:9111/")
            server.Start()
            Console.WriteLine("Jalur uji siap: buka https://staging.gamapos.id/pos/cashier di browser VM ini.")
            Console.WriteLine("Buat satu transaksi TUNAI BERDISKON dengan barang uji, lalu cetak ulang dari Daftar Nota. Ctrl+C setelah cetak selesai.")
            Using finished As New ManualResetEventSlim(False)
                Dim cancel As ConsoleCancelEventHandler = Sub(sender As Object, e As ConsoleCancelEventArgs)
                                                             e.Cancel = True
                                                             If policy.IsPrinting Then
                                                                 Console.WriteLine("Print command is still running; wait before stopping the test.")
                                                             Else
                                                                 finished.Set()
                                                             End If
                                                         End Sub
                AddHandler Console.CancelKeyPress, cancel
                Try
                    finished.Wait(TimeSpan.FromMinutes(30))
                    While policy.IsPrinting
                        Thread.Sleep(50)
                    End While
                Finally
                    RemoveHandler Console.CancelKeyPress, cancel
                End Try
            End Using
            Return 0
        Catch ex As Exception
            Dim listenerError As System.Net.HttpListenerException = TryCast(ex, System.Net.HttpListenerException)
            Dim code As String = If(listenerError Is Nothing, "", " (Windows code " & listenerError.NativeErrorCode & ")")
            Console.Error.WriteLine("Jalur uji tidak aktif: " & ex.GetType().Name & code & ". Periksa printer default dan port 9111; jangan ubah driver/URL ACL atau hentikan agent lain otomatis.")
            Return 1
        Finally
            If server IsNot Nothing Then server.Stop()
        End Try
    End Function

    Private Function SafePreflightReason(failure As Exception) As String
        ' Hanya pesan konstan dari layout yang boleh tampil; pesan driver/payload tidak dicetak.
        Select Case failure.Message
            Case "Identitas nota melampaui area cetak.", "Baris jumlah melampaui kolom nota.",
                 "Baris jumlah melampaui area cetak.", "Baris item melampaui area cetak.",
                 "Nama item melampaui area cetak.", "Teks/header nota tidak sah untuk area cetak.",
                 "Metrik area cetak tidak sah.", "Metrik lebar cetak tidak sah.",
                 "Metrik lebar item tidak sah.", "Kolom identitas nota bertumpuk.",
                 "Kolom identitas nota tidak sah.", "Metrik lebar nama tidak sah.",
                 "Metrik lebar teks tidak sah.", "Metrik area item tidak sah.",
                 "Karakter item melampaui area cetak.", "Karakter header/pelanggan melampaui area cetak.",
                 "Satu karakter nama melampaui area cetak.", "Metrik pergantian font tidak sah."
                Return " — " & failure.Message
            Case Else
                Return " — tahap layout/driver belum teridentifikasi; detail disembunyikan"
        End Select
    End Function

    Private Function FamilyFor(jobType As String) As String
        Select Case jobType
            Case "cashier_receipt", "kasbon_receipt", "split_receipt"
                Return "Sale"
            Case "receivable_selected", "receivable_selected_card", "receivable_proof"
                Return "Receivable"
            Case "return_note"
                Return "Return"
            Case Else
                Return ""
        End Select
    End Function

    Private Function FindMethod(agent As Assembly, moduleName As String, methodName As String) As MethodInfo
        Dim moduleType As Type = agent.GetType(AgentNamespace & moduleName, True)
        Dim method As MethodInfo = moduleType.GetMethod(methodName,
            BindingFlags.Static Or BindingFlags.NonPublic, Nothing, New Type() {GetType(String)}, Nothing)
        If method Is Nothing Then Throw New MissingMethodException(moduleName, methodName & "(String)")
        Return method
    End Function
End Module
