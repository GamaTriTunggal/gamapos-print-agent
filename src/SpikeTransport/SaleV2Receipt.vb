' Formatter awal tiga nota penjualan schema 2. Belum terhubung ke Dispatch;
' tidak boleh diumumkan di /health sebelum smoke Windows dan printer fisik.
Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Globalization
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6
Imports Newtonsoft.Json.Linq

Module SaleV2Receipt
    Friend Sub PrintSaleV2Receipt(body As String)
        Dim root As JObject = ParseSaleV2(body)
        RunSta(Sub() RenderSaleV2(root))
    End Sub

    Private Sub RenderSaleV2(root As JObject)
        Dim payload As JObject = CType(root("payload"), JObject)
        Dim amounts As JObject = CType(payload("amounts"), JObject)
        Dim storeJson As JObject = CType(root("store"), JObject)
        Dim customerJson As JObject = CType(payload("customer"), JObject)
        Dim store As New StoreInfo With {
            .name = CStr(storeJson("name")), .address = CStr(storeJson("address")), .contact = CStr(storeJson("contact"))}
        Dim customer As New CustomerInfo With {
            .name = CStr(customerJson("name")), .address = CStr(customerJson("address")),
            .contact = CStr(customerJson("contact")), .poNo = CStr(customerJson("poNo"))}
        Dim reprint As JObject = TryCast(payload("reprint"), JObject)
        Dim reprintDate As String = Nothing
        Dim reprintTime As String = Nothing
        If reprint IsNot Nothing Then
            reprintDate = CStr(reprint("date"))
            reprintTime = CStr(reprint("time"))
        End If

        Dim printer As New Printer()
        PrintStoreHeader(printer, store)
        PrintCustomerBlock(printer, customer)
        PrintReceiptNoLine(printer, CStr(payload("receiptNo")), CStr(payload("date")),
                           CStr(payload("time")), reprintDate, reprintTime)
        printer.Print(Line2())
        PrintSaleItems(printer, CType(payload("items"), JArray))
        PrintSaleAmounts(printer, CStr(root("jobType")), CStr(payload("paymentMethod")),
                         CStr(payload("noncashMethod")), amounts)
        Dim originalName As String = CStr(payload("originalProcessor")("name"))
        Dim reprintName As String = If(reprint Is Nothing, Nothing, CStr(reprint("processor")("name")))
        PrintSaleFooter(printer, originalName, reprintName)
        printer.EndDoc()
    End Sub

    Private Sub PrintSaleItems(printer As Printer, items As JArray)
        Dim limit As Single = Math.Min(CSng(printer.ScaleWidth), CSng(printer.TextWidth(StrDup(TotCol, " "))))
        For index As Integer = 0 To items.Count - 1
            Dim item As JObject = CType(items(index), JObject)
            Dim quantity As Long = Long.Parse(CStr(item("quantity100")), CultureInfo.InvariantCulture)
            Dim price As Long = Long.Parse(CStr(item("priceSen")), CultureInfo.InvariantCulture)
            Dim total As Long = Long.Parse(CStr(item("totalSen")), CultureInfo.InvariantCulture)
            Dim unit As String = NormalizeProcessorName(CStr(item("unit")))
            Dim prefix As String = FormatSaleQuantity(quantity) & If(unit = "", " ", " " & unit & " ")
            Dim continuation As String = StrDup(prefix.Length, " ")
            Dim name As String = NormalizeProcessorName(CStr(item("name")))
            Dim current As String = ""
            Dim initial As Boolean = True
            Dim elements As TextElementEnumerator = StringInfo.GetTextElementEnumerator(name)
            While elements.MoveNext()
                Dim element As String = elements.GetTextElement()
                Dim start As String = If(initial, prefix, continuation)
                If printer.TextWidth(start & current & element) > limit Then
                    If current = "" Then Throw New ArgumentException("Nama item melampaui area cetak.")
                    printer.Print(start & current)
                    initial = False
                    current = ""
                End If
                current &= element
            End While
            printer.Print(If(initial, prefix, continuation) & current)

            Dim left As String = "  X " & FormatSaleSen(price)
            Dim right As String = FormatSaleSen(total)
            If left.Length + right.Length + 1 <= TotCol Then
                printer.Print(left & StrDup(TotCol - left.Length - right.Length, " ") & right)
            Else
                printer.Print(left)
                printer.Print(StrDup(Math.Max(0, TotCol - right.Length), " ") & right)
            End If
            printer.Print(If(index = items.Count - 1, Line2(), Line1()))
        Next
    End Sub

    Private Sub PrintSaleAmounts(printer As Printer, jobType As String, method As String,
                                 noncashMethod As String, amounts As JObject)
        AmountLine(printer, "TOTAL BELANJA", Sen(amounts, "grossSen"))
        If Sen(amounts, "discountSen") > 0 Then AmountLine(printer, "DISKON (-)", Sen(amounts, "discountSen"))
        If Sen(amounts, "roundingSen") > 0 Then AmountLine(printer, "PEMBULATAN (-)", Sen(amounts, "roundingSen"))
        printer.Print(Line1())
        AmountLine(printer, "TOTAL NOTA", Sen(amounts, "netSen"))
        If Sen(amounts, "customerFeeSen") > 0 Then
            AmountLine(printer, "BIAYA EDC", Sen(amounts, "customerFeeSen"))
            printer.Print(Line1())
            AmountLine(printer, "TOTAL DIBAYAR", Sen(amounts, "customerPaysSen"))
        End If
        If jobType = "kasbon_receipt" Then
            AmountLine(printer, "BAYAR", Sen(amounts, "principalAppliedSen"))
            AmountLine(printer, "SISA UTANG", Sen(amounts, "remainingSen"))
        ElseIf method = "SPLIT" Then
            AmountLine(printer, "TUNAI", Sen(amounts, "cashSen"))
            AmountLine(printer, If(noncashMethod = "EDC", "EDC", "TRANSFER"), Sen(amounts, "noncashSen"))
        ElseIf method = "CASH" AndAlso Sen(amounts, "changeSen") > 0 Then
            AmountLine(printer, "UANG DITERIMA", Sen(amounts, "tenderSen"))
            AmountLine(printer, "KEMBALIAN", Sen(amounts, "changeSen"))
        End If
    End Sub

    Private Sub AmountLine(printer As Printer, caption As String, amountSen As Long)
        Dim value As String = FormatSaleSen(amountSen)
        If caption.Length + value.Length + 1 > TotCol Then Throw New ArgumentException("Baris jumlah melampaui kolom nota.")
        printer.Print(T(1), caption, T(TotCol - value.Length + 1), value)
    End Sub

    Private Function Sen(amounts As JObject, field As String) As Long
        Return Long.Parse(CStr(amounts(field)), CultureInfo.InvariantCulture)
    End Function

    Private Sub PrintSaleFooter(printer As Printer, originalName As String, reprintName As String)
        printer.Print(T(1), Line1())
        printer.Print(T(6), Foot1, T(25), Foot2)
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        Dim printable As Single = Math.Min(CSng(printer.ScaleWidth), CSng(printer.TextWidth(StrDup(TotCol, " "))))
        Dim footCenter As Single = CSng(printer.TextWidth(StrDup(24, " ") & Foot2)) - CSng(printer.TextWidth(Foot2)) / 2.0F
        Dim measure As Func(Of String, Single) = Function(value As String) CSng(printer.TextWidth(value))
        For Each line As PositionedNameLine In LayoutOriginalName(originalName, footCenter, printable, measure)
            printer.CurrentX = line.X
            printer.Print(line.Text)
        Next
        If reprintName IsNot Nothing Then
            For Each line As PositionedNameLine In LayoutReprintName(reprintName, printable, measure)
                printer.CurrentX = line.X
                printer.Print(line.Text)
            Next
        End If
        printer.Print(T(1), FootGama)
    End Sub
End Module
