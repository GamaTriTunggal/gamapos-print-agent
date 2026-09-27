' Parser nota penjualan schema 2 yang terpisah dari model/formatter v1.
' Dispatch baru dapat menjangkaunya sesudah gerbang schema dinaikkan; /health
' masih schema 1 sambil menunggu uji Windows/printer fisik. Semua jumlah
' dibaca sebagai string-sen, bukan Double.
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

Module SaleV2Parser
    Private Const MaxCent As Long = 99999999999999L
    Private Const MaxQuantity As Long = 999999999999L
    Private ReadOnly Canonical As New Regex("\A(0|[1-9][0-9]{0,13})\z", RegexOptions.CultureInvariant)
    Private ReadOnly EventIdPattern As New Regex("\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)

    Friend Function ParseSaleV2(body As String) As JObject
        If body Is Nothing OrElse Encoding.UTF8.GetByteCount(body) > 1048576 Then
            Throw New ArgumentException("Payload nota v2 terlalu besar.")
        End If
        Dim root As JObject
        Using reader As New JsonTextReader(New StringReader(body)) With {
            .MaxDepth = 32, .DateParseHandling = DateParseHandling.None}
            root = JObject.Load(reader, New JsonLoadSettings With {
                .DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error})
            If reader.Read() Then Throw New ArgumentException("Lebih dari satu nilai JSON.")
        End Using
        Exact(root, "envelope", "schemaVersion,jobId,jobType,printerRole,copies,store,payload")
        If IntegerToken(root("schemaVersion"), 2) <> 2 Then Throw New ArgumentException("Schema nota bukan 2.")
        Dim jobType As String = Text(root, "jobType", True)
        If jobType <> "cashier_receipt" AndAlso jobType <> "kasbon_receipt" AndAlso jobType <> "split_receipt" Then
            Throw New ArgumentException("Jenis nota v2 belum didukung.")
        End If
        Text(root, "jobId", True)
        If Text(root, "printerRole", True) <> "CASHIER" Then Throw New ArgumentException("Peran printer bukan CASHIER.")
        If IntegerToken(root("copies"), Integer.MaxValue) <> 1 Then
            Throw New ArgumentException("copies harus 1; cetak salinan lewat job cetak ulang.")
        End If

        Dim store As JObject = Exact(root("store"), "store", "name,address,contact")
        For Each field As String In {"name", "address", "contact"}
            Text(store, field, False)
        Next
        Dim payload As JObject = Exact(root("payload"), "payload",
            "eventId,transactionId,receiptNo,date,time,customer,items,amounts,paymentMethod,noncashMethod,originalProcessor", "reprint")
        If Not EventIdPattern.IsMatch(Text(payload, "eventId", True)) Then Throw New ArgumentException("ID kejadian tidak sah.")
        Dim transactionId As String = Text(payload, "transactionId", False)
        PositiveString(payload, "receiptNo", Integer.MaxValue)
        Text(payload, "date", True)
        Text(payload, "time", True)
        Dim customer As JObject = Exact(payload("customer"), "customer", "name,address,contact,poNo")
        For Each field As String In {"name", "address", "contact", "poNo"}
            Text(customer, field, False)
        Next
        Processor(payload("originalProcessor"), "originalProcessor")
        If payload.Property("reprint") IsNot Nothing Then
            Dim reprint As JObject = Exact(payload("reprint"), "reprint", "date,time,processor")
            Text(reprint, "date", True)
            Text(reprint, "time", True)
            Processor(reprint("processor"), "reprint.processor")
        End If

        Dim items As JArray = TryCast(payload("items"), JArray)
        If items Is Nothing OrElse items.Count = 0 Then Throw New ArgumentException("Item nota kosong.")
        Dim productTotal As Decimal = 0D
        Dim lineTotal As Decimal = 0D
        For Each token As JToken In items
            Dim item As JObject = Exact(token, "item", "name,quantity100,unit,priceSen,totalSen")
            PrintedName(item, "name")
            Text(item, "unit", False)
            Dim quantity As Long = PositiveString(item, "quantity100", MaxQuantity)
            Dim price As Long = Cent(item, "priceSen")
            Dim total As Long = Cent(item, "totalSen")
            Dim product As Decimal = CDec(quantity) * CDec(price)
            If Math.Abs(CDec(total) - Decimal.Truncate(product / 100D)) > 1D Then
                Throw New ArgumentException("Total baris tidak cocok dengan jumlah dan harga.")
            End If
            productTotal += product
            lineTotal += CDec(total)
        Next

        Dim amounts As JObject = Exact(payload("amounts"), "amounts",
            "grossSen,discountSen,roundingSen,netSen,principalAppliedSen,remainingSen,cashSen,noncashSen,customerFeeSen,merchantFeeSen,customerPaysSen,merchantReceivesSen,tenderSen,changeSen")
        Dim money As New Dictionary(Of String, Long)(StringComparer.Ordinal)
        For Each propertyName As String In {"grossSen", "discountSen", "roundingSen", "netSen", "principalAppliedSen", "remainingSen",
            "cashSen", "noncashSen", "customerFeeSen", "merchantFeeSen", "customerPaysSen", "merchantReceivesSen", "tenderSen", "changeSen"}
            money(propertyName) = Cent(amounts, propertyName)
        Next
        Dim gross As Decimal = CDec(money("grossSen"))
        If lineTotal <> gross OrElse Decimal.Truncate((productTotal + 50D) / 100D) <> gross OrElse
           gross - CDec(money("discountSen")) - CDec(money("roundingSen")) <> CDec(money("netSen")) OrElse
           CDec(money("principalAppliedSen")) + CDec(money("remainingSen")) <> CDec(money("netSen")) OrElse
           CDec(money("cashSen")) + CDec(money("noncashSen")) <> CDec(money("principalAppliedSen")) OrElse
           CDec(money("principalAppliedSen")) + CDec(money("customerFeeSen")) <> CDec(money("customerPaysSen")) OrElse
           CDec(money("customerPaysSen")) - CDec(money("merchantFeeSen")) <> CDec(money("merchantReceivesSen")) OrElse
           CDec(money("tenderSen")) - CDec(money("changeSen")) <> CDec(money("cashSen")) Then
            Throw New ArgumentException("Hubungan jumlah nota tidak cocok.")
        End If
        If money("netSen") <= 0 Then Throw New ArgumentException("Neto nota harus positif.")
        If money("principalAppliedSen") > 0 AndAlso transactionId = "" Then
            Throw New ArgumentException("Transaksi pembayaran tidak tercatat.")
        End If

        Dim method As String = Text(payload, "paymentMethod", False)
        Dim noncashMethod As String = Text(payload, "noncashMethod", False)
        Select Case jobType
            Case "cashier_receipt"
                If method <> "CASH" AndAlso method <> "WIRE" AndAlso method <> "EDC" Then Throw New ArgumentException("Metode nota kasir salah.")
                If money("remainingSen") <> 0 Then Throw New ArgumentException("Nota lunas menyimpan sisa.")
            Case "kasbon_receipt"
                If method <> "CREDIT" OrElse money("discountSen") <> 0 OrElse
                   money("principalAppliedSen") >= money("netSen") Then Throw New ArgumentException("Komposisi kasbon salah.")
            Case "split_receipt"
                If method <> "SPLIT" OrElse money("remainingSen") <> 0 OrElse
                   money("cashSen") = 0 OrElse money("noncashSen") = 0 Then Throw New ArgumentException("Komposisi split salah.")
        End Select
        If ((method = "CASH" OrElse method = "CREDIT") AndAlso noncashMethod <> "") OrElse
           ((method = "WIRE" OrElse method = "EDC") AndAlso noncashMethod <> method) OrElse
           (method = "SPLIT" AndAlso noncashMethod <> "WIRE" AndAlso noncashMethod <> "EDC") Then
            Throw New ArgumentException("Metode nontunai tidak cocok.")
        End If
        If (method = "CASH" OrElse method = "CREDIT") AndAlso
           (money("noncashSen") <> 0 OrElse money("customerFeeSen") <> 0 OrElse money("merchantFeeSen") <> 0) Then
            Throw New ArgumentException("Metode tunai/kasbon mempunyai nontunai/fee.")
        End If
        If ((method = "WIRE" OrElse method = "EDC") AndAlso money("cashSen") <> 0) OrElse
           ((method = "WIRE" OrElse noncashMethod = "WIRE") AndAlso
            (money("customerFeeSen") <> 0 OrElse money("merchantFeeSen") <> 0)) Then
            Throw New ArgumentException("Komposisi kas/nontunai/fee salah.")
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

    ' P-606: nama yang lenyap saat normalisasi tidak boleh dicetak kosong.
    Private Function PrintedName(obj As JObject, name As String) As String
        Dim value As String = Text(obj, name, True)
        For Each ch As Char In value
            If Not Char.IsWhiteSpace(ch) AndAlso Not Char.IsControl(ch) AndAlso
               Char.GetUnicodeCategory(ch) <> UnicodeCategory.Format Then
                Return value
            End If
        Next
        Throw New ArgumentException(name & " harus punya karakter terlihat.")
    End Function

    Private Function PositiveString(obj As JObject, name As String, upper As Long) As Long
        Dim value As Long = CanonicalNumber(Text(obj, name, True), name)
        If value <= 0 OrElse value > upper Then Throw New ArgumentException(name & " melewati batas.")
        Return value
    End Function

    Private Function Cent(obj As JObject, name As String) As Long
        Return CanonicalNumber(Text(obj, name, True), name)
    End Function

    Private Function CanonicalNumber(raw As String, name As String) As Long
        Dim value As Long = 0
        If Not Canonical.IsMatch(raw) OrElse Not Long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse
           value > MaxCent Then Throw New ArgumentException(name & " bukan jumlah kanonik.")
        Return value
    End Function

    Private Function IntegerToken(token As JToken, upper As Integer) As Integer
        Dim value As Integer = 0
        If token Is Nothing OrElse token.Type <> JTokenType.Integer OrElse
           Not Integer.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse
           value < 1 OrElse value > upper Then Throw New ArgumentException("Bilangan schema/copies salah.")
        Return value
    End Function

    Private Sub Processor(token As JToken, path As String)
        Dim actor As JObject = Exact(token, path, "userId,name")
        Text(actor, "userId", True)
        PrintedName(actor, "name")
    End Sub
End Module
