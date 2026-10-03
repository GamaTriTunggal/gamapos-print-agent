' Baris uang bukti piutang schema 2 murni. Hanya panggil sesudah ParseReceivableV2.
' Biaya merchant tidak ditampilkan sebagai tagihan pelanggan.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports Newtonsoft.Json.Linq

Friend Class ReceivableAmountRow
    Friend ReadOnly Property Caption As String
    Friend ReadOnly Property Sen As Long
    Friend ReadOnly Property SeparatorBefore As Boolean

    Friend Sub New(captionValue As String, senValue As Long, Optional separator As Boolean = False)
        Caption = captionValue
        Sen = senValue
        SeparatorBefore = separator
    End Sub
End Class

Module ReceivableV2Rows
    Private ReadOnly RupiahCulture As CultureInfo = CultureInfo.GetCultureInfo("id-ID")
    Private Const MaxCent As Long = 99999999999999L

    Friend Function BuildReceivableAllocationRows(payload As JObject) As List(Of ReceivableAmountRow)
        Dim rows As New List(Of ReceivableAmountRow)()
        For Each token As JToken In CType(payload("allocations"), JArray)
            Dim allocation As JObject = CType(token, JObject)
            rows.Add(New ReceivableAmountRow("BON " & CStr(allocation("receiptNo")),
                                             Sen(allocation, "principalAppliedSen")))
        Next
        Return rows
    End Function

    Friend Function BuildReceivableAmountRows(jobType As String, method As String,
                                               amounts As JObject) As List(Of ReceivableAmountRow)
        Dim selected As Boolean = jobType <> "receivable_proof"
        Dim rows As New List(Of ReceivableAmountRow) From {
            New ReceivableAmountRow("TOTAL BON", Sen(amounts, "totalBeforeSen"))}
        If selected Then
            AddPositive(rows, "DISKON (-)", amounts, "discountSen")
            AddPositive(rows, "PEMBULATAN (-)", amounts, "roundingSen")
            AddPositive(rows, "BIAYA TF (-)", amounts, "transferFeeSen")
            AddPositive(rows, "MATERAI (-)", amounts, "stampFeeSen")
            rows.Add(New ReceivableAmountRow("TOTAL BAYAR", Sen(amounts, "netPaymentSen"), True))
        Else
            rows.Add(New ReceivableAmountRow("BAYAR", Sen(amounts, "netPaymentSen")))
            rows.Add(New ReceivableAmountRow("SISA BON", Sen(amounts, "remainingSen"), True))
        End If
        If method = "EDC" Then
            AddPositive(rows, "BIAYA EDC", amounts, "customerFeeSen")
            If Sen(amounts, "customerFeeSen") > 0 Then
                rows.Add(New ReceivableAmountRow("TOTAL DITAGIH", Sen(amounts, "customerPaysSen"), True))
            End If
        End If
        ' P-604: keputusan pemilik 3 Okt 2026: kedua angka tetap diaudit
        ' dalam payload, tetapi tidak ditambah sebagai baris cetak baru.
        Return rows
    End Function

    Friend Function FormatReceivableSen(value As Long) As String
        If value < -MaxCent OrElse value > MaxCent Then Throw New ArgumentOutOfRangeException(NameOf(value))
        Dim magnitude As Long = Math.Abs(value)
        Dim formatted As String = (CDec(magnitude) / 100D).ToString(
            If(magnitude Mod 100L = 0L, "#,##0", "#,##0.00"), RupiahCulture)
        Return If(value < 0L, "-" & formatted, formatted)
    End Function

    Private Sub AddPositive(rows As List(Of ReceivableAmountRow), caption As String,
                            amounts As JObject, name As String)
        Dim value As Long = Sen(amounts, name)
        If value > 0L Then rows.Add(New ReceivableAmountRow(caption, value))
    End Sub

    Private Function Sen(obj As JObject, name As String) As Long
        Return Long.Parse(CStr(obj(name)), CultureInfo.InvariantCulture)
    End Function
End Module
