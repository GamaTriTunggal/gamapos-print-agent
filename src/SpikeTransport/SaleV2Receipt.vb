' Formatter awal tiga nota penjualan schema 2. Belum terhubung ke Dispatch;
' tidak boleh diumumkan di /health sebelum smoke Windows dan printer fisik.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
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
        ' Validasi semua item dengan font/area cetak nyata sebelum mencetak header.
        printer.Font = New Font(FontCourier, 9, FontStyle.Bold)
        Dim printable As Single = Math.Min(CSng(printer.ScaleWidth), CSng(printer.TextWidth(StrDup(TotCol, " "))))
        Dim measure As Func(Of String, Single) = Function(value As String) CSng(printer.TextWidth(value))
        Dim itemLayouts As New List(Of List(Of String))()
        For Each token As JToken In CType(payload("items"), JArray)
            Dim item As JObject = CType(token, JObject)
            itemLayouts.Add(BuildSaleItemLines(Long.Parse(CStr(item("quantity100")), CultureInfo.InvariantCulture),
                CStr(item("unit")), CStr(item("name")),
                Long.Parse(CStr(item("priceSen")), CultureInfo.InvariantCulture),
                Long.Parse(CStr(item("totalSen")), CultureInfo.InvariantCulture),
                printable, TotCol, measure))
        Next
        Dim originalName As String = CStr(payload("originalProcessor")("name"))
        Dim reprintName As String = If(reprint Is Nothing, Nothing, CStr(reprint("processor")("name")))
        Dim footCenter As Single = CSng(printer.TextWidth(StrDup(24, " ") & Foot2)) - CSng(printer.TextWidth(Foot2)) / 2.0F
        Dim originalLines As List(Of PositionedNameLine) = LayoutOriginalName(originalName, footCenter, printable, measure)
        Dim reprintLines As New List(Of PositionedNameLine)()
        If reprintName IsNot Nothing Then reprintLines = LayoutReprintName(reprintName, printable, measure)
        PrintStoreHeader(printer, store)
        PrintCustomerBlock(printer, customer)
        PrintReceiptNoLine(printer, CStr(payload("receiptNo")), CStr(payload("date")),
                           CStr(payload("time")), reprintDate, reprintTime)
        printer.Print(Line2())
        PrintSaleItems(printer, itemLayouts)
        For Each row As SaleAmountRow In BuildSaleAmountRows(CStr(root("jobType")),
                  CStr(payload("paymentMethod")), CStr(payload("noncashMethod")), amounts)
            If row.SeparatorBefore Then printer.Print(Line1())
            AmountLine(printer, row.Caption, row.Sen)
        Next
        PrintSaleFooter(printer, originalLines, reprintLines)
        printer.EndDoc()
    End Sub

    Private Sub PrintSaleItems(printer As Printer, layouts As List(Of List(Of String)))
        For index As Integer = 0 To layouts.Count - 1
            For Each line As String In layouts(index)
                printer.Print(line)
            Next
            printer.Print(If(index = layouts.Count - 1, Line2(), Line1()))
        Next
    End Sub

    Private Sub AmountLine(printer As Printer, caption As String, amountSen As Long)
        Dim value As String = FormatSaleSen(amountSen)
        If caption.Length + value.Length + 1 > TotCol Then Throw New ArgumentException("Baris jumlah melampaui kolom nota.")
        printer.Print(T(1), caption, T(TotCol - value.Length + 1), value)
    End Sub

    Private Sub PrintSaleFooter(printer As Printer, originalLines As List(Of PositionedNameLine),
                                reprintLines As List(Of PositionedNameLine))
        printer.Print(T(1), Line1())
        printer.Print(T(6), Foot1, T(25), Foot2)
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        For Each line As PositionedNameLine In originalLines
            printer.CurrentX = line.X
            printer.Print(line.Text)
        Next
        For Each line As PositionedNameLine In reprintLines
            printer.CurrentX = line.X
            printer.Print(line.Text)
        Next
        printer.Print(T(1), FootGama)
    End Sub
End Module
