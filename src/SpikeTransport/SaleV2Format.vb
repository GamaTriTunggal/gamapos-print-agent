' Format tampilan dari integer sen/quantity×100; tidak memakai Double.
Option Strict On
Option Explicit On

Imports System
Imports System.Globalization

Module SaleV2Format
    Private ReadOnly RupiahCulture As CultureInfo = CultureInfo.GetCultureInfo("id-ID")

    Friend Function FormatSaleSen(value As Long) As String
        If value < 0L OrElse value > 99999999999999L Then Throw New ArgumentOutOfRangeException(NameOf(value))
        Dim amount As Decimal = CDec(value) / 100D
        Return amount.ToString(If(value Mod 100L = 0L, "#,##0", "#,##0.00"), RupiahCulture)
    End Function

    Friend Function FormatSaleQuantity(value As Long) As String
        If value <= 0L OrElse value > 999999999999L Then Throw New ArgumentOutOfRangeException(NameOf(value))
        Dim amount As Decimal = CDec(value) / 100D
        Return amount.ToString(If(value Mod 100L = 0L, "#,##0", "#,##0.##"), RupiahCulture)
    End Function
End Module
