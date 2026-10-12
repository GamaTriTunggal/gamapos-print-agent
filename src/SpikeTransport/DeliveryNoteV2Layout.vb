' Tata letak surat jalan schema 2 (`delivery_note`, P-645 / CO-11) tanpa printer.
' Mockup disetujui pemilik 12 Okt 2026: kertas continuous form 9,5 × 5,5 in,
' 80 kolom × 32 baris per halaman (Courier New 12 Regular), tanpa harga dan
' tanpa kolom satuan. Hasilnya daftar halaman berisi tepat 32 baris teks;
' renderer mencetak tiap baris apa adanya di kolom 1 sehingga spasi = posisi
' kolom, sama seperti TAB pada `delivery_order` v1. Modul ini tidak memakai
' PowerPacks/TextWidth agar dapat diuji lintas-platform di SchemaGateTests.
' BELUM diuji fisik di LX-310.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports Newtonsoft.Json.Linq

Module DeliveryNoteV2Layout
    Friend Const DeliveryNoteColumns As Integer = 80
    Friend Const DeliveryNoteLinesPerPage As Integer = 32
    Friend Const DeliveryNoteItemsPerPage As Integer = 10
    ' Blok kanan header mulai kolom 47; blok kiri maksimal 45 kolom (kolom 46 = jeda).
    Private Const RightBlockColumn As Integer = 47
    Private Const LeftBlockWidth As Integer = 45
    Private Const CaptionWidth As Integer = 10
    Private Const AddressWidth As Integer = 35
    Private Const AddressLineCount As Integer = 3
    Private Const NumberWidth As Integer = 3
    Private Const QuantityWidth As Integer = 7

    Private Const ItemHeader As String = " No     QTY  NAMA BARANG"
    Private Const SignCaptions As String = "        Pengirim,                  Sopir,                   Penerima,"
    Private Const SignBrackets As String = "   (___________________)    (___________________)    (___________________)"
    Private Const SignDates As String = "   Tgl:                     Tgl:                     Tgl:"
    Private Const Acknowledgement As String = "Barang diterima dalam keadaan baik dan jumlah sesuai."

    ' Urutan cetak lengkap: seluruh halaman dokumen diulang `copies` kali.
    Friend Function BuildDeliveryNotePrintSequence(root As JObject) As List(Of List(Of String))
        Dim pages As List(Of List(Of String)) = BuildDeliveryNotePages(root)
        Dim copies As Integer = CInt(root("copies"))
        If copies < 1 OrElse copies > DeliveryNoteMaxCopies Then Throw New ArgumentException("Salinan surat jalan tidak sah.")
        Dim sequence As New List(Of List(Of String))()
        For copy As Integer = 1 To copies
            sequence.AddRange(pages)
        Next
        Return sequence
    End Function

    ' Satu salinan dokumen; root dianggap sudah lolos ParseDeliveryNoteV2.
    Friend Function BuildDeliveryNotePages(root As JObject) As List(Of List(Of String))
        If root Is Nothing Then Throw New ArgumentException("Surat jalan kosong.")
        Dim store As JObject = CType(root("store"), JObject)
        Dim payload As JObject = CType(root("payload"), JObject)
        Dim customer As JObject = CType(payload("customer"), JObject)
        Dim recipient As JObject = CType(payload("recipient"), JObject)
        Dim items As JArray = CType(payload("items"), JArray)
        If items.Count = 0 Then Throw New ArgumentException("Baris surat jalan kosong.")

        Dim header As List(Of String) = BuildHeader(store, payload, customer, recipient)
        Dim itemLines As New List(Of String)()
        For index As Integer = 0 To items.Count - 1
            Dim item As JObject = CType(items(index), JObject)
            Dim quantity As Long = Long.Parse(CStr(item("quantity100")), NumberStyles.None, CultureInfo.InvariantCulture)
            itemLines.Add(ItemLine(index + 1, quantity, CStr(item("name"))))
        Next
        Dim note As String = Flatten(CStr(payload("note")))
        Dim noteLine As String = FitLine("Catatan : " & If(note.Length = 0, "-", note))

        Dim pageCount As Integer = (items.Count + DeliveryNoteItemsPerPage - 1) \ DeliveryNoteItemsPerPage
        Dim pages As New List(Of List(Of String))()
        For page As Integer = 1 To pageCount
            Dim lines As New List(Of String)(header)
            Dim start As Integer = (page - 1) * DeliveryNoteItemsPerPage
            For slot As Integer = 0 To DeliveryNoteItemsPerPage - 1
                lines.Add(If(start + slot < itemLines.Count, itemLines(start + slot), ""))
            Next
            lines.Add(New String("-"c, DeliveryNoteColumns))
            lines.Add(noteLine)
            lines.Add("")
            lines.Add(SignCaptions)
            lines.Add("")
            lines.Add("")
            lines.Add("")
            lines.Add(SignBrackets)
            lines.Add(SignDates)
            lines.Add(PlaceRight(Acknowledgement, "Halaman " & page.ToString(CultureInfo.InvariantCulture) &
                                 " / " & pageCount.ToString(CultureInfo.InvariantCulture)))
            pages.Add(lines)
        Next
        Return pages
    End Function

    ' Preflight murni sebelum Printer.Print pertama: tiap halaman tepat 32 baris,
    ' tiap baris maksimal 80 kolom dan tanpa karakter kendali (pemutus baris).
    Friend Sub CheckDeliveryNotePages(pages As List(Of List(Of String)))
        If pages Is Nothing OrElse pages.Count = 0 Then Throw New ArgumentException("Surat jalan tanpa halaman.")
        For Each page As List(Of String) In pages
            If page Is Nothing OrElse page.Count <> DeliveryNoteLinesPerPage Then
                Throw New ArgumentException("Halaman surat jalan harus tepat 32 baris.")
            End If
            For Each line As String In page
                If line Is Nothing OrElse line.Length > DeliveryNoteColumns Then
                    Throw New ArgumentException("Baris surat jalan melampaui 80 kolom.")
                End If
                For Each ch As Char In line
                    If Char.IsControl(ch) Then Throw New ArgumentException("Baris surat jalan memuat karakter kendali.")
                Next
            Next
        Next
    End Sub

    ' quantity100 → teks koma desimal tanpa nol di belakang dan tanpa pemisah ribuan
    ' ("79", "2,5", "0,25"). Bilangan bulat murni; tidak memakai Double.
    Friend Function FormatDeliveryQuantity(quantity100 As Long) As String
        If quantity100 <= 0L OrElse quantity100 > 999999999999L Then Throw New ArgumentOutOfRangeException(NameOf(quantity100))
        Dim whole As String = (quantity100 \ 100L).ToString(CultureInfo.InvariantCulture)
        Dim fraction As Long = quantity100 Mod 100L
        If fraction = 0L Then Return whole
        Return whole & "," & fraction.ToString("00", CultureInfo.InvariantCulture).TrimEnd("0"c)
    End Function

    Friend Function DeliveryNoteTitle(kind As String, reprint As Boolean) As String
        Dim title As String
        Select Case kind
            Case "normal"
                title = "SURAT JALAN"
            Case "return"
                title = "SURAT JALAN RETUR"
            Case "replacement"
                title = "SURAT JALAN PENGGANTI"
            Case Else
                Throw New ArgumentException("Jenis surat jalan tidak dikenal.")
        End Select
        Return If(reprint, title & "  CETAK ULANG", title)
    End Function

    ' Alamat dipecah per kata ke lebar 35, maksimal 3 baris; sisa dibuang.
    ' Kata lebih dari 35 karakter dipotong keras dan dilanjutkan di baris berikut.
    Friend Function WrapDeliveryAddress(raw As String) As List(Of String)
        Dim lines As New List(Of String)()
        Dim current As String = ""
        Dim text As String = Flatten(raw)
        If text.Length > 0 Then
            For Each word As String In text.Split(" "c)
                If current.Length > 0 AndAlso current.Length + 1 + word.Length <= AddressWidth Then
                    current &= " " & word
                    Continue For
                End If
                If current.Length > 0 Then lines.Add(current)
                Dim rest As String = word
                While rest.Length > AddressWidth
                    Dim piece As String = Clip(rest, AddressWidth)
                    lines.Add(piece)
                    rest = rest.Substring(piece.Length)
                End While
                current = rest
            Next
        End If
        If current.Length > 0 Then lines.Add(current)
        If lines.Count > AddressLineCount Then lines.RemoveRange(AddressLineCount, lines.Count - AddressLineCount)
        While lines.Count < AddressLineCount
            lines.Add("")
        End While
        Return lines
    End Function

    Private Function BuildHeader(store As JObject, payload As JObject, customer As JObject,
                                 recipient As JObject) As List(Of String)
        Dim rule As String = New String("-"c, DeliveryNoteColumns)
        Dim title As String = DeliveryNoteTitle(CStr(payload("kind")), CBool(payload("reprint")))
        Dim dateText As String = CStr(payload("date"))
        Dim printedDate As String = dateText.Substring(8, 2) & "-" & dateText.Substring(5, 2) & "-" & dateText.Substring(0, 4)
        Dim revision As String = CStr(payload("revision"))
        Dim orderNo As String = CStr(payload("soNo")) & If(revision = "0", "", " R" & revision)
        Dim poNo As String = Flatten(CStr(payload("poNo")))
        Dim address As List(Of String) = WrapDeliveryAddress(CStr(customer("address")))
        Dim indent As String = New String(" "c, CaptionWidth)
        Dim blank As String = New String("_"c, 21)
        Dim pickedUp As Boolean = CBool(payload("pickedUp"))
        Dim driver As String = Flatten(CStr(payload("driverName")))
        Dim vehicle As String = Flatten(CStr(payload("vehicleNumber")))
        Dim driverText As String = If(pickedUp, "DIAMBIL PELANGGAN", "Sopir    : " & If(driver.Length = 0, blank, driver))
        Dim vehicleText As String = If(pickedUp, "", "Kendaraan: " & If(vehicle.Length = 0, blank, vehicle))
        Dim recipientName As String = Flatten(CStr(recipient("name")))
        Dim recipientContact As String = Flatten(CStr(recipient("contact")))
        Dim recipientLine As String
        If recipientName.Length = 0 AndAlso recipientContact.Length = 0 Then
            recipientLine = "Penerima: " & New String("_"c, 30)
        Else
            recipientLine = "Penerima: " & recipientName & If(recipientContact.Length = 0, "", "  " & recipientContact)
        End If

        Return New List(Of String) From {
            PlaceRight(Clip(Flatten(CStr(store("name"))), LeftBlockWidth), title),
            TwoBlocks(Flatten(CStr(store("address"))), "No. DO   : " & CStr(payload("doNo"))),
            TwoBlocks(Flatten(CStr(store("contact"))), "Tanggal  : " & printedDate),
            rule,
            TwoBlocks("Kepada  : " & Clip(Flatten(CStr(customer("name"))), AddressWidth), "No. SO   : " & orderNo),
            TwoBlocks("Alamat  : " & address(0), "No. PO   : " & If(poNo.Length = 0, "-", poNo)),
            TwoBlocks(indent & address(1), driverText),
            TwoBlocks(indent & address(2), vehicleText),
            FitLine(recipientLine),
            New String("="c, DeliveryNoteColumns),
            ItemHeader,
            rule}
    End Function

    Private Function ItemLine(number As Integer, quantity100 As Long, name As String) As String
        ' Angka tidak pernah dipotong; nama mulai kolom 14 dan dipotong di kolom 80.
        Return FitLine(number.ToString(CultureInfo.InvariantCulture).PadLeft(NumberWidth) & " " &
                       FormatDeliveryQuantity(quantity100).PadLeft(QuantityWidth) & "  " & Flatten(name))
    End Function

    ' Blok kiri (dipotong 45 kolom) + blok kanan mulai kolom 47, lalu dipotong di kolom 80.
    Private Function TwoBlocks(left As String, right As String) As String
        Return FitLine(Clip(left, LeftBlockWidth).PadRight(RightBlockColumn - 1) & right)
    End Function

    ' Teks kanan rata kanan berakhir di kolom 80; kiri tidak boleh menyentuhnya.
    Private Function PlaceRight(left As String, right As String) As String
        Dim start As Integer = DeliveryNoteColumns - right.Length
        If start <= left.Length Then Throw New ArgumentException("Teks kanan menabrak teks kiri.")
        Return left.PadRight(start) & right
    End Function

    Private Function FitLine(value As String) As String
        Return Clip(value, DeliveryNoteColumns).TrimEnd(" "c)
    End Function

    ' Potong ke lebar kolom tanpa memisahkan pasangan surrogate.
    Private Function Clip(value As String, width As Integer) As String
        If value.Length <= width Then Return value
        Dim cut As Integer = width
        If cut > 0 AndAlso Char.IsHighSurrogate(value(cut - 1)) Then cut -= 1
        Return value.Substring(0, cut)
    End Function

    ' Spasi/kendali apa pun (tab, baris baru) menjadi satu spasi; karakter format
    ' tak terlihat (Cf) dibuang; tepi dirapikan. Menjaga jumlah baris tetap 32.
    Private Function Flatten(raw As String) As String
        Dim result As New StringBuilder()
        Dim pendingSpace As Boolean = False
        For Each ch As Char In If(raw, "")
            If Char.IsWhiteSpace(ch) OrElse Char.IsControl(ch) Then
                pendingSpace = result.Length > 0
            ElseIf Char.GetUnicodeCategory(ch) = UnicodeCategory.Format Then
                Continue For
            Else
                If pendingSpace Then result.Append(" "c)
                result.Append(ch)
                pendingSpace = False
            End If
        Next
        Return result.ToString()
    End Function
End Module
