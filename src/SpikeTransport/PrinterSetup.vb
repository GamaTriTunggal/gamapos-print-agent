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

            ' 2) Jalankan senyap + baca Result Code.
            Dim resultCode As Integer = RunApdSilent(pkg)
            ' 0 = sukses, -3 = sudah terpasang — dua-duanya berarti driver ADA di mesin ini.
            If resultCode <> 0 AndAlso resultCode <> -3 Then
                Return ErrJson("INSTALL_FAILED", "Instalasi gagal (Result Code " & resultCode & "). Lihat APD4SilentSetup.log.")
            End If

            ' 3) Verifikasi printer benar-benar terpasang.
            If Not IsPrinterInstalled(rec.PrinterName) Then
                Return ErrJson("PRINTER_NOT_FOUND", "Driver terpasang tapi printer '" & rec.PrinterName & "' tak ditemukan.")
            End If

            ' 4) Auto-map peran ke printers.json (merge — peran lain tak diubah).
            Printers.SetRole(rec.Role, rec.PrinterName)

            Console.WriteLine("   setup OK: " & rec.PrinterName & " → " & rec.Role & " (Result Code " & resultCode & ")")
            Return "{""ok"":true,""printer"":" & JStr(rec.PrinterName) & ",""role"":" & JStr(rec.Role) & ",""resultCode"":" & resultCode & "}"
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

    ' Jalankan paket APD Silent (senyap, tanpa reboot), lalu baca Result Code dari APD4SilentSetup.log
    ' yang dibuat APD di folder yang sama dengan paket. UAC muncul (paket minta admin) → user klik Yes.
    Private Function RunApdSilent(pkg As String) As Integer
        Dim dir As String = Path.GetDirectoryName(pkg)
        Dim resultLog As String = Path.Combine(dir, "APD4SilentSetup.log")
        ' Hapus log lama supaya tak salah baca hasil sebelumnya.
        Try
            If File.Exists(resultLog) Then File.Delete(resultLog)
        Catch
        End Try

        Dim psi As New ProcessStartInfo() With {
            .FileName = pkg,
            .Arguments = "/rN",          ' /rN = jangan reboot; TANPA /d = tanpa dialog (senyap)
            .WorkingDirectory = dir,
            .UseShellExecute = True      ' WAJIB True agar bisa elevasi (UAC). Konsekuensi: tak bisa redirect stdout → hasil dibaca dari log.
        }

        Dim p As Process = Process.Start(psi)
        If p Is Nothing Then Throw New Exception("Gagal menjalankan paket installer.")
        Try
            If Not p.WaitForExit(5 * 60 * 1000) Then   ' timeout 5 menit
                Try
                    p.Kill()
                Catch
                End Try
                Throw New Exception("Instalasi melewati batas waktu (5 menit).")
            End If
        Finally
            p.Dispose()
        End Try

        Return ParseResultCode(resultLog)
    End Function

    ' Ambil angka "Result Code" (bisa negatif) dari APD4SilentSetup.log.
    Private Function ParseResultCode(logPath As String) As Integer
        If Not File.Exists(logPath) Then
            ' Tak ada log → paket tak jalan (mis. UAC dibatalkan user).
            Throw New Exception("Log hasil tak ditemukan — kemungkinan izin admin (UAC) dibatalkan.")
        End If
        Dim txt As String = File.ReadAllText(logPath)
        Dim m As Match = Regex.Match(txt, "Result\s*Code\D*(-?\d+)", RegexOptions.IgnoreCase)
        If Not m.Success Then Throw New Exception("Result Code tak terbaca di log.")
        Return Integer.Parse(m.Groups(1).Value)
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
