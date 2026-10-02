' Ukuran font badan nota v2 mengikuti area cetak nyata; nota v1 tidak berubah.
Option Strict On
Option Explicit On

Imports System

Module V2FontSize
    Friend Function ChooseV2BodyFontSize(printableWidth As Single, preferredPoints As Integer,
                                         measureColumns As Func(Of Integer, Single)) As Integer
        If Single.IsNaN(printableWidth) OrElse Single.IsInfinity(printableWidth) OrElse
           printableWidth <= 0.0F OrElse preferredPoints < 7 OrElse measureColumns Is Nothing Then
            Throw New ArgumentException("Metrik area cetak v2 tidak sah.")
        End If
        For points As Integer = preferredPoints To 7 Step -1
            Dim width As Single = measureColumns(points)
            If Single.IsNaN(width) OrElse Single.IsInfinity(width) OrElse width <= 0.0F Then
                Throw New ArgumentException("Metrik font v2 tidak sah.")
            End If
            If width <= printableWidth * 0.98F Then Return points
        Next
        Throw New ArgumentException("Area cetak terlalu sempit untuk nota v2 40 kolom.")
    End Function
End Module
