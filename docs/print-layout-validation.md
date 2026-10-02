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
Inspeksi DLL membuktikan perilaku kepemilikan objek; keberhasilan perbaikan
di Windows dan cetakan penuh tetap perlu hasil probe/kertas. Mode
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
| Perbaikan kepemilikan Font | Menunggu Windows/kertas | Build agent/probe dan schema gate lulus lokal; transisi font dan nota penuh perlu diuji kembali. |
| Nota piutang dan retur v2 | Belum diuji | Masing-masing perlu uji fisik; font retur 10 pt belum dikalibrasi pada foto ini. |
| Aktivasi/HTTP/role mapping/rilis | Belum diuji/diizinkan | Jangan aktifkan dari bukti kalibrasi ini. |

Jalankan hanya fixture sintetis melalui
[`ci/V2PhysicalProbe/README.md`](../ci/V2PhysicalProbe/README.md). Simpan
foto, dump, dan data pelanggan di luar Git. Perbarui tabel status dengan
bukti baru; jangan mengganti “belum diuji” dengan “lulus” berdasarkan asumsi.
