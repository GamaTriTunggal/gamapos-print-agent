# CLAUDE.md — gamapos-print-agent

Gama Print Agent = program tray Windows (.NET Framework 4.8, VB.NET) di tiap PC kasir GamaPOS,
`http://localhost:9111`. Kontrak dan aturannya DIPUTUSKAN di repo utama `gamapos-go-2`; repo ini
hanya implementasi. Lahir 5 Sep 2026 lewat item PR-03 papan
`gamapos-go-2/docs/analisis/printer-onboarding-board.md`.

## Dua fungsi agent — jangan dicampur

| Fungsi | Printer apa pun? | Butuh "resep"? |
|---|---|---|
| Mencetak tanpa dialog browser (INTI) | YA — merek/nama bebas, dipetakan per peran (`printers.json`) | tidak |
| Tombol "Pasang Otomatis" (bantu pasang driver) | hanya model yang punya resep | ya — sejak PR-12 resep = DATA katalog dari server, bukan kode |

Menambah printer yang didukung TIDAK boleh lagi menuntut rilis agent (setelah PR-12).

## ATURAN MUTLAK

1. **Amplop `POST /print` v1 + 14 jobType BEKU** (K-02 amandemen 5 Sep 2026). Bentuk nota tidak
   berubah. Mengubahnya = keputusan pemilik di `gamapos-go-2/docs/keputusan.md` + naikkan
   `schemaVersion`. Sumber kontrak yang boleh ditulis:
   `gamapos-go-2/docs/referensi/print-agent-contract.md` — setiap rilis yang mengubah endpoint
   lain WAJIB menambah bagian §C (delta berversi) di sana, di commit repo Go yang menyebut commit
   repo ini (kewajiban dua arah: commit di sini menyebut `P-xxx` register repo Go).
2. **Agent TIDAK PERNAH menghapus, menurunkan, atau meng-uninstall driver/antrean printer apa
   pun** (keputusan pemilik 5 Sep 2026, K-4 papan printer). Tidak boleh ada jalur kode
   `/s /uninstall`, `printui /dl`, `Remove-Printer`, atau padanannya. Obat resmi Epson bila
   driver lama menghalangi = UPDATE (APD4 ≥ 4.56), bukan hapus.
3. **Perubahan perilaku CETAK armada yang sudah berjalan = ketok pemilik terpisah** sebelum
   ditulis: menolak job saat printer offline, menolak cetak bila peran tidak dipetakan (hari ini
   jatuh ke printer default Windows — perilaku terdokumentasi), pemilihan stock label
   (`SelectLabelStock`, commit 72fc131). Endpoint/field baru yang ADITIF bebas, dengan syarat
   web tetap bekerja melawan agent v1.0.2.
4. **Kompatibilitas mundur adalah kontrak**: field lama tidak berubah arti; `state` lama
   (`idle|running|done|failed`) tetap; endpoint baru yang tidak ada di agent lama = web jatuh
   ke jalur lama.
5. **Versi SATU sumber**: `<Version>` di `src/SpikeTransport/SpikeTransport.vbproj`. `AgentVersion`
   yang dilaporkan `/health` WAJIB dibaca dari assembly (PR-12), bukan konstanta terpisah —
   sampai itu terjadi, ceklis rilis mewajibkan keduanya dinaikkan bersama.
6. **Tidak ada telemetri/penyimpanan PII**: body `/print` berisi data pelanggan toko; hanya
   disimpan bila debug di-opt-in (`GAMA_AGENT_DEBUG_JOBS=1` / `debug.flag`). Jangan tambah log
   yang memuat isi nota.
7. **Identifier & komentar**: identifier BARU bahasa Inggris (aturan repo Go 17 Agt 2026);
   komentar boleh Indonesia. Nama berkas `.vb` baru = Inggris.

## Jalur data yang tidak boleh berpindah

- `%LOCALAPPDATA%\GamaPrintAgent\` = `printers.json`, `jobs/`, `logs/`, `setup/` (cache paket),
  penanda pulih default printer — bertahan lintas update Velopack. Folder exe (`...\current\`)
  DIGANTI tiap update; jangan simpan apa pun di sana.
- `device-id` (PR-12) di `%APPDATA%\GamaPrintAgent\` — di LUAR folder instalasi, bertahan
  lintas uninstall.

## Build, uji, rilis

- Build lintas-platform: `dotnet build src/SpikeTransport/SpikeTransport.vbproj -c Release`
  (paket `Microsoft.NETFramework.ReferenceAssemblies` membuatnya jalan di Linux/CI; exe tetap
  hanya jalan di Windows). PowerPacks di-vendor di `lib/`.
- **CI** (`.github/workflows/ci.yml`, `windows-latest`): build + smoke `ci/smoke.ps1` —
  menjalankan exe sebagai proses latar, menunggu `:9111`, memeriksa bentuk `/health`, `/printers`,
  `/setup/status`, `/print/test`, memutar 18 `fixtures/*.sample.json` ke `POST /print` dengan
  `printers.json` yang memetakan SEMUA peran ke "Microsoft Print to PDF", lalu mematikan proses.
  Merah = tidak boleh dirilis.
- **Rilis** = `pack.ps1 -Version X` di VM Windows → unggah isi `Releases\` ke GitHub Release
  `vX`. Ceklis WAJIB di `docs/RELEASE.md`; tiap rilis dicatat di `docs/release-journal.md`
  (siapa, kapan, versi, commit, isi, hasil pilot). **Saluran pilot = GitHub PRE-RELEASE**
  (armada Velopack dan `releases/latest/download` mengabaikannya); promosi = hapus centang
  pre-release, tanpa rebuild. Jangan menumpuk > 9 pre-release lebih baru dari rilis penuh.
- Rilis penuh = SELURUH armada dalam ≤ 6 jam + saat PC dimulai ulang, dan tidak ada jalan
  pulang otomatis (Velopack tidak pernah menawarkan versi lebih rendah). Karena itu: pilot dulu
  di PC uji pemilik + Vin Jaya (papan printer PR-13).

## Rujukan

- Papan: `gamapos-go-2/docs/analisis/printer-onboarding-board.md` (PR-xx, T-x, tabel skenario).
- Kontrak: `gamapos-go-2/docs/referensi/print-agent-contract.md`.
- Keputusan: `gamapos-go-2/docs/keputusan.md` K-02 + amandemen 5 Sep 2026.
- Register temuan: `gamapos-go-2/docs/register-bug-paritas.md` (`P-xxx`).
- Dokumen asal (READ-ONLY): repo Laravel `docs/companion-print-agent/`.
