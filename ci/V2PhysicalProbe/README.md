# Uji kertas nota v2 tanpa memasang agent

Alat ini khusus PC Windows uji dengan printer nota fisik. Ia memanggil
parser/renderer schema 2 dari assembly hasil build secara langsung;
`Program.Main` agent **tidak dijalankan**. Alat tidak membuka port 9111,
menulis autostart, mengganti printer default, memasang driver, atau
mengiklankan kemampuan v2. Jangan jalankan pada PC toko yang sedang
melayani transaksi. Gunakan hanya fixture sintetis, bukan data pelanggan.

## Persiapan operator

1. Pastikan printer nota uji terpasang, kertas cukup, dan nama persisnya
   terlihat sebagai printer **default Windows**. Catat printer default
   sebelumnya bila Anda mengubahnya sendiri, lalu pulihkan sesudah uji.
2. Dari repo agent, build `src/SpikeTransport/SpikeTransport.vbproj` dan
   `ci/V2PhysicalProbe/V2PhysicalProbe.vbproj` pada konfigurasi Release.
   Untuk menjalankan di PC lain, salin seluruh folder output masing-masing,
   bukan hanya file `.exe`, beserta folder fixture sintetis `fixtures/v2`.
3. Jalankan dari PowerShell dengan path lengkap (contoh di bawah). Tahap
   `--verify` hanya memeriksa parser dan printer default, **tidak mencetak**.
   `--preflight` untuk nota penjualan memakai metrik printer dan menjalankan
   seluruh perencanaan layout serta transisi font 9 → 18 → 9 pt tanpa
   `Printer.Print` atau `EndDoc`; ini juga
   **tidak mencetak**. Pakai bila `--print` gagal, sebelum mencoba lagi.
   Tahap `--print` meminta Anda mengetik `CETAK` sebelum satu job dikirim.

```powershell
& .\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe --verify `
  .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe `
  .\fixtures\v2\return_note.sample.json "NAMA PRINTER UJI"
& .\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe --print `
  .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe `
  .\fixtures\v2\return_note.sample.json "NAMA PRINTER UJI"
```

Jika cetak penjualan gagal, **jangan langsung mengulang `--print`**. Setelah
membangun ulang agent dan probe dari commit yang sama, jalankan diagnostik
tanpa kertas berikut dan catat hasilnya:

```powershell
& .\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe --preflight `
  .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe `
  .\fixtures\v2\sale_cash.sample.json "NAMA PRINTER UJI"
```

Jika preflight lulus tetapi cetak gagal, probe melaporkan nama tahap tetap
(`store-name`, `store-details`, `customer`, `receipt`, `items`, `amounts`,
`corrections`, `footer`, atau `end-doc`) tanpa isi nota atau pesan driver.
Jangan mengulang job hanya untuk mengejar keberhasilan; laporkan tahap dan
apakah ada kertas keluar.

Ulangi untuk empat belas contoh sintetis lainnya:

- `sale_cash.sample.json` — penjualan tunai;
- `sale_split_edc.sample.json` — tunai + EDC, termasuk dua biaya;
- `sale_edc_only.sample.json` — pembayaran EDC tunggal dengan dua biaya;
- `sale_split_wire.sample.json` — tunai + transfer, tanpa biaya EDC;
- `sale_kasbon_dp0.sample.json` — kasbon tanpa DP;
- `sale_kasbon_dp.sample.json` — kasbon dengan DP tunai dan sisa utang;
- `sale_long_item.sample.json` — nama barang, toko, pelanggan, dan kasir
  panjang untuk memeriksa pemenggalan dan posisi footer;
- `sale_corrected_reprint.sample.json` — cetak ulang penjualan dengan nama
  pemroses panjang dan catatan koreksi pelanggan/metode;
- `receivable_selected.sample.json` — pelunasan bon terpilih tunai;
- `receivable_selected_card.sample.json` — pelunasan bon terpilih EDC;
- `receivable_selected_reprint.sample.json` — cetak ulang pelunasan bon
  terpilih dengan pembulatan, diskon, dan biaya;
- `receivable_proof.sample.json` — bukti pembayaran piutang;
- `receivable_fifo_edc.sample.json` — bukti cicilan FIFO via EDC dengan
  biaya pelanggan/merchant dan sisa utang;
- `return_reprint.sample.json` — cetak ulang retur, termasuk identitas
  pemroses awal dan pencetak ulang.

Kelima belas fixture bawaan, termasuk `return_note.sample.json`, masih contoh
awal. Retur tanpa nota serta kombinasi lain memerlukan fixture sintetis tambahan
dari korpus Go→agent. Jangan menganggap lima belas contoh ini sebagai matriks
cetak akhir.

Untuk tiap cetakan, catat commit agent, model/driver printer, lebar kertas,
nama fixture, ukuran/font yang terlihat, hasil angka/identitas, bagian
yang terpotong, dan foto kertas. Cocokkan uang terhadap fixture, termasuk
sen, diskon, pembulatan, fee, saldo, serta nama pemroses jika relevan.
Pada TM-U220 uji, `ScaleWidth=3600` dan `TextWidth(40 spasi, Courier New 9 pt
Bold)=4320,703`, tetapi kalibrasi fisik 40 karakter 9 pt tercetak utuh dalam
satu baris. Karena itu preflight v2 memakai batas logis 40 kolom dan font v1
(9 pt penjualan/piutang, 10 pt retur), bukan memperkecil font menurut
`ScaleWidth`. Cetak setiap keluarga nota tetap perlu pemeriksaan fisik.
Keputusan, batas bukti, dan status tiap gerbang tercatat di
[`docs/print-layout-validation.md`](../../docs/print-layout-validation.md).
Simpan bukti privat secara lokal, bukan di Git. Hasil `--print` sukses
hanya berarti API printer tidak melempar galat; **bukan** bukti tampilan
kertas benar. Ini juga belum menguji HTTP `/print`, role mapping,
capability `/health`, atau kompatibilitas agent yang terpasang.
