' Gama Print Agent — Handler nota split payment (split_receipt)
'
' Port faithful dari frmCashier.printReceipt_SplitPayment. Header/customer/item/footer dari ReceiptCommon.
' Selalu nota (blok customer selalu dicetak — sub asli tak punya param isReceipt).
'
' BEDA total dari nota kasir (faithful, sengaja dipertahankan):
'   1. Urutan: TOTAL BELANJA → BIAYA LAYANAN → DISKON → GRAND TOTAL (kasir: DISKON dulu, baru BIAYA LAYANAN).
'   2. SEMUA nilai total mulai di kolom shopping-total (TAB(totCol - shoppingTotalLen + 1)),
'      bukan rata-kanan per panjang nilai seperti nota kasir.

Option Strict On
Option Explicit On

Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module SplitReceipt

    Public Sub PrintSplitReceipt(store As StoreInfo, p As SplitReceiptPayload)
        RunSta(Sub() RenderSplit(store, p))
    End Sub

    Private Sub RenderSplit(store As StoreInfo, p As SplitReceiptPayload)
        Dim printer As New Printer
        PrintStoreHeader(printer, store)
        PrintCustomerBlock(printer, p.customer)
        PrintReceiptNoLine(printer, If(p.rcptNo, ""), If(p.date, ""), If(p.time, ""))

        printer.Print(Line2())
        PrintItems(printer, p.items)

        Dim totalShopping As Double = p.shoppingTotal
        Dim discount As Double = p.discount
        Dim serviceCharge As Double = p.serviceCharge
        Dim strShoppingTotal As String = Fmt(totalShopping)
        Dim strServiceCharge As String = Fmt(serviceCharge)
        Dim strDiscount As String = Fmt(discount)
        Dim strGrandTotal As String = Fmt(totalShopping - discount + serviceCharge)
        Dim stLen As Integer = strShoppingTotal.Length()

        ' capCol = anchor caption (sama spt nota lain); valCol = SEMUA nilai mulai di kolom shopping-total.
        Dim capCol As Integer = TotCol - stLen - CapTransTS.Length() + 1
        Dim valCol As Integer = TotCol - stLen + 1

        printer.Print(T(capCol), CapTransTS, T(valCol), strShoppingTotal)
        If serviceCharge <> 0 Then printer.Print(T(capCol), CapTransSC, T(valCol), strServiceCharge)
        If discount <> 0 Then printer.Print(T(capCol), CapTransDS, T(valCol), strDiscount)
        If serviceCharge <> 0 OrElse discount <> 0 Then
            printer.Print(T(1), Line1())
            printer.Print(T(capCol), CapTransGT, T(valCol), strGrandTotal)
        End If

        PrintFooter(printer)
        printer.EndDoc()
    End Sub

End Module
