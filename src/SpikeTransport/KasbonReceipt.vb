' Gama Print Agent — Handler nota kasbon (kasbon_receipt)
'
' Port faithful dari frmCashier.printReceipt_Kasbon. Header/customer/item/footer dari ReceiptCommon.
' Beda dari nota kasir: TIDAK ada diskon (kasbon, diskon diberikan saat pelunasan di piutang);
' total menampilkan BAYAR (-) & SISA. Kasbon selalu nota (selalu cetak blok customer).
'
' Math (sesuai asli):
'   grandTotal = shoppingTotal + serviceCharge   (kasbon tak hitung diskon)
'   bayar      = payment + serviceCharge          (BAYAR yang ditampilkan termasuk biaya layanan)
'   sisa       = grandTotal - bayar  = shoppingTotal - payment

Option Strict On
Option Explicit On

Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module KasbonReceipt

    Public Sub PrintKasbonReceipt(store As StoreInfo, p As KasbonReceiptPayload)
        RunSta(Sub() RenderKasbon(store, p))
    End Sub

    Private Sub RenderKasbon(store As StoreInfo, p As KasbonReceiptPayload)
        Dim printer As New Printer
        PrintStoreHeader(printer, store)
        PrintCustomerBlock(printer, p.customer)
        PrintReceiptNoLine(printer, If(p.rcptNo, ""), If(p.date, ""), If(p.time, ""))

        printer.Print(Line2())
        PrintItems(printer, p.items)

        Dim dblShoppingTotal As Double = p.shoppingTotal
        Dim dblServiceCharge As Double = p.serviceCharge
        Dim payment As Double = p.payment
        Dim dblGrandTotal As Double = dblShoppingTotal + dblServiceCharge
        Dim dblPayment As Double = payment + dblServiceCharge
        Dim dblRemains As Double = dblGrandTotal - dblPayment

        Dim strShoppingTotal As String = Fmt(dblShoppingTotal)
        Dim strServiceCharge As String = Fmt(dblServiceCharge)
        Dim strPayment As String = Fmt(dblPayment)
        Dim strRemains As String = Fmt(dblRemains)
        Dim stLen As Integer = strShoppingTotal.Length()

        PrintTotalLine(printer, stLen, CapTransTS, strShoppingTotal)

        If dblServiceCharge = 0 Then
            ' Fmt(0) = "" (format "###,###"), jadi BAYAR=0 dicetak literal "0" — sama spt asli.
            PrintTotalLine(printer, stLen, CapTransPY, If(payment = 0, "0", strPayment))
            printer.Print(T(1), Line1())
            PrintTotalLine(printer, stLen, CapTransRM, strRemains)
        Else
            PrintTotalLine(printer, stLen, CapTransSC, strServiceCharge)
            PrintTotalLine(printer, stLen, CapTransPY, strPayment)
            printer.Print(T(1), Line1())
            PrintTotalLine(printer, stLen, CapTransRM, strRemains)
        End If

        PrintFooter(printer)
        printer.EndDoc()
    End Sub

End Module
