# Petunjuk agent — gamapos-print-agent

Baca `CLAUDE.md` untuk kontrak agent, batas perubahan armada, dan alur uji/rilis.
Sebelum mengubah font, posisi, wrapping, pagination, preflight, atau renderer
cetakan, wajib baca `docs/printer-v1-formatting.md` pada bagian printer terkait
dan `docs/print-layout-validation.md`. V1 adalah baseline operasional yang
sudah teruji; pertahankan mekanisme kolom, font, dan susunan pada bagian yang
tidak perlu berubah. Penggantian dengan koordinat atau pengukuran baru perlu
pembandingan fisik, bukan asumsi kesetaraan. Jangan menyimpulkan ukuran font
dari `Printer.ScaleWidth` saja: bandingkan dengan hasil kertas pada printer
sasaran. Uji sintetis dan build tidak menggantikan persetujuan rilis.
PowerPacks memiliki Font aktif dan membuangnya saat diganti; gunakan
`SetV2ReceiptFont` untuk v2, tanpa memakai ulang objek Font lama.
