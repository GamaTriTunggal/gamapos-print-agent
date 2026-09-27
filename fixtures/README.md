# fixtures/

Sample payload JSON per jenis nota — untuk uji **Lapis 1** (lihat `02-testing.md` di repo `gamapos`).
Satu file per `jobType`, struktur mengikuti amplop job di `01-print-contract.md` (kontrak masih DRAFT, di-lock di M0).

Pakai dengan spike:

```powershell
Invoke-RestMethod -Uri http://localhost:9111/print -Method Post -ContentType application/json -InFile fixtures/cashier_receipt.sample.json
```

Konvensi nama: `<jobType>.sample.json` (mis. `cashier_receipt.sample.json`, `return_note.sample.json`, ...).

`v2/sale_cash.sample.json` adalah contoh **kontrak nota v2 yang belum aktif**
untuk tes parser murni, bukan job yang boleh dikirim ke agent rilis. Seluruh
fixture di akar folder tetap v1; smoke Windows saat ini mengharapkan 17 job
v1 dan menolak schema 2. Jangan memasukkan fixture v2 ke putaran cetak v1.
