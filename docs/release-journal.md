# Jurnal rilis Gama Print Agent

Satu baris per rilis. Kolom: versi · tanggal · commit · pre-release/penuh · isi · pilot · promosi · perilis.

| Versi | Tanggal | Commit | Saluran | Isi | Pilot | Promosi | Perilis |
|---|---|---|---|---|---|---|---|
| 1.0.0 | 30 Jun 2026 | — | penuh | transport + 14 jobType + targeting peran | — | 30 Jun 2026 | pemilik |
| 1.0.1 | 1 Jul 2026 | — | penuh | perbaikan awal | PC toko Vin Jaya (cetak kasir + QR Inv jalan) | 1 Jul 2026 | pemilik |
| 1.0.2 | 12 Jul 2026 | 2d184d5 | penuh | Pasang Otomatis TM-U220 (APD4 silent installer), `/setup/*`, `/printers/config` | VM saja (uji fisik ditunda) | 12 Jul 2026 | pemilik |
| (belum dirilis) | — | 72fc131 (23 Jul 2026) | — | resep Xprinter Seagull + `SelectLabelStock` 40×30 — MENUNGGU uji fisik XP-360B; `SelectLabelStock` berjalan di SETIAP cetak label = perubahan perilaku armada (aturan mutlak 3) | — | — | — |

Catatan: baris 1.0.0–1.0.2 direkonstruksi 5 Sep 2026 dari GitHub Releases + memori sesi; commit
1.0.0/1.0.1 tidak tercatat.

## Catatan gerbang

- 5 Sep 2026 — CI lahir (PR-03 papan printer): run pertama hijau di c0608bc (https://github.com/GamaTriTunggal/gamapos-print-agent/actions/runs/33968009972) setelah 3 iterasi smoke: "Microsoft Print to PDF" memunculkan Save As di jalur PowerPacks (17× PRINT_TIMEOUT) → printer virtual "Generic / Text Only" berport berkas → driver harus didaftarkan (`Add-PrinterDriver`) → bukti = berkas keluaran tidak kosong (port berkas menimpa per job). Sejak ini: rilis tanpa CI hijau = pelanggaran `docs/RELEASE.md`.
