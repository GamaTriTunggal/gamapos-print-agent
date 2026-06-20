' Gama Print Agent — Handler nota kasir (cashier_receipt)
'
' Port FAITHFUL dari frmCashier.printReceipt (vbnet-reference), memakai PowerPacks Printer
' (VB6 compat) + TAB (lewat helper T(), butuh Short) supaya layout IDENTIK. Input dari payload JSON,
' bukan kontrol form / DataGridView.
'
' Cetak ke printer DEFAULT Windows (PowerPacks Printer tidak punya pemilihan device).
' Untuk tes: set default Windows ke "Microsoft Print to PDF" (akan muncul dialog Save As,
' itu wajar untuk driver PDF). Di produksi: default = printer kasir (TM-U220), cetak silent.
'
' Dijalankan di thread STA (PowerPacks Printer / GDI butuh STA — sama seperti spike 2a).

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Threading
Imports Microsoft.VisualBasic                                       ' StrDup, TAB, TabInfo
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6 ' Printer

Module CashierReceipt

    Private Const StoreNameCol As Integer = 20    ' TMU220_MAX_ROW_FS18
    Private Const TotCol As Integer = 40          ' TMU220_MAX_ROW_FS09
    Private Const FontCourier As String = "Courier New"
    Private Const FmtAccounting As String = "###,###"

    Private Const CapCustName As String = "PEMBELI  : "
    Private Const CapCustAddr As String = "ALAMAT   : "
    Private Const CapCustCont As String = "NO HP    : "
    Private Const CapCustPoNo As String = "PO       : "
    Private Const CapRcptNo As String = "NO:"
    Private Const CapTransTS As String = "TOTAL BELANJA: "
    Private Const CapTransSC As String = "BIAYA LAYANAN: "
    Private Const CapTransDS As String = "DISKON   (-) : "
    Private Const CapTransGT As String = "GRAND TOTAL  : "
    Private Const SignMultiply As String = " X "
    Private Const Foot1 As String = "TANDA TERIMA"
    Private Const Foot2 As String = "HORMAT KAMI"
    Private Const FootGama As String = "           powered by GamaPOS           "
    Private Const NotesNotReceipt As String = "Catatan: INI ADALAH PENAWARAN HARGA, BUKAN NOTA PEMBAYARAN! HARGA TIDAK MENGIKAT DAN DAPAT BERUBAH SEWAKTU-WAKTU!"

    ' Entry point: jalankan render di thread STA, marshal exception balik ke pemanggil.
    Public Sub PrintCashierReceipt(store As StoreInfo, p As CashierReceiptPayload)
        Dim err As Exception = Nothing
        Dim worker As New Thread(Sub() err = TryRender(store, p))
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        worker.Join()
        If err IsNot Nothing Then Throw err
    End Sub

    Private Function TryRender(store As StoreInfo, p As CashierReceiptPayload) As Exception
        Try
            RenderReceipt(store, p)
            Return Nothing
        Catch ex As Exception
            Return ex
        End Try
    End Function

    Private Function Fmt(v As Double) As String
        Return v.ToString(FmtAccounting)
    End Function

    Private Function QtyStr(it As ReceiptItem) As String
        If Not String.IsNullOrEmpty(it.qtyDisplay) Then Return it.qtyDisplay
        Return FmtQty(it.qty)
    End Function

    ' Format qty meniru teks grid lama: integer apa adanya; pecahan umum jadi "1/2"/"1/4"/"3/4";
    ' sisanya desimal InvariantCulture (titik). Untuk fidelity penuh, web kirim qtyDisplay.
    Private Function FmtQty(v As Double) As String
        Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture
        If v = Math.Truncate(v) Then Return CLng(v).ToString(inv)
        Dim whole As Long = CLng(Math.Truncate(v))
        Dim frac As Double = Math.Abs(v - Math.Truncate(v))
        Dim fracStr As String = Nothing
        If Math.Abs(frac - 0.5) < 0.0001 Then fracStr = "1/2"
        If Math.Abs(frac - 0.25) < 0.0001 Then fracStr = "1/4"
        If Math.Abs(frac - 0.75) < 0.0001 Then fracStr = "3/4"
        If fracStr IsNot Nothing Then
            If whole = 0 Then Return fracStr
            Return whole.ToString(inv) & " " & fracStr
        End If
        Return v.ToString(inv)
    End Function

    ' TAB() butuh Short; kolom kita Integer → bungkus CShort. (Printer dari PowerPacks;
    ' TAB/StrDup/TabInfo dari Microsoft.VisualBasic.)
    Private Function T(col As Integer) As Microsoft.VisualBasic.TabInfo
        Return TAB(CShort(col))
    End Function

    Private Sub RenderReceipt(store As StoreInfo, p As CashierReceiptPayload)
        Dim printer As New Printer

        Dim storeName As String = If(store?.name, "")
        Dim storeAddress As String = If(store?.address, "")
        Dim storeContact As String = If(store?.contact, "")

        Dim cust As CustomerInfo = p.customer
        Dim custName As String = If(cust?.name, "")
        Dim custAddr As String = If(cust?.address, "")
        Dim custCont As String = If(cust?.contact, "")
        Dim custPoNo As String = If(cust?.poNo, "")
        Dim custAddrLen As Integer = custAddr.Length()

        Dim rcptNo As String = If(p.rcptNo, "")
        Dim rcptDate As String = If(p.date, "")
        Dim rcptTime As String = If(p.time, "")
        Dim isReceipt As Boolean = p.isReceipt

        Dim line1 As String = StrDup(TotCol, "-")
        Dim line2 As String = StrDup(TotCol, "=")

        Dim len_cca As Integer = CapCustAddr.Length()
        Dim len_ctt As Integer = CapTransTS.Length()
        Dim len_ca_max As Integer = TotCol - len_cca
        Dim len_notes As Integer = NotesNotReceipt.Length()

        Const sl_len_max As Integer = 22
        Const sl_i_price_last As Integer = 32
        Const sl_len_price_max As Integer = 7
        Const sl_len_total_max As Integer = 7

        Dim dblDiscount As Double = p.discount
        Dim dblServiceCharge As Double = p.serviceCharge
        Dim dblShoppingTotal As Double = p.shoppingTotal
        Dim dblGrandTotal As Double = dblShoppingTotal - dblDiscount + dblServiceCharge
        Dim strShoppingTotal As String = Fmt(dblShoppingTotal)
        Dim strDiscount As String = Fmt(dblDiscount)
        Dim strServiceCharge As String = Fmt(dblServiceCharge)
        Dim strGrandTotal As String = Fmt(dblGrandTotal)
        Dim shoppingTotalLen As Integer = strShoppingTotal.Length()

        ' variabel kerja (di VB.NET lama ini field kelas frmCashier)
        Dim intCharLen As Integer
        Dim row As Integer
        Dim intRemainder As Integer
        Dim intSpaceAvailable As Integer
        Dim intSpaceLeft As Integer
        Dim i As Integer
        Dim k As Integer
        Dim j As Integer
        Dim rowCount As Integer
        Dim colIndex As Integer

        Dim len_qty_max As Integer = 0

        printer.FontName = FontCourier
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18

        ' ---- Store name (font 18) ----
        intCharLen = storeName.Length()
        row = (intCharLen \ StoreNameCol) + 1
        intRemainder = intCharLen Mod StoreNameCol
        If row <= 1 Then
            If intRemainder = 0 Then
                printer.Print(T(1), "NO NAME")
            Else
                intSpaceAvailable = StoreNameCol - intRemainder
                intSpaceLeft = intSpaceAvailable \ 2
                printer.Print(T(intSpaceLeft + 1), storeName)
            End If
        Else
            For i = 1 To row
                k = 0
                If i = row Then
                    intSpaceAvailable = StoreNameCol - intRemainder
                    intSpaceLeft = intSpaceAvailable \ 2
                    printer.Print(T(intSpaceLeft + 1), storeName.Substring(k, intRemainder))
                Else
                    printer.Print(T(1), storeName.Substring(k, StoreNameCol))
                    k = k + StoreNameCol
                End If
            Next
        End If

        printer.Font = New Font(FontCourier, 9, FontStyle.Bold)

        ' ---- Store address (font 9) ----
        intCharLen = storeAddress.Length()
        row = (intCharLen \ TotCol) + 1
        intRemainder = intCharLen Mod TotCol
        If row <= 1 Then
            If intRemainder = 0 Then
                printer.Print(T(1), "NO NAME")
            Else
                intSpaceAvailable = TotCol - intRemainder
                intSpaceLeft = intSpaceAvailable \ 2
                printer.Print(T(intSpaceLeft + 1), storeAddress)
            End If
        Else
            For i = 1 To row
                k = 0
                If i = row Then
                    intSpaceAvailable = TotCol - intRemainder
                    intSpaceLeft = intSpaceAvailable \ 2
                    printer.Print(T(intSpaceLeft + 1), storeAddress.Substring(k, intRemainder))
                Else
                    printer.Print(T(1), storeAddress.Substring(k, TotCol))
                    k = k + TotCol
                End If
            Next
        End If

        ' ---- Store contact (font 9) ----
        intCharLen = storeContact.Length()
        row = (intCharLen \ TotCol) + 1
        intRemainder = intCharLen Mod TotCol
        If row <= 1 Then
            If intRemainder = 0 Then
                printer.Print(T(1), "NO NAME")
            Else
                intSpaceAvailable = TotCol - intRemainder
                intSpaceLeft = intSpaceAvailable \ 2
                printer.Print(T(intSpaceLeft + 1), storeContact)
            End If
        Else
            For i = 1 To row
                k = 0
                If i = row Then
                    intSpaceAvailable = TotCol - intRemainder
                    intSpaceLeft = intSpaceAvailable \ 2
                    printer.Print(T(intSpaceLeft + 1), storeContact.Substring(k, intRemainder))
                Else
                    printer.Print(T(1), storeContact.Substring(k, TotCol))
                    k = k + TotCol
                End If
            Next
        End If

        printer.Print()

        ' ---- Customer block / notes ----
        If isReceipt = True Then
            If custName <> Nothing Then
                printer.Print(CapCustName + custName)
            End If
            If custAddr <> Nothing Then
                If custAddrLen > len_ca_max Then
                    rowCount = CInt(Math.Ceiling(custAddrLen / len_ca_max))
                    printer.Print(CapCustAddr + custAddr.Substring(0, len_ca_max))
                    colIndex = 0
                    For i = 2 To rowCount
                        colIndex = colIndex + len_ca_max
                        If (custAddrLen - colIndex) > len_ca_max Then
                            printer.Print(T(len_cca + 1), custAddr.Substring(colIndex, len_ca_max))
                        Else
                            printer.Print(T(len_cca + 1), custAddr.Substring(colIndex, (custAddrLen - colIndex)))
                        End If
                    Next
                Else
                    printer.Print(CapCustAddr + custAddr)
                End If
            End If
            If custCont <> Nothing Then
                printer.Print(CapCustCont + custCont)
            End If
            If custPoNo <> Nothing Then
                printer.Print(CapCustPoNo + custPoNo)
            End If
            If (custName <> Nothing Or custAddr <> Nothing Or custCont <> Nothing Or custPoNo <> Nothing) Then
                printer.Print()
            End If
            printer.Print(T(1), CapRcptNo, T(4), rcptNo, T(19), rcptDate, T(33), rcptTime)
        Else
            j = 0
            rowCount = CInt(Math.Ceiling(len_notes / TotCol))
            If rowCount > 1 Then
                For row = 1 To rowCount
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
        End If

        printer.Print(line2)

        ' ---- Items ----
        Dim items As List(Of ReceiptItem) = If(p.items, New List(Of ReceiptItem)())
        Dim totalRow As Integer = items.Count

        ' pass 1: lebar kolom QTY maksimum (hanya len_qty_max yang dipakai render — sama spt asli)
        For row = 0 To totalRow - 1
            Dim wQty As String = QtyStr(items(row))
            If wQty.Length() > len_qty_max Then len_qty_max = wQty.Length()
        Next

        Dim itemNameLenMax As Integer = TotCol - len_qty_max - 1

        ' pass 2: render tiap item
        For row = 0 To totalRow - 1
            Dim it As ReceiptItem = items(row)
            Dim strQty As String = QtyStr(it)
            Dim strItemName As String = If(it.name, "").Trim()
            Dim strPrice As String = Fmt(it.price)
            If strPrice = Nothing Then strPrice = "0"
            Dim strTotal As String = Fmt(it.total)

            Dim itemNameLen As Integer = strItemName.Length()
            Dim itemPriceLen As Integer = strPrice.Length()
            Dim itemTotalLen As Integer = strTotal.Length()
            rowCount = CInt(Math.Ceiling(itemNameLen / itemNameLenMax))
            colIndex = 0

            If strQty = "1" Then

                If (len_qty_max + 1 + itemNameLen + 1 + itemTotalLen) <= TotCol Then
                    ' v1s — 1 baris tanpa price
                    printer.Print(T(1), strQty,
                                  T(len_qty_max + 2), strItemName,
                                  T(TotCol - itemTotalLen + 1), strTotal)
                Else
                    If (len_qty_max + 1 + itemNameLen) <= TotCol Then
                        ' v2 — 2 baris
                        printer.Print(T(1), strQty,
                                  T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                  T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                  T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        ' vm — multi baris
                        colIndex = 0
                        For i = 1 To rowCount
                            If i = 1 Then
                                printer.Print(T(1), strQty,
                                              T(len_qty_max + 2), strItemName.Substring(colIndex, itemNameLenMax))
                            ElseIf (i > 1) And (i < rowCount) Then
                                printer.Print(T(len_qty_max + 2), strItemName.Substring(colIndex, itemNameLenMax))
                            Else
                                Dim remainingChar As Integer = strItemName.Length() - ((rowCount - 1) * itemNameLenMax)
                                printer.Print(T(len_qty_max + 2), strItemName.Substring(colIndex, (itemNameLenMax - colIndex + remainingChar)))
                            End If
                            colIndex = colIndex + itemNameLenMax
                        Next
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    End If
                End If

            Else

                If (len_qty_max + 1 + itemNameLen + 1) <= sl_len_max Then
                    If (itemPriceLen <= sl_len_price_max) And (itemTotalLen <= sl_len_total_max) Then
                        ' v1 — 1 baris dengan price
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName,
                                      T(23), SignMultiply,
                                      T(sl_i_price_last - itemPriceLen + 1), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        ' v2 — 2 baris
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    End If
                Else
                    If (len_qty_max + 1 + itemNameLen) <= TotCol Then
                        ' v2 — 2 baris
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        ' vm — multi baris
                        colIndex = 0
                        For i = 1 To rowCount
                            If i = 1 Then
                                printer.Print(T(1), strQty,
                                              T(len_qty_max + 2), strItemName.Substring(colIndex, itemNameLenMax))
                            ElseIf (i > 1) AndAlso (i < rowCount) Then
                                printer.Print(T(len_qty_max + 2), strItemName.Substring(colIndex, itemNameLenMax))
                            Else
                                Dim remainingChar As Integer = strItemName.Length() - ((rowCount - 1) * itemNameLenMax)
                                printer.Print(T(len_qty_max + 2), strItemName.Substring(colIndex, (itemNameLenMax - colIndex + remainingChar)))
                            End If
                            colIndex = colIndex + itemNameLenMax
                        Next
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    End If
                End If

            End If

            If row < (totalRow - 1) Then
                printer.Print(T(1), line1)
            Else
                printer.Print(T(1), line2)
            End If
        Next

        ' ---- Totals ----
        printer.Print(T(TotCol - shoppingTotalLen - len_ctt + 1), CapTransTS,
                      T(TotCol - shoppingTotalLen + 1), strShoppingTotal)

        If dblDiscount <> 0 Then
            printer.Print(T(TotCol - shoppingTotalLen - len_ctt + 1), CapTransDS,
                          T(TotCol - strDiscount.Length() + 1), strDiscount)
        End If

        If dblServiceCharge <> 0 Then
            printer.Print(T(TotCol - shoppingTotalLen - len_ctt + 1), CapTransSC,
                          T(TotCol - strServiceCharge.Length() + 1), strServiceCharge)
        End If

        If (dblServiceCharge <> 0 Or dblDiscount <> 0) Then
            printer.Print(T(1), line1)
            printer.Print(T(TotCol - shoppingTotalLen - len_ctt + 1), CapTransGT,
                          T(TotCol - strGrandTotal.Length() + 1), strGrandTotal)
        End If

        printer.Print(T(1), line1)
        printer.Print(T(6), Foot1, T(25), Foot2)
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), FootGama)
        printer.EndDoc()
    End Sub

End Module
