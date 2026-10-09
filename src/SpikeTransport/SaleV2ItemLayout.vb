' Tata letak item nota v2 murni agar setiap baris diuji sebelum Printer.Print.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization

Module SaleV2ItemLayout
    Friend Function BuildSaleItemLines(quantity100 As Long, rawUnit As String, rawName As String,
                                       priceSen As Long, totalSen As Long, printableWidth As Single,
                                       columns As Integer, measure As Func(Of String, Single)) As List(Of String)
        If measure Is Nothing OrElse Single.IsNaN(printableWidth) OrElse Single.IsInfinity(printableWidth) OrElse
           printableWidth <= 0.0F OrElse columns < 1 Then
            Throw New ArgumentException("Metrik area item tidak sah.")
        End If
        Dim unit As String = NormalizeProcessorName(rawUnit)
        Dim prefix As String = FormatSaleQuantity(quantity100) & If(unit = "", " ", " " & unit & " ")
        Dim continuation As String = StrDup(prefix.Length, " ")
        Dim name As String = NormalizeProcessorName(rawName)
        If name = "" Then Throw New ArgumentException("Nama item kosong setelah normalisasi.")
        ' P-604 (putusan pemilik 9 Okt 2026): nota ringkas — satu baris diutamakan
        ' bila muat, sama dengan semangat formatter v1; dua baris hanya cadangan.
        Dim singleLine As String = BuildSingleItemLine(prefix & name, quantity100, priceSen, totalSen, columns)
        If singleLine IsNot Nothing AndAlso Fits(singleLine, printableWidth, measure) Then
            Return New List(Of String) From {singleLine}
        End If
        Dim lines As New List(Of String)()
        Dim current As String = ""
        Dim initial As Boolean = True
        Dim elements As TextElementEnumerator = StringInfo.GetTextElementEnumerator(name)
        While elements.MoveNext()
            Dim element As String = elements.GetTextElement()
            Dim start As String = If(initial, prefix, continuation)
            If Not Fits(start & current & element, printableWidth, measure) Then
                If current = "" Then Throw New ArgumentException("Nama item melampaui area cetak.")
                lines.Add(start & current)
                initial = False
                current = ""
                If Not Fits(continuation & element, printableWidth, measure) Then
                    Throw New ArgumentException("Karakter item melampaui area cetak.")
                End If
            End If
            current &= element
        End While
        Dim lastNameLine As String = If(initial, prefix, continuation) & current
        If Not Fits(lastNameLine, printableWidth, measure) Then Throw New ArgumentException("Nama item melampaui area cetak.")
        lines.Add(lastNameLine)

        Dim left As String = "  X " & FormatSaleSen(priceSen)
        Dim right As String = FormatSaleSen(totalSen)
        If left.Length + right.Length + 1 <= columns Then
            lines.Add(left & StrDup(columns - left.Length - right.Length, " ") & right)
        Else
            lines.Add(left)
            lines.Add(StrDup(Math.Max(0, columns - right.Length), " ") & right)
        End If
        For Each line As String In lines
            If Not Fits(line, printableWidth, measure) Then Throw New ArgumentException("Baris item melampaui area cetak.")
        Next
        Return lines
    End Function

    ' Urutan sama dengan v1 (ReceiptCommon.PrintItems): jumlah 1 dengan harga =
    ' total cukup "1 NAMA ... TOTAL"; selain itu kolom tetap v1 (" X " di kolom 23,
    ' harga rata kanan di 32, total di 40) bila nama pendek dan angka <= 7 karakter;
    ' bila tidak, satu baris rapat "NAMA X HARGA ... TOTAL" selama muat. Nothing =
    ' tidak muat satu baris.
    Friend Function BuildSingleItemLine(head As String, quantity100 As Long, priceSen As Long,
                                        totalSen As Long, columns As Integer) As String
        Dim price As String = FormatSaleSen(priceSen)
        Dim total As String = FormatSaleSen(totalSen)
        If quantity100 = 100L AndAlso priceSen = totalSen Then
            If head.Length + 1 + total.Length > columns Then Return Nothing
            Return head & StrDup(columns - head.Length - total.Length, " ") & total
        End If
        If columns = V1Columns AndAlso head.Length + 1 <= V1NameColumns AndAlso
           price.Length <= V1AmountWidth AndAlso total.Length <= V1AmountWidth Then
            Return head.PadRight(V1NameColumns + 1) & "X " & price.PadLeft(V1AmountWidth) & " " &
                   total.PadLeft(V1AmountWidth)
        End If
        Dim left As String = head & " X " & price
        If left.Length + 1 + total.Length > columns Then Return Nothing
        Return left & StrDup(columns - left.Length - total.Length, " ") & total
    End Function

    Private Const V1Columns As Integer = 40
    Private Const V1NameColumns As Integer = 22
    Private Const V1AmountWidth As Integer = 7

    Private Function Fits(value As String, width As Single, measure As Func(Of String, Single)) As Boolean
        Dim actual As Single = measure(value)
        If Single.IsNaN(actual) OrElse Single.IsInfinity(actual) OrElse actual < 0.0F Then
            Throw New ArgumentException("Metrik lebar item tidak sah.")
        End If
        Return actual <= width
    End Function
End Module
