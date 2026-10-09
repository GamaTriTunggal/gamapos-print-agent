' Kemampuan cetak yang benar-benar boleh diumumkan oleh build ini.
' 1.3.0 (D-024 adendum 9 Okt 2026, putusan pemilik C): v2 dibuka untuk semua
' keluarga yang parser/renderer-nya teruji; v1 tetap diterima apa adanya.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Module PrintCapabilities
    ' Versi amplop tertinggi yang dirutekan Dispatch (v1 tetap ke formatter v1).
    Friend Const MaxPrintSchema As Integer = 2

    Friend ReadOnly V2JobTypes As String() = {
        "cashier_receipt", "kasbon_receipt", "split_receipt",
        "receivable_selected", "receivable_selected_card", "receivable_proof",
        "return_note"}

    Friend Function HealthPrintCapabilities() As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {
            {"supportedPrintSchemas", New Integer() {1, MaxPrintSchema}},
            {"supportedPrintJobTypesV2", V2JobTypes.Clone()}}
    End Function
End Module
