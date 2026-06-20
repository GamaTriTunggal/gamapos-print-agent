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

    ' Port faithful dari frmReceivable.printReceipt_paymentSelected (bayar beberapa nota terpilih).
    ' Beda dari receivable_proof: ADA nama toko (FS18 centered, TIDAK bold) di atas; "TELAH TERIMA DARI";
    ' KODE/NAMA/ALAMAT/HP; potongan BIAYA TF / MATERAI / DISKON → TOTAL. (Tak me-list nota individual.)
    Public Sub PrintReceivableSelected(store As StoreInfo, p As ReceivableSelectedPayload)
        RunSta(Sub() RenderReceivableSelected(store, p))
    End Sub

    Private Sub RenderReceivableSelected(store As StoreInfo, p As ReceivableSelectedPayload)
        Dim printer As New Printer
        Dim strTotalSelected As String = Fmt(p.totalSelected)
        Dim strFeeTransfer As String = Fmt(p.feeTransfer)
        Dim strFeeMaterai As String = Fmt(p.feeMaterai)
        Dim strDiscount As String = Fmt(p.discount)
        Dim strPayment As String = Fmt(p.totalSelected - p.feeTransfer - p.feeMaterai - p.discount)

        printer.FontName = FontCourier
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18
        PrintCentered(printer, If(store Is Nothing, "", If(store.name, "")), StoreNameCol)   ' nama toko FS18 (tak bold)
        printer.FontSize = 9
        printer.Print(T(1), "         TANDA TERIMA BAYAR BON         ")
        printer.Print(T(16), If(p.printDate, ""))
        printer.Print()
        printer.Print(T(1), "TELAH TERIMA DARI : ")
        printer.Print(T(1), "KODE   : ", T(10), If(p.custId, ""))
        printer.Print(T(1), "NAMA   : ", T(10), If(p.custName, ""))
        printer.Print(T(1), "ALAMAT : ", T(10), If(p.custAddr, ""))
        If Not String.IsNullOrEmpty(p.custHP) Then
            printer.Print(T(1), "HP     : ", T(10), p.custHP)
        End If
        printer.Print(T(1), Line1())
        printer.Print(T(1), "TOTAL BON : ", T(TotCol - strTotalSelected.Length() + 1), strTotalSelected)

        If p.feeTransfer > 0 Then printer.Print(T(1), "BIAYA TF  : (-) ", T(TotCol - strFeeTransfer.Length() + 1), strFeeTransfer)
        If p.feeMaterai > 0 Then printer.Print(T(1), "MATERAI   : (-) ", T(TotCol - strFeeMaterai.Length() + 1), strFeeMaterai)
        If p.discount > 0 Then printer.Print(T(1), "DISKON    : (-) ", T(TotCol - strDiscount.Length() + 1), strDiscount)

        If Not (p.feeTransfer = 0 AndAlso p.feeMaterai = 0 AndAlso p.discount = 0) Then
            printer.Print(T(1), Line1())
            printer.Print(T(1), "TOTAL     : ", T(TotCol - strPayment.Length() + 1), strPayment)
        End If

        printer.Print(T(1), Line1())
        printer.Print(T(1), "HORMAT KAMI")
        printer.Print()
        printer.Print()
        printer.Print()
        printer.Print(T(1), If(store Is Nothing, "", If(store.name, "")))
        printer.Print(T(1), If(p.operatorName, ""))
        printer.EndDoc()
    End Sub

    ' Port faithful dari frmReceivable.printReceipt_paymentSelectedCard. = receivable_selected TAPI
    ' caption beda spasi ("TOTAL BON   : ", "BIAYA TF (-): ", dst.) + bagian BIAYA EDC → GRAND TOTAL (bila SC>0).
    ' Math: total = totalSelected - TF - materai - diskon; grandTotal = total + serviceCharge(EDC).
    Public Sub PrintReceivableSelectedCard(store As StoreInfo, p As ReceivableSelectedCardPayload)
        RunSta(Sub() RenderReceivableSelectedCard(store, p))
    End Sub

    Private Sub RenderReceivableSelectedCard(store As StoreInfo, p As ReceivableSelectedCardPayload)
        Dim printer As New Printer
        Dim strTotalSelected As String = Fmt(p.totalSelected)
        Dim strFeeTransfer As String = Fmt(p.feeTransfer)
        Dim strFeeMaterai As String = Fmt(p.feeMaterai)
        Dim strDiscount As String = Fmt(p.discount)
        Dim strServiceCharge As String = Fmt(p.serviceCharge)
        Dim dblTotal As Double = p.totalSelected - p.feeTransfer - p.feeMaterai - p.discount
        Dim strTotal As String = Fmt(dblTotal)
        Dim strGrandTotal As String = Fmt(dblTotal + p.serviceCharge)

        printer.FontName = FontCourier
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18
        PrintCentered(printer, If(store Is Nothing, "", If(store.name, "")), StoreNameCol)
        printer.FontSize = 9
        printer.Print(T(1), "         TANDA TERIMA BAYAR BON         ")
        printer.Print(T(16), If(p.printDate, ""))
        printer.Print()
        printer.Print(T(1), "TELAH TERIMA DARI : ")
        printer.Print(T(1), "KODE   : ", T(10), If(p.custId, ""))
        printer.Print(T(1), "NAMA   : ", T(10), If(p.custName, ""))
        printer.Print(T(1), "ALAMAT : ", T(10), If(p.custAddr, ""))
        If Not String.IsNullOrEmpty(p.custHP) Then
            printer.Print(T(1), "HP     : ", T(10), p.custHP)
        End If
        printer.Print(T(1), Line1())
        printer.Print(T(1), "TOTAL BON   : ", T(TotCol - strTotalSelected.Length() + 1), strTotalSelected)

        If p.feeTransfer > 0 Then printer.Print(T(1), "BIAYA TF (-): ", T(TotCol - strFeeTransfer.Length() + 1), strFeeTransfer)
        If p.feeMaterai > 0 Then printer.Print(T(1), "MATERAI  (-): ", T(TotCol - strFeeMaterai.Length() + 1), strFeeMaterai)
        If p.discount > 0 Then printer.Print(T(1), "DISKON   (-): ", T(TotCol - strDiscount.Length() + 1), strDiscount)

        If Not (p.feeTransfer = 0 AndAlso p.feeMaterai = 0 AndAlso p.discount = 0) Then
            printer.Print(T(1), Line1())
            printer.Print(T(1), "TOTAL       : ", T(TotCol - strTotal.Length() + 1), strTotal)
        End If

        If p.serviceCharge > 0 Then
            printer.Print(T(1), "BIAYA EDC   : ", T(TotCol - strServiceCharge.Length() + 1), strServiceCharge)
            printer.Print(T(1), Line2())
            printer.Print(T(1), "GRAND TOTAL : ", T(TotCol - strGrandTotal.Length() + 1), strGrandTotal)
        End If

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
