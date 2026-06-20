' Gama Print Agent — Handler nota split payment (split_receipt)
'
' Port dari frmCashier.printReceipt_SplitPayment. Header/customer/item/footer dari ReceiptCommon.
' Selalu nota (blok customer selalu dicetak — sub asli tak punya param isReceipt).
'
' Total = total standar (sama persis dgn nota kasir): rata-kanan + urutan DISKON→BIAYA LAYANAN.
' DEVIASI SENGAJA dari aplikasi lama (disetujui user 2026-06-20): aplikasi lama me-rata-kiri nilai
' di kolom shopping-total & menaruh BIAYA LAYANAN sebelum DISKON. Diseragamkan dgn nota kasir.

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
        PrintReceiptNoLine(printer, If(p.rcptNo, ""), If(p.date, ""), If(p.time, ""), p.reprintDate, p.reprintTime)

        printer.Print(Line2())
        PrintItems(printer, p.items)

        ' Total nota split = total standar (sama dgn nota kasir): rata-kanan + urutan DISKON→BIAYA LAYANAN.
        ' (Disetujui user 2026-06-20 — deviasi sengaja dari aplikasi lama yg rata-kiri & BIAYA LAYANAN dulu.)
        PrintStandardTotals(printer, p.shoppingTotal, p.discount, p.serviceCharge)

        PrintFooter(printer)
        printer.EndDoc()
    End Sub

End Module
