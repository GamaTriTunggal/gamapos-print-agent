' IC-15 (gamapos-go-2 papan board-number-space; register P-022): kode
' pelanggan/barang baru tumbuh satu angka sesudah ruang lama habis
' (CS9999 → CS10000, VJ9999 → VJ10000) dan nota toko besar menjadi 9 digit
' (yyMM + 5 angka). Parser v2 menerima kode 1..12 karakter dan menolak 13;
' nomor nota 9 digit tetap muat di kolom 4 tanpa menggeser tanggal kolom 19.
' Uji murni, bukan bukti kertas (uji fisik = IC-17 pemilik).
Option Strict On
Option Explicit On

Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Linq

Module CodeLengthTests
    Private ReadOnly Characters As Func(Of String, Single) = Function(value As String) CSng(value.Length)

    ' Panjang 7 = kode pertama sesudah ruang 4 angka habis; 12 = batas lebar bersama.
    Private ReadOnly AcceptedCustomerIds As String() = {"CS10000", "CS99999", "CS1234567890"}
    Private ReadOnly AcceptedItemIds As String() = {"VJ10000", "TN99999", "VJ1234567890"}
    Private ReadOnly RejectedIds As String() = {"CS12345678901", "VJ12345678901", "vj10000", "VJ-1000", "VJ 1000", ""}

    Friend Sub Run(v2Folder As String)
        If MaxCodeLength <> 12 OrElse Not CodePattern.IsMatch(New String("A"c, 12)) OrElse
           CodePattern.IsMatch(New String("A"c, 13)) Then
            Throw New InvalidOperationException("Lebar kode agent tidak sama dengan codelength.Max (12).")
        End If
        CheckReturnCodes(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "return_note.sample.json")))
        CheckSaleCorrectionCodes(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "sale_corrected_reprint_simple.sample.json")))
        CheckReceivableCodes(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "receivable_selected.sample.json")))
        CheckLongCodeSamples(v2Folder)
        Console.WriteLine("Code length: return/sale-correction ids 7..12 accepted, 13 rejected; 9-digit receipts laid out at column 4 (not physical).")
    End Sub

    Private Sub CheckReturnCodes(source As String)
        Dim original As JObject = ParseReturnV2(source)
        For Each id As String In AcceptedCustomerIds
            Dim job As JObject = CType(original.DeepClone(), JObject)
            job("payload")("customer")("id") = id
            AcceptReturn("kode pelanggan retur " & id, job)
        Next
        For Each id As String In AcceptedItemIds
            Dim job As JObject = CType(original.DeepClone(), JObject)
            job("payload")("items")(0)("itemId") = id
            job("payload")("items")(1)("itemId") = id
            AcceptReturn("kode barang retur " & id, job)
        Next
        For Each id As String In RejectedIds
            Dim customerJob As JObject = CType(original.DeepClone(), JObject)
            customerJob("payload")("customer")("id") = id
            RejectReturn("kode pelanggan retur '" & id & "'", customerJob)
            Dim itemJob As JObject = CType(original.DeepClone(), JObject)
            itemJob("payload")("items")(1)("itemId") = id
            RejectReturn("kode barang retur '" & id & "'", itemJob)
        Next

        Dim nineDigits As JObject = CType(original.DeepClone(), JObject)
        nineDigits("payload")("receiptNo") = "261110000"
        Dim plan As ReturnV2CorePlan = BuildReturnV2CorePlan(ParseReturnV2(nineDigits.ToString(Formatting.None)), 40.0F, Characters)
        ExpectReceiptLine("retur 9 digit", plan.ReceiptLine, "261110000", CStr(nineDigits("payload")("date")))
        ' Kode pelanggan/barang tidak dicetak di nota retur v2: hanya diperiksa.
        Dim longCodes As JObject = CType(nineDigits.DeepClone(), JObject)
        longCodes("payload")("customer")("id") = "CS1234567890"
        longCodes("payload")("items")(0)("itemId") = "VJ1234567890"
        Dim longPlan As ReturnV2CorePlan = BuildReturnV2CorePlan(ParseReturnV2(longCodes.ToString(Formatting.None)), 40.0F, Characters)
        If longPlan.CustomerLines.Concat(longPlan.ItemLines.SelectMany(Function(lines) lines)).
            Any(Function(line) line.Contains("CS1234567890") OrElse line.Contains("VJ1234567890")) OrElse
           Not longPlan.CustomerLines.SequenceEqual(plan.CustomerLines) Then
            Throw New InvalidOperationException("Kode retur panjang mengubah susunan nota retur.")
        End If
    End Sub

    Private Sub CheckSaleCorrectionCodes(source As String)
        Dim original As JObject = ParseSaleV2(source)
        For Each id As String In AcceptedCustomerIds
            Dim job As JObject = CType(original.DeepClone(), JObject)
            job("payload")("reprint")("corrections")(0)("customerId") = id
            AcceptSale("kode pelanggan koreksi " & id, job)
        Next
        For Each id As String In RejectedIds.Concat({"CS0001"})
            Dim job As JObject = CType(original.DeepClone(), JObject)
            job("payload")("reprint")("corrections")(0)("customerId") = id
            RejectSale("kode pelanggan koreksi '" & id & "'", job)
        Next
        Dim nineDigits As JObject = CType(original.DeepClone(), JObject)
        nineDigits("payload")("receiptNo") = "261110000"
        Dim parsed As JObject = ParseSaleV2(nineDigits.ToString(Formatting.None))
        ExpectReceiptLine("nota jual 9 digit",
            LayoutSaleReceiptLine(CStr(parsed("payload")("receiptNo")), CStr(parsed("payload")("date")),
                                  CStr(parsed("payload")("time")), False, 40.0F, Characters),
            "261110000", CStr(parsed("payload")("date")))
    End Sub

    Private Sub CheckReceivableCodes(source As String)
        ' Parser piutang tidak membatasi lebar kode (perilaku lama); yang dijaga di
        ' sini adalah susunan "KODE" dan "BON <nota 9 digit>" di 40 kolom.
        Dim job As JObject = JObject.Parse(source)
        Dim allocations As JArray = CType(job("payload")("allocations"), JArray)
        allocations(0)("receiptNo") = "261110000"
        allocations(1)("receiptNo") = "261110001"
        For Each id As String In AcceptedCustomerIds
            job("payload")("customer")("id") = id
            Dim parsed As JObject = ParseReceivableV2(job.ToString(Formatting.None))
            Dim plan As ReceivableV2PrintPlan = BuildReceivableV2Plan(parsed, 40.0F, 20.0F, Characters, Characters)
            If Not plan.Metadata.Contains("KODE      : " & id) OrElse
               plan.Metadata.Any(Function(line) line.Length > 40) OrElse
               plan.AllocationLines.Count <> 2 OrElse
               Not plan.AllocationLines(0).StartsWith("BON 261110000 ", StringComparison.Ordinal) OrElse
               Not plan.AllocationLines(1).StartsWith("BON 261110001 ", StringComparison.Ordinal) OrElse
               plan.AllocationLines.Any(Function(line) line.Length <> 40) Then
                Throw New InvalidOperationException("Bukti piutang kode " & id & " / nota 9 digit tidak muat 40 kolom.")
            End If
        Next
    End Sub

    Private Sub CheckLongCodeSamples(v2Folder As String)
        ' Fixture kertas (juga diputar smoke Windows ke printer virtual) harus tetap
        ' membawa kode 7 karakter dan nota 9 digit.
        Dim returnJob As JObject = ParseReturnV2(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "return_long_code.sample.json")))
        If CStr(returnJob("payload")("customer")("id")) <> "CS10000" OrElse
           CStr(returnJob("payload")("receiptNo")) <> "261110000" OrElse
           Not CType(returnJob("payload")("items"), JArray).All(Function(item) CStr(item("itemId")).Length = 7) Then
            Throw New InvalidOperationException("Fixture retur kode panjang kehilangan kode 7 karakter / nota 9 digit.")
        End If
        Dim saleJob As JObject = ParseSaleV2(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "sale_corrected_reprint_long_code.sample.json")))
        If CStr(saleJob("payload")("receiptNo")) <> "261110000" OrElse
           CStr(saleJob("payload")("reprint")("corrections")(0)("customerId")) <> "CS10000" Then
            Throw New InvalidOperationException("Fixture koreksi kode panjang kehilangan CS10000 / nota 9 digit.")
        End If
        Dim receivableJob As JObject = ParseReceivableV2(IO.File.ReadAllText(IO.Path.Combine(v2Folder, "receivable_long_code.sample.json")))
        Dim receivablePlan As ReceivableV2PrintPlan = BuildReceivableV2Plan(receivableJob, 40.0F, 20.0F, Characters, Characters)
        If Not receivablePlan.Metadata.Contains("KODE      : CS10000") OrElse
           Not receivablePlan.AllocationLines(0).StartsWith("BON 261110000 ", StringComparison.Ordinal) Then
            Throw New InvalidOperationException("Fixture piutang kode panjang kehilangan KODE CS10000 / BON 9 digit.")
        End If
    End Sub

    ' "NO:" kolom 1, nomor mulai kolom 4, tanggal kolom 19 (tetap seperti nota 8 digit).
    Private Sub ExpectReceiptLine(name As String, line As String, number As String, printedDate As String)
        If line Is Nothing OrElse Not line.StartsWith("NO:" & number & " ", StringComparison.Ordinal) OrElse
           line.Length > 40 OrElse line.Length < 28 OrElse line.Substring(18, printedDate.Length) <> printedDate Then
            Throw New InvalidOperationException(name & ": baris nomor nota bergeser: " & If(line, "(kosong)"))
        End If
    End Sub

    Private Sub AcceptReturn(name As String, job As JObject)
        Try
            ParseReturnV2(job.ToString(Formatting.None))
        Catch ex As Exception
            Throw New InvalidOperationException(name & " ditolak: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub RejectReturn(name As String, job As JObject)
        Try
            ParseReturnV2(job.ToString(Formatting.None))
        Catch ex As ArgumentException
            Return
        End Try
        Throw New InvalidOperationException(name & " diterima padahal tidak sah.")
    End Sub

    Private Sub AcceptSale(name As String, job As JObject)
        Try
            ParseSaleV2(job.ToString(Formatting.None))
        Catch ex As Exception
            Throw New InvalidOperationException(name & " ditolak: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub RejectSale(name As String, job As JObject)
        Try
            ParseSaleV2(job.ToString(Formatting.None))
        Catch ex As ArgumentException
            Return
        End Try
        Throw New InvalidOperationException(name & " diterima padahal tidak sah.")
    End Sub
End Module
