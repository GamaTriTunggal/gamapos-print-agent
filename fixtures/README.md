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

`v2/delivery_note*.sample.json` (P-645 / CO-11) adalah salinan apa adanya dari
`gamapos-go-2/internal/platform/printagent/testdata/` — sumber kebenaran
kontraknya di repo Go. Ubah di sana dulu, lalu salin ulang ke sini.

`v2/*_long_code.sample.json` (IC-15 papan gamapos-go-2 `board-number-space`,
P-022) membawa kode 7 karakter (`CS10000`, `VJ10000`, `TN10000`) dan nomor nota
9 digit (`261110000`): nota retur, salinan nota jual dengan koreksi pelanggan,
dan bukti piutang (`KODE` + `BON`). Smoke Windows memutarnya ke printer virtual;
untuk uji kertas IC-17 cetak lewat `ci/V2PhysicalProbe --print`.
