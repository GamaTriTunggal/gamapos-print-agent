' Tata letak nama pemroses nota v2, tanpa ketergantungan printer.
' Helper posisi menerima metrik; helper footer kolom tidak memakai TextWidth.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Friend Class PositionedNameLine
    Friend ReadOnly Property Text As String
    Friend ReadOnly Property X As Single

    Friend Sub New(value As String, left As Single)
        Text = value
        X = left
    End Sub
End Class

Module ReceiptNameLayout
    ' P-604: footer memakai kolom seperti TAB v1, bukan CurrentX dari TextWidth.
    Friend Function LayoutOriginalNameColumns(raw As String, caption As String,
                                             captionColumn As Integer, columns As Integer) As List(Of String)
        CheckNameColumns(columns)
        If String.IsNullOrEmpty(caption) OrElse caption <> NormalizeProcessorName(caption) OrElse
           captionColumn < 1 OrElse captionColumn - 1 + caption.Length > columns Then
            Throw New ArgumentException("Caption nama melampaui kolom.")
        End If
        Dim center As Single = CSng(captionColumn - 1) + CSng(caption.Length) / 2.0F
        Return ToNameColumnLines(LayoutOriginalName(raw, center, CSng(columns),
            Function(value As String) CSng(value.Length)), columns)
    End Function

    Friend Function LayoutReprintNameColumns(raw As String, columns As Integer) As List(Of String)
        CheckNameColumns(columns)
        Return ToNameColumnLines(LayoutReprintName(raw, CSng(columns),
            Function(value As String) CSng(value.Length)), columns)
    End Function

    Private Sub CheckNameColumns(columns As Integer)
        If columns < 1 OrElse columns > 40 Then Throw New ArgumentException("Area nama tidak sah.")
    End Sub

    Private Function ToNameColumnLines(lines As List(Of PositionedNameLine), columns As Integer) As List(Of String)
        Dim result As New List(Of String)()
        For Each line As PositionedNameLine In lines
            Dim spaces As Integer = CInt(Math.Floor(line.X))
            If spaces < 0 OrElse spaces + line.Text.Length > columns Then
                Throw New ArgumentException("Nama melampaui kolom.")
            End If
            result.Add(New String(" "c, spaces) & line.Text)
        Next
        Return result
    End Function

    Friend Function NormalizeProcessorName(raw As String) As String
        Dim result As New StringBuilder()
        Dim pendingSpace As Boolean = False
        For Each ch As Char In If(raw, "")
            If Char.IsWhiteSpace(ch) OrElse Char.IsControl(ch) Then
                pendingSpace = result.Length > 0
            Else
                If pendingSpace Then result.Append(" "c)
                result.Append(ch)
                pendingSpace = False
            End If
        Next
        Return result.ToString()
    End Function

    Friend Function LayoutOriginalName(raw As String, footCenter As Single, printableWidth As Single,
                                       measure As Func(Of String, Single)) As List(Of PositionedNameLine)
        Dim normalized As String = NormalizeProcessorName(raw)
        Dim lines As New List(Of PositionedNameLine)()
        If normalized = "" Then Return lines
        CheckMetrics(footCenter, printableWidth, measure)
        Dim width As Single = measure(normalized)
        If Single.IsNaN(width) OrElse Single.IsInfinity(width) OrElse width < 0.0F Then
            Throw New ArgumentException("Metrik lebar nama tidak sah.")
        End If
        Dim centerLeft As Single = footCenter - width / 2.0F
        If centerLeft >= 0.0F AndAlso centerLeft + width <= printableWidth Then
            lines.Add(New PositionedNameLine(normalized, centerLeft))
            Return lines
        End If
        If width <= printableWidth Then
            lines.Add(New PositionedNameLine(normalized, printableWidth - width))
            Return lines
        End If
        Return LayoutWrappedRight(normalized, printableWidth, measure)
    End Function

    Friend Function LayoutReprintName(raw As String, printableWidth As Single,
                                      measure As Func(Of String, Single)) As List(Of PositionedNameLine)
        Dim normalized As String = NormalizeProcessorName(raw)
        If normalized = "" Then Return New List(Of PositionedNameLine)()
        CheckMetrics(0.0F, printableWidth, measure)
        Return LayoutWrappedRight("Dicetak ulang oleh: " & normalized, printableWidth, measure)
    End Function

    Private Sub CheckMetrics(center As Single, width As Single, measure As Func(Of String, Single))
        If measure Is Nothing OrElse Single.IsNaN(center) OrElse Single.IsNaN(width) OrElse
           Single.IsInfinity(center) OrElse Single.IsInfinity(width) OrElse width <= 0.0F OrElse
           center < 0.0F Then
            Throw New ArgumentException("Metrik area cetak tidak sah.")
        End If
    End Sub

    Private Function LayoutWrappedRight(value As String, printableWidth As Single,
                                        measure As Func(Of String, Single)) As List(Of PositionedNameLine)
        Dim texts As New List(Of String)()
        Dim current As String = ""
        For Each word As String In value.Split(" "c)
            Dim joined As String = If(current = "", word, current & " " & word)
            If Fits(joined, printableWidth, measure) Then
                current = joined
                Continue For
            End If
            If current <> "" Then
                texts.Add(current)
                current = ""
            End If
            If Fits(word, printableWidth, measure) Then
                current = word
                Continue For
            End If
            Dim elements As TextElementEnumerator = StringInfo.GetTextElementEnumerator(word)
            While elements.MoveNext()
                Dim element As String = elements.GetTextElement()
                If Not Fits(element, printableWidth, measure) Then
                    Throw New ArgumentException("Satu karakter nama melampaui area cetak.")
                End If
                If current <> "" AndAlso Not Fits(current & element, printableWidth, measure) Then
                    texts.Add(current)
                    current = ""
                End If
                current &= element
            End While
        Next
        If current <> "" Then texts.Add(current)
        Dim result As New List(Of PositionedNameLine)(texts.Count)
        For Each line As String In texts
            result.Add(New PositionedNameLine(line, printableWidth - measure(line)))
        Next
        Return result
    End Function

    Private Function Fits(value As String, width As Single, measure As Func(Of String, Single)) As Boolean
        Dim measured As Single = measure(value)
        If Single.IsNaN(measured) OrElse Single.IsInfinity(measured) OrElse measured < 0.0F Then
            Throw New ArgumentException("Metrik lebar teks tidak sah.")
        End If
        Return measured <= width
    End Function
End Module
