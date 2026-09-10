# Ceklis rilis Gama Print Agent

Satu rilis = SELURUH armada PC kasir (≤ 6 jam + restart PC), tanpa jalan pulang otomatis.
Setiap butir wajib dicentang; hasilnya dicatat di `release-journal.md`.

## Sebelum `pack.ps1`

- [ ] CI hijau di commit yang akan dirilis (`.github/workflows/ci.yml`).
- [ ] `<Version>` di `SpikeTransport.vbproj` dinaikkan; `AgentVersion` di `Program.vb` SAMA
      (sampai PR-12 menyatukannya).
- [ ] Tidak ada jalur kode uninstall/downgrade driver (aturan mutlak 2): `grep -ri "uninstall\|printui\|Remove-Printer" src/` hanya mengenai komentar.
- [ ] Perubahan endpoint dicatat di `gamapos-go-2/docs/reference/print-agent-contract.md` §C
      (delta berversi) + `P-xxx` register — commit repo Go menyebut commit repo ini.
- [ ] Perubahan perilaku cetak armada (aturan mutlak 3) sudah DIKETOK pemilik dan disebut di
      catatan rilis; bila belum, cabut dari rilis.
- [ ] Hash SHA-256 paket golden yang dirujuk resep/katalog dicatat di catatan rilis.

## `pack.ps1` + unggah

- [ ] `pack.ps1 -Version X` di VM Windows 64-bit (bit-spesifik).
- [ ] GitHub Release `vX` dibuat sebagai **PRE-RELEASE** (pilot) — 6 aset dari `Releases\`.
- [ ] PC uji pemilik memasang `Setup.exe` pre-release; `/health` melaporkan versi baru.

## Pilot (papan printer PR-13)

- [ ] Vin Jaya: keadaan kedua PC dicatat SEBELUM disentuh; ganti di luar jam layanan; jalan
      kembali diuji (< 1 menit); N hari nota nyata.
- [ ] Hasil pilot ditulis di jurnal.

## Promosi

- [ ] Hapus centang pre-release (bit identik, tanpa rebuild).
- [ ] Hapus pre-release lama yang menumpuk (feed Velopack membaca 10 rilis terakhir).
- [ ] Baris jurnal ditutup dengan tanggal promosi.
