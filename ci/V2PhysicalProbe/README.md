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
   Tahap `--print` meminta Anda mengetik `CETAK` sebelum satu job dikirim.

```powershell
& .\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe --verify `
  .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe `
  .\fixtures\v2\return_note.sample.json "NAMA PRINTER UJI"
& .\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe --print `
  .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe `
  .\fixtures\v2\return_note.sample.json "NAMA PRINTER UJI"
```

Ulangi untuk sepuluh contoh sintetis lainnya:

- `sale_cash.sample.json` — penjualan tunai;
- `sale_split_edc.sample.json` — tunai + EDC, termasuk dua biaya;
- `sale_kasbon_dp0.sample.json` — kasbon tanpa DP;
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

Kesebelas fixture bawaan, termasuk `return_note.sample.json`, masih contoh
awal. EDC penjualan tunggal, transfer campuran, DP positif, nama item panjang,
retur tanpa nota, serta kombinasi lain memerlukan fixture sintetis tambahan
dari korpus Go→agent. Jangan menganggap sebelas contoh ini sebagai matriks
cetak akhir.

Untuk tiap cetakan, catat commit agent, model/driver printer, lebar kertas,
nama fixture, ukuran/font yang terlihat, hasil angka/identitas, bagian
yang terpotong, dan foto kertas. Cocokkan uang terhadap fixture, termasuk
sen, diskon, pembulatan, fee, saldo, serta nama pemroses jika relevan.
Simpan bukti privat secara lokal, bukan di Git. Hasil `--print` sukses
hanya berarti API printer tidak melempar galat; **bukan** bukti tampilan
kertas benar. Ini juga belum menguji HTTP `/print`, role mapping,
capability `/health`, atau kompatibilitas agent yang terpasang.
