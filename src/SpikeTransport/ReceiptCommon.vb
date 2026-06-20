' Gama Print Agent — Komponen nota bersama (CASHIER, lebar 40 kolom)
'
' Bagian header toko / blok customer / daftar item / footer IDENTIK di semua nota kasir
' (printReceipt, printReceipt_Kasbon, printReceipt_SplitPayment, retur, dst. di VB.NET lama
' adalah salinan karakter-per-karakter). Diekstrak sekali di sini supaya layout dijamin sama
' & tidak perlu disalin ulang tiap jenis nota. Tiap handler nota memanggil helper ini +
' menambah bagian total/khusus-nya sendiri.
'
' Port faithful dari frmCashier.printReceipt — termasuk perilaku asli (bug-for-bug) pada
' wrapping store header (lihat PrintCentered).

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Threading
Imports Microsoft.VisualBasic                                       ' StrDup, TAB, TabInfo
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6 ' Printer

Module ReceiptCommon

    Friend Const StoreNameCol As Integer = 20   ' TMU220_MAX_ROW_FS18
    Friend Const TotCol As Integer = 40         ' TMU220_MAX_ROW_FS09
    Friend Const FontCourier As String = "Courier New"
    Friend Const FmtAccounting As String = "###,###"

    Friend Const CapCustName As String = "PEMBELI  : "
    Friend Const CapCustAddr As String = "ALAMAT   : "
    Friend Const CapCustCont As String = "NO HP    : "
    Friend Const CapCustPoNo As String = "PO       : "
    Friend Const CapRcptNo As String = "NO:"
    Friend Const CapTransTS As String = "TOTAL BELANJA: "
    Friend Const CapTransSC As String = "BIAYA LAYANAN: "
    Friend Const CapTransDS As String = "DISKON   (-) : "
    Friend Const CapTransGT As String = "GRAND TOTAL  : "
    Friend Const CapTransPY As String = "BAYAR    (-) : "
    Friend Const CapTransRM As String = "SISA         : "
    Friend Const SignMultiply As String = " X "
    Friend Const Foot1 As String = "TANDA TERIMA"
    Friend Const Foot2 As String = "HORMAT KAMI"
    Friend Const FootGama As String = "           powered by GamaPOS           "
    Friend Const NotesNotReceipt As String = "Catatan: INI ADALAH PENAWARAN HARGA, BUKAN NOTA PEMBAYARAN! HARGA TIDAK MENGIKAT DAN DAPAT BERUBAH SEWAKTU-WAKTU!"

    Friend Function Line1() As String
        Return StrDup(TotCol, "-")
    End Function

    Friend Function Line2() As String
        Return StrDup(TotCol, "=")
    End Function

    ' TAB() butuh Short; kolom kita Integer → bungkus CShort. (Printer dari PowerPacks;
    ' TAB/StrDup/TabInfo dari Microsoft.VisualBasic.)
    Friend Function T(col As Integer) As TabInfo
        Return TAB(CShort(col))
    End Function

    Friend Function Fmt(v As Double) As String
        Return v.ToString(FmtAccounting)
    End Function

    Friend Function QtyStr(it As ReceiptItem) As String
        If Not String.IsNullOrEmpty(it.qtyDisplay) Then Return it.qtyDisplay
        Return FmtQty(it.qty)
    End Function

    ' Format qty meniru teks grid lama: integer apa adanya; pecahan umum jadi "1/2"/"1/4"/"3/4";
    ' sisanya desimal InvariantCulture (titik). Untuk fidelity penuh, web kirim qtyDisplay.
    Friend Function FmtQty(v As Double) As String
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

    ' Jalankan render di thread STA (PowerPacks Printer / GDI butuh STA; thread loop HttpListener MTA).
    Friend Sub RunSta(body As Action)
        Dim err As Exception = Nothing
        Dim worker As New Thread(Sub()
                                     Try
                                         body()
                                     Catch ex As Exception
                                         err = ex
                                     End Try
                                 End Sub)
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        worker.Join()
        If err IsNot Nothing Then Throw err
    End Sub

    ' --- Header toko: nama (font 18) + alamat + kontak (font 9 bold), semua tengah + wrap, lalu baris kosong.
    Friend Sub PrintStoreHeader(printer As Printer, store As StoreInfo)
        printer.FontName = FontCourier
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18
        PrintCentered(printer, If(store Is Nothing, "", store.name), StoreNameCol)
        printer.Font = New Font(FontCourier, 9, FontStyle.Bold)
        PrintCentered(printer, If(store Is Nothing, "", store.address), TotCol)
        PrintCentered(printer, If(store Is Nothing, "", store.contact), TotCol)
        printer.Print()
    End Sub

    ' Cetak teks di tengah lebar `col`, wrap bila panjang.
    ' DEVIASI SENGAJA dari VB.NET lama (disetujui user 2026-06-20): kode asli me-reset k=0 tiap
    ' baris → baris ke-2 header toko yang >col char mengulang dari awal. Di sini k diakumulasi
    ' supaya wrap header toko panjang benar (lanjut ke karakter berikutnya). Header ≤col char tak terpengaruh.
    Private Sub PrintCentered(printer As Printer, text As String, col As Integer)
        Dim s As String = If(text, "")
        Dim intCharLen As Integer = s.Length()
        Dim row As Integer = (intCharLen \ col) + 1
        Dim intRemainder As Integer = intCharLen Mod col

        If row <= 1 Then
            If intRemainder = 0 Then
                printer.Print(T(1), "NO NAME")
            Else
                Dim left As Integer = (col - intRemainder) \ 2
                printer.Print(T(left + 1), s)
            End If
        Else
            Dim k As Integer = 0
            For i As Integer = 1 To row
                If i = row Then
                    Dim left As Integer = (col - intRemainder) \ 2
                    printer.Print(T(left + 1), s.Substring(k, intRemainder))
                Else
                    printer.Print(T(1), s.Substring(k, col))
                    k = k + col
                End If
            Next
        End If
    End Sub

    ' Blok customer (PEMBELI/ALAMAT wrap/NO HP/PO) + baris kosong bila ada isinya.
    Friend Sub PrintCustomerBlock(printer As Printer, c As CustomerInfo)
        Dim custName As String = If(c Is Nothing, "", If(c.name, ""))
        Dim custAddr As String = If(c Is Nothing, "", If(c.address, ""))
        Dim custCont As String = If(c Is Nothing, "", If(c.contact, ""))
        Dim custPoNo As String = If(c Is Nothing, "", If(c.poNo, ""))
        Dim custAddrLen As Integer = custAddr.Length()
        Dim len_cca As Integer = CapCustAddr.Length()
        Dim len_ca_max As Integer = TotCol - len_cca

        If custName <> Nothing Then printer.Print(CapCustName + custName)

        If custAddr <> Nothing Then
            If custAddrLen > len_ca_max Then
                Dim rowCount As Integer = CInt(Math.Ceiling(custAddrLen / len_ca_max))
                printer.Print(CapCustAddr + custAddr.Substring(0, len_ca_max))
                Dim colIndex As Integer = 0
                For i As Integer = 2 To rowCount
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

        If custCont <> Nothing Then printer.Print(CapCustCont + custCont)
        If custPoNo <> Nothing Then printer.Print(CapCustPoNo + custPoNo)

        If (custName <> Nothing Or custAddr <> Nothing Or custCont <> Nothing Or custPoNo <> Nothing) Then
            printer.Print()
        End If
    End Sub

    ' Baris NO (tgl di kolom 19, JAM rata-kanan ke kolom 40). Bila reprintDate/reprintTime diisi →
    ' tambah baris "CETAK ULANG: <date> <time>" (kolom sama). Reprint = reuse nota asli + penanda ini
    ' (Opsi B). DEVIASI SENGAJA (disetujui user 2026-06-20): jam rata-kanan kolom 40 (aplikasi lama: kolom 33).
    Friend Sub PrintReceiptNoLine(printer As Printer, rcptNo As String, rcptDate As String, rcptTime As String,
                                  Optional reprintDate As String = Nothing, Optional reprintTime As String = Nothing)
        printer.Print(T(1), CapRcptNo, T(4), rcptNo, T(19), rcptDate, T(40 - rcptTime.Length() + 1), rcptTime)
        If Not String.IsNullOrEmpty(reprintDate) OrElse Not String.IsNullOrEmpty(reprintTime) Then
            Dim rt As String = If(reprintTime, "")
            printer.Print(T(1), "CETAK ULANG:", T(19), If(reprintDate, ""), T(40 - rt.Length() + 1), rt)
        End If
    End Sub

    ' Daftar item: 4 varian format (v1s/v1/v2/vm) + separator antar baris. Port faithful.
    Friend Sub PrintItems(printer As Printer, itemsIn As List(Of ReceiptItem))
        Dim items As List(Of ReceiptItem) = If(itemsIn, New List(Of ReceiptItem)())
        Dim totalRow As Integer = items.Count

        Const sl_len_max As Integer = 22
        Const sl_i_price_last As Integer = 32
        Const sl_len_price_max As Integer = 7
        Const sl_len_total_max As Integer = 7

        Dim len_qty_max As Integer = 0
        For r As Integer = 0 To totalRow - 1
            Dim wQty As String = QtyStr(items(r))
            If wQty.Length() > len_qty_max Then len_qty_max = wQty.Length()
        Next

        Dim itemNameLenMax As Integer = TotCol - len_qty_max - 1

        For row As Integer = 0 To totalRow - 1
            Dim it As ReceiptItem = items(row)
            Dim strQty As String = QtyStr(it)
            Dim strItemName As String = If(it.name, "").Trim()
            Dim strPrice As String = Fmt(it.price)
            If strPrice = Nothing Then strPrice = "0"
            Dim strTotal As String = Fmt(it.total)

            Dim itemNameLen As Integer = strItemName.Length()
            Dim itemPriceLen As Integer = strPrice.Length()
            Dim itemTotalLen As Integer = strTotal.Length()
            Dim rowCount As Integer = CInt(Math.Ceiling(itemNameLen / itemNameLenMax))
            Dim colIndex As Integer = 0

            If strQty = "1" Then

                If (len_qty_max + 1 + itemNameLen + 1 + itemTotalLen) <= TotCol Then
                    printer.Print(T(1), strQty,
                                  T(len_qty_max + 2), strItemName,
                                  T(TotCol - itemTotalLen + 1), strTotal)
                Else
                    If (len_qty_max + 1 + itemNameLen) <= TotCol Then
                        printer.Print(T(1), strQty,
                                  T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                  T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                  T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        colIndex = 0
                        For i As Integer = 1 To rowCount
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
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName,
                                      T(23), SignMultiply,
                                      T(sl_i_price_last - itemPriceLen + 1), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    End If
                Else
                    If (len_qty_max + 1 + itemNameLen) <= TotCol Then
                        printer.Print(T(1), strQty,
                                      T(len_qty_max + 2), strItemName)
                        printer.Print(T(len_qty_max + 2), SignMultiply,
                                      T(len_qty_max + 2 + SignMultiply.Length()), strPrice,
                                      T(TotCol - itemTotalLen + 1), strTotal)
                    Else
                        colIndex = 0
                        For i As Integer = 1 To rowCount
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
                printer.Print(T(1), Line1())
            Else
                printer.Print(T(1), Line2())
            End If
        Next
    End Sub

    ' Satu baris total: caption rata-kiri (anchor di lebar caption TS), nilai rata-kanan ke kolom 40.
    Friend Sub PrintTotalLine(printer As Printer, stLen As Integer, caption As String, value As String)
        printer.Print(T(TotCol - stLen - CapTransTS.Length() + 1), caption,
                      T(TotCol - value.Length() + 1), value)
    End Sub

    ' Total standar — dipakai nota kasir & split: TOTAL BELANJA, DISKON(≠0), BIAYA LAYANAN(≠0),
    ' lalu line1 + GRAND TOTAL bila ada diskon/biaya layanan. Nilai rata-kanan ke kolom 40.
    Friend Sub PrintStandardTotals(printer As Printer, shoppingTotal As Double, discount As Double, serviceCharge As Double)
        Dim strShoppingTotal As String = Fmt(shoppingTotal)
        Dim stLen As Integer = strShoppingTotal.Length()
        PrintTotalLine(printer, stLen, CapTransTS, strShoppingTotal)
        If discount <> 0 Then PrintTotalLine(printer, stLen, CapTransDS, Fmt(discount))
        If serviceCharge <> 0 Then PrintTotalLine(printer, stLen, CapTransSC, Fmt(serviceCharge))
        If serviceCharge <> 0 OrElse discount <> 0 Then
            printer.Print(T(1), Line1())
            PrintTotalLine(printer, stLen, CapTransGT, Fmt(shoppingTotal - discount + serviceCharge))
        End If
    End Sub

    Friend Sub PrintFooter(printer As Printer)
        printer.Print(T(1), Line1())
        printer.Print(T(6), Foot1, T(25), Foot2)
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), ".")
        printer.Print(T(1), FootGama)
    End Sub

End Module
