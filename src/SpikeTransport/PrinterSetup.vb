' Gama Print Agent — Pemasangan driver + setting printer OTOMATIS (recipe per model).
'
' Ide: tiap model punya "paket preset" (installer dengan setting sudah dibundel) di R2. Agen:
'   unduh paket → jalankan senyap → baca Result Code → verifikasi printer → auto-map peran ke printers.json.
'
' TM-U220: paket = APD Silent Installer buatan APD sendiri (driver + font substitution FontB terbawa
' sekaligus). Terbukti di VM: install di mesin bersih memunculkan printer + setting FontB otomatis.
'
' Elevation: paket meminta admin lewat manifest-nya → Process.Start(UseShellExecute:=True) memicu UAC
' (user klik "Yes" sekali). Karena UseShellExecute=True tak bisa redirect stdout, HASIL dibaca dari
' file log (APD4SilentSetup.log yang dibuat di folder paket), bukan dari output proses.
'
' Tanpa deklarasi Namespace (RootNamespace GamaPrintAgent.SpikeTransport ditambahkan otomatis) —
' lihat catatan yang sama di Program.vb.

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module PrinterSetup

    ' Resep per model. Tambah Xprinter / LX-310 di sini setelah golden config-nya terbukti.
    Private Class Recipe
        Public Url As String          ' paket preset di R2
        Public FileName As String     ' nama file lokal (di folder cache)
        Public PrinterName As String  ' nama printer Windows yang dibuat → untuk verifikasi + map peran
        Public Role As String         ' peran printers.json (CASHIER / QRLABEL / DELIVERY / REPORT)
        Public Kind As String         ' mekanisme jalankan/parse: "apd" (APD Silent Installer)
    End Class

    Private ReadOnly Recipes As New Dictionary(Of String, Recipe)(StringComparer.OrdinalIgnoreCase) From {
        {"TM-U220", New Recipe With {
            .Url = "https://installers.gamapos.id/golden/TM-U220-golden.exe",
            .FileName = "TM-U220-golden.exe",
            .PrinterName = "EPSON TM-U220 Receipt",
            .Role = "CASHIER",
            .Kind = "apd"}}
    }

    ' Serialize: cegah dua pemasangan konkuren (installer driver tak boleh tumpang tindih).
    Private ReadOnly SetupLock As New Object()

    ' Folder cache paket preset — di DataDir (%LOCALAPPDATA%) supaya bertahan lintas update Velopack.
    Private Function SetupDir() As String
        Dim d As String = Path.Combine(AppPaths.DataDir(), "setup")
        Try
            Directory.CreateDirectory(d)
        Catch
        End Try
        Return d
    End Function

    ' Handler POST /setup/printer. Body: { "model": "TM-U220" }. Mengembalikan body JSON hasil.
    Public Function HandleSetup(body As String) As String
        Dim model As String = Nothing
        Try
            Dim o As JObject = JObject.Parse(body)
            model = o.Value(Of String)("model")
        Catch
            Return ErrJson("BAD_PAYLOAD", "Body harus { ""model"": ""..."" }.")
        End Try
        If String.IsNullOrWhiteSpace(model) Then Return ErrJson("BAD_PAYLOAD", "model kosong.")

        Dim rec As Recipe = Nothing
        If Not Recipes.TryGetValue(model.Trim(), rec) Then
            Return ErrJson("UNSUPPORTED_MODEL", "Model tidak dikenal: " & model)
        End If

        SyncLock SetupLock
            Return InstallRecipe(rec)
        End SyncLock
    End Function

    Private Function InstallRecipe(rec As Recipe) As String
        Try
            Console.WriteLine("Setup printer: " & rec.PrinterName & " (" & rec.Kind & ")")

            ' 1) Unduh paket (skip bila sudah ada di cache).
            Dim pkg As String = EnsurePackage(rec)

            ' 2) Jalankan installer + TUNGGU (poll) sampai printer benar-benar muncul.
            Dim err As String = RunApdAndWait(pkg, rec.PrinterName)
            If err <> "" Then
                Console.WriteLine("   INSTALL: " & err)
                Return ErrJson("INSTALL_FAILED", err)
            End If

            ' 3) Auto-map peran ke printers.json (merge — peran lain tak diubah).
            Printers.SetRole(rec.Role, rec.PrinterName)

            Console.WriteLine("   setup OK: " & rec.PrinterName & " → " & rec.Role)
            Return "{""ok"":true,""printer"":" & JStr(rec.PrinterName) & ",""role"":" & JStr(rec.Role) & "}"
        Catch ex As Exception
            Console.WriteLine("   SETUP_FAILED: " & ex.Message)
            Return ErrJson("SETUP_FAILED", ex.Message)
        End Try
    End Function

    ' Unduh paket ke folder cache; skip bila sudah ada & berukuran wajar (>1 MB). Return path lokal.
    Private Function EnsurePackage(rec As Recipe) As String
        Dim dest As String = Path.Combine(SetupDir(), rec.FileName)
        Try
            If File.Exists(dest) AndAlso New FileInfo(dest).Length > 1024L * 1024L Then
                Console.WriteLine("   paket sudah di cache: " & dest)
                Return dest
            End If
        Catch
        End Try

        Console.WriteLine("   mengunduh paket: " & rec.Url)
        ' Cloudflare butuh TLS 1.2+. net48 umumnya default OS, tapi pastikan.
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim tmp As String = dest & ".tmp"
        Try
            Using wc As New WebClient()
                wc.DownloadFile(rec.Url, tmp)
            End Using
        Catch ex As Exception
            Try
                If File.Exists(tmp) Then File.Delete(tmp)
            Catch
            End Try
            Throw New Exception("Gagal mengunduh paket: " & ex.Message)
        End Try
        If File.Exists(dest) Then File.Delete(dest)
        File.Move(tmp, dest)
        Return dest
    End Function

    ' Jalankan paket APD Silent lalu POLL sampai printer muncul (bukti sebenarnya).
    ' Kenapa poll, bukan WaitForExit: paket minta admin → diluncurkan lewat mekanisme elevasi
    ' (broker), sehingga proses yang dikembalikan Process.Start langsung "exit" → WaitForExit
    ' tak berarti. UAC muncul selama poll → user klik Yes → driver terpasang → printer muncul.
    ' Return "" bila sukses, atau string alasan bila gagal.
    Private Function RunApdAndWait(pkg As String, expectedPrinter As String) As String
        Dim dir As String = Path.GetDirectoryName(pkg)
        Dim resultLog As String = Path.Combine(dir, "APD4SilentSetup.log")
        Try
            If File.Exists(resultLog) Then File.Delete(resultLog)
        Catch
        End Try

        Dim psi As New ProcessStartInfo() With {
            .FileName = pkg,
            .Arguments = "/rN",          ' /rN = jangan reboot; TANPA /d = tanpa dialog (senyap)
            .WorkingDirectory = dir,
            .UseShellExecute = True      ' WAJIB True agar paket bisa minta elevasi (UAC).
        }
        Console.WriteLine("   menjalankan paket (jendela UAC akan muncul — klik Yes)...")
        Try
            Process.Start(psi)
        Catch ex As Exception
            Return "Gagal menjalankan paket installer: " & ex.Message
        End Try

        ' Poll: SUKSES saat printer muncul. Beri waktu user klik Yes di UAC + instalasi jalan.
        Const stepMs As Integer = 2000
        Const maxMs As Integer = 150000    ' 2,5 menit
        Dim waited As Integer = 0
        While waited < maxMs
            Thread.Sleep(stepMs)
            waited += stepMs

            If IsPrinterInstalled(expectedPrinter) Then Return ""   ' SUKSES (ground truth)

            ' Bila installer sudah menulis log dengan kode GAGAL terminal (bukan 0/-3), berhenti awal.
            Dim code As Integer
            If TryParseResultCode(resultLog, code) AndAlso code <> 0 AndAlso code <> -3 Then
                Return "Instalasi gagal (Result Code " & code & ")."
            End If
        End While

        Return "Instalasi tidak terdeteksi dalam 2,5 menit. Pastikan Anda klik 'Yes' saat jendela UAC (izin admin) muncul."
    End Function

    ' Baca "Result Code" dari log TANPA melempar. Return False bila log belum ada / tak terbaca.
    Private Function TryParseResultCode(logPath As String, ByRef code As Integer) As Boolean
        code = 0
        Try
            If Not File.Exists(logPath) Then Return False
            Dim txt As String = File.ReadAllText(logPath)
            Dim m As Match = Regex.Match(txt, "Result\s*Code\D*(-?\d+)", RegexOptions.IgnoreCase)
            If Not m.Success Then Return False
            code = Integer.Parse(m.Groups(1).Value)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Function IsPrinterInstalled(name As String) As Boolean
        For Each n As String In Printers.Installed()
            If String.Equals(n, name, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Function ErrJson(code As String, msg As String) As String
        Return "{""ok"":false,""error"":" & JStr(code) & ",""message"":" & JStr(msg) & "}"
    End Function

    ' Escaper JSON string via Newtonsoft (menghasilkan literal ber-tanda-kutip yang valid).
    Private Function JStr(s As String) As String
        Return JsonConvert.SerializeObject(If(s, ""))
    End Function

End Module
