' Formatter kandidat bukti piutang schema 2. Belum terhubung ke dispatcher;
' /health tetap schema 1 sampai Windows/printer fisik dan gerbang lain lulus.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6
Imports Newtonsoft.Json.Linq

Module ReceivableV2Receipt
    Friend Sub PrintReceivableV2Receipt(body As String)
        Dim root As JObject = ParseReceivableV2(body)
        RunSta(Sub() RenderReceivableV2(root))
    End Sub

    Private Sub RenderReceivableV2(root As JObject)
        Dim printer As New Printer()
        Dim large As New Font(FontCourier, 18, FontStyle.Regular)
        Dim normal As New Font(FontCourier, 9, FontStyle.Bold)
        ' Metrik font dan area printer nyata; seluruh preflight sebelum Print pertama.
        printer.Font = large
        Dim nameWidth As Single = Math.Min(CSng(printer.ScaleWidth), CSng(printer.TextWidth(StrDup(StoreNameCol, " "))))
        printer.Font = normal
        Dim printable As Single = Math.Min(CSng(printer.ScaleWidth), CSng(printer.TextWidth(StrDup(TotCol, " "))))
        Dim measureName As Func(Of String, Single) = Function(value As String)
                                                         printer.Font = large
                                                         Return CSng(printer.TextWidth(value))
                                                     End Function
        Dim measure As Func(Of String, Single) = Function(value As String)
                                                     printer.Font = normal
                                                     Return CSng(printer.TextWidth(value))
                                                 End Function
        Dim plan As ReceivableV2PrintPlan = BuildReceivableV2Plan(root, printable, nameWidth, measure, measureName)

        printer.Font = large
        printer.CurrentX = 0
        printer.CurrentY = 0
        PrintProofPositioned(printer, plan.StoreName)
        printer.Font = normal
        PrintProofPositioned(printer, plan.StoreDetails)
        printer.Print()
        PrintProofPositioned(printer, plan.Title)
        For Each line As String In plan.Metadata
            printer.Print(line)
        Next
        printer.Print(plan.StrongSeparator)
        printer.Print("ALOKASI BON")
        For Each line As String In plan.AllocationLines
            printer.Print(line)
        Next
        printer.Print(plan.Separator)
        For Each line As String In plan.AmountLines
            printer.Print(line)
        Next
        printer.Print(plan.Separator)
        printer.Print("HORMAT KAMI")
        printer.Print()
        printer.Print()
        printer.Print()
        PrintProofPositioned(printer, plan.OriginalName)
        PrintProofPositioned(printer, plan.ReprintName)
        printer.EndDoc()
    End Sub

    Private Sub PrintProofPositioned(printer As Printer, lines As List(Of PositionedNameLine))
        For Each line As PositionedNameLine In lines
            printer.CurrentX = line.X
            printer.Print(line.Text)
        Next
    End Sub
End Module
