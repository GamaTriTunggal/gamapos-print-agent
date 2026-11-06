' Satu sumber lebar kode master di agent — cermin `internal/platform/codelength`
' (Max = 12) di repo gamapos-go-2 (papan board-number-space KN-08, IC-15;
' register P-022). Kode pelanggan/barang baru tumbuh satu angka sesudah ruang
' lama habis (CS9999 → CS10000, VJ9999 → VJ10000), jadi parser v2 menerima
' 1..12 huruf besar/angka. Nilai ini wajib sama dengan konstanta Go; mengubahnya
' = rilis agent.
Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Text.RegularExpressions

Module CodeLength
    ' Panjang maksimum kode pelanggan/barang yang diterima parser v2 (karakter).
    Friend Const MaxCodeLength As Integer = 12

    ' Kode pelanggan/barang: 1..MaxCodeLength huruf besar atau angka.
    Friend ReadOnly CodePattern As New Regex(
        "\A[A-Z0-9]{1," & MaxCodeLength.ToString(CultureInfo.InvariantCulture) & "}\z",
        RegexOptions.CultureInvariant)
End Module
