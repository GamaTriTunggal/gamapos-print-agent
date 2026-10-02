' PowerPacks memiliki Font aktif dan membuangnya saat diganti; jangan cache/reuse objeknya.
Option Strict On
Option Explicit On

Imports System.Drawing
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6

Module V2ReceiptFonts
    Friend Sub SetV2ReceiptFont(printer As Printer, points As Single, style As FontStyle)
        Dim current As Font = printer.Font
        If current.Name = FontCourier AndAlso current.SizeInPoints = points AndAlso current.Style = style Then Return
        printer.Font = New Font(FontCourier, points, style)
    End Sub
End Module
