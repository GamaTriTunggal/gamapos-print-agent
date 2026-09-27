' Rencana bagian inti nota retur v2 tanpa printer. Footer menunggu keputusan
' posisi nama pemroses; jalur v1 dan dispatcher tidak memakai modul ini.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports Newtonsoft.Json.Linq

Friend Class ReturnV2CorePlan
    Friend ReadOnly Title As String
    Friend ReadOnly CustomerLines As List(Of String)
    Friend ReadOnly ReceiptLine As String
    Friend ReadOnly ReprintLine As String
    Friend ReadOnly ItemLines As List(Of List(Of String))
    Friend ReadOnly TotalLine As String

    Friend Sub New(title As String, customerLines As List(Of String), receiptLine As String,
                   reprintLine As String, itemLines As List(Of List(Of String)), totalLine As String)
        Me.Title = title
        Me.CustomerLines = customerLines
        Me.ReceiptLine = receiptLine
        Me.ReprintLine = reprintLine
        Me.ItemLines = itemLines
        Me.TotalLine = totalLine
    End Sub
End Class

Module ReturnV2CoreLayout
    Private Const ReturnColumns As Integer = 40
    Private Const CustomerNameCaption As String = "PEMBELI  : "
    Private Const CustomerAddressCaption As String = "ALAMAT   : "
    Private Const CustomerContactCaption As String = "NO HP    : "
    Private Const ReturnTotalCaption As String = "TOTAL BELANJA: "

    Friend Function BuildReturnV2CorePlan(root As JObject, printableWidth As Single,
                                          measure As Func(Of String, Single)) As ReturnV2CorePlan
        If root Is Nothing OrElse measure Is Nothing OrElse Single.IsNaN(printableWidth) OrElse
           Single.IsInfinity(printableWidth) OrElse printableWidth <= 0.0F Then
            Throw New ArgumentException("Metrik nota retur tidak sah.")
        End If
        Dim payload As JObject = CType(root("payload"), JObject)
        Dim customer As JObject = CType(payload("customer"), JObject)
        Const title As String = "NOTA KEMBALI BARANG"
        CheckWidth(title, printableWidth, measure)
        CheckWidth(New String("-"c, ReturnColumns), printableWidth, measure)
        CheckWidth(New String("="c, ReturnColumns), printableWidth, measure)

        Dim customerLines As New List(Of String)()
        customerLines.AddRange(LayoutSaleCustomer(CustomerNameCaption, CStr(customer("name")), printableWidth, measure))
        customerLines.AddRange(LayoutSaleCustomer(CustomerAddressCaption, CStr(customer("address")), printableWidth, measure))
        customerLines.AddRange(LayoutSaleCustomer(CustomerContactCaption, CStr(customer("contact")), printableWidth, measure))
        Dim receiptLine As String = LayoutSaleReceiptLine(CStr(payload("receiptNo")),
            CStr(payload("date")), CStr(payload("time")), False, printableWidth, measure)
        Dim reprintLine As String = Nothing
        Dim reprint As JObject = TryCast(payload("reprint"), JObject)
        If reprint IsNot Nothing Then
            reprintLine = LayoutSaleReceiptLine("", CStr(reprint("date")), CStr(reprint("time")),
                                                True, printableWidth, measure)
        End If

        Dim itemLines As New List(Of List(Of String))()
        For Each token As JToken In CType(payload("items"), JArray)
            Dim item As JObject = CType(token, JObject)
            itemLines.Add(BuildSaleItemLines(Long.Parse(CStr(item("quantity100")), CultureInfo.InvariantCulture),
                "", CStr(item("name")),
                Long.Parse(CStr(item("priceSen")), CultureInfo.InvariantCulture),
                Long.Parse(CStr(item("totalSen")), CultureInfo.InvariantCulture),
                printableWidth, ReturnColumns, measure))
        Next
        Dim totalLine As String = LayoutSaleAmountLine(ReturnTotalCaption,
            Long.Parse(CStr(payload("totalSen")), CultureInfo.InvariantCulture),
            ReturnColumns, printableWidth, measure)
        Return New ReturnV2CorePlan(title, customerLines, receiptLine, reprintLine, itemLines, totalLine)
    End Function

    Private Sub CheckWidth(value As String, printableWidth As Single, measure As Func(Of String, Single))
        Dim width As Single = measure(value)
        If Single.IsNaN(width) OrElse Single.IsInfinity(width) OrElse width < 0.0F OrElse
           width > printableWidth Then Throw New ArgumentException("Baris retur melampaui area cetak.")
    End Sub
End Module
