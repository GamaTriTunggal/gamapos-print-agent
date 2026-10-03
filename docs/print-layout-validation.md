# Validasi tata letak nota fisik

Dokumen ini mencatat bukti dan aturan kerja untuk renderer nota schema 2
(`P-604`). Kontrak bisnis tetap diputuskan di repo utama GamaPOS; dokumen ini
tidak mengizinkan aktivasi schema 2, rilis agent, atau perubahan nota schema 1.

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

**Belum lulus fisik:** Windows build/preflight/cetak kandidat ini belum
dijalankan. Smoke HTTP Windows dan pengujian fisik piutang/retur juga tidak
dijalankan pada checkpoint Linux ini. Tes kolom bukan bukti bahwa kertas
sudah tepat. Jangan aktifkan v2 atau menyatakan papan selesai dari hasil ini.

Uji berikutnya: build ulang agent dan probe dari commit kandidat yang sama,
jalankan `--preflight` lalu cetak **satu** `sale_cash.sample.json`, dan foto
seluruh kertas rata sampai footer. Bandingkan TOKO terhadap area isi nota dan
Siti terhadap HORMAT KAMI, bukan terhadap perspektif tepi foto. Setelah itu,
fixture `sale_long_item.sample.json` dan `sale_corrected_reprint.sample.json`
menguji wrapping/nama panjang serta penanda pencetak ulang.

Jalankan hanya fixture sintetis melalui
[`ci/V2PhysicalProbe/README.md`](../ci/V2PhysicalProbe/README.md). Simpan
foto, dump, dan data pelanggan di luar Git. Perbarui tabel status dengan
bukti baru; jangan mengganti “belum diuji” dengan “lulus” berdasarkan asumsi.
