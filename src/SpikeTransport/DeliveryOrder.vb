' Gama Print Agent — Surat Jalan (delivery_order)
'
' Port dari modGlobalProcedure.print_DeliveryOrder. BEDA dari semua nota lain:
'   - LEBAR 82 kolom (bukan 40), printer DELIVERY (LX-310 wide-carriage).
'   - Font Courier New 12 REGULAR (bukan bold).
'   - PAGINASI: 17 item/halaman; header (toko+customer+kolom) diulang tiap halaman.
'   - Kolom QTY / UNIT / NAMA BARANG (tanpa harga/total).
' Generalisasi: HEAD1/2/3 hardcode single-store ("VIN JAYA"...) → store.name/address/contact.
' FIX bug asli (disetujui prinsip user): jumlah baris halaman terakhir dihitung benar
'   (asli pakai `itemTotal Mod rowMax` → kelipatan-17 cetak halaman kosong).
'
' Catatan: asli memanggil EndDoc TIAP halaman (tiap halaman = job terpisah) — direproduksi apa adanya.
' Lebar 82 kolom @font 12 butuh kertas lebar (LX-310); saat tes ke PDF A4 sisi kanan bisa terpotong.

Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports Microsoft.VisualBasic                                       ' StrDup
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6 ' Printer

Module DeliveryOrder

    Private Const TotLen As Integer = 82
    Private Const RowMax As Integer = 17

    Public Sub PrintDeliveryOrder(store As StoreInfo, p As DeliveryOrderPayload)
        RunSta(Sub() RenderDeliveryOrder(store, p))
    End Sub

    Private Sub RenderDeliveryOrder(store As StoreInfo, p As DeliveryOrderPayload)
        Dim printer As New Printer
        Dim line1 As String = StrDup(TotLen, "-")
        Dim line2 As String = StrDup(TotLen, "=")

        Dim head1 As String = If(store Is Nothing, "", If(store.name, ""))
        Dim head2 As String = If(store Is Nothing, "", If(store.address, ""))
        Dim head3 As String = If(store Is Nothing, "", If(store.contact, ""))
        Dim custName As String = If(p.customer Is Nothing, "", If(p.customer.name, ""))
        Dim custAdd As String = If(p.customer Is Nothing, "", If(p.customer.address, ""))
        Dim custHP As String = If(p.customer Is Nothing, "", If(p.customer.contact, ""))
        Dim strDate As String = If(p.printDate, "")
        Dim ref As String = If(p.ref, "")

        Dim items As List(Of ReceiptItem) = If(p.items, New List(Of ReceiptItem)())
        Dim itemTotal As Integer = items.Count
        Dim pageTotal As Integer = CInt(Math.Ceiling(itemTotal / RowMax))

        For i As Integer = 0 To pageTotal - 1
            printer.Font = New Font("Courier New", 12, FontStyle.Regular)
            printer.Print(T(1), "SURAT JALAN", T(TotLen - strDate.Length() + 1), strDate)
            printer.Print(T(1), head1, T(TotLen - 30), "Untuk   : " & custName)
            printer.Print(T(1), head2, T(TotLen - 30), "Alamat  : " & custAdd)
            printer.Print(T(1), head3, T(TotLen - 30), "No. HP  : " & custHP)
            printer.Print()
            printer.Print()
            printer.Print(T(2), "QTY", T(10), "UNIT", T(20), "NAMA BARANG")
            printer.Print(T(1), line2)

            ' Item halaman ini. countThis dihitung dari sisa item (FIX: bukan Mod → kelipatan-17 aman).
            Dim startIdx As Integer = i * RowMax
            Dim countThis As Integer = Math.Min(RowMax, itemTotal - startIdx)
            For j As Integer = startIdx To startIdx + countThis - 1
                Dim it As ReceiptItem = items(j)
                printer.Print(T(2), QtyStr(it), T(10), If(it.unit, ""), T(20), If(it.name, ""))
            Next
            ' Pad baris kosong agar tinggi halaman konsisten (rowMax baris) — hanya kena di halaman terakhir.
            For j As Integer = countThis To RowMax - 1
                printer.Print()
            Next

            printer.Print(T(1), line1)
            printer.Print(T(10), "Diterima oleh,", T(TotLen - 30), "Hormat Kami,")
            printer.Print(T(1))
            printer.Print(T(1))
            printer.Print(T(1))
            printer.Print(T(10), "Nama dan tanggal: ")

            Dim halaman As String = "Halaman : " & CStr(i + 1) & " / " & CStr(pageTotal)
            If ref.Trim() = "" Then
                printer.Print(T(TotLen - 17 + 1), halaman)
            Else
                printer.Print(T(1), "Referensi: " & ref, T(TotLen - 17 + 1), halaman)
            End If

            printer.EndDoc()
        Next
    End Sub

End Module
