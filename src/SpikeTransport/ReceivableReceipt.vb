' Gama Print Agent — Handler tanda terima bayar bon / piutang (receivable_proof)
'
' Port faithful dari frmReceivable.printReceipt ("TANDA TERIMA BAYAR BON").
' Layout SENDIRI (beda total dari nota penjualan): tanpa header toko di atas, tanpa daftar item.
' Judul → tanggal → KODE/NAMA/HP → TOTAL BON → [BIAYA LAYANAN/GRAND TOTAL bila ada] →
' BAYAR (-) → SISA BON → HORMAT KAMI → (3 baris kosong) → nama toko + operator di BAWAH.
' Font 9 bold. Hanya pakai T()/Fmt()/Line1()/Line2() dari ReceiptCommon.
'
' Math (sesuai asli): grandTotal = totalReceivable + serviceCharge; bayar = nominal + serviceCharge;
' sisa = grandTotal - bayar (= totalReceivable - nominal).

Option Strict On
Option Explicit On

Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module ReceivableReceipt

    Public Sub PrintReceivableProof(store As StoreInfo, p As ReceivableProofPayload)
        RunSta(Sub() RenderReceivableProof(store, p))
    End Sub

    Private Sub RenderReceivableProof(store As StoreInfo, p As ReceivableProofPayload)
        Dim printer As New Printer
        Dim strServiceCharge As String = Fmt(p.serviceCharge)
        Dim strReceivable As String = Fmt(p.totalReceivable)
        Dim dblGrandTotal As Double = p.totalReceivable + p.serviceCharge
        Dim strGrandTotal As String = Fmt(dblGrandTotal)
        Dim dblPayment As Double = p.nominal + p.serviceCharge
        Dim strPayment As String = Fmt(dblPayment)
        Dim strRemains As String = Fmt(dblGrandTotal - dblPayment)

        printer.FontName = FontCourier
        printer.FontBold = True
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 9

        printer.Print(T(1), "         TANDA TERIMA BAYAR BON         ")
        printer.Print()
        printer.Print(T(1), If(p.printDate, ""))
        printer.Print(T(1), Line2())
        printer.Print(T(1), "KODE   : ", T(10), If(p.custId, ""))
        printer.Print(T(1), "NAMA   : ", T(10), If(p.custName, ""))
        If Not String.IsNullOrEmpty(p.custHP) Then
            printer.Print(T(1), "HP     : ", T(10), p.custHP)
        End If
        printer.Print(T(1), Line2())
        printer.Print(T(1), "TOTAL BON     : ", T(TotCol - strReceivable.Length() + 1), strReceivable)

        If p.serviceCharge <> 0 Then
            printer.Print(T(1), "BIAYA LAYANAN : ", T(TotCol - strServiceCharge.Length() + 1), strServiceCharge)
            printer.Print(T(1), Line1())
            printer.Print(T(1), "GRAND TOTAL   : ", T(TotCol - strGrandTotal.Length() + 1), strGrandTotal)
        End If

        printer.Print(T(1), "BAYAR         : (-) ", T(TotCol - strPayment.Length() + 1), strPayment)
        printer.Print(T(1), Line1())
        printer.Print(T(1), "SISA BON      : ", T(TotCol - strRemains.Length() + 1), strRemains)
        printer.Print(T(1), Line1())
        printer.Print(T(1), "HORMAT KAMI")
        printer.Print()
        printer.Print()
        printer.Print()
        printer.Print(T(1), If(store Is Nothing, "", If(store.name, "")))
        printer.Print(T(1), If(p.operatorName, ""))
        printer.EndDoc()
    End Sub

End Module
