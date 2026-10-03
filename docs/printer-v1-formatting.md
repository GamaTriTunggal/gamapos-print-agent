# Acuan Formatting Printer V1

> STATUS: RUJUKAN
> Telaah kode: 2–3 Oktober 2026, commit `11c95fa`.
> V1 merupakan baseline operasional yang sudah digunakan dan dinyatakan battle-tested oleh pemilik.

Dokumen ini menjelaskan formatting per printer. Perilaku lama, keterbatasan
kode, dan contoh pengembangan dibedakan agar pembaca tidak menganggap semuanya
sebagai izin perubahan.

Dokumen ini tidak mengizinkan perubahan v1, aktivasi v2, atau rilis agent.
Pengalaman operasional pemilik bukan klaim bahwa semua input ekstrem atau
seluruh printer telah diuji ulang dalam telaah ini.

## 1. Epson TM-U220IIB — Nota 40 Kolom

### Fungsi dan sumber kode

Digunakan untuk nota kasir, campuran, kasbon, retur, bukti piutang, ambil bon,
serta slip gaji/BPJS. Jenis cetakan dibedakan oleh `jobType`; model printer
bukan pengganti pemetaan peran printer.

Sumber utama:

- [ReceiptCommon.vb](../src/SpikeTransport/ReceiptCommon.vb)
- [CashierReceipt.vb](../src/SpikeTransport/CashierReceipt.vb),
  [SplitReceipt.vb](../src/SpikeTransport/SplitReceipt.vb),
  [KasbonReceipt.vb](../src/SpikeTransport/KasbonReceipt.vb)
- [ReturnReceipt.vb](../src/SpikeTransport/ReturnReceipt.vb),
  [ReceivableReceipt.vb](../src/SpikeTransport/ReceivableReceipt.vb),
  [AmountListSlip.vb](../src/SpikeTransport/AmountListSlip.vb)

### Font dan area formatting

| Bagian | Font | Area logis |
|---|---|---|
| Nama toko pada header penjualan | Courier New, 18 pt | 20 kolom |
| Alamat, kontak, isi penjualan | Courier New, 9 pt Bold | 40 kolom |
| Retur | Courier New, 10 pt Bold | 40 kolom |
| Nama toko pada bon terpilih | Courier New, 18 pt Bold | 20 kolom |
| Isi bon terpilih/bukti piutang biasa | Courier New, 9 pt Bold | 40 kolom |
| Judul “AMBIL BON” | Courier New, 18 pt Bold | 20 kolom |
| Isi “AMBIL BON” | Courier New, 10 pt Bold | 40 kolom |

Lebar kolom bukan satuan milimeter. Area 20 kolom pada font besar tidak boleh
disamakan dengan 20 kolom font isi. Header penjualan mengatur nama font dan
ukuran 18 pt sebelum mengganti font isi menjadi objek baru 9 pt Bold; jangan
menambahkan asumsi gaya font yang tidak ditetapkan eksplisit oleh jalur itu.

### Cara membuat teks tengah, kiri, dan kanan

V1 menggunakan posisi karakter melalui `TAB`, dibungkus helper `T(col)`.
Kolom pertama adalah **1**. Contoh berikut merupakan alternatif penempatan,
bukan tiga instruksi yang harus dijalankan sekaligus:

```vb
' Rata kiri
printer.Print(T(1), text)

' Rata tengah dalam area yang dipilih
Dim centerColumn As Integer = ((columnCount - text.Length) \ 2) + 1
printer.Print(T(centerColumn), text)

' Rata kanan
Dim rightColumn As Integer = columnCount - text.Length + 1
printer.Print(T(rightColumn), text)
```

Rumus tersebut untuk teks satu baris yang muat dalam area. Teks panjang harus
mengikuti aturan wrapping.

Contoh nama toko **TOKO** pada font 18 pt:

```text
Area: 20 kolom
Panjang: 4 karakter
Spasi kiri: (20 − 4) \ 2 = 8
Posisi awal: TAB(9)
```

Alamat dan kontak menggunakan area **40 kolom**, bukan 20.

Pembagian bilangan bulat dapat menghasilkan selisih setengah kolom ketika
sisa ruang ganjil. Patokan tengah adalah area isi cetakan, bukan otomatis
seluruh lebar kertas.

### Header dan pelanggan

Header penjualan: nama toko → alamat → kontak → satu baris kosong.

`PrintCentered` memotong teks panjang berdasarkan jumlah karakter, bukan
batas kata. Kode sekarang melanjutkan potongan berikutnya; perbaikan terhadap
pengulangan potongan awal sudah tercatat di kode sebagai perubahan yang
disetujui pada 20 Juni 2026.

Keterbatasan saat ini:

- Teks kosong menghasilkan `NO NAME`, termasuk ketika helper dipakai untuk
  alamat/kontak.
- Panjang tepat kelipatan lebar kolom menghasilkan iterasi terakhir dengan
  teks kosong.

Blok pelanggan: PEMBELI → ALAMAT → NO HP → PO. Alamat memiliki wrapping dengan
baris lanjutan sejajar setelah caption. Nama, telepon, dan PO tidak memiliki
wrapping eksplisit yang sama.

Retur tanpa PO bergantung pada payload PO kosong; helper bersama tidak
otomatis membuang PO.

### Nomor nota dan waktu

- `NO:` di kolom 1; nomor di kolom 4.
- Tanggal di kolom 19.
- Jam rata kanan hingga kolom 40.
- Kasir, campuran, dan kasbon mendukung baris `CETAK ULANG:` tambahan.

Penanda cetak ulang tidak mengganti nomor atau waktu transaksi asli. Posisi
jam rata kanan merupakan penyimpangan yang disetujui dan dicatat di kode;
jangan mengembalikannya ke posisi aplikasi asal tanpa keputusan pemilik.

### Barang dan angka

Lebar kuantitas mengikuti teks kuantitas terpanjang dalam daftar. Nama barang
dimulai setelah area kuantitas.

Renderer memilih format satu baris, dua baris, atau beberapa baris berdasarkan
panjang nama dan angka. Total barang diarahkan ke kolom 40. Pemisah antarbarang
berupa `-`; setelah barang terakhir berupa `=`. Jangan memaksakan satu baris
atau mengubah lebar kuantitas per barang tanpa pembandingan hasil lama.

`qtyDisplay` didahulukan. Jika kosong, helper memformat bilangan bulat, pecahan
umum (`1/2`, `1/4`, `3/4`), atau desimal dengan titik. Pecahan negatif perlu
pemeriksaan tersendiri sebelum helper digunakan untuk kebutuhan baru.

Uang v1 menggunakan `Double` dan format `###,###` mengikuti culture proses.
Nilai nol dapat menjadi teks kosong; beberapa pemanggil menanganinya dengan
literal `0`.

Ini catatan kompatibilitas v1, bukan anjuran menggunakan `Double` untuk
perhitungan uang baru.

Keputusan desain pemilik, ditegaskan 3 Oktober 2026 setelah uji fisik:
“UANG DITERIMA” dan “KEMBALIAN” **sengaja tidak dimasukkan** dalam desain
nota agar tampilan cetak sederhana. Nota penjualan menggambarkan total
nilai belanja setelah diskon/pembulatan dan komponen nota yang berlaku;
bukti piutang menggambarkan nilai pembayaran/alokasi piutang setelah
pengurangan yang berlaku. Ini bukan kekurangan v1 yang boleh “dilengkapi”
atas inisiatif implementer. Penambahan memerlukan keputusan pemilik.

V2 mengikuti keputusan tersebut: kedua baris tidak dicetak dan
`tenderSen`/`changeSen` tidak dikirim dalam kontrak printer penjualan/piutang.
Parser menolak kedua field sebagai field asing. Snapshot, data audit, dan
perhitungan uang diterima/kembalian tetap milik server aplikasi; penghapusan
dari proyeksi cetak tidak menghapus data tersebut atau mengubah pembayaran.
Nota v1, font, posisi kolom, dan renderer yang sudah teruji tidak diubah.

Riwayat kesalahan: `BuildReceivableAmountRows` sempat menambahkan kedua
baris untuk CASH dengan kembalian positif. Selected tunai bisa memakai
uang fisik lebih besar dari tagihan, tetapi pelunasan tetap sebesar tagihan
dan selisih dikembalikan; itu bukan izin melebihi saldo piutang.
FIFO membentuk tender sama dengan nominal kas dan kembalian nol, sehingga
baris tambahan tersebut tidak muncul pada hasil FIFO yang sah.
Kedua baris dihapus pada `7dbc4d2`; kemudian pengirim Go dan parser agent
diselaraskan untuk menghapus field dari kontrak cetak atas persetujuan pemilik.

### Perbedaan logika nota

| Jenis | Perilaku v1 |
|---|---|
| Kasir/campuran | Belanja → diskon → biaya layanan. Grand total muncul jika diskon atau biaya bukan nol. |
| Kasbon | Bayar yang ditampilkan termasuk biaya layanan; sisa = belanja − pembayaran. Renderer tidak memiliki diskon. |
| Retur | Tanpa header toko; satu total dari payload; footer khusus. |
| Piutang biasa | Sisa = total bon − nominal; biaya layanan ditambahkan pada grand total dan bayar. |
| Bon terpilih | Total terpilih dikurangi biaya transfer, materai, dan diskon. |
| Bon terpilih kartu | Perhitungan bon terpilih ditambah biaya EDC. |
| Gaji/BPJS | Mencetak baris nominal positif; grand total berasal dari payload. |
| Penawaran harga | Blok pelanggan/nomor diganti peringatan bukan nota pembayaran. |

Rumus renderer lama tidak otomatis menjadi aturan bisnis untuk versi baru.
Kasir v1 menghitung grand total dari komponen; jangan menganggap semua
properti model payload pasti digunakan oleh renderer.

Jika komentar berbeda dari implementasi, periksa kode yang dijalankan.
Misalnya komentar lama di model split tentang urutan biaya/diskon berbeda
dari pemakaian `PrintStandardTotals` sekarang, dan komentar header piutang
terpilih menyebut tidak bold padahal kode mengaktifkan bold.

### Footer dan nama pemroses

Footer penjualan:

```text
TAB(6)  : TANDA TERIMA
TAB(25) : HORMAT KAMI
Tiga baris ruang tanda tangan
Branding dengan spasi tetap
```

`HORMAT KAMI` menempati kolom 25–35.

Nama kasir di bawahnya adalah tambahan **v2**, bukan fitur footer penjualan
v1. Penempatannya harus memakai acuan caption yang sama, bukan asumsi
kesetaraan antara `TAB` dan `CurrentX`.

Kandidat penjualan v2 pada checkpoint 3 Oktober 2026 memakai
`LayoutSaleHeaderColumns`, `LayoutSaleOriginalNameColumns`, dan
`LayoutSaleReprintNameColumns` untuk menghasilkan teks dengan spasi kolom.
Caption tetap kolom 25; Siti dimulai pada kolom 28, dengan toleransi setengah
kolom karena panjang nama/caption genap-ganjil. Pemilik menerima hasil fisik
contoh penjualan tunai `a4e560d` pada 3 Oktober 2026. Ini tidak mengubah v1
atau membuktikan seluruh variasi v2; hasil dan batas uji dicatat di
[print-layout-validation.md](print-layout-validation.md).

Footer retur hanya menggunakan `TANDA TERIMA`, keterangan nota merah, dan
ruang tanda tangan khusus. Bukti piutang biasa memiliki susunan lain: nama
toko dan operator di bagian bawah. Jangan menyeragamkan footer semua jenis.

Kandidat piutang/retur v2, checkpoint 3 Oktober 2026 setelah foto batch:

- Piutang mempertahankan `HORMAT KAMI` di kolom **1**, bukan 25 seperti
  penjualan. Retur mempertahankan `TANDA TERIMA` di kolom **25**.
- `LayoutOriginalNameColumns` memakai pusat caption masing-masing. Jika
  center tidak muat, nama rata kanan; lebih dari 40 kolom dibungkus tanpa
  membuang teks. `LayoutReprintNameColumns` merencanakan baris pencetak ulang
  terpisah dan rata kanan. Normalisasi whitespace tetap berlaku.
- Renderer mencetak baris nama melalui `printer.Print(T(1), line)` dengan
  spasi kolom. **Jangan memakai `CurrentX` hasil `TextWidth` untuk footer**:
  foto batch memperlihatkan `SYNTHETIC OWNER` menjadi `SYNTHETIC` dan
  `KASIR ASAL/AWAL` menjadi `KASIR`, walaupun tes metrik murni dahulu lulus.
- Font 9 pt piutang/10 pt retur, header, angka dan caption lainnya tetap.
  Branding retur tetap terakhir; piutang tidak mendapat branding tambahan.
  Hasil fisik setelah perbaikan belum dibuktikan; batch uji ulang hanya
  lima bukti piutang dan dua retur, bukan mengulang penjualan.

### Catatan koreksi cetak ulang v2 — keputusan 3 Oktober 2026

Snapshot penerbitan mempertahankan identitas/angka asal, **bukan izin
mencetak seluruh audit**. Pemilik menyetujui draft ringkas setelah melihat
foto cetak ulang yang terlalu padat. Nota tanpa koreksi tidak memiliki
blok koreksi; nama asli tetap, kemudian “Dicetak ulang oleh: …” di footer.
Baris nomor/waktu CETAK ULANG yang sudah ada pada v1 tetap dipertahankan.

Untuk penjualan yang dikoreksi, blok sesudah jumlah hanya memuat:

```text
KOREKSI SETELAH NOTA DIBUAT:
Pelanggan: Andi
Diubah menjadi KASBON
Metode DP: TRANSFER

Angka di atas mengikuti nota asal.
```

Tampilkan hanya koreksi yang benar-benar ada. Koreksi metode yang bukan
konversi kasbon memakai “Metode: TUNAI/TRANSFER/EDC”, bukan METODE DP.
Nama pelanggan kosong memakai “Pelanggan: diperbarui”, tanpa mengarang nama
atau mencetak ID internal. Nilai DP/saldo terkini tidak direka dari nota
asal. ID pelanggan, waktu/pelaku setiap koreksi, dan “SALDO SEKARANG: LIHAT
RIWAYAT” tidak dicetak; data audit/snapshot dan validator tetap dipertahankan.
Nama panjang tetap dibungkus dengan helper pelanggan yang sama.

Fixture Unicode ekstrem tetap untuk regresi teknis; contoh penilaian
tampilan normal terpisah dari fixture tersebut. Jangan memperlakukan lolos
tes Unicode di Linux sebagai bukti glyph/posisi benar pada printer fisik.
Jangan menambahkan detail lain pada nota tanpa draft yang disetujui pemilik.

## 2. Epson LX-310 — Surat Jalan 82 Kolom

### Fungsi, kertas, dan font

Digunakan untuk `delivery_order`, dengan kertas surat jalan yang mendukung
area 82 kolom.

- Font: Courier New, 12 pt Regular.
- Lebar logis: 82 kolom.
- Maksimal 17 item per halaman.
- Sumber: [DeliveryOrder.vb](../src/SpikeTransport/DeliveryOrder.vb).

Kode tidak menetapkan ukuran kertas fisik tertentu dalam milimeter.
Pengaturan kertas dan kelayakan 82 kolom tetap harus diperiksa pada printer
sasaran. PDF A4 bukan bukti bahwa seluruh lebar cetak fisik sesuai.

### Cara membuat kalimat center, kiri, dan kanan

Gunakan prinsip kolom yang sama, tetapi dengan area **82**, bukan 40:

```vb
Dim text As String = "SURAT JALAN"
Dim centerColumn As Integer = ((82 - text.Length) \ 2) + 1
printer.Print(T(centerColumn), text)
```

Panjang `SURAT JALAN` adalah 11 karakter; posisi awal menjadi `TAB(36)`.
Rata kiri memakai `T(1)`; rata kanan memakai `T(82 - text.Length + 1)`.
Contoh ini berlaku untuk teks satu baris yang muat pada area tersebut.

**Ini contoh cara membuat teks center, bukan susunan judul v1 saat ini.**
Kode v1 mencetak judul di kolom 1. Dokumentasi tidak mengizinkan pemindahan
judul tersebut.

### Susunan dan pagination v1

- Judul di kiri, tanggal rata kanan.
- Identitas toko di kiri; pelanggan mulai kolom 52.
- QTY di kolom 2, UNIT di kolom 10, NAMA BARANG di kolom 20.
- Tidak mencetak harga atau total uang.
- Header diulang setiap halaman.
- Halaman terakhir diberi baris kosong hingga area 17 item terisi.
- `EndDoc` dipanggil setiap halaman.

Nama barang tidak memiliki wrapping eksplisit. Jangan menambah wrapping
tanpa mempertimbangkan tinggi baris dan pagination.

Perhitungan sisa item halaman terakhir memakai
`Math.Min(RowMax, itemTotal - startIdx)`, sehingga kelipatan 17 tetap mencetak
item halaman terakhir. Jangan mengembalikannya ke perhitungan `Mod` yang
dahulu menghasilkan halaman terakhir tanpa item.

## 3. XPrinter — Label 4 × 3 cm

### Fungsi dan mekanisme

Label barang dan label invoice menggunakan area sekitar **40 × 30 mm**.

Sumber: [QrLabel.vb](../src/SpikeTransport/QrLabel.vb).

Berbeda dari dua printer sebelumnya:

- Menggunakan `PrintDocument` dan GDI.
- Teks digambar melalui `DrawString`.
- QR dibuat dengan ZXing, lalu digambar melalui `DrawImage`.
- Tidak menggunakan grid `TAB` 40/82 kolom.
- Printer ditentukan melalui peran `QRLABEL`, bukan hardcode merek.

### Pemilihan ukuran label

`SelectLabelStock` mencari ukuran sekitar 40 × 30 mm dari daftar kertas
driver, termasuk orientasi terbalik.

`PaperSize.Width/Height` memakai satuan **1/100 inci**, bukan milimeter.
Target kode sekitar 157 × 118, dengan toleransi ±8.

Jika stock tidak ditemukan atau pencarian gagal, kode mempertahankan kertas
default printer. Karena itu, pemasangan printer belum membuktikan stock label
yang benar sudah terpilih. Penerimaan orientasi terbalik juga bukan bukti
layout otomatis diputar atau sudah lulus secara fisik.

### Format label saat ini

| Label | Teks | QR dan salinan |
|---|---|---|
| Barang | ID dan harga, Courier New 17 pt Bold | QR berisi ID; salinan dari payload dibatasi 1–20 |
| Invoice | Nomor invoice, Courier New 8 pt Bold | QR berisi nomor invoice; satu salinan per pemanggilan |

Teks dimulai pada koordinat `x = 10`, `y = 3`. Pada label barang, posisi
vertikal maju sebesar `fontSize + 5` setelah setiap baris teks; invoice
memiliki satu baris sebelum QR. QR dibuat sebagai bitmap 70 × 70 dengan
margin encoder nol.

Angka koordinat GDI tersebut tidak boleh dibaca sebagai milimeter. Ukuran
bitmap bukan jaminan ukuran QR fisik 70 mm atau 70 unit kertas. Layout saat
ini bukan layout teks center; jangan menganggapnya otomatis ditengahkan pada
label.

Batas salinan label barang tidak boleh disalin sebagai aturan salinan nota.
Jika `QRLABEL` belum dipetakan, jalur lama memakai printer default Windows
dengan peringatan pada hasil; dokumentasi ini tidak mengubah fallback itu.

## 4. Aturan Bersama dan Perlindungan Baseline

1. **Pertahankan formatting v1 yang sudah bekerja** pada bagian yang tidak
   perlu berubah. Amplop v1 dan bentuk cetakan tetap mengikuti kontrak.
2. Pisahkan perubahan isi nota dari perubahan mekanisme penempatan.
3. Jangan menganggap `TAB` setara dengan `CurrentX` hasil `TextWidth` tanpa
   pembandingan fisik. Inspeksi DLL PowerPacks menunjukkan `TAB` membentuk
   teks dengan spasi, sedangkan `TextWidth` mengukur melalui GDI.
4. Jangan mengecilkan font atau mengganti driver hanya karena `ScaleWidth`
   bertentangan dengan hasil kertas.
5. PowerPacks membuang Font lama saat diganti; jangan memakai ulang objek
   tersebut. V2 menggunakan `SetV2ReceiptFont`.
6. Build, preflight, dan respons sukses bukan bukti hasil cetak sudah rapi.
7. Validasi memakai printer, stock, dan fixture yang sama; periksa posisi
   tengah, tepi kanan, wrapping, angka, footer, serta pagination.

Renderer yang dibahas menjalankan pekerjaan printer di thread STA. Pada
jalur bersama, batas tunggu adalah 30 detik; timeout bukan bukti pekerjaan
spooler sudah dibatalkan atau bahwa tidak ada kertas yang keluar.

Pada pengujian TM-U220IIB tanggal 2 Oktober 2026, 40 karakter 9 pt tercetak
utuh. Foto v2 berikutnya yang dikirim pemilik memperlihatkan nama Siti
bergeser ke kanan dari pusat “HORMAT KAMI”; alignment belum lulus. Header v2
juga memakai mekanisme posisi berbeda dari v1 dan belum boleh dinyatakan
setara berdasarkan rumus saja. Hasil ini tidak boleh dicatat sebagai masalah
desain v1. Pada 3 Oktober, pemilik menyatakan cetakan perbaikan `a4e560d`
sudah oke; penerimaan tersebut terbatas pada contoh penjualan tunai yang
dikirim, bukan seluruh variasi nama/cetak ulang/piutang/retur.

Bukti terperinci berada di
[print-layout-validation.md](print-layout-validation.md). Foto pelanggan dan
bukti privat tidak dimasukkan ke Git. Bukti pengujian satu printer tidak
otomatis berlaku untuk printer lain atau seluruh variasi payload.

### Cara menggunakan dokumen ini

1. Pilih bagian printer yang akan diubah; jangan memakai aturan printer lain.
2. Baca implementasi terbaru, bukan hanya komentar, ringkasan, atau dokumen.
3. Bedakan perilaku v1, contoh penempatan, dan tambahan v2.
4. Perbarui acuan ini bersama perubahan formatting yang sudah diizinkan,
   dengan sumber dan bukti baru; jangan mengganti catatan gagal dengan lulus
   berdasarkan asumsi.
