' Gama Print Agent — Handler nota kasir (cashier_receipt)
'
' Port faithful dari frmCashier.printReceipt. Bagian header/customer/item/footer dipakai
' bersama dari ReceiptCommon; di sini hanya bagian khusus: varian "penawaran harga" (isReceipt=False)
' dan total (TOTAL BELANJA / DISKON / BIAYA LAYANAN / GRAND TOTAL).

Option Strict On
Option Explicit On

Imports System
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module CashierReceipt

    Public Sub PrintCashierReceipt(store As StoreInfo, p As CashierReceiptPayload)
        RunSta(Sub() RenderCashier(store, p))
    End Sub

    Private Sub RenderCashier(store As StoreInfo, p As CashierReceiptPayload)
        Dim printer As New Printer
        PrintStoreHeader(printer, store)

        If p.isReceipt Then
            PrintCustomerBlock(printer, p.customer)
            PrintReceiptNoLine(printer, If(p.rcptNo, ""), If(p.date, ""), If(p.time, ""))
        Else
            PrintQuoteNotes(printer)
        End If

        printer.Print(Line2())
        PrintItems(printer, p.items)

        PrintStandardTotals(printer, p.shoppingTotal, p.discount, p.serviceCharge)

        PrintFooter(printer)
        printer.EndDoc()
    End Sub

    ' Varian penawaran harga (isReceipt=False): ganti blok customer/NO dengan catatan "BUKAN NOTA PEMBAYARAN".
    Private Sub PrintQuoteNotes(printer As Printer)
        Dim len_notes As Integer = NotesNotReceipt.Length()
        Dim j As Integer = 0
        Dim rowCount As Integer = CInt(Math.Ceiling(len_notes / TotCol))
        If rowCount > 1 Then
            For row As Integer = 1 To rowCount
                If row < rowCount Then
                    printer.Print(T(1), NotesNotReceipt.Substring(j, TotCol))
                Else
                    printer.Print(T(1), NotesNotReceipt.Substring(j, len_notes - j))
                End If
                j = j + TotCol
            Next
        Else
            printer.Print(T(1), NotesNotReceipt)
        End If
    End Sub

End Module
