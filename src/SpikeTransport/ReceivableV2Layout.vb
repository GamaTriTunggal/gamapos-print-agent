' Rencana tata letak bukti piutang schema 2 tanpa menyentuh printer.
' Semua teks dinamis harus masuk rencana sebelum Printer.Print pertama.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports Newtonsoft.Json.Linq

Friend Class ReceivableV2PrintPlan
    Friend ReadOnly StoreName As List(Of PositionedNameLine)
    Friend ReadOnly StoreDetails As List(Of PositionedNameLine)
    Friend ReadOnly Title As List(Of PositionedNameLine)
    Friend ReadOnly Metadata As List(Of String)
    Friend ReadOnly AllocationLines As List(Of String)
    Friend ReadOnly AmountLines As List(Of String)
    Friend ReadOnly OriginalName As List(Of String)
    Friend ReadOnly ReprintName As List(Of String)
    Friend ReadOnly Separator As String
    Friend ReadOnly StrongSeparator As String

    Friend Sub New(storeNameValue As List(Of PositionedNameLine), storeDetailsValue As List(Of PositionedNameLine),
                   titleValue As List(Of PositionedNameLine), metadataValue As List(Of String),
                   allocationValue As List(Of String), amountValue As List(Of String),
                   originalValue As List(Of String), reprintValue As List(Of String),
                   separatorValue As String, strongSeparatorValue As String)
        StoreName = storeNameValue
        StoreDetails = storeDetailsValue
        Title = titleValue
        Metadata = metadataValue
        AllocationLines = allocationValue
        AmountLines = amountValue
        OriginalName = originalValue
        ReprintName = reprintValue
        Separator = separatorValue
        StrongSeparator = strongSeparatorValue
    End Sub
End Class

Module ReceivableV2Layout
    Private Const Columns As Integer = 40
    Friend Const ReceivableSignCaption As String = "HORMAT KAMI"
    Friend Const ReceivableSignColumn As Integer = 1

    Friend Function BuildReceivableV2Plan(root As JObject, printableWidth As Single, nameWidth As Single,
                                         measure As Func(Of String, Single),
                                         measureName As Func(Of String, Single)) As ReceivableV2PrintPlan
        Dim payload As JObject = CType(root("payload"), JObject)
        Dim store As JObject = CType(root("store"), JObject)
        Dim customer As JObject = CType(payload("customer"), JObject)
        Dim reprint As JObject = TryCast(payload("reprint"), JObject)

        Dim storeName As List(Of PositionedNameLine) = LayoutCenteredSaleText(CStr(store("name")), "", nameWidth, measureName)
        Dim details As New List(Of PositionedNameLine)()
        details.AddRange(LayoutCenteredSaleText(CStr(store("address")), "", printableWidth, measure))
        details.AddRange(LayoutCenteredSaleText(CStr(store("contact")), "", printableWidth, measure))
        Dim title As List(Of PositionedNameLine) = LayoutCenteredSaleText("TANDA TERIMA BAYAR BON", "", printableWidth, measure)

        Dim metadata As New List(Of String)()
        metadata.AddRange(LayoutSaleCustomer("JENIS     : ", If(CStr(root("jobType")) = "receivable_proof", "CICILAN BON", "PELUNASAN BON"), printableWidth, measure))
        metadata.AddRange(LayoutSaleCustomer("TRANSAKSI : ", CStr(payload("transactionId")), printableWidth, measure))
        metadata.AddRange(LayoutSaleCustomer("DIBAYAR   : ", CStr(payload("date")) & " " & CStr(payload("time")), printableWidth, measure))
        If reprint IsNot Nothing Then
            metadata.AddRange(LayoutSaleCustomer("CETAK ULANG: ", CStr(reprint("date")) & " " & CStr(reprint("time")), printableWidth, measure))
        End If
        metadata.AddRange(LayoutSaleCustomer("KODE      : ", CStr(customer("id")), printableWidth, measure))
        metadata.AddRange(LayoutSaleCustomer("PELANGGAN : ", CStr(customer("name")), printableWidth, measure))
        metadata.AddRange(LayoutSaleCustomer("ALAMAT    : ", CStr(customer("address")), printableWidth, measure))
        metadata.AddRange(LayoutSaleCustomer("KONTAK    : ", CStr(customer("contact")), printableWidth, measure))
        Dim method As String = CStr(payload("paymentMethod"))
        metadata.AddRange(LayoutSaleCustomer("METODE    : ", If(method = "CASH", "TUNAI", If(method = "WIRE", "TRANSFER", "EDC")), printableWidth, measure))

        Dim allocations As New List(Of String)()
        For Each row As ReceivableAmountRow In BuildReceivableAllocationRows(payload)
            allocations.Add(LayoutReceivableAmountLine(row.Caption, row.Sen, printableWidth, measure))
        Next
        Dim amounts As New List(Of String)()
        For Each row As ReceivableAmountRow In BuildReceivableAmountRows(CStr(root("jobType")), method, CType(payload("amounts"), JObject))
            If row.SeparatorBefore Then amounts.Add(CheckedRule("-"c, printableWidth, measure))
            amounts.Add(LayoutReceivableAmountLine(row.Caption, row.Sen, printableWidth, measure))
        Next
        Dim separator As String = CheckedRule("-"c, printableWidth, measure)
        Dim strong As String = CheckedRule("="c, printableWidth, measure)
        If Width(ReceivableSignCaption, measure) > printableWidth OrElse
           Width("ALOKASI BON", measure) > printableWidth Then
            Throw New ArgumentException("Judul alokasi/tanda tangan melewati area cetak.")
        End If
        Dim original As List(Of String) = LayoutOriginalNameColumns(
            CStr(payload("originalProcessor")("name")), ReceivableSignCaption, ReceivableSignColumn, Columns)
        Dim reprintName As New List(Of String)()
        If reprint IsNot Nothing Then
            reprintName = LayoutReprintNameColumns(CStr(reprint("processor")("name")), Columns)
        End If
        Return New ReceivableV2PrintPlan(storeName, details, title, metadata, allocations, amounts,
                                          original, reprintName, separator, strong)
    End Function

    Friend Function LayoutReceivableAmountLine(caption As String, sen As Long, printableWidth As Single,
                                               measure As Func(Of String, Single)) As String
        Dim amount As String = FormatReceivableSen(sen)
        If String.IsNullOrEmpty(caption) OrElse caption <> NormalizeProcessorName(caption) OrElse
           caption.Length + amount.Length + 1 > Columns Then
            Throw New ArgumentException("Baris jumlah bukti melampaui kolom.")
        End If
        Dim line As String = caption & StrDup(Columns - caption.Length - amount.Length, " ") & amount
        If Width(line, measure) > printableWidth Then Throw New ArgumentException("Baris jumlah bukti melewati area cetak.")
        Return line
    End Function

    Private Function CheckedRule(character As Char, printableWidth As Single,
                                 measure As Func(Of String, Single)) As String
        Dim line As String = StrDup(Columns, character)
        If Width(line, measure) > printableWidth Then Throw New ArgumentException("Garis bukti melewati area cetak.")
        Return line
    End Function

    Private Function Width(value As String, measure As Func(Of String, Single)) As Single
        If measure Is Nothing Then Throw New ArgumentException("Metrik cetak tidak ada.")
        Dim actual As Single = measure(value)
        If Single.IsNaN(actual) OrElse Single.IsInfinity(actual) OrElse actual < 0.0F Then
            Throw New ArgumentException("Metrik cetak tidak sah.")
        End If
        Return actual
    End Function
End Module
