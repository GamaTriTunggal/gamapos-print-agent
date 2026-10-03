# Uji kertas nota v2 tanpa memasang agent

Alat ini khusus PC Windows uji dengan printer nota fisik. Ia memanggil
parser/renderer schema 2 dari assembly hasil build secara langsung;
`Program.Main` agent **tidak dijalankan**. Mode fixture tidak membuka port 9111;
mode `--serve` membuka loopback sementara setelah konfirmasi operator. Alat tidak
menulis autostart, mengganti printer default, memasang driver, atau
mengiklankan kemampuan v2. Jangan jalankan pada PC toko yang sedang
melayani transaksi. Gunakan hanya fixture sintetis, bukan data pelanggan.

## Uji kasir staging → kertas, tanpa memasang agent — 3 Oktober 2026

**Jangan jalankan `ci/smoke.ps1` langsung pada VM pemilik.** Skrip tersebut
untuk runner sekali pakai: membuat printer virtual, menimpa `printers.json`
dan menjalankan startup agent yang mendaftarkan autostart. Gunakan mode
terpisah berikut; tidak ada startup agent, instalasi, perubahan default,
pemetaan, driver, katalog, atau penyimpanan isi nota.

Prasyarat: PC Windows uji, printer default `EPSON TM-U220 Receipt`, dan
aplikasi staging sudah memiliki handoff/payload minimal yang sesuai agent.
Saat checkpoint dibuat, aplikasi remote masih `f18dff69`; tujuh commit
lokal sampai `43257037` belum dipush. Tanpa pembaruan aplikasi yang diizinkan
terpisah, alat ini **belum cukup** untuk mencetak dari kasir staging.

Sesudah mengambil commit kandidat dan build ulang kedua proyek:

```powershell
dotnet build .\src\SpikeTransport\SpikeTransport.vbproj -c Release
dotnet build .\ci\V2PhysicalProbe\V2PhysicalProbe.vbproj -c Release
& ".\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe" --serve ".\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe" "EPSON TM-U220 Receipt"
```

Jangan melanjutkan bila salah satu build gagal. Ketik `UJI STAGING` untuk
konfirmasi; terminal akan memberi pesan jalur uji siap. Jika printer
tidak cocok, port 9111 terpakai atau hak listener tidak tersedia, alat
berhenti. Laporkan galat; jangan otomatis mematikan agent lain, menjalankan
sebagai administrator, mengubah URL ACL/driver, atau menimpa konfigurasi.
Biarkan terminal terbuka, **jangan ubah printer default selama sesi**.

Di browser **VM yang sama**, buka `https://staging.gamapos.id/pos/cashier`.
Izinkan akses jaringan lokal bila browser meminta. Gunakan barang/pelanggan
uji, bukan transaksi toko: satu penjualan **TUNAI BERDISKON**, lalu cetak
ulang nota yang sama dari Daftar Nota. Gunakan barang uji staging yang
dikonfirmasi masih ada sebelum memulai; jangan mengulang bayar bila nota
gagal dicetak. Ambil kedua struk sekaligus setelah selesai. Catat nomor
nota, total/diskon/pembulatan yang ditampilkan aplikasi, hash commit
aplikasi/agent dan hasil konsol; foto asli/ulang harus cocok dan nama asal
tetap, baris pencetak ulang hanya pada salinan. Jangan masukkan data
pelanggan atau screenshot privat ke Git.

Selama alat ini aktif jangan memproses transaksi tanpa diskon atau alur
pembayaran/cetak lain: alat menolaknya, tetapi transaksi aplikasi dapat
tetap tersimpan. Probe bukan pengganti agent harian; hanya checkpoint uji
yang telah disepakati.

Batas alat:

- Hanya peer loopback dan origin tepat `https://staging.gamapos.id`.
  Produksi, origin kosong/`null` pada POST, schema1, keluarga lain dan
  pembayaran nontunai ditolak; tidak ada fallback formatter v1.
- `/health` menyebut `mode:staging-test` dan kemampuan hanya
  `cashier_receipt` tunai berdiskon schema2. Agent normal tetap mengumumkan schema1.
  Ini menguji browser → HTTP alat sementara → parser/renderer agent
  sebenarnya, **bukan dispatcher/role mapping agent terpasang atau izin rilis**.
- Maksimal dua job: nota asli kemudian salinan snapshot nota yang sama,
  masing-masing copies=1. Retry jobId/body identik yang sudah sukses tidak
  mengirim ulang ke printer; jobId sama dengan body berbeda ditolak.
  Ledger hanya RAM sesi ini, bukan jaminan idempotensi setelah restart.
- Parser tetap memeriksa semua nilai, body maksimal 1 MiB dan depth parser
  yang sama; payload/snapshot tidak diubah. Empat request HTTP bersamaan
  dibatasi; pekerjaan cetak diserialkan. Tidak ada log body/nama/nomor nota.
- Galat renderer menjadi `PRINT_OUTCOME_UNKNOWN`: kertas mungkin sudah
  keluar; semua cetak baru ditahan, tidak retry otomatis. Jangan memulai
  sesi baru untuk mengejar hasil tanpa pemeriksaan. Timeout browser juga
  bukan bukti tidak ada kertas; jangan ulang checkout.
- Sesi maksimal 30 menit; Ctrl+C untuk berhenti **setelah cetak selesai**.
  Penghentian menunggu renderer, tidak membatalkan spooler. Setelah alat
  ditutup tidak ada port/autostart baru yang bertahan. Agent terpasang
  tidak di-upgrade/diubah dan tetap hanya mendukung schema1.

Mode ini belum dibuktikan pada Windows/browser staging/printer fisik.
Tes HTTP Linux menggunakan renderer spy; jangan menulisnya sebagai UAT
atau aktivasi agent. Foto tujuh fixture sebelumnya adalah bukti renderer
footer saja, bukan bukti handoff browser ini.

## Batch uji ulang nama piutang/retur — 3 Oktober 2026

Sesudah mengambil commit perbaikan dan build ulang **kedua** proyek, cetak
tujuh contoh berikut sekaligus. Penjualan tidak berubah dan tidak perlu
diulang pada checkpoint ini. Jalankan dari root repo di PowerShell; setiap
contoh tetap meminta `CETAK`. Tunggu batch selesai, baru ambil semua kertas
sekali. Tidak ada retry otomatis; laporkan nama fixture yang gagal dan
apakah kertas keluar. `--preflight` hanya untuk penjualan, jangan dipakai
untuk ketujuh contoh ini.

```powershell
$ErrorActionPreference = 'Stop'
$probe = (Resolve-Path '.\ci\V2PhysicalProbe\bin\Release\net48\V2PhysicalProbe.exe').Path
$agent = (Resolve-Path '.\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe').Path
$printerName = 'EPSON TM-U220 Receipt'
$samples = @(
  'receivable_selected.sample.json',
  'receivable_selected_card.sample.json',
  'receivable_selected_reprint.sample.json',
  'receivable_proof.sample.json',
  'receivable_fifo_edc.sample.json',
  'return_note.sample.json',
  'return_reprint.sample.json'
)
$failedSamples = @()
$completedCount = 0
foreach ($sample in $samples) {
  Write-Host "`n=== $sample ==="
  $fixture = (Resolve-Path (Join-Path '.\fixtures\v2' $sample)).Path
  & $probe --verify $agent $fixture $printerName
  if ($LASTEXITCODE -ne 0) {
    $failedSamples += "$sample (verify)"
    continue
  }
  & $probe --print $agent $fixture $printerName
  if ($LASTEXITCODE -ne 0) {
    $failedSamples += "$sample (print)"
  } else {
    $completedCount++
  }
}
Write-Host "`nPerintah cetak sukses: $completedCount / $($samples.Count)"
if ($failedSamples.Count -gt 0) { $failedSamples | ForEach-Object { Write-Host "Gagal: $_" } }
```

Sukses konsol bukan bukti kertas. Periksa nama `SYNTHETIC OWNER` utuh pada
selected reprint/FIFO EDC, `KASIR ASAL` dan `KASIR AWAL` utuh pada retur,
serta pencetak ulang pada baris tersendiri. Nama pendek sejajar dengan
caption masing-masing; nama yang tidak muat center rata kanan sampai
kolom 40. Nama panjang pada fixture piutang tersebut memang rata kanan,
bukan dipindah ke bawah caption penjualan. Angka dan label harus tetap
sama dengan cetakan sebelumnya. Foto seluruh struk dan kirim hasil
konsol batch beserta hash `git rev-parse --short HEAD`.

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

Untuk checkpoint alignment penjualan (3 Oktober 2026), mulai dengan **satu**
`sale_cash.sample.json` setelah preflight lulus. Foto seluruh struk dalam
keadaan rata, termasuk TOKO dan nama Siti di bawah HORMAT KAMI. Kandidat ini
memakai posisi kolom v1 untuk header/footer penjualan; build/preflight sukses
belum membuktikan posisi di kertas. Setelah contoh pendek diperiksa, gunakan
`sale_long_item.sample.json` untuk nama panjang/rata kanan, lalu
`sale_customer_metadata_long.sample.json` untuk PEMBELI, ALAMAT, dan PO
lebih dari satu baris. Pada contoh metadata panjang, caption hanya muncul
di baris pertama; lanjutan sejajar dengan nilai setelah caption, bukan
dengan tepi kiri nota. PO sengaja tanpa spasi agar pemenggalan token juga
diuji. Pastikan alamat berakhir `12345` dan PO berakhir `000123456789`, tanpa
huruf/angka hilang atau terulang. Nilai contoh tetap Rp19.752 − Rp252 =
Rp19.500. Berikutnya gunakan `sale_corrected_reprint_simple.sample.json`
untuk menilai catatan koreksi ringkas dan penanda pencetak ulang dengan
nama realistis. Perubahan formatter ini memerlukan build ulang agent dan
probe sebelum preflight/cetak. Contoh `sale_corrected_reprint.sample.json`
tetap menguji Unicode/nama ekstrem, **bukan contoh desain normal**; hasil
fisiknya belum lulus untuk glyph/posisi Unicode. Pada checkpoint penjualan
tersebut piutang/retur belum diperbaiki; tindak lanjut nama footer berada
pada bagian batch tujuh contoh di atas.

Ulangi untuk enam belas contoh sintetis lainnya:

- `sale_cash.sample.json` — penjualan tunai;
- `sale_split_edc.sample.json` — tunai + EDC, termasuk dua biaya;
- `sale_edc_only.sample.json` — pembayaran EDC tunggal dengan dua biaya;
- `sale_split_wire.sample.json` — tunai + transfer, tanpa biaya EDC;
- `sale_kasbon_dp0.sample.json` — kasbon tanpa DP;
- `sale_kasbon_dp.sample.json` — kasbon dengan DP tunai dan sisa utang;
- `sale_long_item.sample.json` — nama barang, toko, pelanggan, dan kasir
  panjang untuk memeriksa pemenggalan dan posisi footer;
- `sale_customer_metadata_long.sample.json` — PEMBELI dan ALAMAT panjang,
  serta PO panjang tanpa spasi, untuk memeriksa lanjutan sejajar dan teks utuh;
- `sale_corrected_reprint.sample.json` — cetak ulang penjualan dengan nama
  pemroses Unicode ekstrem dan catatan koreksi pelanggan/metode;
- `sale_corrected_reprint_simple.sample.json` — contoh realistis dengan Siti
  dan Andi, dua catatan koreksi ringkas, serta angka asal Rp19.500;
- `receivable_selected.sample.json` — pelunasan bon terpilih tunai;
- `receivable_selected_card.sample.json` — pelunasan bon terpilih EDC;
- `receivable_selected_reprint.sample.json` — cetak ulang pelunasan bon
  terpilih dengan pembulatan, diskon, dan biaya;
- `receivable_proof.sample.json` — bukti pembayaran piutang;
- `receivable_fifo_edc.sample.json` — bukti cicilan FIFO via EDC dengan
  biaya pelanggan/merchant dan sisa utang;
- `return_reprint.sample.json` — cetak ulang retur, termasuk identitas
  pemroses awal dan pencetak ulang.

Ketujuh belas fixture bawaan, termasuk `return_note.sample.json`, masih contoh
awal. Retur tanpa nota serta kombinasi lain memerlukan fixture sintetis tambahan
dari korpus Go→agent. Jangan menganggap tujuh belas contoh ini sebagai matriks
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
