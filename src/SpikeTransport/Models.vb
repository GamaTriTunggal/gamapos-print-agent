' Model amplop job + payload — mirror docs/companion-print-agent/01-print-contract.md (repo gamapos).

Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PrintJob
    Public Property schemaVersion As Integer
    Public Property jobId As String
    Public Property jobType As String
    Public Property printerRole As String
    Public Property copies As Integer
    Public Property store As StoreInfo
    Public Property payload As JObject   ' beda per jobType → di-ToObject sesuai jobType
End Class

Public Class StoreInfo
    Public Property name As String
    Public Property address As String
    Public Property contact As String
End Class

Public Class CustomerInfo
    Public Property name As String
    Public Property address As String
    Public Property contact As String
    Public Property poNo As String
End Class

Public Class ReceiptItem
    Public Property name As String
    Public Property qty As Double
    Public Property qtyDisplay As String   ' opsional: teks qty persis nota lama (mis. "1/2"); kalau ada dipakai apa adanya
    Public Property unit As String
    Public Property price As Double
    Public Property total As Double
End Class

Public Class CashierReceiptPayload
    Public Property rcptNo As String
    <JsonProperty("date")> Public Property [date] As String   ' 'date' keyword VB → map via JsonProperty
    Public Property time As String
    Public Property customer As CustomerInfo
    Public Property items As List(Of ReceiptItem)
    Public Property shoppingTotal As Double
    Public Property discount As Double
    Public Property serviceCharge As Double
    Public Property grandTotal As Double
    Public Property paymentMethod As String
    Public Property isReceipt As Boolean
End Class

' Kasbon: tanpa diskon; menampilkan BAYAR & SISA. Selalu nota (selalu cetak blok customer).
Public Class KasbonReceiptPayload
    Public Property rcptNo As String
    <JsonProperty("date")> Public Property [date] As String
    Public Property time As String
    Public Property customer As CustomerInfo
    Public Property items As List(Of ReceiptItem)
    Public Property shoppingTotal As Double
    Public Property serviceCharge As Double
    Public Property payment As Double
End Class

' Split payment: total spt nota kasir tapi urutan BIAYA LAYANAN→DISKON & nilai rata-kolom shopping-total.
Public Class SplitReceiptPayload
    Public Property rcptNo As String
    <JsonProperty("date")> Public Property [date] As String
    Public Property time As String
    Public Property customer As CustomerInfo
    Public Property items As List(Of ReceiptItem)
    Public Property shoppingTotal As Double
    Public Property discount As Double
    Public Property serviceCharge As Double
End Class

' Retur "NOTA KEMBALI BARANG": tanpa header toko, customer tanpa PO, 1 total (TOTAL BELANJA), footer khusus.
Public Class ReturnReceiptPayload
    Public Property rcptNo As String
    <JsonProperty("date")> Public Property [date] As String
    Public Property time As String
    Public Property customer As CustomerInfo
    Public Property items As List(Of ReceiptItem)
    Public Property total As Double
End Class

' Slip daftar-nominal — dipakai slip gaji & pembayaran BPJS (template VB.NET identik, beda hanya teks header):
' header → tanggal → garis → baris "nama ... nominal" (hanya nominal>0, rata-kanan) → garis → Total. Tanpa header toko/footer.
Public Class AmountRow
    Public Property name As String
    Public Property amount As Double
End Class

Public Class AmountListSlipPayload
    Public Property header As String
    Public Property printDate As String
    Public Property rows As List(Of AmountRow)
    Public Property grandTotal As Double
End Class

' Tanda terima bayar bon (piutang): layout sendiri (tanpa header toko/daftar item); nama toko & operator di bawah.
Public Class ReceivableProofPayload
    Public Property printDate As String
    Public Property custId As String
    Public Property custName As String
    Public Property custHP As String
    Public Property totalReceivable As Double
    Public Property nominal As Double
    Public Property serviceCharge As Double
    <JsonProperty("operator")> Public Property operatorName As String   ' 'operator' keyword VB → map via JsonProperty
End Class
