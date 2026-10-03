# Validasi tata letak nota fisik

Dokumen ini mencatat bukti dan aturan kerja untuk renderer nota schema 2
(`P-604`). Kontrak bisnis tetap diputuskan di repo utama GamaPOS; dokumen ini
tidak mengizinkan aktivasi schema 2, rilis agent, atau perubahan nota schema 1.

## Batch footer diterima dan jalur HTTP staging disiapkan — 3 Oktober 2026

Pemilik mengirim `IMG_3927.HEIC` lalu menyetujui hasil pemeriksaan. Setelah
decoding lokal tanpa mengubah foto sumber, ketujuh contoh terlihat utuh:
selected tunai/EDC, selected reprint, FIFO transfer/EDC, retur asli/salinan.
`SYNTHETIC OWNER`, `KASIR ASAL/AWAL` dan kedua baris pencetak ulang lengkap.
Nama retur berada di pusat TANDA TERIMA; nama piutang panjang rata kanan
sesuai fallback kolom, bukan dipaksa center di luar tepi kiri.
Angka yang terlihat cocok dengan fixture (9; 9,20; 14.000; bayar 4/sisa 16;
tagihan 10.200/sisa 20.000; retur 2,52 dan 1.000).
SHA-256 foto privat:
`d4be1432e7d82417e7fc497274abb2edde93cbee6eacd20381594bcd45430bc3`.
Ini bukti visual tujuh contoh, bukan seluruh input atau integrasi HTTP.
Konsol/hash binary Windows tidak disertakan pada pesan foto; konteks uji
adalah instruksi mengambil `330a310`. Tidak meminta ulang tujuh contoh ini.

Pemilik mengizinkan persiapan satu checkpoint berikutnya: kasir staging
→ cetak pertama tunai berdiskon → cetak ulang nota yang sama. Probe kini
memiliki `--serve` terpisah dari startup agent. Ia membuka localhost:9111
sementara setelah konfirmasi, hanya loopback/origin staging, hanya
cashier_receipt CASH schema2, dua job asli/salinan yang snapshot-nya sama.
Tidak mengubah autostart, pemetaan/default printer, driver, payload, money,
formatter atau capability agent normal. Isi nota hanya RAM sesi, tidak log
atau disk. Renderer/validator memakai assembly agent yang sama dengan uji
kertas. Retry identik tidak mencetak ulang; konflik, galat ambigu, nota lain,
origin produksi, schema1, keluarga/metode lain dan sesi kedaluwarsa ditahan.
Sesi 30 menit; penghentian menunggu renderer yang sedang berjalan.

Tes policy dan HTTP TCP loopback Linux memakai renderer spy, **bukan
printer fisik**. Termasuk CORS/PNA, body/depth/parser, batas dua job,
snapshot asli/salinan, printer berubah, retry/konflik/konkurensi, galat
ambigu, kedaluwarsa dan penghentian. Browser sintetis repo Go memeriksa
health alat hanya membuka kasir v2, payload tetap, enam job lain tertahan;
fetch tiruan tersebut bukan bukti CORS browser Windows sebenarnya.
Build net48 agent/probe dan schema gate dijalankan melalui perintah
checkpoint sebelumnya; hasil/log privat:
`.local/discount-rounding/staging-print-bridge-20261003/`.
Fixture, golden dan renderer v1/v2 tidak diubah. Satu nilai diskon hanya
dibentuk di RAM tes: 19.752 − 500 − 252 = 19.000, bukan oracle baru.

Build net48 agent/probe lulus 0 warning/error; schema gate lulus 11 kasus
+ 7 rute + 17 fixture v1 + 17 fixture v2 serta policy/HTTP bridge.
`make ci TEST_PG_DSN=` lulus pada salinan HEAD Go `43257037` + checkpoint
saja, cache lint terisolasi. Termasuk 12 tes capability browser, 314 tes
piutang dan dokumentasi 75 dokumen/24 keputusan. Integrasi ber-DSN
dilewati. Warning Vue readonly pada tes sintetis tetap ada di log; tidak
mengubah frontend untuk menyembunyikannya. Tidak mengikutkan perubahan
docs/proposal/docscheck sesi lain. Dokumentasi hasil akhir dicek kembali.

**Belum dijalankan di Windows/browser staging atau diaktifkan.** Remote
main repo Go terverifikasi `f18dff69`; tujuh commit lokal sampai `43257037`
memuat handoff/payload minimal yang belum dipush. Push aplikasi untuk
staging dan push alat ke branch uji memerlukan izin tersendiri. Tidak
mengklaim halaman staging sekarang sudah bisa mencetak v2. Panduan ada
di README probe. Jangan menjalankan smoke CI lama pada VM pemilik: skrip
itu menimpa pemetaan dan menulis autostart. Dispatcher/role mapping agent
terpasang, kompatibilitas penuh, Unicode ekstrem dan penutupan papan masih
gerbang terpisah. Ini bukan rilis agent, perubahan produksi atau izin VPS.

## Nama footer piutang/retur mengikuti kolom — 3 Oktober 2026 (P-604 / DR-08)

Pemilik menyetujui perbaikan **posisi nama saja** setelah batch fisik:
`IMG_3925.HEIC` memperlihatkan nama asli selected reprint terpotong menjadi
`SYNTHETIC`; `IMG_3926.HEIC` memperlihatkan hal yang sama pada FIFO EDC dan
retur asli hanya `KASIR`; retur cetak ulang pada `IMG_3924.HEIC` juga hanya
`KASIR`. Payload menyimpan `SYNTHETIC OWNER`, `KASIR ASAL`, dan `KASIR AWAL`
utuh. Ini kegagalan penempatan v2, **bukan masalah desain v1 atau data**.
Foto batch lain `IMG_3923.HEIC` berisi campuran EDC/transfer; nama penjualan
tidak termasuk perubahan ini. Angka yang terlihat cocok dengan fixture,
tetapi foto bukan bukti seluruh integrasi HTTP/role mapping atau glyph.
Identitas commit/binary Windows belum dicocokkan dari konsol batch ini.

Rencana footer sekarang berupa string berindentasi kolom; renderer memakai
`T(1)` seperti mekanisme v1, bukan `CurrentX` dari `TextWidth`. Caption
piutang tetap kolom 1; caption retur tetap kolom 25. Nama yang tidak muat
di-center rata kanan, nama panjang dibungkus, dan pencetak ulang tetap baris
tersendiri. Font, header/body, angka, fixture, parser/schema, v1, branding,
label EDC, snapshot dan aplikasi Go tidak berubah. Tidak ada migrasi.

Validasi Linux: kedua build net48 agent/probe lulus 0 warning/error;
schema gate lulus 11 kasus + 7 rute + 17 fixture v1 + 17 fixture v2.
Tes mengunci nama kasus yang terpotong, anchor kolom, center/rata kanan,
batas 40/41/60 karakter, wrapping tanpa kehilangan teks, input tidak sah,
seluruh tujuh fixture piutang/retur, ketiadaan mutasi payload dan posisi
nama yang tetap meski skala metrik driver berubah 100×. Perintah:

```text
dotnet run --project ci/SchemaGateTests/SchemaGateTests.vbproj -c Release --no-restore -- fixtures
dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release --no-restore
dotnet build ci/V2PhysicalProbe/V2PhysicalProbe.vbproj -c Release --no-restore
git diff --check
```

Log privat repo Go: `.local/discount-rounding/footer-column-20261003/`.
`make ci TEST_PG_DSN=` repo Go lulus pada salinan HEAD `c84f534e` +
hanya dokumentasi checkpoint, dengan cache lint terisolasi. Termasuk 314
tes browser piutang dan gerbang dokumentasi 75 dokumen/24 keputusan.
Integrasi yang memerlukan DSN dilewati; CI memuat warning Vue readonly
pada tes sintetis, bukan klaim seluruh CI tanpa warning. Tidak ada kode
Go/frontend berubah; perubahan sesi lain tidak masuk kandidat.
Foto tetap privat; SHA-256 masing-masing:

- IMG_3923: `1978a2cbcf958cb0cacf953ccca799ab8ff7fcb39235d9acc129bb438d6f99de`.
- IMG_3924: `874964cf587e8a819ed2f448037ed1c93c9c46861c53dec1fd91dfee1508592d`.
- IMG_3925: `d9abc512a0f8e0ce66c064f8a5b1284cc591db43594d0f3e04f0d10bc47f1bd4`.
- IMG_3926: `e20fb70f445a7891195f0464b2aae6e2f6e6fa2a681d8a889cf7848da7da57b5`.

**Belum lulus fisik setelah perbaikan.** Windows build/HTTP smoke, uji
kertas kandidat, lebar kertas, glyph Unicode ekstrem dan gerbang lain
tetap belum lengkap. Tidak ada aktivasi schema2/rilis/pemasangan agent atau
penutupan papan. Tujuh cetakan ulang disiapkan dalam satu batch pada
README probe; tidak meminta ulang seluruh fixture penjualan.
Catatan checkpoint di bawah tetap riwayat, bukan status terbaru batch ini.

## Pelajaran dari uji TM-U220 (2 Oktober 2026)

- Perangkat uji: VM Windows 11, antrean `EPSON TM-U220 Receipt`, driver
  `EPSON TM-U220 ReceiptE4`, port `ESDPRT001`. Windows test page berhasil dua
  kali. Lebar kertas fisik belum dicatat, jadi jangan menganggap ukuran
  antrean ini mewakili seluruh armada.
- PowerPacks melaporkan `Printer.ScaleWidth = 3600` dan
  `Printer.TextWidth(40 spasi) = 4320,703` dengan Courier New 9 pt Bold.
  Renderer schema 2 semula membatasi rencana baris pada nilai terkecil dari
  kedua angka itu. Probe `sale_cash.sample.json` mengembalikan
  `ArgumentException` tanpa mengeluarkan kertas. Lokasi lemparan tidak
  tercatat di probe; batas tersebut adalah dugaan penyebab terkuat, bukan
  stack trace yang terbukti.
- Kalibrasi langsung melalui PowerPacks, font dan printer yang sama, mencetak
  `1234567890` empat kali dalam **satu baris utuh**; digit terakhir terlihat
  pada foto pemilik. Jadi, pada konfigurasi uji ini `ScaleWidth` tidak boleh
  diperlakukan sendirian sebagai batas keras 40 karakter. Foto tetap privat,
  tidak dimasukkan ke Git.
- Commit `7d807d6` sempat memilih font hingga 7 pt berdasarkan angka driver.
  Keputusan itu terlalu cepat dan dibatalkan oleh `2f03f8e`: renderer schema 2
  kembali memakai font lama (9 pt untuk penjualan/piutang, 10 pt untuk retur)
  dan merencanakan teks terhadap lebar logis 40 kolom. Tidak ada perubahan
  pada renderer schema 1. Ini masih **kandidat uji**, bukan bukti nota schema 2
  sudah benar di kertas.

## Aturan saat mengubah renderer atau preflight

1. Pertahankan font, 40 kolom, dan hasil visual schema 1 sebagai baseline.
   Jangan mengecilkan font, mengganti driver, atau mengubah tata letak armada
   hanya karena satu metrik driver bertentangan dengan hasil cetak. Bila
   metrik dan kertas berbeda, ukur dan cetak contoh sintetis pada antrean
   Windows yang sama sebelum memilih perbaikan.
2. Preflight schema 2 tetap wajib memeriksa payload, pembungkusan teks,
   benturan kolom, dan panjang setiap baris **sebelum** `Printer.Print` pertama.
   Batas logis 40 kolom bukan jaminan muat pada setiap printer/kertas; jangan
   mengganti pemeriksaan fisik dengan tes unit atau status API sukses.
3. Jika muncul galat tanpa kertas, identifikasi tahap dan lokasi galat sebelum
   menyimpulkan penyebab. `--verify` hanya memeriksa parser dan printer default;
   ia tidak menjalankan layout atau membuktikan hasil cetak. Jangan mengubah
   oracle/golden untuk membuat kandidat tampak lulus.
4. Setiap perubahan font, posisi, atau lebar perlu pembandingan nota schema 1
   dan schema 2 pada printer sasaran: baris 40 kolom, sisi kanan, pemenggalan
   teks panjang, angka, footer, dan keterbacaan. Catat model, driver, kertas,
   commit, fixture, hasil konsol, serta foto secara privat. Printer lain tidak
   otomatis mewarisi hasil TM-U220 ini.
5. PowerPacks **memiliki objek Font aktif**. Jangan menyimpan lalu memakai
   ulang objek tersebut setelah mengganti `Printer.Font`, dan jangan
   memanggil `Dispose()` pada font yang masih dipakai printer. Renderer v2
   memakai `SetV2ReceiptFont` untuk membuat objek baru hanya saat atribut
   font berubah. Simpan ukuran/gaya yang diinginkan, bukan objek Font lama.

## Kepemilikan Font: penyebab kegagalan tahap store-details

Setelah build agent/probe `5d1e732` berhasil di Windows, uji cetak penjualan
melaporkan `SALE_V2_STAGE:store-details`, penyebab `ArgumentException`.
Preflight Windows sebelumnya lulus; kegagalan ini terletak pada transisi
font/tahap cetak, bukan diselesaikan oleh perubahan batas lebar.

Inspeksi IL DLL lokal `lib/Microsoft.VisualBasic.PowerPacks.dll` membuktikan:

- `Printer.set_Font` meneruskan objek ke `GraphicsFactory.set_Font`.
- Setter itu memanggil `Font.Dispose()` pada font lama ketika font berbeda,
  lalu menyimpan referensi objek baru tanpa mengkloningnya.
- Kode penjualan v2 menyimpan objek 9 pt (`normal`), memasangnya untuk
  preflight, lalu memasang 18 pt saat mencetak nama toko. Pergantian itu
  membuang objek `normal`; penggunaan ulangnya pada `store-details` salah.
  Renderer v1 membuat objek baru pada pergantian tersebut. Nota piutang v2
  memiliki pola penggunaan ulang serupa dan diperbaiki dengan helper yang sama.

SHA-256 DLL yang diperiksa:
`81edea696a5d42d8641eabd03c57d11c4236915b484157d52743046344430a50`.
Inspeksi DLL membuktikan perilaku kepemilikan objek. Pada `35d8119`, pemilik
berhasil menjalankan preflight dan mencetak fixture penjualan tunai; bukti
visual bagian utama dicatat di bawah. Foto awal belum memperlihatkan footer
lengkap; foto berikutnya menunjukkan nama tidak selaras (lihat checkpoint
3 Oktober di bawah). Mode
`--preflight` setelah perbaikan juga mengukur teks sesudah transisi
9 → 18 → 9 pt untuk mendeteksi objek Font yang tidak lagi sah, tanpa mencetak.

## Status bukti saat dokumen dibuat

| Gerbang | Status | Batas bukti |
|---|---|---|
| Windows test page | Lulus 2× | Membuktikan antrean/printer bekerja, bukan layout agent. |
| Kalibrasi PowerPacks 9 pt, 40 karakter | Lulus | Satu baris sintetis; bukan nota lengkap. |
| Build agent `2f03f8e` | Lulus, 0 warning | Linux dapat mengompilasi net48, tidak menjalankan printer Windows. |
| Schema gate | Lulus: 17 fixture v1, 15 fixture v2 | Parser/layout murni; bukan hasil cetak fisik. |
| `--preflight` penjualan pada binary `799c36e` | Lulus di Windows | Agent dan probe dibangun ulang; semua layout direncanakan tanpa `Printer.Print`/`EndDoc`. |
| Cetak penjualan v2 pada `5d1e732` | Gagal | Build agent/probe terkonfirmasi; galat di `store-details`, penyebab `ArgumentException`. |
| Perbaikan kepemilikan Font pada `35d8119` | Lulus preflight dan pengiriman cetak Windows | Build agent/probe, preflight transisi font, dan `--print` berhasil; kertas tercetak. |
| Visual `sale_cash.sample.json` pada `35d8119` | Angka utama lulus; alignment nama gagal pada foto lengkap | Belanja 19.752 − pembulatan 252 = nota 19.500; diterima 20.000, kembalian 500. Tepi kanan nilai utuh. Foto berikutnya memperlihatkan Siti bergeser ke kanan dari HORMAT KAMI. |
| Cetakan penjualan tunai setelah `a4e560d` | Diterima pemilik pada 3 Oktober 2026 | Pemilik mengirim foto HEIC dan menyatakan hasil “sudah oke”. Penerimaan contoh ini bukan bukti seluruh variasi penjualan/piutang/retur. |
| Nota piutang dan retur v2 | Belum diuji | Masing-masing perlu uji fisik; font retur 10 pt belum dikalibrasi pada foto ini. |
| Aktivasi/HTTP/role mapping/rilis | Belum diuji/diizinkan | Jangan aktifkan dari bukti kalibrasi ini. |

Foto dan rincian pemeriksaan disimpan privat di repo utama:
`.local/discount-rounding/physical-print-20261002/sale_cash-35d8119.{jpg,md}`.
SHA-256 foto:
`6bd49dd7491e42c7309889dea2fa38316580ce01a45fd064e7075b8efb5cff3d`.
Fixture ini tidak memuat diskon; hasilnya membuktikan pembulatan tunai dan
angka yang terlihat, belum membuktikan seluruh kombinasi pembayaran.

## Checkpoint alignment penjualan — 3 Oktober 2026

Foto lengkap berikutnya disimpan privat sebagai
`.local/discount-rounding/physical-print-20261002/sale_cash-full-footer-35d8119.jpg`
di repo utama. SHA-256:
`eab3b93a68879cd6b36cd1b825573115d72aa51f4006fc06b3734485b2572f28`.
Foto ini membuktikan ketidakselarasan Siti dengan caption, bukan kegagalan
desain v1. Header v2 juga berbeda mekanisme penempatannya dari v1; foto
perspektif tidak dipakai untuk mengklaim ukuran offset header yang pasti.

Kandidat checkpoint ini mengubah **hanya penempatan header dan nama footer
tiga nota penjualan v2** (kasir, kasbon, campuran):

- `LayoutSaleHeaderColumns` merencanakan nama toko pada 20 kolom, alamat dan
  kontak pada 40 kolom. Center berupa spasi karakter, bukan `TextWidth`/`CurrentX`.
- `LayoutSaleOriginalNameColumns` memakai anchor caption yang sama dengan
  renderer (`SaleFooterCaptionColumn=25`). Siti dimulai pada kolom 28;
  perbedaan panjang genap/ganjil memiliki toleransi setengah kolom, dibulatkan
  ke kiri. Nama yang tidak muat pada posisi center rata kanan; nama lebih
  panjang dari 40 kolom dibungkus tanpa membuang teks.
- `LayoutSaleReprintNameColumns` menjaga baris terpisah “Dicetak ulang oleh: …”
  rata kanan. Semua baris ini dicetak dari `T(1)` dengan spasi yang direncanakan,
  setara mekanisme pembentukan teks `TAB` v1, tanpa memindahkan `CurrentX` per baris.
- Normalisasi, wrapping kata/grapheme v2, alamat kosong yang tidak dicetak,
  dan header tepat kelipatan kolom tanpa baris kosong tambahan tetap dijaga.
  Ini mengikuti mekanisme posisi v1, bukan menyalin keterbatasan input v1.
- Font, nilai uang, snapshot, parser, capability aktif schema 1, renderer v1,
  serta printer LX-310/label tidak diubah. Piutang/retur v2 belum mengikuti
  perubahan posisi ini dan tetap membutuhkan checkpoint/uji sendiri.

Validasi lokal pada perubahan ini:

```text
dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release --no-restore --nologo
dotnet build ci/V2PhysicalProbe/V2PhysicalProbe.vbproj -c Release --no-restore --nologo
dotnet run --project ci/SchemaGateTests/SchemaGateTests.vbproj -c Release -- fixtures
git diff --check
```

Kedua build Linux lulus dengan 0 warning/0 error. Schema gate lulus 11 kasus,
7 rute, 17 fixture v1 dan 15 fixture v2, termasuk tes baru posisi kolom TOKO,
alamat/kontak, Siti, batas center/rata kanan, nama panjang, cetak ulang,
teks kosong, kelipatan lebar kolom, Unicode, dan input kolom tidak sah.
Perbandingan diff terhadap `5f5ba69` pada renderer v1, parser/rumus uang,
capability dan dispatch tidak menunjukkan perubahan.

**Status saat checkpoint dibuat:** Windows build/preflight/cetak kandidat ini belum
dijalankan. Smoke HTTP Windows dan pengujian fisik piutang/retur juga tidak
dijalankan pada checkpoint Linux ini. Tes kolom bukan bukti bahwa kertas
sudah tepat. Jangan aktifkan v2 atau menyatakan papan selesai dari hasil ini.

Uji berikutnya: build ulang agent dan probe dari commit kandidat yang sama,
jalankan `--preflight` lalu cetak **satu** `sale_cash.sample.json`, dan foto
seluruh kertas rata sampai footer. Bandingkan TOKO terhadap area isi nota dan
Siti terhadap HORMAT KAMI, bukan terhadap perspektif tepi foto. Setelah itu,
fixture `sale_long_item.sample.json` dan `sale_corrected_reprint.sample.json`
menguji wrapping/nama panjang serta penanda pencetak ulang.

### Tindak lanjut uji fisik dan keputusan baris pembayaran — 3 Oktober 2026

Sesudah instruksi mengambil `a4e560d`, pemilik mengirim `IMG_3914.HEIC` dan
menyatakan “hasilnya, sudah oke”. Konfirmasi itu menjadi bukti penerimaan
manusia atas contoh penjualan tunai, bukan hasil tes layout otomatis. Foto
HEIC disimpan privat di repo utama:
`.local/discount-rounding/physical-print-20261003/sale_cash-a4e560d.HEIC`.
SHA-256:
`d31982d3ef8421f8289b96e960c42c2c6ceed1d68120c52a73f356cca2c331ae`.
Format HEIC tidak dapat dibaca oleh alat inspeksi gambar sesi ini; penerimaan
visual dicatat dari konfirmasi pemilik, bukan klaim inspeksi independen agent.
Tidak ada hasil konsol build/preflight baru pada pesan ini.

Pada pesan yang sama, pemilik meminta tidak menambahkan “UANG DITERIMA” dan
“KEMBALIAN”, mengikuti format aplikasi cetak yang sudah digunakan. Checkpoint
berikutnya menghapus kedua caption dari `BuildSaleAmountRows` dan
`BuildReceivableAmountRows`, termasuk tunai dan cetak ulang. Baris diskon,
pembulatan, biaya, bagian tunai/nontunai, BAYAR, dan sisa utang tidak berubah.
Angka `tenderSen`/`changeSen` tetap ada dalam fixture/payload immutable;
persamaan `tender - change = cash` dan penolakan selisih satu sen tetap berlaku.
Tidak ada perubahan data transaksi, kontrak JSON, parser, atau renderer v1.

Build agent dan probe Linux serta schema gate dijalankan dengan perintah
checkpoint di atas. Tes mengunci baris tunai/diskon/piutang selected/FIFO
tanpa kedua caption; seluruh fixture penjualan/piutang juga diperiksa agar
perakitan baris tidak memutasi payload dan parser menolak perubahan kembalian
satu sen. Ekspektasi tes baris diubah karena keputusan eksplisit pemilik,
bukan untuk menutupi regresi. Fixture dan golden tidak direkam ulang.

Penerimaan alignment `a4e560d` tidak digugurkan oleh penghapusan dua baris
angka; posisi header/footer tidak diubah lagi. Namun cetakan setelah
penghapusan baris **belum diuji fisik**. Nama panjang, cetak ulang, piutang,
retur, dan smoke HTTP Windows tetap memiliki gerbang tersendiri. Ini bukan
izin aktivasi/rilis schema2 atau penutupan papan.

Jalankan hanya fixture sintetis melalui
[`ci/V2PhysicalProbe/README.md`](../ci/V2PhysicalProbe/README.md). Simpan
foto, dump, dan data pelanggan di luar Git. Perbarui tabel status dengan
bukti baru; jangan mengganti “belum diuji” dengan “lulus” berdasarkan asumsi.

### Penyelarasan kontrak cetak minimal — 3 Oktober 2026 (P-604 / DR-08)

Sesudah checkpoint penghapusan baris `7dbc4d2`, pemilik menyetujui penghapusan
`tenderSen`/`changeSen` dari kontrak printer, bukan hanya tampilan.
Catatan sebelumnya bahwa kedua field tetap wajib dalam payload adalah
riwayat checkpoint lama, **bukan kebijakan terbaru**.
Desain tanpa “UANG DITERIMA”/“KEMBALIAN” disengaja oleh pemilik untuk
menyederhanakan nota: nilai belanja/piutang setelah pengurangan, bukan
rincian pertukaran uang fisik. Jangan menambahkannya kembali tanpa keputusan.

Pengirim Go penjualan/piutang dan kedua parser agent sekarang selaras:
field tidak dikirim dan ditolak jika disisipkan. Audit pembayaran server,
persamaan uang diterima − kembalian = kas, serta aturan larangan lebih
bayar piutang tetap dipertahankan. Fixture sintetis hanya menghapus kedua
kunci; nilai total, alokasi, diskon, pembulatan, dan biaya tidak diubah.
Fixture retur, golden regresi, kontrak v1, renderer, font dan kolom tidak diubah.

Validasi Linux:

- `dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release --no-restore`
  dan `dotnet build ci/V2PhysicalProbe/V2PhysicalProbe.vbproj -c Release --no-restore`:
  keduanya lulus, 0 warning/error.
- `dotnet run --project ci/SchemaGateTests/SchemaGateTests.vbproj -c Release -- fixtures`:
  11 kasus schema + 7 rute + 17 fixture v1 + 15 fixture v2 lulus;
  parser menolak kedua field lama, builder tidak mencetak kedua caption atau
  memutasi payload. Persamaan jumlah/alokasi dan penolakan selisih kas
  tetap diuji. Gerbang `/health` tetap schema1.
- Validator Go juga menerima seluruh 15 fixture agent v2 yang sama.
  Tes produsen Go memeriksa cetak pertama/ulang, semua metode penjualan,
  selected/FIFO tunai/transfer/EDC, tidak adanya kedua kunci, snapshot
  tidak berubah, dan penolakan snapshot uang tidak konsisten.

Bukti dan batas CI utama dicatat pada papan/register repo Go; log privat di
`.local/discount-rounding/print-payment-contract-20261003/`.
Tidak ada migrasi, akses data pelanggan, perubahan produksi, push repo Go,
atau rilis/aktivasi agent. Windows smoke dan cetakan fisik kandidat ini
**belum diulang**; tes Linux tidak menggantikannya. Papan tetap terbuka.

CI repo Go: `make ci TEST_PG_DSN=` lulus pada worktree HEAD `d338587c`
ditambah hanya perubahan checkpoint, memakai cache lint terisolasi.
Tes integrasi PostgreSQL yang memerlukan DSN dilewati. Pemeriksaan khusus
P-591 tetap menguji penolakan cicilan berlebih satu sen untuk semua metode.
CI working tree awal mencakup perubahan docs sesi lain dan cache lint
lama; log kegagalannya disimpan, tidak diklaim hijau.
Percobaan terisolasi pertama menemukan fixture route cetak ulang yang
masih mengirim field lama; itu diperbaiki sesuai kontrak baru dan penolakan
kedua field ditambahkan sebelum CI ulang lulus.
Audit membandingkan fixture sebelum/sesudah: tepat 13 job hanya kehilangan
dua kunci; semua nilai lainnya dan dua fixture retur tidak berubah.

### Foto penjualan dan contoh metadata panjang — 3 Oktober 2026 (P-604 / DR-08)

Pemilik mengirim `IMG_3917.HEIC` setelah instruksi mengambil checkpoint
kontrak minimal, kemudian `IMG_3918.HEIC` untuk contoh nama panjang.
Keduanya berhasil diinspeksi setelah decoding HEIC lokal, tanpa mengubah
foto sumber. Identitas commit/binary di Windows belum diverifikasi ulang
melalui hasil konsol pada pesan foto; bukti berikut berlaku untuk cetakan
yang terlihat, bukan seluruh kombinasi v2.

- Foto tunai: TOKO dan nama Siti tampak sejajar dengan anchor center
  masing-masing; total belanja Rp19.752, pembulatan Rp252, total nota
  Rp19.500. Tidak ada UANG DITERIMA/KEMBALIAN; footer terlihat utuh.
  Diskon nol pada contoh ini, sehingga bukan bukti cetak diskon positif.
- Foto nama panjang: header toko, PEMBELI, item dan nama pemroses terbaca
  utuh; lanjutan PEMBELI sejajar setelah caption, nama pemroses panjang
  rata kanan. ALAMAT dan PO pelanggan masih satu baris pada contoh ini:
  **belum membuktikan wrapping fisik kedua field tersebut**.
- Foto privat tidak dimasukkan ke Git. SHA-256 `IMG_3917.HEIC`:
  `ac5e330e1837d361f45ba0be2b5ad38bc7c19e7ab1624150110cbbcd490b4930`;
  `IMG_3918.HEIC`:
  `677ef906f31f3495147e3ba2f3b827ecba6c1f700cedd79710f610474490c7ad`.

Pemilik menyetujui satu contoh gabungan berikutnya:
`fixtures/v2/sale_customer_metadata_long.sample.json`. PEMBELI dan ALAMAT
panjang menguji pembungkusan per kata; PO panjang tanpa spasi menguji
pemenggalan karakter. Tes schema gate mengharuskan masing-masing lebih
dari satu baris, caption di awal, lanjutan sejajar, lebar maksimal 40
kolom, dan rekonstruksi teks persis tanpa kehilangan/duplikasi. Nilai uang
sama dengan contoh tunai pendek. Checkpoint ini **hanya menambah fixture,
tes, dan dokumentasi**: renderer, font, parser, kontrak, v1, serta aplikasi
Go tidak diubah.

Validasi Linux:

```text
dotnet run --project ci/SchemaGateTests/SchemaGateTests.vbproj -c Release --no-restore -- fixtures
dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release --no-restore
dotnet build ci/V2PhysicalProbe/V2PhysicalProbe.vbproj -c Release --no-restore
git diff --check
```

Schema gate lulus 11 kasus + 7 rute + 17 fixture v1 + 16 fixture v2.
Validator Go juga menerima seluruh 16 fixture yang sama, melalui skrip
lokal `physical-metadata-20261003/check-fixtures.go`; skrip checkpoint
lama untuk 15 fixture tidak ditimpa. Kedua build lulus, 0 warning/error.
Log privat di repo Go:
`.local/discount-rounding/physical-metadata-20261003/`.
Wrapping dengan ukuran karakter pada tes Linux bukan pengganti ukuran font
Windows atau foto kertas. Contoh metadata baru **belum diuji fisik**;
cetak ulang berkoreksi, piutang/retur, dan smoke HTTP Windows tetap belum
dibuktikan oleh kedua foto tersebut. Tidak ada aktivasi schema2, rilis,
pemasangan agent, atau penutupan papan pada checkpoint ini.

### Catatan koreksi ringkas sesudah uji cetak ulang — 3 Oktober 2026 (P-604)

Foto `IMG_3919.HEIC` cocok dengan contoh metadata panjang: PEMBELI,
ALAMAT dan PO memiliki lanjutan sejajar, teks alamat sampai `12345` dan
PO sampai `000123456789` tampak utuh. Belanja Rp19.752 − pembulatan Rp252
= Rp19.500, header/footer terlihat utuh. Ini bukti visual satu contoh pada
antrean uji, bukan jaminan semua keluarga/glyph. SHA-256 foto privat:
`915bd52b7df3cced63b394f8ad6c434219ea0e995a2ed6062bb4ae2847f5dcfa`.

Foto `IMG_3920.HEIC` cocok dengan fixture cetak ulang ekstrem: nama aktor
`Kasir名` berulang pada dua koreksi dan footer, serta nilai berpecahan sen
memang berasal dari fixture, bukan tambahan data oleh printer. Pemilik
menolak kepadatan informasi pada struk. Unicode/glyph/posisi pada foto ini
**tidak dinyatakan lulus**; bukan masalah yang ditutup hanya dengan nama
contoh baru. SHA-256 foto privat:
`94b35c1856f2eb1b347ef3ded5e060d45f9415c99cc1561c32198cdeda891df2`.

Pemilik menyetujui draft ringkas sebelum implementasi: judul koreksi,
pelanggan/metode/konversi yang berubah, lalu “Angka di atas mengikuti nota
asal.” ID pelanggan, waktu/pelaku tiap koreksi dan arahan saldo tidak
dicetak. Snapshot asal, audit/field/parser dan angka tidak diubah. Footer
nama asli dan “Dicetak ulang oleh: …”, header, font, mekanisme kolom dan
v1 tetap utuh. Nota tanpa koreksi tetap tanpa blok koreksi.

Fixture `sale_corrected_reprint_simple.sample.json` terpisah memakai
Siti/Andi/Budi dan Rp19.752 − Rp252 = Rp19.500 untuk penilaian tampilan
normal. Fixture ekstrem dan golden tidak diubah/dihapus. Tes mengunci teks
draft persis, konversi/nonkonversi, ketiga metode, nama kosong/panjang,
tanpa koreksi, lebar 40 kolom dan tidak memutasi payload.

Schema gate lulus 11 kasus + 7 rute + 17 fixture v1 + 17 fixture v2.
Validator Go menerima seluruh 17 fixture yang sama. Build agent/probe
net48 lulus 0 warning/error. CI aplikasi (`make ci TEST_PG_DSN=`) lulus
pada salinan HEAD `ca784800` + dokumentasi checkpoint saja; integrasi
ber-DSN dilewati. Perintah sama dengan
checkpoint di atas; log privat di repo Go:
`.local/discount-rounding/compact-reprint-20261003/`.
Perubahan ekspektasi layout mengikuti draft eksplisit pemilik, bukan
rekam ulang oracle agar tes hijau. Tidak ada perubahan endpoint/capability,
rilis/pemasangan agent, atau aktivasi v2. Contoh ringkas **belum diuji
fisik**; perlu build ulang Windows, preflight, cetak satu contoh, dan foto.
