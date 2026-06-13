# reference/

PDF "kebenaran" hasil cetak **app VB.NET lama** per jenis nota. Dipakai sebagai pembanding
saat verifikasi output agent (cetak ke "Microsoft Print to PDF" lalu diff) — lihat `02-testing.md`
di repo `gamapos`, lapis "output identik".

Cara membuat: di mesin yang masih menjalankan app VB.NET lama, set printer target ke
"Microsoft Print to PDF", cetak tiap jenis nota dengan data contoh yang sama seperti di `fixtures/`,
simpan di sini.

Konvensi nama: `<jobType>.reference.pdf` (mis. `cashier_receipt.reference.pdf`).

> Catatan: "Microsoft Print to PDF" me-reflow ke halaman A4 → akurat untuk konten & alignment
> kolom, tapi fidelity fisik (lebar kertas dot-matrix, spasi baris) tetap perlu printer asli di checkpoint.
