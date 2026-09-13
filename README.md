# Gama Print Agent

Companion print agent untuk **GamaPOS** — server HTTP lokal di `http://localhost:9111` pada
tiap PC kasir Windows. Menerima job cetak (JSON) dari web app dan mencetaknya ke printer USB
(dot matrix / thermal) dengan **reuse kode cetak VB.NET (PrintDocument / PowerPacks)**.
Arsitektur "Opsi B": web mengirim *data terstruktur*, agent yang menata layout.

> **Desain & kontrak (sumber kebenaran) ada di repo `gamapos`:** `docs/companion-print-agent/`
> — `00-architecture.md` (keputusan desain), `01-print-contract.md` (kontrak JSON),
> `02-testing.md` (strategi uji 3-lapis), `README.md` (tracker progres M0–M5).

## Status

**Step 1 — Transport spike.** Membuktikan browser ↔ `http://localhost:9111` (CORS /
mixed-content) **tanpa mencetak**: `POST /print` hanya menyimpan JSON yang diterima ke file.
Printing nyata (PowerPacks) menyusul di Step 2.

## Tech

- **.NET Framework 4.8**, **VB.NET** (dipilih agar kode cetak VB.NET lama bisa di-port nyaris verbatim).
- Build/run **hanya di Windows**. Alur dev dari Linux via VM Windows: lihat `02-testing.md` di repo `gamapos`.

## Layout

```
src/SpikeTransport/   → Step 1 transport spike (console, HttpListener, TANPA printing)
fixtures/             → sample JSON payload per jenis nota (uji Lapis 1)
reference/            → PDF "kebenaran" hasil cetak app VB.NET lama (untuk diff)
```

## Build & run (di Windows)

Prasyarat: .NET SDK / Visual Studio 2022 + Build Tools dengan targeting pack **.NET Framework 4.8**.

```powershell
dotnet run --project src/SpikeTransport
# atau: buka folder di Visual Studio 2022, jalankan project SpikeTransport
```

> `dotnet run` untuk `net48` butuh environment Build Tools/Developer (bukan hanya SDK dotnet biasa).
> Cara paling andal: buka folder di Visual Studio 2022 lalu Run. Jika `dotnet run` error, jalankan
> `dotnet build` lalu eksekusi exe di `bin\Debug\net48\`.

Jika muncul **"Access is denied"** saat start (HttpListener butuh URL ACL), jalankan sekali sebagai Administrator:

```powershell
netsh http add urlacl url=http://localhost:9111/ user=$env:USERNAME
```

...atau jalankan agent sebagai Administrator.

## Uji cepat — Lapis 1 (tanpa Laravel, semua di VM)

```powershell
Invoke-RestMethod http://localhost:9111/health
Invoke-RestMethod -Uri http://localhost:9111/print -Method Post -ContentType application/json -InFile fixtures/cashier_receipt.sample.json
# body tersimpan ke folder jobs/ di samping exe (bin\Debug\net48\jobs\)
```

Uji transport browser — **Lapis 2 (CORS)**: buka DevTools console di browser **di dalam VM**:

```js
fetch('http://localhost:9111/health').then(r => r.json()).then(console.log)
```

> Ingat: `localhost` relatif ke tempat **browser** berjalan → browser harus satu mesin dengan agent.

## Endpoints (v1.1.0 — kontrak: `gamapos-go-2/docs/reference/print-agent-contract.md`)

| Method  | Path                 | Balasan |
|---------|----------------------|---------|
| GET     | `/health`            | `{ ok, agentVersion, schemaVersion, mode, deviceId, osArch, catalogVersion, catalogSource }` — versi dibaca dari assembly (`<Version>` vbproj = satu sumber) |
| GET     | `/printers`          | `{ ok, installed[], roles{CASHIER,DELIVERY,QRLABEL,REPORT}, default }` |
| POST    | `/printers/config`   | simpan peta peran → `printers.json` |
| POST    | `/print`             | amplop job v1 (BEKU) → cetak ke printer peran |
| POST    | `/print/test`        | tanpa body: halaman uji ke PDF; body `{printerRole}`: halaman uji ke printer PERAN (`{ok, printer, role}`; `ROLE_UNMAPPED` bila belum dipetakan) |
| GET     | `/recipes`           | model yang DIKENAL agent ini (dari katalog; resep `disabled` disembunyikan) — gerbang fakta tombol Pasang Otomatis (P-573) |
| POST    | `/setup/printer`     | `{model}` → mulai pemasangan (async); `UNSUPPORTED_MODEL` bila tak ada di katalog |
| GET     | `/setup/status`      | `{ ok, state, model, printer, role, message, error }` — `error` = kode terstruktur saat `failed` (`DOWNLOAD_FAILED`, `HASH_MISMATCH`, `UAC_TIMEOUT`, `INSTALL_FAILED`, `INSTALL_START_FAILED`, `EXTRACT_FAILED`, `PACKAGE_INVALID`, `UNSUPPORTED_KIND`, `SETUP_FAILED`) |
| POST    | `/catalog/refresh`   | segarkan katalog resep sekarang; body opsional `{url}` HANYA loopback (smoke CI) → `{ok, catalogVersion, source, error?}` |
| OPTIONS | *                    | CORS preflight |

### Katalog resep (PR-12, K-5)

Resep "Pasang Otomatis" = DATA dari server, bukan kode: `GET https://app.gamapos.id/print-agent/catalog.json`
(amplop `{alg:"ed25519", keyId, payload(base64), signature}`), diverifikasi dengan kunci publik yang tertanam
(`RecipeCatalog.vb`, sama dengan `pubkey.go` server) SEBELUM dipakai; versi tidak boleh mundur; salinan
terakhir-berhasil di `%LOCALAPPDATA%\GamaPrintAgent\catalog\`; benih bawaan = TM-U220 (resep v1.0.2).
Paket golden diverifikasi SHA-256 (cache maupun unduhan; 3 percobaan). Disegarkan 15 dtk setelah start lalu
tiap 6 jam (bersama cek update). Override alamat: env `GAMA_AGENT_CATALOG_URL` — loopback saja.
`fixtures/catalog/` = katalog uji versi 0 yang ditandatangani kunci pemilik (sah / diubah / rusak) untuk smoke CI.

## Targeting printer per-role (printers.json)

Tiap job punya `printerRole` (CASHIER/DELIVERY/QRLABEL/REPORT). Agent memetakan role → nama printer Windows
lewat **`printers.json` di samping exe** (mis. `bin\Debug\net48\printers.json`). Lihat `printers.example.json`.

- Copy `printers.example.json` → `printers.json` di folder exe, sesuaikan nama printer (lihat nama persis via `GET /printers`).
- Role **tidak dipetakan** atau `printers.json` tak ada → job dicetak ke **printer default Windows** (jadi tes di VM/PDF tetap jalan tanpa setup).
- Nota teks (PowerPacks) → agent mengganti default Windows **sesaat** saat cetak lalu dikembalikan. Label QR (PrintDocument) → target printer langsung.
- `printers.json` **tidak** di-commit (per-PC; ada di folder `bin/` yang gitignored). Yang di-commit hanya `printers.example.json`.

## Next — Step 2 (silent print)

Port `frmCashier.printReceipt` ke handler `cashier_receipt`, lepaskan dari kontrol form
(jadi field payload), cetak ke "Microsoft Print to PDF", lalu diff vs `reference/`.

> **Gotcha PowerPacks:** `Microsoft.VisualBasic.PowerPacks` (`Printing.Compatibility.VB6`) =
> assembly legacy, **bukan NuGet**, hanya .NET Framework. Pastikan DLL-nya tersedia di mesin
> (salin dari proyek VB.NET lama / install redistributable) sebelum port kode cetak.
