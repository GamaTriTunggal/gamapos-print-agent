' Parser murni tiga bukti pembayaran piutang schema 2. Belum terhubung
' ke dispatcher atau /health; v1 tetap dibaca oleh jalurnya sendiri.
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

Module ReceivableV2Parser
    Private Const MaxCent As Long = 99999999999999L
    Private ReadOnly CentPattern As New Regex("\A(0|[1-9][0-9]{0,13})\z", RegexOptions.CultureInvariant)
    Private ReadOnly SignedPattern As New Regex("\A(0|[1-9][0-9]{0,13}|-[1-9][0-9]{0,13})\z", RegexOptions.CultureInvariant)
    Private ReadOnly EventPattern As New Regex("\A[0-9a-f]{32}\z", RegexOptions.CultureInvariant)

    Friend Function ParseReceivableV2(body As String) As JObject
        If body Is Nothing OrElse Encoding.UTF8.GetByteCount(body) > 1048576 Then
            Throw New ArgumentException("Payload bukti piutang v2 terlalu besar.")
        End If
        Dim root As JObject
        Using reader As New JsonTextReader(New StringReader(body)) With {
            .MaxDepth = 32, .DateParseHandling = DateParseHandling.None}
            root = JObject.Load(reader, New JsonLoadSettings With {
                .DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error})
            If reader.Read() Then Throw New ArgumentException("Lebih dari satu nilai JSON.")
        End Using
        Exact(root, "envelope", "schemaVersion,jobId,jobType,printerRole,copies,store,payload")
        If IntegerToken(root("schemaVersion"), Integer.MaxValue) <> 2 OrElse
           IntegerToken(root("copies"), Integer.MaxValue) <> 1 OrElse
           Text(root, "printerRole", True) <> "CASHIER" Then
            Throw New ArgumentException("Versi, printer, atau jumlah salinan tidak sah.")
        End If
        Text(root, "jobId", True)
        Dim kind As String = Text(root, "jobType", True)
        If kind <> "receivable_selected" AndAlso kind <> "receivable_selected_card" AndAlso kind <> "receivable_proof" Then
            Throw New ArgumentException("Jenis bukti piutang v2 belum didukung.")
        End If
        Dim store As JObject = Exact(root("store"), "store", "name,address,contact")
        PrintedName(store, "name")
        Text(store, "address", False)
        Text(store, "contact", False)

        Dim payload As JObject = Exact(root("payload"), "payload",
            "eventId,transactionId,date,time,customer,allocations,amounts,paymentMethod,originalProcessor", "reprint")
        If Not EventPattern.IsMatch(Text(payload, "eventId", True)) OrElse
           Text(payload, "transactionId", True).Length <> 16 Then
            Throw New ArgumentException("Identitas pembayaran tidak sah.")
        End If
        Text(payload, "date", True)
        Text(payload, "time", True)
        Dim customer As JObject = Exact(payload("customer"), "customer", "id,name,address,contact")
        Text(customer, "id", True)
        PrintedName(customer, "name")
        Text(customer, "address", False)
        Text(customer, "contact", False)
        Processor(payload("originalProcessor"), "originalProcessor")
        If payload.Property("reprint") IsNot Nothing Then
            Dim reprint As JObject = Exact(payload("reprint"), "reprint", "date,time,processor")
            Text(reprint, "date", True)
            Text(reprint, "time", True)
            Processor(reprint("processor"), "reprint.processor")
        End If
        Dim method As String = Text(payload, "paymentMethod", True)
        If (method <> "CASH" AndAlso method <> "WIRE" AndAlso method <> "EDC") OrElse
           (kind = "receivable_selected_card" AndAlso method <> "EDC") OrElse
           (kind = "receivable_selected" AndAlso method = "EDC") Then
            Throw New ArgumentException("Metode dan jenis bukti tidak cocok.")
        End If

        Dim amounts As JObject = Exact(payload("amounts"), "amounts",
            "totalBeforeSen,discountSen,roundingSen,transferFeeSen,stampFeeSen,netPaymentSen,remainingSen,cashSen,noncashSen,customerFeeSen,merchantFeeSen,customerPaysSen,merchantReceivesSen,tenderSen,changeSen")
        Dim money As New Dictionary(Of String, Long)(StringComparer.Ordinal)
        For Each name As String In {"totalBeforeSen", "discountSen", "roundingSen", "transferFeeSen", "stampFeeSen", "netPaymentSen", "remainingSen", "cashSen", "noncashSen", "customerFeeSen", "merchantFeeSen", "customerPaysSen", "merchantReceivesSen", "tenderSen", "changeSen"}
            money(name) = If(name = "merchantReceivesSen", SignedCent(amounts, name), Cent(amounts, name))
        Next

        Dim selected As Boolean = kind <> "receivable_proof"
        Dim allocations As JArray = TryCast(payload("allocations"), JArray)
        If allocations Is Nothing OrElse allocations.Count = 0 Then Throw New ArgumentException("Alokasi bon kosong.")
        Dim sumBefore, sumPrincipal, sumDiscount, sumRounding, sumTransfer, sumStamp As Decimal
        Dim previous As Long = 0
        For Each token As JToken In allocations
            Dim line As JObject = Exact(token, "allocation", "receiptNo,beforeSen,principalAppliedSen,afterSen,discountSen,roundingSen,transferFeeSen,stampFeeSen")
            Dim receiptNo As Long = PositiveString(line, "receiptNo", Integer.MaxValue)
            If receiptNo <= previous Then Throw New ArgumentException("Nomor bon tidak terurut atau ganda.")
            previous = receiptNo
            Dim before As Long = SignedCent(line, "beforeSen")
            Dim principal As Long = SignedCent(line, "principalAppliedSen")
            Dim after As Long = SignedCent(line, "afterSen")
            Dim discount As Long = Cent(line, "discountSen")
            Dim rounding As Long = Cent(line, "roundingSen")
            Dim transfer As Long = Cent(line, "transferFeeSen")
            Dim stamp As Long = Cent(line, "stampFeeSen")
            If CDec(before) <> CDec(principal) + CDec(after) OrElse
               (selected AndAlso after <> 0) OrElse
               (Not selected AndAlso (discount <> 0 OrElse rounding <> 0 OrElse transfer <> 0 OrElse stamp <> 0)) Then
                Throw New ArgumentException("Alokasi bon tidak konservatif.")
            End If
            sumBefore += CDec(before)
            sumPrincipal += CDec(principal)
            sumDiscount += CDec(discount)
            sumRounding += CDec(rounding)
            sumTransfer += CDec(transfer)
            sumStamp += CDec(stamp)
        Next
        If sumDiscount <> CDec(money("discountSen")) OrElse
           sumRounding <> CDec(money("roundingSen")) OrElse
           sumStamp <> CDec(money("stampFeeSen")) Then
            Throw New ArgumentException("Pengurangan tidak cocok dengan alokasi.")
        End If
        If selected Then
            If sumBefore <> CDec(money("totalBeforeSen")) OrElse
               sumPrincipal <> CDec(money("totalBeforeSen")) OrElse
               sumTransfer <> CDec(money("transferFeeSen")) Then
                Throw New ArgumentException("Pokok selected atau biaya tidak cocok.")
            End If
        ElseIf sumPrincipal <> CDec(money("netPaymentSen")) OrElse sumTransfer <> 0D OrElse
               money("discountSen") <> 0 OrElse money("roundingSen") <> 0 OrElse money("stampFeeSen") <> 0 Then
            Throw New ArgumentException("Pokok FIFO atau pengurangan tidak cocok.")
        End If

        Dim net As Decimal = CDec(money("netPaymentSen"))
        Dim pays As Decimal = CDec(money("customerPaysSen"))
        If (selected AndAlso money("totalBeforeSen") = 0) OrElse
           net <> CDec(money("cashSen")) + CDec(money("noncashSen")) OrElse
           pays <> net + CDec(money("customerFeeSen")) OrElse
           CDec(money("cashSen")) <> CDec(money("tenderSen")) - CDec(money("changeSen")) Then
            Throw New ArgumentException("Komposisi uang bukti tidak cocok.")
        End If
        If selected AndAlso net = 0D AndAlso
           (pays <> 0D OrElse money("merchantReceivesSen") <> 0 OrElse
            money("tenderSen") <> 0 OrElse money("changeSen") <> 0) Then
            Throw New ArgumentException("Pembayaran selected nol mempunyai kas atau fee.")
        End If
        Select Case method
            Case "CASH"
                If money("noncashSen") <> 0 OrElse money("customerFeeSen") <> 0 OrElse money("merchantFeeSen") <> 0 Then
                    Throw New ArgumentException("Tunai mempunyai nontunai/EDC fee.")
                End If
            Case "WIRE"
                If money("cashSen") <> 0 OrElse money("tenderSen") <> 0 OrElse money("changeSen") <> 0 OrElse
                   money("customerFeeSen") <> 0 OrElse money("merchantFeeSen") <> 0 Then
                    Throw New ArgumentException("Transfer mempunyai kas/EDC fee.")
                End If
            Case "EDC"
                If money("noncashSen") = 0 OrElse money("cashSen") <> 0 OrElse money("tenderSen") <> 0 OrElse
                   money("changeSen") <> 0 OrElse (Not selected AndAlso money("transferFeeSen") <> 0) Then
                    Throw New ArgumentException("EDC mempunyai kas/biaya transfer yang tidak sah.")
                End If
        End Select
        If selected Then
            If money("remainingSen") <> 0 OrElse
               CDec(money("totalBeforeSen")) <> net + CDec(money("discountSen")) + CDec(money("roundingSen")) + CDec(money("transferFeeSen")) + CDec(money("stampFeeSen")) OrElse
               CDec(money("merchantReceivesSen")) <> pays - CDec(money("merchantFeeSen")) Then
                Throw New ArgumentException("Total pelunasan selected tidak cocok.")
            End If
        ElseIf net = 0D OrElse
               CDec(money("totalBeforeSen")) <> net + CDec(money("remainingSen")) OrElse
               CDec(money("merchantReceivesSen")) <> pays - CDec(money("merchantFeeSen")) - CDec(money("transferFeeSen")) Then
            Throw New ArgumentException("Total cicilan FIFO tidak cocok.")
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

    Private Function PositiveString(obj As JObject, name As String, upper As Long) As Long
        Dim value As Long = CanonicalNumber(obj, name, False)
        If value <= 0 OrElse value > upper Then Throw New ArgumentException(name & " melewati batas.")
        Return value
    End Function

    Private Function Cent(obj As JObject, name As String) As Long
        Return CanonicalNumber(obj, name, False)
    End Function

    Private Function SignedCent(obj As JObject, name As String) As Long
        Return CanonicalNumber(obj, name, True)
    End Function

    Private Function CanonicalNumber(obj As JObject, name As String, signed As Boolean) As Long
        Dim raw As String = Text(obj, name, True)
        Dim value As Long = 0
        Dim pattern As Regex = If(signed, SignedPattern, CentPattern)
        If Not pattern.IsMatch(raw) OrElse
           Not Long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, value) OrElse
           Math.Abs(CDec(value)) > CDec(MaxCent) Then Throw New ArgumentException(name & " bukan sen kanonik.")
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
