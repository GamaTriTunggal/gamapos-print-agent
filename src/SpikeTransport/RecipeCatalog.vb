' Katalog resep "Pasang Otomatis" dari server (papan printer PR-12; keputusan pemilik K-5
' 5 Sep 2026; register repo Go P-575 sisi server). Resep = DATA yang ditandatangani OFFLINE
' oleh pemilik (Ed25519); agent hanya mengenal `Kind` sebagai kode. Kontrak amplop + isi:
' gamapos-go-2/docs/reference/print-agent-contract.md §C.2.
'
' Urutan kepercayaan: benih bawaan (resep v1.0.2, TM-U220) → salinan terakhir-berhasil di disk
' (diverifikasi ulang saat dibaca) → katalog server (diverifikasi, versi tidak boleh mundur).
' Katalog yang tidak sah TIDAK PERNAH dipakai — yang lama tetap berlaku.

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Org.BouncyCastle.Crypto.Parameters
Imports Org.BouncyCastle.Crypto.Signers

' Satu resep pasang otomatis (bentuk = catalog.json server).
Public Class Recipe
    Public Property Model As String
    Public Property Kind As String          ' "apd" | "seagull" — KODE di PrinterSetup
    Public Property Url As String
    Public Property FileName As String
    Public Property Sha256 As String        ' hex 64 huruf kecil
    Public Property Size As Long
    Public Property PrinterName As String
    Public Property Role As String          ' CASHIER | QRLABEL | DELIVERY | REPORT
    Public Property DriverModel As String   ' seagull saja
    Public Property Disabled As Boolean
End Class

Module RecipeCatalog

    ' Alamat TETAP (K-5). Override HANYA ke loopback (smoke CI) lewat env GAMA_AGENT_CATALOG_URL
    ' atau body POST /catalog/refresh {url}.
    Public Const CatalogUrl As String = "https://app.gamapos.id/print-agent/catalog.json"
    Private Const PublicKeyHex As String = "4e630f211f79045223fad7d6dc4c9f9a5202e7fa78bee3e3783ef972a836a53f"
    Private Const SchemaVersionSupported As Integer = 1
    Private Const PackageHost As String = "installers.gamapos.id"

    ' Benih bawaan = resep yang tertanam di agent v1.0.2 (TM-U220; TM-U220IIB memakai kunci yang
    ' sama — T-11 A). Versi 0 supaya katalog server versi ≥ 1 selalu menggantikannya.
    Private Const SeedJson As String =
        "{""schemaVersion"":1,""version"":0,""signedAt"":""2026-09-13T00:00:00+07:00"",""recipes"":[" &
        "{""model"":""TM-U220"",""kind"":""apd"",""url"":""https://installers.gamapos.id/golden/TM-U220-golden.exe""," &
        """fileName"":""TM-U220-golden.exe"",""sha256"":""61871ea3565d5ba33fc3d2f7880a4bdc422a32e22ee527596fb5d1438c47b7f6""," &
        """size"":15179776,""printerName"":""EPSON TM-U220 Receipt"",""role"":""CASHIER"",""disabled"":false}]}"

    Private ReadOnly StateLock As New Object()
    Private _recipes As New Dictionary(Of String, Recipe)(StringComparer.OrdinalIgnoreCase)
    Private _version As Integer = -1
    Private _source As String = "none"     ' none | seed | disk | server
    Private _lastError As String = ""
    Private _lastRefreshUtc As DateTime = DateTime.MinValue
    Private ReadOnly RefreshLock As New Object()

    ' ---------------------------------------------------------------- akses

    ' Resep aktif untuk model (Nothing bila tak dikenal ATAU disabled).
    Public Function Find(model As String) As Recipe
        If String.IsNullOrWhiteSpace(model) Then Return Nothing
        SyncLock StateLock
            Dim r As Recipe = Nothing
            If _recipes.TryGetValue(model.Trim(), r) AndAlso Not r.Disabled Then Return r
            Return Nothing
        End SyncLock
    End Function

    Public Function Version() As Integer
        SyncLock StateLock
            Return _version
        End SyncLock
    End Function

    Public Function Source() As String
        SyncLock StateLock
            Return _source
        End SyncLock
    End Function

    ' GET /recipes → {ok, catalogVersion, source, recipes:[{model, kind, printerName, role, disabled:false}]}
    ' Hanya resep AKTIF (kontrak §C.2: resep ber-disabled:true tidak ditawarkan).
    Public Function RecipesJson() As String
        Dim list As New List(Of Object)()
        SyncLock StateLock
            For Each r As Recipe In _recipes.Values
                If r.Disabled Then Continue For
                list.Add(New Dictionary(Of String, Object) From {
                    {"model", r.Model}, {"kind", r.Kind}, {"printerName", r.PrinterName},
                    {"role", r.Role}, {"disabled", False}})
            Next
            Return JsonConvert.SerializeObject(New Dictionary(Of String, Object) From {
                {"ok", True}, {"catalogVersion", _version}, {"source", _source},
                {"lastError", _lastError}, {"recipes", list}})
        End SyncLock
    End Function

    ' ---------------------------------------------------------------- muat saat start

    ' Benih dulu, lalu salinan terakhir-berhasil di disk bila sah (verifikasi ulang — berkas bisa diedit).
    Public Sub Load()
        Dim code As String = Nothing
        Dim ver As Integer = 0
        Dim recs As Dictionary(Of String, Recipe) = Nothing
        If ParseCatalog(Encoding.UTF8.GetBytes(SeedJson), ver, recs, code) Then
            Swap(recs, ver, "seed")
        Else
            Console.WriteLine("Katalog benih cacat: " & code)
        End If
        Try
            Dim p As String = LastGoodPath()
            If File.Exists(p) Then
                Dim payload As Byte() = Nothing
                If ParseAndVerifyEnvelope(File.ReadAllText(p, Encoding.UTF8), payload, code) AndAlso
                   ParseCatalog(payload, ver, recs, code) Then
                    Swap(recs, ver, "disk")
                    Console.WriteLine("Katalog dari disk: versi " & ver & " (" & recs.Count & " resep)")
                Else
                    Console.WriteLine("Katalog di disk DITOLAK (" & code & ") — memakai benih")
                End If
            End If
        Catch ex As Exception
            Console.WriteLine("Katalog di disk tak terbaca: " & ex.Message)
        End Try
    End Sub

    ' ---------------------------------------------------------------- refresh dari server

    Public Function RefreshAsync() As Task(Of String)
        Return Task.Run(Function() Refresh(Nothing))
    End Function

    ' POST /catalog/refresh — body opsional {url} (loopback saja). Balasan {ok, catalogVersion, source, error?, message?}.
    Public Function RefreshJson(body As String) As String
        Dim url As String = Nothing
        If Not String.IsNullOrWhiteSpace(body) Then
            Try
                Dim o As JObject = JObject.Parse(body)
                url = o.Value(Of String)("url")
            Catch
                Return "{""ok"":false,""error"":""BAD_PAYLOAD"",""message"":""Body harus { \""url\"": \""...\"" } atau kosong.""}"
            End Try
        End If
        Dim code As String = Refresh(url)
        SyncLock StateLock
            Dim o As New Dictionary(Of String, Object) From {
                {"ok", code = ""}, {"catalogVersion", _version}, {"source", _source}}
            If code <> "" Then
                o("error") = code
                o("message") = _lastError
            End If
            Return JsonConvert.SerializeObject(o)
        End SyncLock
    End Function

    ' Ambil + verifikasi + terapkan. Return "" bila sukses, atau KODE galat (pesan di _lastError).
    Public Function Refresh(urlOverride As String) As String
        SyncLock RefreshLock
            Dim url As String = ResolveUrl(urlOverride)
            If url Is Nothing Then Return Fail("CATALOG_URL_REJECTED", "Override alamat katalog hanya boleh ke loopback (127.0.0.1/localhost).")

            Dim envelope As String
            Try
                envelope = HttpGetString(url, 15000)
            Catch ex As Exception
                Return Fail("CATALOG_FETCH_FAILED", "Gagal mengambil katalog: " & ex.Message)
            End Try

            Dim code As String = Nothing
            Dim payload As Byte() = Nothing
            If Not ParseAndVerifyEnvelope(envelope, payload, code) Then Return Fail(code, _lastError)

            Dim ver As Integer = 0
            Dim recs As Dictionary(Of String, Recipe) = Nothing
            If Not ParseCatalog(payload, ver, recs, code) Then Return Fail(code, _lastError)

            Dim current As Integer
            SyncLock StateLock
                current = _version
            End SyncLock
            ' Anti-rollback: katalog server tidak boleh lebih tua dari yang sudah dipercaya.
            If ver < current Then Return Fail("CATALOG_ROLLBACK", "Katalog versi " & ver & " lebih tua dari versi tersimpan " & current & ".")

            Try
                Dim p As String = LastGoodPath()
                File.WriteAllText(p & ".tmp", envelope, New UTF8Encoding(False))
                If File.Exists(p) Then File.Delete(p)
                File.Move(p & ".tmp", p)
            Catch ex As Exception
                Console.WriteLine("Katalog: gagal menyimpan salinan terakhir-berhasil: " & ex.Message)
            End Try
            Swap(recs, ver, "server")
            SyncLock StateLock
                _lastError = ""
                _lastRefreshUtc = DateTime.UtcNow
            End SyncLock
            Console.WriteLine("Katalog dari server: versi " & ver & " (" & recs.Count & " resep)")
            Return ""
        End SyncLock
    End Function

    Private Function Fail(code As String, msg As String) As String
        SyncLock StateLock
            _lastError = If(msg, "")
        End SyncLock
        Console.WriteLine("Katalog DITOLAK: " & code & " — " & msg)
        Return code
    End Function

    Private Sub Swap(recs As Dictionary(Of String, Recipe), ver As Integer, source As String)
        SyncLock StateLock
            _recipes = recs
            _version = ver
            _source = source
        End SyncLock
    End Sub

    ' Alamat: override body → env → alamat tetap. Override/env WAJIB loopback; selain itu Nothing (ditolak).
    Private Function ResolveUrl(urlOverride As String) As String
        Dim cand As String = urlOverride
        If String.IsNullOrWhiteSpace(cand) Then cand = Environment.GetEnvironmentVariable("GAMA_AGENT_CATALOG_URL")
        If String.IsNullOrWhiteSpace(cand) Then Return CatalogUrl
        Dim u As Uri = Nothing
        If Not Uri.TryCreate(cand.Trim(), UriKind.Absolute, u) Then Return Nothing
        If u.Scheme <> "http" AndAlso u.Scheme <> "https" Then Return Nothing
        If Not u.IsLoopback Then Return Nothing
        Return u.ToString()
    End Function

    Private Function HttpGetString(url As String, timeoutMs As Integer) As String
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim req As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
        req.Method = "GET"
        req.Timeout = timeoutMs
        req.ReadWriteTimeout = timeoutMs
        req.UserAgent = "GamaPrintAgent/" & Program.AgentVersionString()
        req.Accept = "application/json"
        Using res As HttpWebResponse = CType(req.GetResponse(), HttpWebResponse)
            If CInt(res.StatusCode) <> 200 Then Throw New Exception("HTTP " & CInt(res.StatusCode))
            Using sr As New StreamReader(res.GetResponseStream(), Encoding.UTF8)
                Return sr.ReadToEnd()
            End Using
        End Using
    End Function

    ' ---------------------------------------------------------------- verifikasi + parse

    ' Amplop {ok, alg, keyId, payload(base64), signature(base64)} → payload byte mentah bila tanda tangan sah.
    Private Function ParseAndVerifyEnvelope(envelope As String, ByRef payload As Byte(), ByRef code As String) As Boolean
        payload = Nothing
        Dim payloadB64 As String = Nothing
        Dim sigB64 As String = Nothing
        Try
            Dim o As JObject = JObject.Parse(envelope)
            payloadB64 = o.Value(Of String)("payload")
            sigB64 = o.Value(Of String)("signature")
            Dim alg As String = o.Value(Of String)("alg")
            If Not String.IsNullOrEmpty(alg) AndAlso Not String.Equals(alg, "ed25519", StringComparison.OrdinalIgnoreCase) Then
                code = "CATALOG_ENVELOPE_INVALID"
                Fail(code, "alg tidak dikenal: " & alg)
                Return False
            End If
        Catch ex As Exception
            code = "CATALOG_ENVELOPE_INVALID"
            Fail(code, "Amplop bukan JSON yang dikenal: " & ex.Message)
            Return False
        End Try
        If String.IsNullOrEmpty(payloadB64) OrElse String.IsNullOrEmpty(sigB64) Then
            code = "CATALOG_ENVELOPE_INVALID"
            Fail(code, "Amplop tanpa payload/signature.")
            Return False
        End If
        Dim sig As Byte()
        Try
            payload = Convert.FromBase64String(payloadB64)
            sig = Convert.FromBase64String(sigB64)
        Catch ex As Exception
            code = "CATALOG_ENVELOPE_INVALID"
            Fail(code, "payload/signature bukan base64: " & ex.Message)
            payload = Nothing
            Return False
        End Try
        If sig.Length <> 64 OrElse Not VerifyEd25519(payload, sig) Then
            code = "SIGNATURE_INVALID"
            Fail(code, "Tanda tangan katalog tidak sah untuk kunci publik yang tertanam.")
            payload = Nothing
            Return False
        End If
        Return True
    End Function

    Private Function VerifyEd25519(payload As Byte(), sig As Byte()) As Boolean
        Try
            Dim pub As New Ed25519PublicKeyParameters(HexToBytes(PublicKeyHex), 0)
            Dim signer As New Ed25519Signer()
            signer.Init(False, pub)
            signer.BlockUpdate(payload, 0, payload.Length)
            Return signer.VerifySignature(sig)
        Catch ex As Exception
            Console.WriteLine("Katalog: verifikasi gagal dijalankan: " & ex.Message)
            Return False
        End Try
    End Function

    Private Function HexToBytes(hex As String) As Byte()
        Dim b(hex.Length \ 2 - 1) As Byte
        For i As Integer = 0 To b.Length - 1
            b(i) = Convert.ToByte(hex.Substring(i * 2, 2), 16)
        Next
        Return b
    End Function

    ' Isi katalog (sudah terverifikasi) → resep. Satu resep cacat = SELURUH katalog ditolak.
    Private Function ParseCatalog(payload As Byte(), ByRef version As Integer, ByRef recipes As Dictionary(Of String, Recipe), ByRef code As String) As Boolean
        recipes = Nothing
        version = 0
        Try
            Dim o As JObject = JObject.Parse(Encoding.UTF8.GetString(payload))
            If o.Value(Of Integer)("schemaVersion") <> SchemaVersionSupported Then
                code = "CATALOG_INVALID"
                Fail(code, "schemaVersion katalog tidak didukung.")
                Return False
            End If
            version = o.Value(Of Integer)("version")
            Dim arr As JArray = TryCast(o("recipes"), JArray)
            If arr Is Nothing Then
                code = "CATALOG_INVALID"
                Fail(code, "Katalog tanpa daftar recipes.")
                Return False
            End If
            Dim d As New Dictionary(Of String, Recipe)(StringComparer.OrdinalIgnoreCase)
            For Each it As JToken In arr
                Dim r As Recipe = it.ToObject(Of Recipe)()
                Dim why As String = ValidateRecipe(r)
                If why <> "" Then
                    code = "CATALOG_INVALID"
                    Fail(code, "Resep " & If(r.Model, "?") & " cacat: " & why)
                    Return False
                End If
                If d.ContainsKey(r.Model) Then
                    code = "CATALOG_INVALID"
                    Fail(code, "Model ganda: " & r.Model)
                    Return False
                End If
                d(r.Model) = r
            Next
            recipes = d
            Return True
        Catch ex As Exception
            code = "CATALOG_INVALID"
            Fail(code, "Katalog bukan JSON yang dikenal: " & ex.Message)
            Return False
        End Try
    End Function

    Private ReadOnly HexSha As New Regex("^[0-9a-f]{64}$", RegexOptions.Compiled)

    Private Function ValidateRecipe(r As Recipe) As String
        If r Is Nothing Then Return "kosong"
        If String.IsNullOrWhiteSpace(r.Model) Then Return "model kosong"
        If r.Kind <> "apd" AndAlso r.Kind <> "seagull" Then Return "kind tidak dikenal"
        Dim u As Uri = Nothing
        If Not Uri.TryCreate(If(r.Url, ""), UriKind.Absolute, u) OrElse u.Scheme <> "https" OrElse
           Not String.Equals(u.Host, PackageHost, StringComparison.OrdinalIgnoreCase) Then
            Return "url harus https://" & PackageHost & "/…"
        End If
        If String.IsNullOrWhiteSpace(r.FileName) OrElse r.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then Return "fileName cacat"
        If r.Sha256 Is Nothing OrElse Not HexSha.IsMatch(r.Sha256) Then Return "sha256 bukan hex 64"
        If String.IsNullOrWhiteSpace(r.PrinterName) Then Return "printerName kosong"
        Select Case r.Role
            Case "CASHIER", "QRLABEL", "DELIVERY", "REPORT"
            Case Else
                Return "role tidak dikenal"
        End Select
        If r.Kind = "seagull" AndAlso String.IsNullOrWhiteSpace(r.DriverModel) Then Return "seagull butuh driverModel"
        Return ""
    End Function

    Private Function LastGoodPath() As String
        Return Path.Combine(AppPaths.CatalogDir(), "catalog.json")
    End Function

End Module
