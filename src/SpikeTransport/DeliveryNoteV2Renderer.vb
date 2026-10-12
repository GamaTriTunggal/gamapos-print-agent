' Renderer surat jalan schema 2 (`delivery_note`, P-645 / CO-11) untuk printer
' peran DELIVERY (Epson LX-310). Mekanisme mengikuti `delivery_order` v1
' (DeliveryOrder.vb, tidak diubah): Courier New 12 Regular, cetak per baris
' berbasis kolom dari kolom 1, `EndDoc` tiap halaman. Seluruh halaman dan
' salinan direncanakan serta diperiksa (PlanDeliveryNoteV2) SEBELUM printer
' dipilih dan sebelum Printer.Print pertama; gagal = galat tanpa kertas.
' BELUM diuji fisik: LX-310 dan kertas 9,5 × 5,5 in belum tersedia.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6
Imports Newtonsoft.Json.Linq

Module DeliveryNoteV2Renderer
    Friend Function PlanDeliveryNoteV2(root As JObject) As List(Of List(Of String))
        Dim sequence As List(Of List(Of String)) = BuildDeliveryNotePrintSequence(root)
        CheckDeliveryNotePages(sequence)
        Return sequence
    End Function

    Friend Sub PrintDeliveryNoteV2(sequence As List(Of List(Of String)))
        CheckDeliveryNotePages(sequence)
        RunSta(Sub() RenderDeliveryNoteV2(sequence))
    End Sub

    Private Sub RenderDeliveryNoteV2(sequence As List(Of List(Of String)))
        Dim printer As New Printer()
        For Each page As List(Of String) In sequence
            ' Seperti v1: objek Font BARU tiap halaman (sesudah EndDoc). PowerPacks
            ' memiliki dan membuang Font lama saat diganti; tidak ada objek Font
            ' yang disimpan atau dipakai ulang di sini.
            printer.Font = New Font(FontCourier, 12, FontStyle.Regular)
            For Each line As String In page
                printer.Print(T(1), line)
            Next
            printer.EndDoc()
        Next
    End Sub
End Module
