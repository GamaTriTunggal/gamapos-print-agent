' P-645 (CO-11): surat jalan schema 2 `delivery_note`. Parser harus seketat
' validator Go (internal/platform/printagent/v2_delivery.go), dan layout murni
' harus menghasilkan halaman 80 kolom × 32 baris sesuai mockup pemilik 12 Okt
' 2026. Murni (tanpa printer) sehingga jalan di CI Linux/Windows; bukan bukti
' hasil kertas LX-310.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module DeliveryNoteTests
    Private ReadOnly SampleNames As String() = {
        "delivery_note.sample.json", "delivery_note_long.sample.json",
        "delivery_note_reprint.sample.json", "delivery_note_return.sample.json"}

    Friend Sub Run(v2Folder As String, v1Folder As String)
        Dim samples As New Dictionary(Of String, JObject)(StringComparer.Ordinal)
        For Each name As String In SampleNames
            Dim body As String = IO.File.ReadAllText(IO.Path.Combine(v2Folder, name))
            If RoutePrintSchema(body, 1) <> "UNSUPPORTED_SCHEMA" OrElse RoutePrintSchema(body, 2) <> "V2" OrElse
               SelectV2Family(CStr(JObject.Parse(body)("jobType"))) <> "DELIVERY" Then
                Throw New InvalidOperationException("Surat jalan v2 salah rute: " & name)
            End If
            Dim root As JObject = ParseDeliveryNoteV2(body)
            For Each other As Action In {
                Sub() ParseSaleV2(body), Sub() ParseReturnV2(body), Sub() ParseReceivableV2(body)}
                ExpectRejected("parser keluarga lain menerima " & name, other)
            Next
            Dim before As String = root.ToString(Formatting.None)
            Dim pages As List(Of List(Of String)) = BuildDeliveryNotePages(root)
            CheckDeliveryNotePages(pages)
            If root.ToString(Formatting.None) <> before Then Throw New InvalidOperationException("Layout memutasi payload: " & name)
            If pages.Any(Function(page) page.Count <> 32 OrElse page.Any(Function(line) line.Length > 80)) Then
                Throw New InvalidOperationException("Halaman surat jalan bukan 32 baris ≤ 80 kolom: " & name)
            End If
            samples.Add(name, root)
        Next

        CheckV1Untouched(v1Folder)
        CheckSamplePage(samples("delivery_note.sample.json"))
        CheckLongSample(samples("delivery_note_long.sample.json"))
        CheckReprintAndReturn(samples("delivery_note_reprint.sample.json"), samples("delivery_note_return.sample.json"))
        CheckTitles(samples("delivery_note.sample.json"))
        CheckQuantityFormat()
        CheckAddressWrap()
        CheckEdgeLayouts(samples("delivery_note.sample.json"))
        CheckPreflight()
        CheckParserMutations(samples("delivery_note.sample.json"), IO.File.ReadAllText(IO.Path.Combine(v2Folder, "delivery_note.sample.json")))
        Console.WriteLine("Delivery note v2 (P-645): 4 samples parsed, laid out 32×80 and preflighted; parser rejects Go mutation list; delivery_order v1 untouched (not physical).")
    End Sub

    Private Sub CheckV1Untouched(v1Folder As String)
        Dim v1 As String = IO.File.ReadAllText(IO.Path.Combine(v1Folder, "delivery_order.sample.json"))
        If CheckPrintSchema(v1, 1) <> "OK" OrElse RoutePrintSchema(v1, 2) <> "V1" OrElse
           SelectV2Family("delivery_order") <> "UNSUPPORTED_JOBTYPE" Then
            Throw New InvalidOperationException("delivery_order v1 tidak lagi dirutekan ke formatter v1.")
        End If
        ExpectRejected("parser surat jalan v2 menerima delivery_order v1", Sub() ParseDeliveryNoteV2(v1))
    End Sub

    Private Sub CheckSamplePage(root As JObject)
        Dim pages As List(Of List(Of String)) = BuildDeliveryNotePages(root)
        Dim blank21 As String = New String("_"c, 21)
        Dim expected As String() = {
            "VIN JAYA" & S(61) & "SURAT JALAN",
            "JL. RAYA SERANG KM 12, BALARAJA" & S(15) & "No. DO   : DO-2610-00013",
            "0812-8888-1234" & S(32) & "Tanggal  : 12-10-2026",
            New String("-"c, 80),
            "Kepada  : PT. ARTHA SINAR BAJA" & S(16) & "No. SO   : SO-2610-00008",
            "Alamat  : JL. INDUSTRI RAYA III NO. 8," & S(8) & "No. PO   : PO-ASB-0930",
            S(10) & "JATAKE, GUDANG B (MASUK GERBANG 2)," & S(1) & "Sopir    : " & blank21,
            S(10) & "TANGERANG" & S(27) & "Kendaraan: " & blank21,
            "Penerima: PAK RUDI  0812 1234 567",
            New String("="c, 80),
            " No     QTY  NAMA BARANG",
            New String("-"c, 80),
            "  1      79  SEMEN DYNAMIX 40KG",
            "  2      60  PAKU 10CM (1 KG)",
            "  3      18  BESI BETON 10MM SNI 12M",
            "  4       3  CAT TEMBOK AVITEX 25KG PUTIH",
            "  5     2,5  PASIR BANGKA CURAH",
            "", "", "", "", "",
            New String("-"c, 80),
            "Catatan : BONGKAR DI GUDANG B, TELEPON DULU",
            "",
            "        Pengirim,                  Sopir,                   Penerima,",
            "", "", "",
            "   (___________________)    (___________________)    (___________________)",
            "   Tgl:                     Tgl:                     Tgl:",
            "Barang diterima dalam keadaan baik dan jumlah sesuai." & S(14) & "Halaman 1 / 1"}
        If pages.Count <> 1 Then Throw New InvalidOperationException("Contoh surat jalan bukan satu halaman.")
        ExpectLines("delivery_note.sample.json halaman 1", pages(0), expected)
        ' Kolom penting dari mockup: blok kanan header di kolom 47, nama barang di kolom 14.
        If pages(0)(1).IndexOf("No. DO", StringComparison.Ordinal) <> 46 OrElse
           pages(0)(6).IndexOf("Sopir", StringComparison.Ordinal) <> 46 OrElse
           pages(0)(12).IndexOf("SEMEN", StringComparison.Ordinal) <> 13 OrElse
           pages(0)(10).IndexOf("NAMA", StringComparison.Ordinal) <> 13 OrElse
           pages(0)(0).Length <> 80 OrElse pages(0)(31).Length <> 80 Then
            Throw New InvalidOperationException("Kolom surat jalan bergeser dari mockup.")
        End If
        Dim sequence As List(Of List(Of String)) = BuildDeliveryNotePrintSequence(root)
        If sequence.Count <> 1 OrElse Not sequence(0).SequenceEqual(pages(0)) Then
            Throw New InvalidOperationException("Satu salinan harus satu urutan halaman.")
        End If
    End Sub

    Private Sub CheckLongSample(root As JObject)
        Dim pages As List(Of List(Of String)) = BuildDeliveryNotePages(root)
        If pages.Count <> 3 Then Throw New InvalidOperationException("23 barang harus 3 halaman, dapat " & pages.Count)
        For index As Integer = 0 To 2
            Dim page As List(Of String) = pages(index)
            ' Header (1–12) dan footer (23–31) identik di tiap halaman.
            If Not page.Take(12).SequenceEqual(pages(0).Take(12)) OrElse
               Not page.Skip(22).Take(9).SequenceEqual(pages(0).Skip(22).Take(9)) Then
                Throw New InvalidOperationException("Header/footer surat jalan tidak diulang sama.")
            End If
            Expect("halaman " & (index + 1), page(31),
                   "Barang diterima dalam keadaan baik dan jumlah sesuai." & S(14) & "Halaman " & (index + 1) & " / 3")
        Next
        Dim first As List(Of String) = pages(0)
        Expect("judul long", first(0), "VIN JAYA" & S(61) & "SURAT JALAN")
        Expect("SO revisi", first(4), "Kepada  : PT. ARTHA SINAR BAJA" & S(16) & "No. SO   : SO-2610-00008 R1")
        Expect("PO dipotong", first(5), "Alamat  : JL. INDUSTRI RAYA III NO. 8," & S(8) & "No. PO   : PO/VJ2/2026-10/PABRIK-W")
        Expect("diambil", first(6), S(10) & "JATAKE, GUDANG B (MASUK GERBANG 2)," & S(1) & "DIAMBIL PELANGGAN")
        Expect("tanpa kendaraan", first(7), S(10) & "TANGERANG")
        If first.Any(Function(line) line.Contains("Sopir    :") OrElse line.Contains("Kendaraan:")) Then
            Throw New InvalidOperationException("Diambil pelanggan masih mencetak sopir/kendaraan.")
        End If
        Expect("barang 1", first(12), "  1       1  BARANG UJI PANJANG NOMOR 01 DENGAN NAMA YANG SANGAT PANJANG SEKALI")
        Expect("barang 10", first(21), " 10      10  BARANG UJI PANJANG NOMOR 10 DENGAN NAMA YANG SANGAT PANJANG SEKALI")
        Expect("barang 11", pages(1)(12), " 11      11  BARANG UJI PANJANG NOMOR 11 DENGAN NAMA YANG SANGAT PANJANG SEKALI")
        Expect("barang 23", pages(2)(14), " 23      23  BARANG UJI PANJANG NOMOR 23 DENGAN NAMA YANG SANGAT PANJANG SEKALI")
        If Not pages(2).Skip(15).Take(7).All(Function(line) line.Length = 0) Then
            Throw New InvalidOperationException("Halaman terakhir tidak diisi baris kosong sampai 10 barang.")
        End If
        If pages.Any(Function(page) page.Any(Function(line) line.Length > 80)) Then
            Throw New InvalidOperationException("Baris long melebihi 80 kolom.")
        End If
        Dim sequence As List(Of List(Of String)) = BuildDeliveryNotePrintSequence(root)
        If sequence.Count <> 9 Then Throw New InvalidOperationException("copies 3 × 3 halaman harus 9 halaman cetak.")
        For index As Integer = 0 To 8
            If Not sequence(index).SequenceEqual(pages(index Mod 3)) Then
                Throw New InvalidOperationException("Salinan tidak mengulang seluruh dokumen berurutan.")
            End If
        Next
        CheckDeliveryNotePages(sequence)
    End Sub

    Private Sub CheckReprintAndReturn(reprint As JObject, returned As JObject)
        Dim page As List(Of String) = BuildDeliveryNotePages(reprint)(0)
        Expect("judul cetak ulang", page(0), "VIN JAYA" & S(48) & "SURAT JALAN  CETAK ULANG")
        Expect("sopir", page(6), S(10) & "JATAKE, GUDANG B (MASUK GERBANG 2)," & S(1) & "Sopir    : BUDI")
        Expect("kendaraan", page(7), S(10) & "TANGERANG" & S(27) & "Kendaraan: B 9123 KX")
        Dim original As List(Of String) = BuildDeliveryNotePages(CType(reprint.DeepClone(), JObject))(0)
        Dim back As JObject = CType(reprint.DeepClone(), JObject)
        back("payload")("reprint") = False
        Dim notReprinted As List(Of String) = BuildDeliveryNotePages(back)(0)
        If Not notReprinted.Skip(1).SequenceEqual(original.Skip(1)) OrElse notReprinted(0).Contains("CETAK ULANG") Then
            Throw New InvalidOperationException("Penanda cetak ulang mengubah isi selain judul.")
        End If

        Dim r As List(Of String) = BuildDeliveryNotePages(returned)(0)
        Expect("judul retur", r(0), "VIN JAYA" & S(55) & "SURAT JALAN RETUR")
        Expect("barang retur", r(12), "  1       2  SEMEN DYNAMIX 40KG")
        Expect("catatan kosong", r(23), "Catatan : -")
        If Not r.Skip(13).Take(9).All(Function(line) line.Length = 0) Then
            Throw New InvalidOperationException("Retur satu barang tidak diisi baris kosong.")
        End If
    End Sub

    Private Sub CheckTitles(sample As JObject)
        For Each scenario In {
            (Kind:="normal", Reprint:=False, Title:="SURAT JALAN"),
            (Kind:="return", Reprint:=False, Title:="SURAT JALAN RETUR"),
            (Kind:="replacement", Reprint:=False, Title:="SURAT JALAN PENGGANTI"),
            (Kind:="normal", Reprint:=True, Title:="SURAT JALAN  CETAK ULANG"),
            (Kind:="return", Reprint:=True, Title:="SURAT JALAN RETUR  CETAK ULANG"),
            (Kind:="replacement", Reprint:=True, Title:="SURAT JALAN PENGGANTI  CETAK ULANG")}
            Dim job As JObject = CType(sample.DeepClone(), JObject)
            job("payload")("kind") = scenario.Kind
            job("payload")("reprint") = scenario.Reprint
            ParseDeliveryNoteV2(job.ToString(Formatting.None))
            Dim line As String = BuildDeliveryNotePages(job)(0)(0)
            If line.Length <> 80 OrElse Not line.EndsWith(scenario.Title, StringComparison.Ordinal) OrElse
               Not line.StartsWith("VIN JAYA ", StringComparison.Ordinal) OrElse
               line.Substring(8, 80 - 8 - scenario.Title.Length).Trim().Length <> 0 Then
                Throw New InvalidOperationException("Judul surat jalan salah: " & scenario.Title)
            End If
        Next
        ' Judul terpanjang tepat mulai kolom 47 (sejajar blok kanan).
        If 80 - "SURAT JALAN PENGGANTI  CETAK ULANG".Length + 1 <> 47 Then
            Throw New InvalidOperationException("Judul terpanjang tidak berawal di kolom 47.")
        End If
    End Sub

    Private Sub CheckQuantityFormat()
        For Each scenario In {
            (Raw:=7900L, Text:="79"), (Raw:=250L, Text:="2,5"), (Raw:=25L, Text:="0,25"),
            (Raw:=1L, Text:="0,01"), (Raw:=110L, Text:="1,1"), (Raw:=105L, Text:="1,05"),
            (Raw:=100L, Text:="1"), (Raw:=123456700L, Text:="1234567"),
            (Raw:=999999999999L, Text:="9999999999,99")}
            Expect("qty " & scenario.Raw, FormatDeliveryQuantity(scenario.Raw), scenario.Text)
        Next
        For Each bad As Long In {0L, -100L, 1000000000000L}
            Dim value As Long = bad
            ExpectRejected("qty " & bad, Sub() FormatDeliveryQuantity(value))
        Next
    End Sub

    Private Sub CheckAddressWrap()
        ExpectLines("alamat kosong", WrapDeliveryAddress(""), {"", "", ""})
        ExpectLines("alamat pendek", WrapDeliveryAddress("JL. MAWAR 1"), {"JL. MAWAR 1", "", ""})
        ExpectLines("alamat berbaris baru", WrapDeliveryAddress("JL. MAWAR" & vbCrLf & vbTab & "NO. 1"), {"JL. MAWAR NO. 1", "", ""})
        Dim longWord As String = New String("A"c, 40)
        ExpectLines("kata panjang", WrapDeliveryAddress("JL. " & longWord & " UJUNG"),
                    {"JL.", New String("A"c, 35), "AAAAA UJUNG"})
        Dim many As String = String.Join(" ", Enumerable.Repeat("GANG MELATI", 12))
        Dim wrapped As List(Of String) = WrapDeliveryAddress(many)
        If wrapped.Count <> 3 OrElse wrapped.Any(Function(line) line.Length > 35 OrElse line.Length = 0) Then
            Throw New InvalidOperationException("Alamat panjang tidak dipotong ke 3 baris × 35.")
        End If
    End Sub

    Private Sub CheckEdgeLayouts(sample As JObject)
        Dim job As JObject = CType(sample.DeepClone(), JObject)
        job("store")("name") = New String("T"c, 60)
        job("store")("address") = New String("J"c, 60)
        job("payload")("customer")("name") = New String("P"c, 50)
        job("payload")("poNo") = ""
        job("payload")("recipient")("name") = ""
        job("payload")("recipient")("contact") = ""
        job("payload")("note") = New String("C"c, 300)
        job("payload")("items")(0)("name") = "SEMEN" & vbLf & "DYNAMIX" & vbTab & "40KG"
        job("payload")("items")(1)("quantity100") = "999999999999"
        job("payload")("revision") = "999"
        ParseDeliveryNoteV2(job.ToString(Formatting.None))
        Dim page As List(Of String) = BuildDeliveryNotePages(job)(0)
        CheckDeliveryNotePages(New List(Of List(Of String)) From {page})
        Expect("nama toko dipotong", page(0), New String("T"c, 45) & S(24) & "SURAT JALAN")
        Expect("alamat toko dipotong", page(1), New String("J"c, 45) & S(1) & "No. DO   : DO-2610-00013")
        Expect("pelanggan dipotong", page(4), "Kepada  : " & New String("P"c, 35) & S(1) & "No. SO   : SO-2610-00008 R999")
        Expect("PO kosong", page(5), "Alamat  : JL. INDUSTRI RAYA III NO. 8," & S(8) & "No. PO   : -")
        Expect("penerima kosong", page(8), "Penerima: " & New String("_"c, 30))
        Expect("catatan dipotong", page(23), "Catatan : " & New String("C"c, 70))
        Expect("nama barang diratakan", page(12), "  1      79  SEMEN DYNAMIX 40KG")
        Expect("qty besar utuh", page(13), "  2 9999999999,99  PAKU 10CM (1 KG)")

        ' Karakter format tak terlihat dibuang; emoji tidak dipotong di tengah pasangan surrogate.
        Dim unicode As JObject = CType(sample.DeepClone(), JObject)
        unicode("payload")("items")(2)("name") = ChrW(&H200E) & "BESI" & ChrW(&H200B) & " BETON"
        Dim truck As String = ChrW(&HD83D) & ChrW(&HDE9A)
        unicode("payload")("vehicleNumber") = String.Concat(Enumerable.Repeat(truck, 15))
        ParseDeliveryNoteV2(unicode.ToString(Formatting.None))
        Dim unicodePage As List(Of String) = BuildDeliveryNotePages(unicode)(0)
        Expect("format dibuang", unicodePage(14), "  3      18  BESI BETON")
        Expect("emoji utuh", unicodePage(7), S(10) & "TANGERANG" & S(27) & "Kendaraan: " & String.Concat(Enumerable.Repeat(truck, 11)))

        Dim contactOnly As JObject = CType(sample.DeepClone(), JObject)
        contactOnly("payload")("recipient")("name") = ""
        Expect("hanya kontak", BuildDeliveryNotePages(contactOnly)(0)(8), "Penerima:   0812 1234 567")

        Dim crowded As JObject = CType(sample.DeepClone(), JObject)
        Dim items As New JArray()
        For index As Integer = 1 To 500
            items.Add(New JObject(New JProperty("name", "BARANG " & index), New JProperty("quantity100", "100")))
        Next
        crowded("payload")("items") = items
        crowded("copies") = 5
        ParseDeliveryNoteV2(crowded.ToString(Formatting.None))
        Dim pages As List(Of List(Of String)) = BuildDeliveryNotePages(crowded)
        If pages.Count <> 50 Then Throw New InvalidOperationException("500 barang harus 50 halaman.")
        Expect("halaman 50", pages(49)(31), "Barang diterima dalam keadaan baik dan jumlah sesuai." & S(12) & "Halaman 50 / 50")
        Expect("barang 500", pages(49)(21), "500       1  BARANG 500")
        Dim sequence As List(Of List(Of String)) = BuildDeliveryNotePrintSequence(crowded)
        If sequence.Count <> 250 Then Throw New InvalidOperationException("5 salinan × 50 halaman harus 250.")
        CheckDeliveryNotePages(sequence)
    End Sub

    Private Sub CheckPreflight()
        Dim good As New List(Of String)(Enumerable.Repeat("", 32))
        CheckDeliveryNotePages(New List(Of List(Of String)) From {good})
        Dim shortPage As New List(Of String)(Enumerable.Repeat("", 31))
        Dim tallPage As New List(Of String)(Enumerable.Repeat("", 33))
        Dim wide As New List(Of String)(good)
        wide(5) = New String("X"c, 81)
        Dim control As New List(Of String)(good)
        control(5) = "A" & vbLf & "B"
        Dim missing As New List(Of String)(good)
        missing(5) = Nothing
        For Each scenario In {
            (Name:="31 baris", Pages:=New List(Of List(Of String)) From {shortPage}),
            (Name:="33 baris", Pages:=New List(Of List(Of String)) From {tallPage}),
            (Name:="81 kolom", Pages:=New List(Of List(Of String)) From {good, wide}),
            (Name:="karakter kendali", Pages:=New List(Of List(Of String)) From {control}),
            (Name:="baris Nothing", Pages:=New List(Of List(Of String)) From {missing}),
            (Name:="tanpa halaman", Pages:=New List(Of List(Of String))())}
            Dim pages As List(Of List(Of String)) = scenario.Pages
            ExpectRejected("preflight menerima " & scenario.Name, Sub() CheckDeliveryNotePages(pages))
        Next
        ExpectRejected("preflight menerima Nothing", Sub() CheckDeliveryNotePages(Nothing))
    End Sub

    Private Sub CheckParserMutations(sample As JObject, source As String)
        Dim payload As Func(Of JObject, JObject) = Function(j) CType(j("payload"), JObject)
        Dim firstItem As Func(Of JObject, JObject) = Function(j) CType(j("payload")("items")(0), JObject)
        ' Daftar pertama = TestDeliveryV2RejectsCorruption (Go), lalu batas tambahan.
        Dim mutations As (Name As String, Edit As Action(Of JObject))() = {
            ("versi lama", Sub(j) j("schemaVersion") = 1),
            ("jenis lama", Sub(j) j("jobType") = "delivery_order"),
            ("printer kasir", Sub(j) j("printerRole") = "CASHIER"),
            ("nol salinan", Sub(j) j("copies") = 0),
            ("enam salinan", Sub(j) j("copies") = 6),
            ("salinan pecahan", Sub(j) j("copies") = 1.5),
            ("salinan teks", Sub(j) j("copies") = "1"),
            ("jobId kosong", Sub(j) j("jobId") = ""),
            ("store kurang", Sub(j) CType(j("store"), JObject).Remove("contact")),
            ("jenis tak dikenal", Sub(j) payload(j)("kind") = "titip"),
            ("nomor DO cacat", Sub(j) payload(j)("doNo") = "DO-1"),
            ("nomor SO cacat", Sub(j) payload(j)("soNo") = "SO-2610-1"),
            ("revisi negatif", Sub(j) payload(j)("revision") = "-1"),
            ("revisi angka JSON", Sub(j) payload(j)("revision") = 1),
            ("PO kepanjangan", Sub(j) payload(j)("poNo") = New String("P"c, 51)),
            ("tanggal palsu", Sub(j) payload(j)("date") = "2026-02-30"),
            ("pelanggan tak terlihat", Sub(j) payload(j)("customer")("name") = ChrW(&H200B)),
            ("alamat kepanjangan", Sub(j) payload(j)("customer")("address") = New String("A"c, 301)),
            ("kontak berkendali", Sub(j) payload(j)("recipient")("contact") = "08" & vbLf & "12"),
            ("diambil + sopir", Sub(j)
                                     payload(j)("pickedUp") = True
                                     payload(j)("driverName") = "BUDI"
                                 End Sub),
            ("diambil bukan boolean", Sub(j) payload(j)("pickedUp") = "false"),
            ("cetak ulang hilang", Sub(j) payload(j).Remove("reprint")),
            ("barang kosong", Sub(j) payload(j)("items") = New JArray()),
            ("qty nol", Sub(j) firstItem(j)("quantity100") = "0"),
            ("qty angka JSON", Sub(j) firstItem(j)("quantity100") = 7900),
            ("harga menyusup", Sub(j) firstItem(j)("priceSen") = "6000000"),
            ("satuan menyusup", Sub(j) firstItem(j)("unit") = "SAK"),
            ("kunci asing", Sub(j) payload(j)("total") = "1"),
            ("schema teks", Sub(j) j("schemaVersion") = "2"),
            ("schema pecahan", Sub(j) j("schemaVersion") = 2.0),
            ("salinan 1.0", Sub(j) j("copies") = 1.0),
            ("jobId hilang", Sub(j) j.Remove("jobId")),
            ("amplop kunci asing", Sub(j) j("extra") = 1),
            ("store kunci asing", Sub(j) j("store")("phone") = ""),
            ("store bukan teks", Sub(j) j("store")("name") = 1),
            ("alamat toko null", Sub(j) j("store")("address") = JValue.CreateNull()),
            ("kontak toko bukan teks", Sub(j) j("store")("contact") = False),
            ("diambil + kendaraan", Sub(j)
                                         payload(j)("pickedUp") = True
                                         payload(j)("vehicleNumber") = "B 1"
                                     End Sub),
            ("revisi 1000", Sub(j) payload(j)("revision") = "1000"),
            ("revisi nol depan", Sub(j) payload(j)("revision") = "01"),
            ("revisi kosong", Sub(j) payload(j)("revision") = ""),
            ("DO baris baru", Sub(j) payload(j)("doNo") = "DO-2610-00013" & vbLf),
            ("SO huruf kecil", Sub(j) payload(j)("soNo") = "so-2610-00008"),
            ("tanggal bulan 13", Sub(j) payload(j)("date") = "2026-13-01"),
            ("tanggal tanpa nol", Sub(j) payload(j)("date") = "2026-10-1"),
            ("PO berkendali", Sub(j) payload(j)("poNo") = "PO" & ChrW(&H7F)),
            ("pelanggan kosong", Sub(j) payload(j)("customer")("name") = ""),
            ("pelanggan spasi", Sub(j) payload(j)("customer")("name") = " " & vbTab),
            ("pelanggan kunci asing", Sub(j) payload(j)("customer")("contact") = ""),
            ("penerima kunci hilang", Sub(j) CType(payload(j)("recipient"), JObject).Remove("contact")),
            ("penerima kepanjangan", Sub(j) payload(j)("recipient")("name") = New String("R"c, 101)),
            ("kontak kepanjangan", Sub(j) payload(j)("recipient")("contact") = New String("9"c, 21)),
            ("sopir kepanjangan", Sub(j) payload(j)("driverName") = New String("S"c, 101)),
            ("kendaraan kepanjangan", Sub(j) payload(j)("vehicleNumber") = New String("K"c, 16)),
            ("catatan kepanjangan", Sub(j) payload(j)("note") = New String("C"c, 301)),
            ("catatan tab", Sub(j) payload(j)("note") = "A" & vbTab & "B"),
            ("cetak ulang teks", Sub(j) payload(j)("reprint") = "false"),
            ("barang 501", Sub(j)
                               Dim items As New JArray()
                               For index As Integer = 1 To 501
                                   items.Add(New JObject(New JProperty("name", "B"), New JProperty("quantity100", "1")))
                               Next
                               payload(j)("items") = items
                           End Sub),
            ("barang bukan larik", Sub(j) payload(j)("items") = New JObject()),
            ("nama barang hilang", Sub(j) firstItem(j).Remove("name")),
            ("nama barang tak terlihat", Sub(j) firstItem(j)("name") = ChrW(&HFEFF)),
            ("qty nol depan", Sub(j) firstItem(j)("quantity100") = "0100"),
            ("qty negatif", Sub(j) firstItem(j)("quantity100") = "-100"),
            ("qty desimal", Sub(j) firstItem(j)("quantity100") = "79.00"),
            ("qty melewati batas", Sub(j) firstItem(j)("quantity100") = "1000000000000"),
            ("payload bukan objek", Sub(j) j("payload") = "x")}
        For Each scenario In mutations
            Dim changed As JObject = CType(sample.DeepClone(), JObject)
            scenario.Edit(changed)
            Dim raw As String = changed.ToString(Formatting.None)
            ExpectRejected("surat jalan v2 menerima " & scenario.Name, Sub() ParseDeliveryNoteV2(raw))
        Next

        ' Batas yang SAH menurut validator Go tetap diterima.
        Dim accepted As (Name As String, Edit As Action(Of JObject))() = {
            ("lima salinan", Sub(j) j("copies") = 5),
            ("revisi 999", Sub(j) payload(j)("revision") = "999"),
            ("PO 50", Sub(j) payload(j)("poNo") = New String("P"c, 50)),
            ("PO kosong", Sub(j) payload(j)("poNo") = ""),
            ("alamat 300", Sub(j) payload(j)("customer")("address") = New String("A"c, 300)),
            ("alamat kosong", Sub(j) payload(j)("customer")("address") = ""),
            ("kendaraan 15", Sub(j) payload(j)("vehicleNumber") = New String("K"c, 15)),
            ("catatan 300", Sub(j) payload(j)("note") = New String("C"c, 300)),
            ("diambil tanpa sopir", Sub(j) payload(j)("pickedUp") = True),
            ("toko kosong", Sub(j)
                                j("store")("name") = ""
                                j("store")("address") = ""
                                j("store")("contact") = ""
                            End Sub),
            ("pengganti", Sub(j) payload(j)("kind") = "replacement"),
            ("qty maksimum", Sub(j) firstItem(j)("quantity100") = "999999999999"),
            ("emoji dihitung satu", Sub(j) payload(j)("vehicleNumber") = String.Concat(Enumerable.Repeat(ChrW(&HD83D) & ChrW(&HDE9A), 15)))}
        For Each scenario In accepted
            Dim changed As JObject = CType(sample.DeepClone(), JObject)
            scenario.Edit(changed)
            Dim root As JObject
            Try
                root = ParseDeliveryNoteV2(changed.ToString(Formatting.None))
            Catch ex As Exception
                Throw New InvalidOperationException("Surat jalan v2 menolak batas sah " & scenario.Name & ": " & ex.Message, ex)
            End Try
            CheckDeliveryNotePages(BuildDeliveryNotePrintSequence(root))
        Next

        For Each raw As String In {
            "{""schemaVersion"":2,""schemaVersion"":2}",
            source.Replace("""jobId"": ""DO-2610-00013:1""", """jobId"": ""DO-2610-00013:1"", ""jobId"": ""DO-2610-00013:2"""),
            source & " {}",
            "[" & source & "]",
            "{"}
            If raw = source Then Throw New InvalidOperationException("Fixture tidak memuat jobId kanonik.")
            Dim body As String = raw
            ExpectRejected("surat jalan v2 menerima JSON cacat/ganda", Sub() ParseDeliveryNoteV2(body))
        Next
    End Sub

    Private Sub ExpectLines(name As String, actual As List(Of String), expected As String())
        If actual.Count <> expected.Length Then
            Throw New InvalidOperationException(name & ": " & actual.Count & " baris, diharapkan " & expected.Length)
        End If
        For index As Integer = 0 To expected.Length - 1
            If actual(index) <> expected(index) Then
                Throw New InvalidOperationException(name & " baris " & (index + 1) & ": [" & actual(index) & "] != [" & expected(index) & "]")
            End If
        Next
    End Sub

    Private Sub Expect(name As String, actual As String, expected As String)
        If actual <> expected Then Throw New InvalidOperationException(name & ": [" & actual & "] != [" & expected & "]")
    End Sub

    Private Sub ExpectRejected(name As String, action As Action)
        Try
            action()
        Catch ex As Exception
            Return
        End Try
        Throw New InvalidOperationException(name)
    End Sub

    Private Function S(count As Integer) As String
        Return New String(" "c, count)
    End Function
End Module
