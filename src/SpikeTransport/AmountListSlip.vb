' Gama Print Agent — Handler slip daftar-nominal (salary_slip & bpjs_slip)
'
' Port faithful dari frmExpendSalary.printReceipt & frmExpendBPJS.printReceipt — keduanya layout
' IDENTIK; beda HANYA teks header (slip gaji = "" , BPJS = "PEMBAYARAN BPJS"). Satu render, dua jobType.
' header (FS18) → tanggal (FS9) → garis → "nama ... nominal" (hanya nominal>0, rata-kanan) → garis → "Total : <gt>".
' Tanpa header toko / footer. Hanya pakai T()/Fmt()/Line1()/Line2() dari ReceiptCommon.

Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module AmountListSlip

    Public Sub PrintAmountListSlip(p As AmountListSlipPayload)
        RunSta(Sub() RenderAmountListSlip(p))
    End Sub

    Private Sub RenderAmountListSlip(p As AmountListSlipPayload)
        Dim printer As New Printer
        printer.FontName = FontCourier
        printer.FontBold = True
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18
        printer.Print(T(1), If(p.header, ""))   ' slip gaji: kosong; BPJS: "PEMBAYARAN BPJS" — dari payload
        printer.FontSize = 9
        printer.Print(T(1), If(p.printDate, ""))
        printer.Print()
        printer.Print(Line2())

        Dim rows As List(Of AmountRow) = If(p.rows, New List(Of AmountRow)())
        For Each r As AmountRow In rows
            If r.amount > 0 Then
                Dim strAmount As String = Fmt(r.amount)
                printer.Print(T(1), If(r.name, ""), T(40 - strAmount.Length() + 1), strAmount)
            End If
        Next

        printer.Print(Line1())
        Dim strGrandTotal As String = Fmt(p.grandTotal)
        Const totalCap As String = "Total : "
        printer.Print(T(40 - totalCap.Length() - strGrandTotal.Length() + 1), totalCap,
                      T(40 - strGrandTotal.Length() + 1), strGrandTotal)
        printer.EndDoc()
    End Sub

End Module
