' Parser murni surat jalan schema 2 (`delivery_note`, P-645 / CO-11 papan
' pesanan pelanggan; amandemen K-02 12 Okt 2026). Ketatnya mengikuti validator
' Go `internal/platform/printagent/v2_delivery.go`: kunci asing, hilang, atau
' ganda ditolak. `delivery_order` v1 tidak memakai modul ini dan tidak berubah.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module DeliveryNoteV2Parser
    Friend Const DeliveryNoteMaxCopies As Integer = 5
    Friend Const DeliveryNoteMaxItems As Integer = 500
    Private Const MaxQuantity As Long = 999999999999L
    Private ReadOnly Canonical As New Regex("\A(0|[1-9][0-9]{0,13})\z", RegexOptions.CultureInvariant)
    Private ReadOnly DeliveryNumber As New Regex("\ADO-[0-9]{4}-[0-9]{5}\z", RegexOptions.CultureInvariant)
    Private ReadOnly OrderNumber As New Regex("\ASO-[0-9]{4}-[0-9]{5}\z", RegexOptions.CultureInvariant)
    Private ReadOnly RevisionPattern As New Regex("\A(0|[1-9][0-9]{0,2})\z", RegexOptions.CultureInvariant)
    Private ReadOnly DatePattern As New Regex("\A[0-9]{4}-[0-9]{2}-[0-9]{2}\z", RegexOptions.CultureInvariant)

    Friend Function ParseDeliveryNoteV2(body As String) As JObject
        If body Is Nothing OrElse Encoding.UTF8.GetByteCount(body) > 1048576 Then
            Throw New ArgumentException("Payload surat jalan v2 terlalu besar.")
        End If
        Dim root As JObject
        Using reader As New JsonTextReader(New StringReader(body)) With {
            .MaxDepth = 32, .DateParseHandling = DateParseHandling.None}
            root = JObject.Load(reader, New JsonLoadSettings With {
                .DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error})
            If reader.Read() Then Throw New ArgumentException("Lebih dari satu nilai JSON.")
        End Using
        Exact(root, "envelope", "schemaVersion,jobId,jobType,printerRole,copies,store,payload")
        If IntegerToken(root("schemaVersion")) <> 2 OrElse
           Text(root, "jobType", False) <> "delivery_note" OrElse
           Text(root, "printerRole", False) <> "DELIVERY" Then
            Throw New ArgumentException("Versi, jenis, atau printer surat jalan salah.")
        End If
        Dim copies As Integer = IntegerToken(root("copies"))
        If copies > DeliveryNoteMaxCopies Then Throw New ArgumentException("copies surat jalan harus 1..5.")
        Text(root, "jobId", True)
        Dim store As JObject = Exact(root("store"), "store", "name,address,contact")
        For Each field As String In {"name", "address", "contact"}
            Text(store, field, False)
        Next

        Dim payload As JObject = Exact(root("payload"), "payload",
            "kind,doNo,soNo,revision,poNo,date,customer,recipient,driverName,vehicleNumber,pickedUp,note,reprint,items")
        Select Case Text(payload, "kind", False)
            Case "normal", "return", "replacement"
            Case Else
                Throw New ArgumentException("Jenis surat jalan tidak dikenal.")
        End Select
        If Not DeliveryNumber.IsMatch(Text(payload, "doNo", True)) Then Throw New ArgumentException("Nomor surat jalan tidak sah.")
        If Not OrderNumber.IsMatch(Text(payload, "soNo", True)) Then Throw New ArgumentException("Nomor pesanan tidak sah.")
        If Not RevisionPattern.IsMatch(Text(payload, "revision", False)) Then Throw New ArgumentException("Revisi pesanan tidak sah.")
        MaxText(payload, "poNo", 50)
        Dim dateText As String = Text(payload, "date", False)
        Dim dateValue As DateTime
        If Not DatePattern.IsMatch(dateText) OrElse
           Not DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, dateValue) Then
            Throw New ArgumentException("Tanggal surat jalan tidak sah.")
        End If
        Dim customer As JObject = Exact(payload("customer"), "customer", "name,address")
        PrintedName(customer, "name")
        MaxText(customer, "address", 300)
        Dim recipient As JObject = Exact(payload("recipient"), "recipient", "name,contact")
        MaxText(recipient, "name", 100)
        MaxText(recipient, "contact", 20)
        Dim driver As String = MaxText(payload, "driverName", 100)
        Dim vehicle As String = MaxText(payload, "vehicleNumber", 15)
        If Flag(payload, "pickedUp") AndAlso (driver <> "" OrElse vehicle <> "") Then
            Throw New ArgumentException("Diambil pelanggan tidak memakai sopir atau kendaraan.")
        End If
        MaxText(payload, "note", 300)
        Flag(payload, "reprint")

        Dim items As JArray = TryCast(payload("items"), JArray)
        If items Is Nothing OrElse items.Count = 0 OrElse items.Count > DeliveryNoteMaxItems Then
            Throw New ArgumentException("Baris surat jalan harus 1..500.")
        End If
        For Each token As JToken In items
            Dim item As JObject = Exact(token, "item", "name,quantity100")
            PrintedName(item, "name")
            Dim raw As String = Text(item, "quantity100", True)
            Dim quantity As Long = 0
            If Not Canonical.IsMatch(raw) OrElse raw = "0" OrElse
               Not Long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, quantity) OrElse
               quantity > MaxQuantity Then
                Throw New ArgumentException("quantity100 harus bilangan positif kanonik.")
            End If
        Next
        Return root
    End Function

    Private Function Exact(token As JToken, path As String, fields As String) As JObject
        Dim result As JObject = TryCast(token, JObject)
        If result Is Nothing Then Throw New ArgumentException(path & " harus objek.")
        Dim names As New HashSet(Of String)(fields.Split(","c), StringComparer.Ordinal)
        For Each name As String In names
            If result.Property(name) Is Nothing Then Throw New ArgumentException(path & "." & name & " hilang.")
        Next
        For Each entry As JProperty In result.Properties()
            If Not names.Contains(entry.Name) Then Throw New ArgumentException(path & "." & entry.Name & " tidak dikenal.")
        Next
        Return result
    End Function

    Private Function Text(obj As JObject, name As String, required As Boolean) As String
        Dim token As JToken = obj(name)
        If token Is Nothing OrElse token.Type <> JTokenType.String Then Throw New ArgumentException(name & " harus teks.")
        Dim value As String = token.Value(Of String)()
        If required AndAlso value.Length = 0 Then Throw New ArgumentException(name & " wajib terisi.")
        Return value
    End Function

    ' Padanan v2MaxText Go: teks boleh kosong, maksimal n titik kode, tanpa
    ' kendali C0 maupun DEL.
    Private Function MaxText(obj As JObject, name As String, limit As Integer) As String
        Dim value As String = Text(obj, name, False)
        Dim count As Integer = 0
        Dim index As Integer = 0
        While index < value.Length
            Dim ch As Char = value(index)
            If AscW(ch) < &H20 OrElse AscW(ch) = &H7F Then Throw New ArgumentException(name & " memuat karakter kendali.")
            If Char.IsHighSurrogate(ch) AndAlso index + 1 < value.Length AndAlso Char.IsLowSurrogate(value(index + 1)) Then
                index += 1
            End If
            count += 1
            index += 1
        End While
        If count > limit Then Throw New ArgumentException(name & " melebihi " & limit & " karakter.")
        Return value
    End Function

    ' Padanan v2PrintableName Go: minimal satu titik kode bukan spasi, kendali, atau Cf.
    Private Function PrintedName(obj As JObject, name As String) As String
        Dim value As String = Text(obj, name, True)
        Dim index As Integer = 0
        While index < value.Length
            Dim category As UnicodeCategory = CharUnicodeInfo.GetUnicodeCategory(value, index)
            Dim ch As Char = value(index)
            If Not Char.IsWhiteSpace(ch) AndAlso Not Char.IsControl(ch) AndAlso
               category <> UnicodeCategory.Format Then Return value
            index += If(Char.IsSurrogatePair(value, index), 2, 1)
        End While
        Throw New ArgumentException(name & " harus punya karakter terlihat.")
    End Function

    Private Function Flag(obj As JObject, name As String) As Boolean
        Dim token As JToken = obj(name)
        If token Is Nothing OrElse token.Type <> JTokenType.Boolean Then Throw New ArgumentException(name & " harus boolean.")
        Return token.Value(Of Boolean)()
    End Function

    Private Function IntegerToken(token As JToken) As Integer
        Dim value As Integer = 0
        If token Is Nothing OrElse token.Type <> JTokenType.Integer OrElse
           Not Integer.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse
           value < 1 Then Throw New ArgumentException("Versi atau salinan salah.")
        Return value
    End Function
End Module
