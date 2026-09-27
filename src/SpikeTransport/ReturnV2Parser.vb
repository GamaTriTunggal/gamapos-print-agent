' Parser murni retur schema 2. Belum terhubung ke dispatcher maupun /health;
' amplop v1 tetap memakai ReturnReceipt lama tanpa perubahan.
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

Module ReturnV2Parser
    Private Const MaxCent As Long = 99999999999999L
    Private Const MaxQuantity As Long = 999999999999L
    Private ReadOnly Canonical As New Regex("\A(0|[1-9][0-9]{0,13})\z", RegexOptions.CultureInvariant)
    Private ReadOnly TransactionPattern As New Regex("\AIN/[0-9]{6}/[0-9]{6}\z", RegexOptions.CultureInvariant)
    Private ReadOnly DomainID As New Regex("\A[A-Z0-9]{1,6}\z", RegexOptions.CultureInvariant)
    Private ReadOnly DatePattern As New Regex("\A[0-9]{4}-[0-9]{2}-[0-9]{2}\z", RegexOptions.CultureInvariant)
    Private ReadOnly TimePattern As New Regex("\A[0-9]{2}:[0-9]{2}:[0-9]{2}\z", RegexOptions.CultureInvariant)

    Friend Function ParseReturnV2(body As String) As JObject
        If body Is Nothing OrElse Encoding.UTF8.GetByteCount(body) > 1048576 Then
            Throw New ArgumentException("Payload retur v2 terlalu besar.")
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
           IntegerToken(root("copies")) <> 1 OrElse
           Text(root, "jobType", True) <> "return_note" OrElse
           Text(root, "printerRole", True) <> "CASHIER" Then
            Throw New ArgumentException("Versi, jenis, printer, atau salinan retur salah.")
        End If
        Text(root, "jobId", True)
        Dim store As JObject = Exact(root("store"), "store", "name,address,contact")
        For Each field As String In {"name", "address", "contact"}
            If Text(store, field, False) <> "" Then Throw New ArgumentException("Header toko retur harus kosong.")
        Next

        Dim payload As JObject = Exact(root("payload"), "payload",
            "transactionId,receiptNo,date,time,customer,items,totalSen,originalProcessor", "reprint")
        If Not TransactionPattern.IsMatch(Text(payload, "transactionId", True)) Then
            Throw New ArgumentException("Nomor transaksi retur tidak sah.")
        End If
        Positive(payload, "receiptNo", Integer.MaxValue)
        DateTimeFields(payload, "payload")
        Dim customer As JObject = Exact(payload("customer"), "customer", "id,name,address,contact")
        If Not DomainID.IsMatch(Text(customer, "id", True)) Then Throw New ArgumentException("Kode pelanggan salah.")
        PrintedName(customer, "name")
        Text(customer, "address", False)
        Text(customer, "contact", False)
        Processor(payload("originalProcessor"), "originalProcessor")
        If payload.Property("reprint") IsNot Nothing Then
            Dim reprint As JObject = Exact(payload("reprint"), "reprint", "date,time,processor")
            DateTimeFields(reprint, "reprint")
            Processor(reprint("processor"), "reprint.processor")
        End If

        Dim items As JArray = TryCast(payload("items"), JArray)
        If items Is Nothing OrElse items.Count = 0 Then Throw New ArgumentException("Baris retur kosong.")
        Dim total As Long = Cent(payload, "totalSen")
        If total = 0 Then Throw New ArgumentException("Total retur harus positif.")
        Dim productSum As Decimal = 0D
        Dim lineSum As Decimal = 0D
        For Each token As JToken In items
            Dim item As JObject = Exact(token, "item", "itemId,name,quantity100,priceSen,totalSen")
            If Not DomainID.IsMatch(Text(item, "itemId", True)) Then Throw New ArgumentException("Kode barang salah.")
            PrintedName(item, "name")
            Dim qty As Long = Positive(item, "quantity100", MaxQuantity)
            Dim price As Long = Cent(item, "priceSen")
            If price = 0 Then Throw New ArgumentException("Harga retur harus positif.")
            Dim line As Long = Cent(item, "totalSen")
            Dim product As Decimal = CDec(qty) * CDec(price)
            If Math.Abs(CDec(line) - Decimal.Truncate(product / 100D)) > 1D Then
                Throw New ArgumentException("Total baris retur tidak cocok.")
            End If
            productSum += product
            lineSum += CDec(line)
        Next
        If lineSum <> CDec(total) OrElse Decimal.Truncate((productSum + 50D) / 100D) <> lineSum Then
            Throw New ArgumentException("Jumlah retur tidak konservatif.")
        End If
        Return root
    End Function

    Private Function Exact(token As JToken, path As String, fields As String, Optional extra As String = Nothing) As JObject
        Dim result As JObject = TryCast(token, JObject)
        If result Is Nothing Then Throw New ArgumentException(path & " harus objek.")
        Dim names As New HashSet(Of String)(fields.Split(","c), StringComparer.Ordinal)
        For Each name As String In names
            If result.Property(name) Is Nothing Then Throw New ArgumentException(path & "." & name & " hilang.")
        Next
        If extra IsNot Nothing Then names.Add(extra)
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

    Private Function PrintedName(obj As JObject, name As String) As String
        Dim value As String = Text(obj, name, True)
        For Each ch As Char In value
            If Not Char.IsWhiteSpace(ch) AndAlso Not Char.IsControl(ch) AndAlso
               Char.GetUnicodeCategory(ch) <> UnicodeCategory.Format Then Return value
        Next
        Throw New ArgumentException(name & " harus punya karakter terlihat.")
    End Function

    Private Function Cent(obj As JObject, name As String) As Long
        Dim raw As String = Text(obj, name, True)
        Dim value As Long = 0
        If Not Canonical.IsMatch(raw) OrElse
           Not Long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse
           value > MaxCent Then Throw New ArgumentException(name & " bukan sen kanonik.")
        Return value
    End Function

    Private Function Positive(obj As JObject, name As String, upper As Long) As Long
        Dim value As Long = Cent(obj, name)
        If value = 0 OrElse value > upper Then Throw New ArgumentException(name & " melewati batas.")
        Return value
    End Function

    Private Function IntegerToken(token As JToken) As Integer
        Dim value As Integer = 0
        If token Is Nothing OrElse token.Type <> JTokenType.Integer OrElse
           Not Integer.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse
           value < 1 Then Throw New ArgumentException("Versi atau salinan salah.")
        Return value
    End Function

    Private Sub Processor(token As JToken, path As String)
        Dim actor As JObject = Exact(token, path, "userId,name")
        Positive(actor, "userId", MaxCent)
        PrintedName(actor, "name")
    End Sub

    Private Sub DateTimeFields(obj As JObject, path As String)
        Dim dateText As String = Text(obj, "date", True)
        Dim dateValue As DateTime
        If Not DatePattern.IsMatch(dateText) OrElse
           Not DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, dateValue) Then
            Throw New ArgumentException(path & ".date tidak sah.")
        End If
        Dim timeText As String = Text(obj, "time", True)
        Dim timeValue As TimeSpan
        If Not TimePattern.IsMatch(timeText) OrElse
           Not TimeSpan.TryParseExact(timeText, "hh\:mm\:ss", CultureInfo.InvariantCulture, timeValue) OrElse
           timeValue.TotalHours >= 24 Then
            Throw New ArgumentException(path & ".time tidak sah.")
        End If
    End Sub
End Module
