' Probe manual nota v2, terpisah dari startup agent. Tidak memasang agent,
' menulis autostart, membuka port, mengganti default printer, atau merilisnya.
Option Strict On
Option Explicit On

Imports System
Imports System.Drawing.Printing
Imports System.IO
Imports System.Reflection
Imports Newtonsoft.Json.Linq

Module Program
    Private Const AgentNamespace As String = "GamaPrintAgent.SpikeTransport."

    Function Main(args As String()) As Integer
        If args.Length <> 4 OrElse
           (args(0) <> "--verify" AndAlso args(0) <> "--preflight" AndAlso args(0) <> "--print") Then
            Console.Error.WriteLine("Pakai: V2PhysicalProbe.exe --verify|--preflight|--print AGENT_EXE FIXTURE_JSON NAMA_PRINTER_DEFAULT")
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
