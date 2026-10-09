' Baris jumlah nota v2 murni; renderer dan tes memakai daftar yang sama.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports Newtonsoft.Json.Linq

Friend Class SaleAmountRow
    Friend ReadOnly Property Caption As String
    Friend ReadOnly Property Sen As Long
    Friend ReadOnly Property SeparatorBefore As Boolean

    Friend Sub New(captionValue As String, senValue As Long, Optional separator As Boolean = False)
        Caption = captionValue
        Sen = senValue
        SeparatorBefore = separator
    End Sub
End Class

Module SaleV2Rows
    Friend Function BuildSaleAmountRows(jobType As String, method As String,
                                        noncashMethod As String, amounts As JObject) As List(Of SaleAmountRow)
        Dim rows As New List(Of SaleAmountRow) From {
            New SaleAmountRow("TOTAL BELANJA", Sen(amounts, "grossSen"))}
        If Sen(amounts, "discountSen") > 0 Then rows.Add(New SaleAmountRow("DISKON (-)", Sen(amounts, "discountSen")))
        If Sen(amounts, "roundingSen") > 0 Then rows.Add(New SaleAmountRow("PEMBULATAN (-)", Sen(amounts, "roundingSen")))
        ' P-604 (putusan pemilik 9 Okt 2026, nota ringkas): TOTAL NOTA hanya bila
        ' berbeda dari TOTAL BELANJA, yaitu ada diskon atau pembulatan.
        If Sen(amounts, "netSen") <> Sen(amounts, "grossSen") Then
            rows.Add(New SaleAmountRow("TOTAL NOTA", Sen(amounts, "netSen"), True))
        End If
        If Sen(amounts, "customerFeeSen") > 0 Then
            rows.Add(New SaleAmountRow("BIAYA EDC", Sen(amounts, "customerFeeSen")))
            rows.Add(New SaleAmountRow("TOTAL DIBAYAR", Sen(amounts, "customerPaysSen"), True))
        End If
        If jobType = "kasbon_receipt" Then
            rows.Add(New SaleAmountRow("BAYAR", Sen(amounts, "principalAppliedSen")))
            rows.Add(New SaleAmountRow("SISA UTANG", Sen(amounts, "remainingSen")))
        ElseIf method = "SPLIT" Then
            rows.Add(New SaleAmountRow("TUNAI", Sen(amounts, "cashSen")))
            rows.Add(New SaleAmountRow(If(noncashMethod = "EDC", "EDC", "TRANSFER"), Sen(amounts, "noncashSen")))
        End If
        ' P-604: pemilik 3 Okt 2026 mempertahankan format v1 tanpa baris
        ' uang diterima/kembalian. Field auditnya tetap ada dan divalidasi parser.
        Return rows
    End Function

    Private Function Sen(amounts As JObject, field As String) As Long
        Return Long.Parse(CStr(amounts(field)), CultureInfo.InvariantCulture)
    End Function
End Module
