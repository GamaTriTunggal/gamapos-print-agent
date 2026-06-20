' Gama Print Agent — Handler slip gaji (salary_slip)
'
' Port faithful dari frmExpendSalary.printReceipt. Layout minimal & berdiri sendiri:
' header (FS18; di kode asli KOSONG) → tanggal (FS9) → garis → daftar "nama ... gaji"
' (hanya karyawan dgn gaji > 0, gaji rata-kanan) → garis → "Total : <grand total>".
' TANPA header toko, TANPA footer. Hanya pakai T()/Fmt()/Line1()/Line2() dari ReceiptCommon.

Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6  ' Printer

Module SalarySlip

    Public Sub PrintSalarySlip(p As SalarySlipPayload)
        RunSta(Sub() RenderSalarySlip(p))
    End Sub

    Private Sub RenderSalarySlip(p As SalarySlipPayload)
        Dim printer As New Printer
        printer.FontName = FontCourier
        printer.FontBold = True
        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.FontSize = 18
        printer.Print(T(1), If(p.header, ""))   ' kode asli: header kosong (FS18). Web boleh isi.
        printer.FontSize = 9
        printer.Print(T(1), If(p.printDate, ""))
        printer.Print()
        printer.Print(Line2())

        Dim emps As List(Of SalaryEmployee) = If(p.employees, New List(Of SalaryEmployee)())
        For Each e As SalaryEmployee In emps
            If e.salary > 0 Then
                Dim strSalary As String = Fmt(e.salary)
                printer.Print(T(1), If(e.name, ""), T(40 - strSalary.Length() + 1), strSalary)
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
