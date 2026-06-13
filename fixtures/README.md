# fixtures/

Sample payload JSON per jenis nota — untuk uji **Lapis 1** (lihat `02-testing.md` di repo `gamapos`).
Satu file per `jobType`, struktur mengikuti amplop job di `01-print-contract.md` (kontrak masih DRAFT, di-lock di M0).

Pakai dengan spike:

```powershell
Invoke-RestMethod -Uri http://localhost:9111/print -Method Post -ContentType application/json -InFile fixtures/cashier_receipt.sample.json
```

Konvensi nama: `<jobType>.sample.json` (mis. `cashier_receipt.sample.json`, `return_note.sample.json`, ...).
