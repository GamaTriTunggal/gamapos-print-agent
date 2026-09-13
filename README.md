# Gama Print Agent

Program tray Windows (.NET Framework 4.8, VB.NET) di tiap PC kasir **GamaPOS** — server HTTP lokal di
`http://localhost:9111`. Web app mengirim *data terstruktur* nota (JSON), agent yang menata layout dan
mencetak ke printer Windows (dot matrix / thermal / label) dengan kode cetak VB.NET lama (PrintDocument /
PowerPacks) — "Opsi B". Tanpa dialog cetak browser.

> **Aturan & kontraknya diputuskan di repo utama `gamapos-go-2`** — repo ini hanya implementasi.
> Baca dulu [`CLAUDE.md`](CLAUDE.md) (7 aturan mutlak: amplop `/print` beku, TIDAK PERNAH uninstall/
> downgrade driver, perilaku cetak armada = ketok pemilik, versi satu sumber, tanpa PII).
> Kontrak: `gamapos-go-2/docs/reference/print-agent-contract.md` · papan kerja:
> `gamapos-go-2/docs/analysis/board-printer-onboarding.md` · register temuan `P-xxx`:
> `gamapos-go-2/docs/parity-bug-register.md`. Dokumen asal Juni 2026 (repo Laravel
> `docs/companion-print-agent/`) READ-ONLY dan tinggal sejarah.

## Status rilis

| Versi | Saluran | Keadaan |
|---|---|---|
| **1.0.2** (12 Jul 2026) | rilis penuh — `releases/latest` | yang dipakai armada PC toko hari ini |
| **1.1.0** (13 Sep 2026) | **pre-release** (pilot) | agent pembaca katalog resep dari server; PC uji pemilik lulus; pilot Vin Jaya menyusul (papan printer PR-13) |

Ceklis rilis: [`docs/RELEASE.md`](docs/RELEASE.md) · jurnal tiap rilis: [`docs/release-journal.md`](docs/release-journal.md).
Satu rilis penuh = SELURUH armada dalam ≤ 6 jam (+ saat PC dimulai ulang) tanpa jalan pulang otomatis —
karena itu pilot dulu lewat GitHub **pre-release** (armada dan `releases/latest` mengabaikannya).

## Dua fungsi agent — jangan dicampur

| Fungsi | Printer apa pun? | Butuh "resep"? |
|---|---|---|
| **Mencetak** tanpa dialog browser (inti) | YA — merek/nama bebas, dipetakan per peran (`printers.json`) | tidak |
| Tombol **"Pasang Otomatis"** di web (bantu pasang driver + setelan) | hanya model yang punya resep | ya — sejak 1.1.0 resep = DATA katalog dari server (bukan kode); 1.0.2 hanya kenal `TM-U220` (tertanam) |

## Layout repo

```
src/SpikeTransport/      → seluruh kode agent (nama folder warisan spike Juni 2026; proyek SpikeTransport.vbproj)
  Program.vb             → HttpListener :9111 + router endpoint
  Printing.vb, *Receipt.vb, DeliveryOrder.vb, QrLabel.vb, AmountListSlip.vb → 14 jobType (layout nota)
  Printers.vb            → peta peran → printer Windows (printers.json), ganti default sesaat utk PowerPacks
  PrinterSetup.vb        → Pasang Otomatis: unduh paket golden (SHA-256), jalankan installer (apd | seagull)
  RecipeCatalog.vb       → katalog resep: benih → disk → server, verifikasi Ed25519, anti-rollback
  Updater.vb, Tray.vb, AutoStart.vb, AppPaths.vb → auto-update Velopack, ikon tray, Run key HKCU, jalur data
fixtures/                → 17 sample JSON per jobType (uji Lapis 1 + smoke CI); fixtures/catalog/ = katalog uji v0 bertanda tangan
reference/               → PDF "kebenaran" hasil cetak app VB.NET lama (pembanding visual)
lib/                     → Microsoft.VisualBasic.PowerPacks.dll (di-vendor; bukan NuGet)
ci/smoke.ps1             → smoke di runner Windows (lihat §CI)
docs/                    → RELEASE.md (ceklis), release-journal.md
pack.ps1                 → bangun installer + paket update (Velopack)
printers.example.json    → contoh peta peran → printer
```

## Build & jalankan

- **Build** (lintas-platform berkat `Microsoft.NETFramework.ReferenceAssemblies`; exe tetap hanya jalan di Windows):

  ```sh
  dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release
  ```

  Di Linux cukup .NET SDK 8 pengguna-lokal (`export PATH=$HOME/.dotnet:$PATH`). CI memakai SDK 8; VM pemilik SDK 9 — keduanya
  menghasilkan exe net48 yang sama.
- **Jalankan** (Windows): `src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe` — aplikasi tray (WinExe,
  tanpa jendela console; log ke berkas, lihat §Jalur data). Mutex tunggal: instance kedua langsung keluar.
- **"Access is denied"** saat start = HttpListener butuh URL ACL. Sekali saja sebagai Administrator:

  ```powershell
  netsh http add urlacl url=http://localhost:9111/ user=$env:USERNAME
  ```

### Uji cepat

```powershell
Invoke-RestMethod http://localhost:9111/health
Invoke-RestMethod -Uri http://localhost:9111/print -Method Post -ContentType application/json -InFile fixtures/cashier_receipt.sample.json
```

Dari browser (DevTools console) di mesin yang sama — `localhost` relatif ke tempat **browser** berjalan, jadi browser wajib
satu mesin dengan agent:

```js
fetch('http://localhost:9111/health').then(r => r.json()).then(console.log)
```

Body `/print` TIDAK disimpan kecuali debug di-opt-in (`GAMA_AGENT_DEBUG_JOBS=1` atau berkas `debug.flag` di folder data).

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
| OPTIONS | *                    | CORS preflight (`Access-Control-Allow-Origin: *`) |

Endpoint lain boleh tumbuh ADITIF (web wajib tetap bekerja melawan 1.0.2); amplop `POST /print` + 14 jobType BEKU.
Setiap rilis yang mengubah endpoint menambah bagian §C di kontrak (repo Go) + `P-xxx` — commit di sini menyebut `P-xxx`.

### Katalog resep (1.1.0; keputusan K-5 papan printer)

Resep "Pasang Otomatis" = DATA dari server, bukan kode: `GET https://app.gamapos.id/print-agent/catalog.json`
(amplop `{alg:"ed25519", keyId, payload(base64), signature}`), diverifikasi dengan kunci publik yang tertanam
(`RecipeCatalog.vb` — WAJIB sama dengan `pubkey.go` server; kunci privat hanya di laptop pemilik) SEBELUM dipakai; versi
tidak boleh mundur; salinan terakhir-berhasil di `%LOCALAPPDATA%\GamaPrintAgent\catalog\`; benih bawaan = TM-U220
(resep 1.0.2). Paket golden (R2 `installers.gamapos.id`) diverifikasi SHA-256 (cache maupun unduhan; 3 percobaan) —
objek yang tidak cocok katalog TIDAK dijalankan (`HASH_MISMATCH`). Disegarkan 15 dtk setelah start lalu tiap 6 jam
(bersama cek update). Override alamat: env `GAMA_AGENT_CATALOG_URL` — loopback saja. `fixtures/catalog/` = katalog uji
versi 0 bertanda tangan kunci nyata (sah / diubah / rusak) untuk smoke CI — jangan naikkan versinya (armada menyimpan ≥ 1).

## Jalur data di PC (bertahan lintas update — jangan simpan apa pun di folder exe)

Exe berjalan dari folder berversi Velopack (`%LOCALAPPDATA%\GamaPrintAgent\current\…`) yang DIGANTI tiap update.

| Jalur | Isi |
|---|---|
| `%LOCALAPPDATA%\GamaPrintAgent\printers.json` | peta peran → nama printer Windows (per PC; `GET /printers` menampilkan nama persis, `POST /printers/config` atau halaman Pengaturan → Printer di web menyimpannya). Peran tak dipetakan → printer default Windows. Contoh: `printers.example.json` |
| `…\GamaPrintAgent\logs\agent-YYYYMMDD.log` | log harian (14 hari), termasuk galat cetak |
| `…\GamaPrintAgent\setup\` · `…\catalog\` | cache paket golden · katalog terakhir-berhasil |
| `…\GamaPrintAgent\jobs\` · `debug.flag` · `default-printer-restore.txt` | body job (hanya saat debug) · sakelar debug · penanda pemulihan printer default |
| `%APPDATA%\GamaPrintAgent\device-id` | GUID perangkat (dibuat sekali, dilaporkan `/health`; bertahan lintas uninstall) |

Nota teks (PowerPacks) dicetak dengan mengganti printer default Windows **sesaat** lalu dikembalikan; label QR (PrintDocument)
langsung menyasar printer peran.

## CI

`.github/workflows/ci.yml` (windows-latest, tiap push/PR): grep larangan uninstall driver (aturan mutlak 2) → `dotnet build`
Release → `ci/smoke.ps1`: jalankan exe sebagai proses latar, tunggu `:9111`, periksa bentuk `/health` (versi = `<Version>`),
`/printers`, `/setup/status`, `/print/test`, katalog uji (sah/diubah/rusak), lalu putar 17 fixture ke `POST /print` dengan
`printers.json` yang memetakan semua peran ke printer virtual "Generic / Text Only" berport berkas (tanpa dialog). **Merah =
tidak boleh dirilis.**

## Rilis (hanya pemilik; VM Windows 64-bit)

1. Naikkan `<Version>` di `src/SpikeTransport/SpikeTransport.vbproj`; CI hijau; ceklis `docs/RELEASE.md`.
2. `dotnet tool install -g vpk --version 1.2.0` (sekali), lalu dari root repo: `.\pack.ps1 -Version X.Y.Z` → folder `Releases\`
   (`GamaPrintAgent-win-Setup.exe`, paket `.nupkg`, `RELEASES`, `releases.win.json`, `assets.win.json`; `publish/` dan
   `Releases/` gitignored).
3. GitHub Release `vX.Y.Z` sebagai **PRE-RELEASE** dengan semua aset → PC uji pemilik → pilot Vin Jaya (PR-13) → promosi =
   hapus centang pre-release (bit identik, tanpa rebuild). Web menaut `releases/latest/download/GamaPrintAgent-win-Setup.exe`;
   agent memperbarui diri dari `releases/latest` (Velopack, `GithubSource`) tiap 6 jam dan memasangnya saat menganggur.
4. Catat di `docs/release-journal.md` (siapa, kapan, versi, commit, isi, hasil pilot, promosi).

Installer belum bertanda tangan penerbit (SmartScreen "Unknown publisher" — T-5 papan printer): toko dipandu bergambar
"More info → Run anyway" di halaman Siapkan Printer. Rantai kepercayaan lengkap (akun GitHub, kunci katalog, R2):
`gamapos-go-2/docs/analysis/server-inventory.md` §Jalur eksekusi kode di PC toko.
