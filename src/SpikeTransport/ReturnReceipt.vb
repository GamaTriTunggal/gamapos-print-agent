' Gama Print Agent — Handler nota retur (return_note) "NOTA KEMBALI BARANG"
'
' Port faithful dari frmRetur.printReceipt. BEDA dari nota kasir:
'   - TANPA header toko. Mulai langsung dgn judul "NOTA KEMBALI BARANG".
'   - Font 10 bold utk seluruh nota (bukan 18/9).
'   - Blok customer TANPA PO (Name/Alamat/HP saja) → reuse PrintCustomerBlock dgn poNo kosong.
'   - Total hanya 1 baris: TOTAL BELANJA (tak ada diskon/biaya layanan/grand total).
'   - Footer khusus: "Nota merah untuk customer." + hanya TANDA TERIMA + titik di kolom 1 & 40.
' Header/customer/item dipakai bersama dari ReceiptCommon (font diwarisi dari setting di sini).

Option Strict On
Option Explicit On

Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module ReturnReceipt

    Public Sub PrintReturnReceipt(p As ReturnReceiptPayload)
        RunSta(Sub() RenderReturn(p))
    End Sub

    Private Sub RenderReturn(p As ReturnReceiptPayload)
        Dim printer As New Printer
        printer.FontName = FontCourier
        printer.FontBold = True
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 10

        printer.Print("NOTA KEMBALI BARANG          ")
        printer.Print()

        PrintCustomerBlock(printer, p.customer)   ' poNo kosong → baris PO tak dicetak (sama spt retur asli)
        PrintReceiptNoLine(printer, If(p.rcptNo, ""), If(p.date, ""), If(p.time, ""))

        printer.Print(Line2())
        PrintItems(printer, p.items)

        Dim strTotal As String = Fmt(p.total)
        PrintTotalLine(printer, strTotal.Length(), CapTransTS, strTotal)

        ' Footer khusus retur (beda dari PrintFooter standar)
        printer.Print(Line1())
        printer.Print("Nota merah untuk customer.")
        printer.Print(T(25), "TANDA TERIMA")
        printer.Print(T(1), ".", T(40), ".")
        printer.Print(T(1), ".", T(40), ".")
        printer.Print(T(1), ".", T(40), ".")
        printer.Print(T(1), FootGama)
        printer.EndDoc()
    End Sub

End Module
