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
' Residu PR-12 (v1.2.0, P-592 register repo Go; K-16 #4): state `waiting_printer` — sesudah driver
' terpasang (apd) atau SEBELUM installer dijalankan (seagull, DriverWizard /autodetect butuh printer
' tercolok), agent menunggu perangkat USB ber-hardware-ID resep hadir (DeviceProbe/WMI) dan antrean
' online; kode `waiting` = WAITING_CABLE | WAITING_ONLINE. Auto-map peran DUA TAHAP: peran kosong atau
' sudah menunjuk printer yang sama → ditulis; menunjuk printer LAIN → tidak ditimpa, status `mapping`
' = "conflict" + `previousPrinter` → web bertanya "ganti X ke Y?". POST /setup/cancel membatalkan
' menunggu/pemasangan. Alur cetak (K-1c) tidak disentuh.
'
' Tanpa deklarasi Namespace (RootNamespace GamaPrintAgent.SpikeTransport ditambahkan otomatis) —
' lihat catatan yang sama di Program.vb.

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Security.Cryptography

Module PrinterSetup

    ' Resep TIDAK lagi tertanam di sini (PR-12, K-5): sumbernya RecipeCatalog (benih bawaan → salinan
    ' terakhir-berhasil → katalog server bertanda tangan). Modul ini hanya mengenal `Kind` sebagai kode.

    ' Galat pemasangan ber-KODE terstruktur (kontrak §C.2: web membaca `error`, bukan mem-parse teks).
    Private Class SetupError
        Inherits Exception
        Public ReadOnly Code As String
        Public Sub New(code As String, message As String)
            MyBase.New(message)
            Me.Code = code
        End Sub
    End Class

    ' Status pemasangan (async). Hanya SATU setup berjalan pada satu waktu (dijaga StatusLock).
    Private ReadOnly StatusLock As New Object()
    Private _state As String = "idle"     ' idle | running | waiting_printer | done | failed
    Private _model As String = ""
    Private _printer As String = ""
    Private _role As String = ""
    Private _message As String = ""
    Private _error As String = ""        ' kode galat terstruktur saat failed (PR-12), "" bila tidak ada
    Private _waiting As String = ""      ' WAITING_CABLE | WAITING_ONLINE saat waiting_printer (P-592)
    Private _mapping As String = ""      ' applied | conflict — hasil auto-map peran (P-592)
    Private _previousPrinter As String = ""   ' printer yang SUDAH dipetakan ke peran saat conflict
    Private _warning As String = ""      ' PRINTER_NOT_DETECTED bila selesai tanpa printer terdeteksi
    Private _cancel As Boolean = False   ' diminta POST /setup/cancel; dibaca loop menunggu/poll

    Private Const DeviceWaitMs As Integer = 10 * 60 * 1000   ' batas menunggu printer tercolok
    Private Const OnlineWaitMs As Integer = 2 * 60 * 1000    ' batas menunggu antrean online sesudah tercolok

    ' Folder cache paket preset — di DataDir (%LOCALAPPDATA%) supaya bertahan lintas update Velopack.
    Private Function SetupDir() As String
        Dim d As String = Path.Combine(AppPaths.DataDir(), "setup")
        Try
            Directory.CreateDirectory(d)
        Catch
        End Try
        Return d
    End Function

    ' Handler POST /setup/printer. Body: {"model":"TM-U220"}. Memulai pemasangan di BACKGROUND
    ' lalu LANGSUNG balas {state:"running"} (tak menggantung). Web memantau via GET /setup/status.
    Public Function HandleSetup(body As String) As String
        Dim model As String = Nothing
        Try
            Dim o As JObject = JObject.Parse(body)
            model = o.Value(Of String)("model")
        Catch
            Return ErrJson("BAD_PAYLOAD", "Body harus { ""model"": ""..."" }.")
        End Try
        If String.IsNullOrWhiteSpace(model) Then Return ErrJson("BAD_PAYLOAD", "model kosong.")

        Dim rec As Recipe = RecipeCatalog.Find(model)
        If rec Is Nothing Then
            Return ErrJson("UNSUPPORTED_MODEL", "Model tidak dikenal: " & model)
        End If

        ' Cek-dan-set atomik: tolak bila sudah ada setup berjalan.
        SyncLock StatusLock
            If _state = "running" OrElse _state = "waiting_printer" Then
                Return "{""ok"":false,""error"":""BUSY"",""message"":""Sedang memasang. Tunggu selesai atau batalkan."",""state"":" & JStr(_state) & ",""model"":" & JStr(_model) & "}"
            End If
            _state = "running"
            _model = rec.Model
            _printer = rec.PrinterName
            _role = rec.Role
            _message = "Menyiapkan…"
            _error = ""
            _waiting = ""
            _mapping = ""
            _previousPrinter = ""
            _warning = ""
            _cancel = False
        End SyncLock
        Program.MarkActivity()

        Dim t As New Thread(Sub() RunSetupBackground(rec))
        t.IsBackground = True
        t.Start()

        Return "{""ok"":true,""state"":""running"",""model"":" & JStr(rec.Model) & "}"
    End Function

    ' JSON untuk GET /setup/status → dipoll web sampai state = done/failed.
    Public Function StatusJson() As String
        SyncLock StatusLock
            Return JsonConvert.SerializeObject(New Dictionary(Of String, Object) From {
                {"ok", True},
                {"state", _state},
                {"model", _model},
                {"printer", _printer},
                {"role", _role},
                {"message", _message},
                {"error", If(_error = "", Nothing, CObj(_error))},
                {"waiting", If(_waiting = "", Nothing, CObj(_waiting))},
                {"mapping", If(_mapping = "", Nothing, CObj(_mapping))},
                {"previousPrinter", If(_previousPrinter = "", Nothing, CObj(_previousPrinter))},
                {"warning", If(_warning = "", Nothing, CObj(_warning))}
            })
        End SyncLock
    End Function

    ' Sedang memasang/menunggu? (dipakai penundaan penerapan update saat menganggur — Tray.OnApplyTick)
    Public Function IsRunning() As Boolean
        SyncLock StatusLock
            Return _state = "running" OrElse _state = "waiting_printer"
        End SyncLock
    End Function

    ' POST /setup/cancel (P-592): hentikan menunggu printer / pemasangan yang sedang berjalan.
    ' Menunggu ≤ 4 dtk sampai thread latar benar-benar berpindah state, lalu balas state akhirnya.
    Public Function HandleCancel() As String
        Dim active As Boolean
        SyncLock StatusLock
            active = (_state = "running" OrElse _state = "waiting_printer")
            If active Then _cancel = True
        End SyncLock
        If active Then
            For i As Integer = 1 To 20
                Thread.Sleep(200)
                SyncLock StatusLock
                    If _state <> "running" AndAlso _state <> "waiting_printer" Then Exit For
                End SyncLock
            Next
        End If
        SyncLock StatusLock
            Return "{""ok"":true,""state"":" & JStr(_state) & ",""cancelled"":" & If(active, "true", "false") & "}"
        End SyncLock
    End Function

    Private Function Cancelled() As Boolean
        SyncLock StatusLock
            Return _cancel
        End SyncLock
    End Function

    Private Sub SetWaiting(code As String, msg As String)
        SyncLock StatusLock
            _state = "waiting_printer"
            _waiting = code
            _message = msg
        End SyncLock
    End Sub

    Private Sub SetRunning(msg As String)
        SyncLock StatusLock
            _state = "running"
            _waiting = ""
            _message = msg
        End SyncLock
    End Sub

    Private Sub SetMessage(msg As String)
        SyncLock StatusLock
            _message = msg
        End SyncLock
    End Sub

    Private Sub SetDone(msg As String, Optional warning As String = "")
        SyncLock StatusLock
            _state = "done"
            _waiting = ""
            _message = msg
            _warning = If(warning, "")
        End SyncLock
    End Sub

    Private Sub SetIdle(msg As String)
        SyncLock StatusLock
            _state = "idle"
            _waiting = ""
            _message = msg
        End SyncLock
    End Sub

    Private Sub SetFailed(code As String, msg As String)
        SyncLock StatusLock
            _state = "failed"
            _waiting = ""
            _message = msg
            _error = If(code, "")
        End SyncLock
    End Sub

    ' Proses pemasangan di background (P-592):
    '   apd     : (antrean belum ada → unduh → jalankan, UAC) → auto-map peran → bila baru dipasang dan resep
    '             punya usbIds: waiting_printer sampai printer tercolok & online (≤10 mnt; lewat = done + warning).
    '   seagull : (antrean belum ada → waiting_printer SAMPAI printer tercolok [DriverWizard /autodetect butuh
    '             printer] → unduh → jalankan, UAC) → auto-map peran.
    ' Antrean yang SUDAH ada (idempoten, toko lama) tidak pernah menunggu — perilaku 1.1.0.
    Private Sub RunSetupBackground(rec As Recipe)
        Try
            Console.WriteLine("Setup printer: " & rec.PrinterName & " (" & rec.Kind & ")")
            Dim queueExisted As Boolean = IsPrinterInstalled(rec.PrinterName)
            Dim kind As String = rec.Kind.ToLowerInvariant()
            If kind <> "apd" AndAlso kind <> "seagull" Then Throw New SetupError("UNSUPPORTED_KIND", "Jenis installer tak dikenal: " & rec.Kind)

            If Not queueExisted Then
                If kind = "seagull" Then
                    ' DriverWizard /autodetect hanya berhasil bila printer tercolok → tunggu DULU (sebelum unduh 57 MB & UAC).
                    WaitForDevice(rec, "Colok kabel USB printer label ke komputer ini dan nyalakan printernya — pemasangan lanjut otomatis begitu printer terdeteksi.", True)
                End If
                SetRunning("Menyiapkan installer…")
                Dim pkg As String = EnsurePackage(rec)
                SetRunning("Memasang driver — klik 'Yes' saat izin admin (UAC) muncul…")
                If kind = "apd" Then
                    RunApdAndWait(pkg, rec.PrinterName)
                Else
                    RunSeagullAndWait(pkg, rec.PrinterName, rec.DriverModel)
                End If
            End If

            Dim mapping As String = ApplyRoleMapping(rec)
            Console.WriteLine("   setup OK: " & rec.PrinterName & " → " & rec.Role & " (mapping " & mapping & ")")

            Dim warning As String = ""
            If kind = "apd" AndAlso Not queueExisted Then
                ' Driver + antrean ada. Sebelum toko menekan "cetak percobaan": tunggu printer tercolok & online.
                If Not WaitForDevice(rec, "Driver terpasang. Sekarang colok kabel USB printer ke komputer ini dan nyalakan printernya — kami menunggu…", False) Then
                    warning = "PRINTER_NOT_DETECTED"
                End If
            End If

            Dim msg As String
            If mapping = "conflict" Then
                msg = "Driver terpasang. Printer untuk peran ini di komputer ini masih '" & PreviousPrinter() & "' — pilih: ganti ke '" & rec.PrinterName & "' atau tetap."
            ElseIf warning <> "" Then
                msg = "Driver terpasang dan printer sudah dipilih. Printer belum terdeteksi di USB — colok dan nyalakan sebelum cetak percobaan."
            Else
                msg = "Selesai — printer siap dipakai."
            End If
            SetDone(msg, warning)
        Catch se As SetupError
            Console.WriteLine("   " & se.Code & ": " & se.Message)
            If se.Code = "CANCELLED_IDLE" Then
                SetIdle("Dibatalkan.")
            Else
                SetFailed(se.Code, se.Message)
            End If
        Catch ex As Exception
            Console.WriteLine("   SETUP_FAILED: " & ex.Message)
            SetFailed("SETUP_FAILED", ex.Message)
        End Try
    End Sub

    Private Function PreviousPrinter() As String
        SyncLock StatusLock
            Return _previousPrinter
        End SyncLock
    End Function

    ' Auto-map DUA TAHAP (P-592): peran kosong / sudah menunjuk printer yang sama → tulis (merge);
    ' peran menunjuk printer LAIN → JANGAN timpa: laporkan conflict + previousPrinter, web yang bertanya.
    Private Function ApplyRoleMapping(rec As Recipe) As String
        Dim cur As String = Printers.Resolve(rec.Role)
        If cur = "" OrElse String.Equals(cur, rec.PrinterName, StringComparison.OrdinalIgnoreCase) Then
            Printers.SetRole(rec.Role, rec.PrinterName)
            SyncLock StatusLock
                _mapping = "applied"
                _previousPrinter = ""
            End SyncLock
            Return "applied"
        End If
        SyncLock StatusLock
            _mapping = "conflict"
            _previousPrinter = cur
        End SyncLock
        Console.WriteLine("   peran " & rec.Role & " sudah menunjuk '" & cur & "' — tidak ditimpa (conflict)")
        Return "conflict"
    End Function

    ' Tunggu perangkat USB resep hadir lalu antrean online (state waiting_printer). Return True bila
    ' terdeteksi (atau tidak bisa diperiksa: tanpa usbIds / WMI gagal → dianggap hadir supaya tidak
    ' mengunci). False = lewat batas waktu. `pre` = sebelum pemasangan: batal → CANCELLED_IDLE (tak ada
    ' yang terpasang, kembali idle); lewat batas → PRINTER_NOT_DETECTED (failed).
    Private Function WaitForDevice(rec As Recipe, cableMsg As String, pre As Boolean) As Boolean
        Dim present As Boolean? = DeviceProbe.UsbDevicePresent(rec.UsbIds)
        If Not present.HasValue Then Return True   ' tidak bisa diperiksa → lanjut
        Dim waited As Integer = 0
        Const stepMs As Integer = 2000
        While Not present.Value
            If waited = 0 Then
                SetWaiting("WAITING_CABLE", cableMsg)
                Console.WriteLine("   menunggu printer tercolok (" & String.Join(",", rec.UsbIds) & ")…")
            End If
            If Cancelled() Then
                If pre Then Throw New SetupError("CANCELLED_IDLE", "Dibatalkan sebelum pemasangan.")
                Return False
            End If
            If waited >= DeviceWaitMs Then
                If pre Then Throw New SetupError("PRINTER_NOT_DETECTED", "Printer tidak terdeteksi di USB dalam 10 menit. Colok dan nyalakan printer, lalu klik Pasang otomatis lagi.")
                Return False
            End If
            Thread.Sleep(stepMs)
            waited += stepMs
            Program.MarkActivity()
            present = DeviceProbe.UsbDevicePresent(rec.UsbIds)
            If Not present.HasValue Then Return True
        End While
        If pre Then Return True   ' sebelum pemasangan antrean belum ada — keadaan online diperiksa nanti

        ' Perangkat hadir. Antrean online? (kertas/penutup/offline) — batas 2 menit, lalu lanjut apa adanya.
        Dim onlineWaited As Integer = 0
        While onlineWaited < OnlineWaitMs
            Dim st As String = DeviceProbe.QueueState(rec.PrinterName)
            If st = "online" OrElse st = "unknown" OrElse st = "missing" Then Exit While
            If onlineWaited = 0 Then
                SetWaiting("WAITING_ONLINE", "Printer terdeteksi tetapi belum siap: pasang kertas dan pita, tutup penutupnya, pastikan lampu tidak berkedip merah.")
                Console.WriteLine("   antrean " & rec.PrinterName & " " & st & " — menunggu online…")
            End If
            If Cancelled() Then Exit While
            Thread.Sleep(stepMs)
            onlineWaited += stepMs
        End While
        Return True
    End Function

    ' Paket golden di cache HANYA dipercaya bila SHA-256-nya = katalog (PR-12; K-16 #2): cache tak
    ' cocok → dihapus + diunduh ulang; unduhan diverifikasi lagi; 3 percobaan bertingkat →
    ' DOWNLOAD_FAILED / HASH_MISMATCH (kode terstruktur → web menawarkan jalur manual).
    Private Function EnsurePackage(rec As Recipe) As String
        Dim dest As String = Path.Combine(SetupDir(), rec.FileName)
        Try
            If File.Exists(dest) Then
                If String.Equals(Sha256Hex(dest), rec.Sha256, StringComparison.OrdinalIgnoreCase) Then
                    Console.WriteLine("   paket di cache cocok SHA-256: " & dest)
                    Return dest
                End If
                Console.WriteLine("   paket di cache TIDAK cocok SHA-256 — dihapus: " & dest)
                File.Delete(dest)
            End If
        Catch ex As Exception
            Console.WriteLine("   cache tak terbaca (" & ex.Message & ") — unduh ulang")
        End Try

        ' Cloudflare butuh TLS 1.2+. net48 umumnya default OS, tapi pastikan.
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim tmp As String = dest & ".tmp"
        Dim lastErr As String = ""
        Dim lastCode As String = "DOWNLOAD_FAILED"
        For attempt As Integer = 1 To 3
            SetMessage("Mengunduh paket driver (" & attempt & "/3)…")
            Console.WriteLine("   mengunduh paket (" & attempt & "/3): " & rec.Url)
            Try
                Using wc As New TimeoutWebClient(180000)
                    wc.DownloadFile(rec.Url, tmp)
                End Using
                Dim got As String = Sha256Hex(tmp)
                If Not String.Equals(got, rec.Sha256, StringComparison.OrdinalIgnoreCase) Then
                    lastCode = "HASH_MISMATCH"
                    lastErr = "SHA-256 paket tidak cocok dengan katalog (" & got.Substring(0, 12) & "… ≠ " & rec.Sha256.Substring(0, 12) & "…)."
                    Console.WriteLine("   " & lastErr)
                    Try
                        File.Delete(tmp)
                    Catch
                    End Try
                Else
                    If File.Exists(dest) Then File.Delete(dest)
                    File.Move(tmp, dest)
                    Return dest
                End If
            Catch ex As Exception
                lastCode = "DOWNLOAD_FAILED"
                lastErr = "Gagal mengunduh paket: " & ex.Message
                Console.WriteLine("   " & lastErr)
                Try
                    If File.Exists(tmp) Then File.Delete(tmp)
                Catch
                End Try
            End Try
            If Cancelled() Then Throw New SetupError("CANCELLED", "Pemasangan dibatalkan.")
            If attempt < 3 Then Thread.Sleep(2000 * attempt)
        Next
        Throw New SetupError(lastCode, lastErr & " Coba lagi nanti, atau pasang manual.")
    End Function

    ' WebClient dengan batas waktu (bawaan WebClient tanpa timeout → pemasangan bisa menggantung selamanya).
    Private Class TimeoutWebClient
        Inherits WebClient
        Private ReadOnly _timeoutMs As Integer
        Public Sub New(timeoutMs As Integer)
            _timeoutMs = timeoutMs
        End Sub
        Protected Overrides Function GetWebRequest(address As Uri) As WebRequest
            Dim r As WebRequest = MyBase.GetWebRequest(address)
            If r IsNot Nothing Then r.Timeout = _timeoutMs
            Return r
        End Function
    End Class

    Private Function Sha256Hex(filePath As String) As String
        Using sha As SHA256 = SHA256.Create()
            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant()
            End Using
        End Using
    End Function

    ' Jalankan paket APD Silent lalu POLL sampai printer muncul (bukti sebenarnya).
    ' Kenapa poll, bukan WaitForExit: paket minta admin → diluncurkan lewat mekanisme elevasi
    ' (broker), sehingga proses yang dikembalikan Process.Start langsung "exit" → WaitForExit
    ' tak berarti. UAC muncul selama poll → user klik Yes → driver terpasang → printer muncul.
    ' Return "" bila sukses, atau string alasan bila gagal.
    Private Sub RunApdAndWait(pkg As String, expectedPrinter As String)
        ' Sudah terpasang? Lewati install (idempotent) — hindari UAC ulang; peran tetap di-map di pemanggil.
        If IsPrinterInstalled(expectedPrinter) Then Return

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
            Throw New SetupError("INSTALL_START_FAILED", "Gagal menjalankan paket installer: " & ex.Message)
        End Try

        ' Poll: SUKSES saat printer muncul. Beri waktu user klik Yes di UAC + instalasi jalan.
        Const stepMs As Integer = 2000
        Const maxMs As Integer = 150000    ' 2,5 menit
        Dim waited As Integer = 0
        While waited < maxMs
            Thread.Sleep(stepMs)
            waited += stepMs

            If IsPrinterInstalled(expectedPrinter) Then Return   ' SUKSES (ground truth)
            If Cancelled() Then Throw New SetupError("CANCELLED", "Pemasangan dibatalkan.")

            ' Bila installer sudah menulis log dengan kode GAGAL terminal (bukan 0/-3), berhenti awal.
            Dim code As Integer
            If TryParseResultCode(resultLog, code) AndAlso code <> 0 AndAlso code <> -3 Then
                Throw New SetupError("INSTALL_FAILED", "Instalasi gagal (Result Code " & code & ").")
            End If
        End While

        Throw New SetupError("UAC_TIMEOUT", "Instalasi tidak terdeteksi dalam 2,5 menit. Pastikan Anda klik 'Yes' saat jendela UAC (izin admin) muncul.")
    End Sub

    ' Paket driver Seagull (zip) → extract → jalankan DriverWizard install (unattended) → POLL printer muncul.
    ' Golden config membawa DAFTAR stock (mis. 40×30) lewat Common\Defaults[SS]_*.sds yang sudah di-bake.
    ' Default stock TIDAK di-set di sini (batasan Seagull pada install baru) — QrLabel.vb yang memilih 40×30
    ' saat cetak. /autodetect = deteksi port USB printer yang tercolok (butuh printer fisik saat install).
    Private Sub RunSeagullAndWait(zipPath As String, expectedPrinter As String, driverModel As String)
        ' Sudah terpasang? Lewati (idempotent) — peran tetap di-map di pemanggil.
        If IsPrinterInstalled(expectedPrinter) Then Return

        ' Extract paket ke folder di samping zip.
        Dim extractDir As String = Path.Combine(Path.GetDirectoryName(zipPath), "xprinter-driver")
        Try
            If Directory.Exists(extractDir) Then Directory.Delete(extractDir, True)
        Catch
        End Try
        Try
            ZipFile.ExtractToDirectory(zipPath, extractDir)
        Catch ex As Exception
            Throw New SetupError("EXTRACT_FAILED", "Gagal mengekstrak paket driver: " & ex.Message)
        End Try

        Dim dw As String = FindFileRecursive(extractDir, "DriverWizard.exe")
        If String.IsNullOrEmpty(dw) Then Throw New SetupError("PACKAGE_INVALID", "DriverWizard.exe tak ditemukan di paket driver.")

        Dim args As String = "install /name:""" & expectedPrinter & """ /model:""" & driverModel & """ /autodetect"
        Dim psi As New ProcessStartInfo() With {
            .FileName = dw,
            .Arguments = args,
            .WorkingDirectory = Path.GetDirectoryName(dw),
            .UseShellExecute = True      ' WAJIB True agar bisa minta elevasi (UAC).
        }
        Console.WriteLine("   menjalankan DriverWizard (jendela UAC mungkin muncul — klik Yes)...")
        Try
            Process.Start(psi)
        Catch ex As Exception
            Throw New SetupError("INSTALL_START_FAILED", "Gagal menjalankan DriverWizard: " & ex.Message)
        End Try

        ' Poll: SUKSES saat printer muncul (bukti). Beri waktu UAC + instalasi.
        Const stepMs As Integer = 2000
        Const maxMs As Integer = 150000    ' 2,5 menit
        Dim waited As Integer = 0
        While waited < maxMs
            Thread.Sleep(stepMs)
            waited += stepMs
            If IsPrinterInstalled(expectedPrinter) Then Return
            If Cancelled() Then Throw New SetupError("CANCELLED", "Pemasangan dibatalkan.")
        End While

        Throw New SetupError("UAC_TIMEOUT", "Instalasi tidak terdeteksi dalam 2,5 menit. Pastikan printer tercolok (USB) & klik 'Yes' saat UAC.")
    End Sub

    ' Cari file (nama persis) rekursif di dalam root. "" bila tak ada.
    Private Function FindFileRecursive(root As String, fileName As String) As String
        Try
            Dim hits() As String = Directory.GetFiles(root, fileName, SearchOption.AllDirectories)
            If hits.Length > 0 Then Return hits(0)
        Catch
        End Try
        Return ""
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
