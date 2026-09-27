' Rencana header, pelanggan, nomor nota, dan jumlah schema 2 sebelum Printer.Print.
' Helper v1 tetap utuh; pengukuran nyata diberikan pemanggil, tes memakai metrik murni.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization

Module SaleV2MetadataLayout
    Friend Function LayoutCenteredSaleText(raw As String, fallback As String, printableWidth As Single,
                                           measure As Func(Of String, Single)) As List(Of PositionedNameLine)
        Dim value As String = NormalizeProcessorName(raw)
        If value = "" Then value = fallback
        Dim result As New List(Of PositionedNameLine)()
        If value = "" Then Return result
        For Each line As String In WrapSaleText(value, "", "", printableWidth, measure)
            result.Add(New PositionedNameLine(line, (printableWidth - CheckedWidth(line, measure)) / 2.0F))
        Next
        Return result
    End Function

    Friend Function LayoutSaleCustomer(caption As String, raw As String, printableWidth As Single,
                                       measure As Func(Of String, Single)) As List(Of String)
        Dim value As String = NormalizeProcessorName(raw)
        If value = "" Then Return New List(Of String)()
        Return WrapSaleText(value, caption, StrDup(caption.Length, " "), printableWidth, measure)
    End Function

    Friend Function LayoutSaleReceiptLine(number As String, printedDate As String, printedTime As String,
                                          isReprint As Boolean, printableWidth As Single,
                                          measure As Func(Of String, Single)) As String
        Dim cells(39) As Char
        For index As Integer = 0 To cells.Length - 1
            cells(index) = " "c
        Next
        Place(cells, 1, If(isReprint, "CETAK ULANG:", "NO:"))
        If Not isReprint Then Place(cells, 4, number)
        Place(cells, 19, printedDate)
        If String.IsNullOrEmpty(printedTime) OrElse printedTime.Length > 40 Then
            Throw New ArgumentException("Jam nota tidak sah.")
        End If
        Place(cells, 41 - printedTime.Length, printedTime)
        Dim line As String = New String(cells).TrimEnd()
        If Not Fits(line, printableWidth, measure) Then Throw New ArgumentException("Identitas nota melampaui area cetak.")
        Return line
    End Function

    Friend Function LayoutSaleAmountLine(caption As String, sen As Long, columns As Integer,
                                         printableWidth As Single, measure As Func(Of String, Single)) As String
        Dim amount As String = FormatSaleSen(sen)
        If String.IsNullOrEmpty(caption) OrElse columns < 1 OrElse
           caption.Length + amount.Length + 1 > columns Then
            Throw New ArgumentException("Baris jumlah melampaui kolom nota.")
        End If
        Dim line As String = caption & StrDup(columns - caption.Length - amount.Length, " ") & amount
        If Not Fits(line, printableWidth, measure) Then Throw New ArgumentException("Baris jumlah melampaui area cetak.")
        Return line
    End Function

    Private Function WrapSaleText(value As String, firstPrefix As String, nextPrefix As String,
                                  printableWidth As Single, measure As Func(Of String, Single)) As List(Of String)
        If String.IsNullOrEmpty(value) OrElse Not Fits(firstPrefix, printableWidth, measure) OrElse
           Not Fits(nextPrefix, printableWidth, measure) Then
            Throw New ArgumentException("Teks/header nota tidak sah untuk area cetak.")
        End If
        Dim result As New List(Of String)()
        Dim current As String = ""
        For Each word As String In value.Split(" "c)
            Dim prefix As String = If(result.Count = 0, firstPrefix, nextPrefix)
            Dim joined As String = If(current = "", word, current & " " & word)
            If Fits(prefix & joined, printableWidth, measure) Then
                current = joined
                Continue For
            End If
            If current <> "" Then
                result.Add(prefix & current)
                current = ""
                prefix = nextPrefix
            End If
            If Fits(prefix & word, printableWidth, measure) Then
                current = word
                Continue For
            End If
            Dim elements As TextElementEnumerator = StringInfo.GetTextElementEnumerator(word)
            While elements.MoveNext()
                Dim element As String = elements.GetTextElement()
                prefix = If(result.Count = 0, firstPrefix, nextPrefix)
                If Not Fits(prefix & current & element, printableWidth, measure) Then
                    If current = "" Then Throw New ArgumentException("Karakter header/pelanggan melampaui area cetak.")
                    result.Add(prefix & current)
                    current = ""
                    prefix = nextPrefix
                    If Not Fits(prefix & element, printableWidth, measure) Then
                        Throw New ArgumentException("Karakter header/pelanggan melampaui area cetak.")
                    End If
                End If
                current &= element
            End While
        Next
        If current <> "" Then result.Add(If(result.Count = 0, firstPrefix, nextPrefix) & current)
        Return result
    End Function

    Private Sub Place(cells As Char(), startColumn As Integer, value As String)
        If String.IsNullOrEmpty(value) OrElse value <> NormalizeProcessorName(value) OrElse
           startColumn < 1 OrElse startColumn + value.Length - 1 > cells.Length Then
            Throw New ArgumentException("Kolom identitas nota tidak sah.")
        End If
        For index As Integer = 0 To value.Length - 1
            Dim target As Integer = startColumn - 1 + index
            If cells(target) <> " "c AndAlso value(index) <> " "c Then
                Throw New ArgumentException("Kolom identitas nota bertumpuk.")
            End If
            cells(target) = value(index)
        Next
    End Sub

    Private Function Fits(value As String, width As Single, measure As Func(Of String, Single)) As Boolean
        If measure Is Nothing OrElse Single.IsNaN(width) OrElse Single.IsInfinity(width) OrElse width <= 0.0F Then
            Throw New ArgumentException("Metrik area cetak tidak sah.")
        End If
        Return CheckedWidth(value, measure) <= width
    End Function

    Private Function CheckedWidth(value As String, measure As Func(Of String, Single)) As Single
        Dim actual As Single = measure(value)
        If Single.IsNaN(actual) OrElse Single.IsInfinity(actual) OrElse actual < 0.0F Then
            Throw New ArgumentException("Metrik lebar cetak tidak sah.")
        End If
        Return actual
    End Function
End Module
