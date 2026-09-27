' Kemampuan cetak yang benar-benar boleh diumumkan oleh build ini.
' Parser/renderer v2 yang masih dormant bukan izin menerima job v2.
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Module PrintCapabilities
    Friend Function HealthPrintCapabilities() As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {
            {"supportedPrintSchemas", New Integer() {1}},
            {"supportedPrintJobTypesV2", Array.Empty(Of String)()}}
    End Function
End Module
