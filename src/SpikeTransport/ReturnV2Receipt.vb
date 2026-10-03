' Renderer kandidat retur schema 2. Tetap dormant sampai dispatcher, /health,
' Windows/printer fisik dan gerbang rilis disetujui; retur v1 tidak berubah.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6
Imports Newtonsoft.Json.Linq

Module ReturnV2Receipt
    Friend Sub PrintReturnV2Receipt(body As String)
        PrintReturnV2Receipt(ParseReturnV2(body))
    End Sub

    Friend Sub PrintReturnV2Receipt(root As JObject)
        RunSta(Sub() RenderReturnV2(root))
    End Sub

    Private Sub RenderReturnV2(root As JObject)
        Dim printer As New Printer()
        SetV2ReceiptFont(printer, 10, FontStyle.Bold)
        Dim printable As Single = CSng(printer.TextWidth(StrDup(TotCol, " ")))
        Dim measure As Func(Of String, Single) = Function(value As String) CSng(printer.TextWidth(value))
        ' Semua data dinamis, baris dan nama direncanakan sebelum Print pertama.
        Dim plan As ReturnV2CorePlan = BuildReturnV2CorePlan(root, printable, measure)

        printer.CurrentX = 0
        printer.CurrentY = 0
        printer.Print(plan.Title)
        printer.Print()
        For Each line As String In plan.CustomerLines
            printer.Print(line)
        Next
        If plan.CustomerLines.Count > 0 Then printer.Print()
        printer.Print(plan.ReceiptLine)
        If plan.ReprintLine IsNot Nothing Then printer.Print(plan.ReprintLine)
        printer.Print(Line2())
        For index As Integer = 0 To plan.ItemLines.Count - 1
            For Each line As String In plan.ItemLines(index)
                printer.Print(line)
            Next
            printer.Print(If(index = plan.ItemLines.Count - 1, Line2(), Line1()))
        Next
        printer.Print(plan.TotalLine)
        printer.Print(Line1())
        printer.Print("Nota merah untuk customer.")
        printer.Print(T(ReturnSignColumn), ReturnSignCaption)
        For index As Integer = 1 To 3
            printer.Print(T(1), ".", T(40), ".")
        Next
        PrintReturnColumnNames(printer, plan.OriginalName)
        PrintReturnColumnNames(printer, plan.ReprintName)
        printer.Print(T(1), ReturnBrandingCaption)
        printer.EndDoc()
    End Sub

    Private Sub PrintReturnColumnNames(printer As Printer, lines As List(Of String))
        For Each line As String In lines
            printer.Print(T(1), line)
        Next
    End Sub
End Module
