' Formatter awal tiga nota penjualan schema 2. Rute Dispatch masih tertutup
' oleh schema 1; jangan umumkan v2 di /health sebelum smoke Windows/printer.
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
        PrintSaleV2Receipt(ParseSaleV2(body))
    End Sub

    Friend Sub PrintSaleV2Receipt(root As JObject)
        RunSta(Sub() RenderSaleV2(root, False))
    End Sub

    ' Jalankan seluruh perencanaan dengan metrik printer asli, tanpa Printer.Print/EndDoc.
    Friend Sub PreflightSaleV2Receipt(body As String)
        Dim root As JObject = ParseSaleV2(body)
        RunSta(Sub() RenderSaleV2(root, True))
    End Sub

    Private Sub RenderSaleV2(root As JObject, preflightOnly As Boolean)
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
        ' Semua teks dinamis harus direncanakan sebelum Printer.Print pertama.
        SetV2ReceiptFont(printer, 18, FontStyle.Regular)
        Dim storeNameLines As List(Of String) = LayoutSaleHeaderColumns(store.name, "NO NAME", StoreNameCol)
        SetV2ReceiptFont(printer, 9, FontStyle.Bold)
        ' PowerPacks/TM-U220 mencetak 40 karakter 9 pt utuh walau TextWidth(40) > ScaleWidth.
        ' Ukur batas logis 40 kolom; kelayakan fisiknya dibuktikan pada printer uji.
        Dim printable As Single = CSng(printer.TextWidth(StrDup(TotCol, " ")))
        Dim measure As Func(Of String, Single) = Function(value As String) CSng(printer.TextWidth(value))
        Dim storeDetails As New List(Of String)()
        storeDetails.AddRange(LayoutSaleHeaderColumns(store.address, "", TotCol))
        storeDetails.AddRange(LayoutSaleHeaderColumns(store.contact, "", TotCol))
        Dim customerLines As New List(Of String)()
        customerLines.AddRange(LayoutSaleCustomer(CapCustName, customer.name, printable, measure))
        customerLines.AddRange(LayoutSaleCustomer(CapCustAddr, customer.address, printable, measure))
        customerLines.AddRange(LayoutSaleCustomer(CapCustCont, customer.contact, printable, measure))
        customerLines.AddRange(LayoutSaleCustomer(CapCustPoNo, customer.poNo, printable, measure))
        Dim receiptLine As String = LayoutSaleReceiptLine(CStr(payload("receiptNo")), CStr(payload("date")),
                                                           CStr(payload("time")), False, printable, measure)
        Dim reprintLine As String = Nothing
        If reprint IsNot Nothing Then
            reprintLine = LayoutSaleReceiptLine("", reprintDate, reprintTime, True, printable, measure)
        End If
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
        Dim originalLines As List(Of String) = LayoutSaleOriginalNameColumns(originalName, Foot2, TotCol)
        Dim reprintLines As New List(Of String)()
        If reprintName IsNot Nothing Then reprintLines = LayoutSaleReprintNameColumns(reprintName, TotCol)
        Dim amountRows As List(Of SaleAmountRow) = BuildSaleAmountRows(CStr(root("jobType")),
                  CStr(payload("paymentMethod")), CStr(payload("noncashMethod")), amounts)
        Dim amountLines As New List(Of String)()
        For Each row As SaleAmountRow In amountRows
            amountLines.Add(LayoutSaleAmountLine(row.Caption, row.Sen, TotCol, printable, measure))
        Next
        Dim correctionLines As List(Of String) = LayoutSaleCorrections(reprint, printable, measure)

        If preflightOnly Then
            ' Uji transisi font yang dipakai saat cetak, tanpa Print/EndDoc.
            SetV2ReceiptFont(printer, 18, FontStyle.Regular)
            Dim largeWidth As Single = CSng(printer.TextWidth("M"))
            SetV2ReceiptFont(printer, 9, FontStyle.Bold)
            Dim normalWidth As Single = CSng(printer.TextWidth("M"))
            If largeWidth <= 0 OrElse normalWidth <= 0 Then Throw New ArgumentException("Metrik pergantian font tidak sah.")
            Return
        End If

        PrintSaleStage("store-name", Sub()
                                          SetV2ReceiptFont(printer, 18, FontStyle.Regular)
                                          printer.CurrentX = 0
                                          printer.CurrentY = 0
                                          PrintSaleColumnLines(printer, storeNameLines)
                                      End Sub)
        PrintSaleStage("store-details", Sub()
                                             SetV2ReceiptFont(printer, 9, FontStyle.Bold)
                                             PrintSaleColumnLines(printer, storeDetails)
                                             printer.Print()
                                         End Sub)
        PrintSaleStage("customer", Sub()
                                        For Each line As String In customerLines
                                            printer.Print(line)
                                        Next
                                        If customerLines.Count > 0 Then printer.Print()
                                    End Sub)
        PrintSaleStage("receipt", Sub()
                                       printer.Print(receiptLine)
                                       If reprintLine IsNot Nothing Then printer.Print(reprintLine)
                                       printer.Print(Line2())
                                   End Sub)
        PrintSaleStage("items", Sub() PrintSaleItems(printer, itemLayouts))
        PrintSaleStage("amounts", Sub()
                                       For index As Integer = 0 To amountRows.Count - 1
                                           If amountRows(index).SeparatorBefore Then printer.Print(Line1())
                                           printer.Print(amountLines(index))
                                       Next
                                   End Sub)
        PrintSaleStage("corrections", Sub()
                                           If correctionLines.Count > 0 Then
                                               printer.Print(Line1())
                                               For Each line As String In correctionLines
                                                   printer.Print(line)
                                               Next
                                           End If
                                       End Sub)
        PrintSaleStage("footer", Sub() PrintSaleFooter(printer, originalLines, reprintLines))
        PrintSaleStage("end-doc", Sub() printer.EndDoc())
    End Sub

    Private Sub PrintSaleStage(stage As String, action As Action)
        Try
            action()
        Catch ex As Exception
            ' Kode tahap tetap, tanpa isi nota; probe tidak menampilkan pesan driver/exception.
            Throw New InvalidOperationException("SALE_V2_STAGE:" & stage, ex)
        End Try
    End Sub

    Private Sub PrintSaleColumnLines(printer As Printer, lines As List(Of String))
        For Each line As String In lines
            printer.Print(T(1), line)
        Next
    End Sub

    Private Sub PrintSaleItems(printer As Printer, layouts As List(Of List(Of String)))
        For index As Integer = 0 To layouts.Count - 1
            For Each line As String In layouts(index)
                printer.Print(line)
            Next
            printer.Print(If(index = layouts.Count - 1, Line2(), Line1()))
        Next
    End Sub

    Private Sub PrintSaleFooter(printer As Printer, originalLines As List(Of String),
                                reprintLines As List(Of String))
        printer.Print(T(1), Line1())
        printer.Print(T(6), Foot1, T(SaleFooterCaptionColumn), Foot2)
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        PrintSaleColumnLines(printer, originalLines)
        PrintSaleColumnLines(printer, reprintLines)
        printer.Print(T(1), FootGama)
    End Sub
End Module
