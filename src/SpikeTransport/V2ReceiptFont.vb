' Adaptor metrik printer Windows untuk kebijakan ukuran font badan nota v2.
Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6

Module V2ReceiptFont
    Friend Function SelectV2BodyFont(printer As Printer, preferredPoints As Integer) As Font
        Dim printableWidth As Single = CSng(printer.ScaleWidth)
        Dim points As Integer = ChooseV2BodyFontSize(printableWidth, preferredPoints,
            Function(candidate As Integer)
                printer.Font = New Font(FontCourier, candidate, FontStyle.Bold)
                Return CSng(printer.TextWidth(StrDup(TotCol, " ")))
            End Function)
        Dim selected As New Font(FontCourier, points, FontStyle.Bold)
        printer.Font = selected
        Return selected
    End Function
End Module
