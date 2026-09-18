# Jurnal rilis Gama Print Agent

Satu baris per rilis. Kolom: versi · tanggal · commit · pre-release/penuh · isi · pilot · promosi · perilis.

| Versi | Tanggal | Commit | Saluran | Isi | Pilot | Promosi | Perilis |
|---|---|---|---|---|---|---|---|
| 1.0.0 | 30 Jun 2026 | — | penuh | transport + 14 jobType + targeting peran | — | 30 Jun 2026 | pemilik |
| 1.0.1 | 1 Jul 2026 | — | penuh | perbaikan awal | PC toko Vin Jaya (cetak kasir + QR Inv jalan) | 1 Jul 2026 | pemilik |
| 1.0.2 | 12 Jul 2026 | 2d184d5 | penuh | Pasang Otomatis TM-U220 (APD4 silent installer), `/setup/*`, `/printers/config` | VM saja (uji fisik ditunda) | 12 Jul 2026 | pemilik |
| 1.1.0 | 13 Sep 2026 06:46 UTC | 5f9c66f (isi: 6de58cf) | **PRE-RELEASE** (pilot PR-13; `releases/latest` tetap v1.0.2, armada tidak menerimanya) — 6 aset: Setup.exe 11.973.384 B, full.nupkg 7.494.920 B (SHA-256 `57C0597C…`), Portable.zip, RELEASES, releases.win.json, assets.win.json; dibangun pemilik di VM (dotnet 9.0.304, vpk 1.2.0) | PC uji pemilik (VM Win11 x64) 13 Sep: Setup OK, `/health` 1.1.0 + deviceId, `/recipes` benih TM-U220, Pasang Otomatis TM-U220 lewat staging selesai (idempoten); katalog server 404 sampai promote produksi; Vin Jaya belum | — | pemilik | PR-12 bagian agent: katalog resep dari server bertanda tangan Ed25519 (benih → disk → server, anti-rollback, SHA-256 paket 3×), `GET /recipes`, `POST /catalog/refresh`, `/health` + deviceId/osArch/versi assembly, `error` terstruktur `/setup/status`, `POST /print/test {printerRole}`, update diterapkan saat menganggur ≥10 mnt. Perilaku CETAK armada TIDAK berubah (aturan 3). | pilot = kedua toko pemilik (Vin Jaya + Vin Jaya 2, satu-satunya pemakai agen) lewat auto-update sesudah promosi; pengamatan N hari nota nyata (versi tiap PC terlihat di daftar "Komputer kasir toko ini", PR-14); 18 Sep 2026: pemilik melapor komputer kasir sudah 1.1.0 — auto-update bekerja di lapangan | **15 Sep 2026 14:59 WIB** — centang pre-release dihapus atas perintah pemilik ("promosi agen"); `releases/latest` → v1.1.0, feed `releases.win.json` 1.1.0 SHA-256 `57C0597C…` (bit identik, tanpa rebuild); jalan mundur bila perlu: centang pre-release lagi + Setup.exe 1.0.2 di PC | pemilik (eksekusi sesi printer) |
| (belum dirilis) | — | 72fc131 (23 Jul 2026) | — | resep Xprinter Seagull + `SelectLabelStock` 40×30 — MENUNGGU uji fisik XP-360B; `SelectLabelStock` berjalan di SETIAP cetak label = perubahan perilaku armada (aturan mutlak 3) | — | — | — |

Catatan: baris 1.0.0–1.0.2 direkonstruksi 5 Sep 2026 dari GitHub Releases + memori sesi; commit
1.0.0/1.0.1 tidak tercatat.

## Catatan gerbang

- 5 Sep 2026 — CI lahir (PR-03 papan printer): run pertama hijau di c0608bc (https://github.com/GamaTriTunggal/gamapos-print-agent/actions/runs/33968009972) setelah 3 iterasi smoke: "Microsoft Print to PDF" memunculkan Save As di jalur PowerPacks (17× PRINT_TIMEOUT) → printer virtual "Generic / Text Only" berport berkas → driver harus didaftarkan (`Add-PrinterDriver`) → bukti = berkas keluaran tidak kosong (port berkas menimpa per job). Sejak ini: rilis tanpa CI hijau = pelanggaran `docs/RELEASE.md`.
