' Formatter kandidat bukti piutang schema 2 pada rute dormant;
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
        PrintReceivableV2Receipt(root)
    End Sub

    Friend Sub PrintReceivableV2Receipt(root As JObject)
        RunSta(Sub() RenderReceivableV2(root))
    End Sub

    Private Sub RenderReceivableV2(root As JObject)
        Dim printer As New Printer()
        ' Metrik font dan batas logis 40 kolom; seluruh preflight sebelum Print pertama.
        SetV2ReceiptFont(printer, 18, FontStyle.Regular)
        Dim nameWidth As Single = CSng(printer.TextWidth(StrDup(StoreNameCol, " ")))
        SetV2ReceiptFont(printer, 9, FontStyle.Bold)
        Dim printable As Single = CSng(printer.TextWidth(StrDup(TotCol, " ")))
        Dim measureName As Func(Of String, Single) = Function(value As String)
                                                         SetV2ReceiptFont(printer, 18, FontStyle.Regular)
                                                         Return CSng(printer.TextWidth(value))
                                                     End Function
        Dim measure As Func(Of String, Single) = Function(value As String)
                                                     SetV2ReceiptFont(printer, 9, FontStyle.Bold)
                                                     Return CSng(printer.TextWidth(value))
                                                 End Function
        Dim plan As ReceivableV2PrintPlan = BuildReceivableV2Plan(root, printable, nameWidth, measure, measureName)

        SetV2ReceiptFont(printer, 18, FontStyle.Regular)
        printer.CurrentX = 0
        printer.CurrentY = 0
        PrintProofPositioned(printer, plan.StoreName)
        SetV2ReceiptFont(printer, 9, FontStyle.Bold)
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
