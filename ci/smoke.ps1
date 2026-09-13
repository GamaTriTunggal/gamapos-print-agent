# Smoke test Gama Print Agent di CI (windows-latest).
# Menjalankan exe hasil build sebagai proses latar, menunggu :9111, memeriksa bentuk balasan
# endpoint, memutar semua fixtures ke POST /print dengan semua peran dipetakan ke printer virtual
# berport berkas, menguji katalog resep bertanda tangan (PR-12: sah/diubah/rusak/non-loopback),
# kode galat pemasangan terstruktur, dan /print/test per peran, lalu mematikan proses.
# Exit 1 bila ada yang meleset.
#
# Pakai lokal (PowerShell, Windows): .\ci\smoke.ps1 -Exe .\src\SpikeTransport\bin\Release\net48\GamaPrintAgent.SpikeTransport.exe
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [int]$TimeoutSec = 60
)
$ErrorActionPreference = "Stop"
$base = "http://localhost:9111"
$fail = @()

function Fail($msg) { $script:fail += $msg; Write-Host "FAIL  $msg" -ForegroundColor Red }
function Ok($msg)   { Write-Host "ok    $msg" -ForegroundColor Green }

# 1) Printer virtual TANPA dialog: antrean "Generic / Text Only" berport BERKAS TETAP.
#    ("Microsoft Print to PDF" memunculkan dialog Save As pada jalur PowerPacks (EndDoc tanpa
#    PrintToFile) → PRINT_TIMEOUT di runner — insiden run pertama 5 Sep 2026.)
#    Keluaran nota ditulis ke $outFile; ukurannya = bukti cetak benar-benar terjadi.
$outDir  = Join-Path $env:RUNNER_TEMP "gama-print-out"
if (-not $env:RUNNER_TEMP) { $outDir = Join-Path $env:TEMP "gama-print-out" }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "smoke.prn"
$vp = "Gama Smoke Printer"
$hasPdf = $false
try {
    if (-not (Get-PrinterPort -Name $outFile -ErrorAction SilentlyContinue)) { Add-PrinterPort -Name $outFile }
    # Driver "Generic / Text Only" ada di driver store Windows tetapi belum terdaftar di runner
    # (run 2, 5 Sep: "The specified driver does not exist") → daftarkan dulu; cadangan = driver
    # apa pun yang sudah terdaftar (mis. Microsoft Print To PDF) — tetap tanpa dialog karena port berkas.
    $drv = "Generic / Text Only"
    if (-not (Get-PrinterDriver -Name $drv -ErrorAction SilentlyContinue)) {
        try { Add-PrinterDriver -Name $drv } catch { Write-Host "Add-PrinterDriver '$drv' gagal: $($_.Exception.Message)" -ForegroundColor Yellow }
    }
    if (-not (Get-PrinterDriver -Name $drv -ErrorAction SilentlyContinue)) {
        $drv = (Get-PrinterDriver | Select-Object -First 1).Name
        Write-Host "memakai driver cadangan: $drv" -ForegroundColor Yellow
    }
    if (-not (Get-Printer -Name $vp -ErrorAction SilentlyContinue)) {
        Add-Printer -Name $vp -DriverName $drv -PortName $outFile
    }
    $hasPdf = $true
    Write-Host "printer virtual '$vp' (driver '$drv') -> $outFile"
} catch { Write-Host "gagal membuat printer virtual: $($_.Exception.Message)" -ForegroundColor Yellow }

# printers.json: semua peran -> printer virtual (folder data stabil agent, bukan samping exe).
$dataDir = Join-Path $env:LOCALAPPDATA "GamaPrintAgent"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
@{ CASHIER = $vp; DELIVERY = $vp; QRLABEL = $vp; REPORT = $vp } | ConvertTo-Json | Set-Content (Join-Path $dataDir "printers.json") -Encoding UTF8

# 1b) PR-12: server statis katalog uji (fixtures/catalog, ditandatangani kunci pemilik; versi 0) di
#     loopback :9112 — python bawaan runner. Agent diarahkan ke sana lewat env (override HANYA loopback).
$catDir = Resolve-Path (Join-Path $PSScriptRoot "..\fixtures\catalog")
$catSrv = $null
try {
    $catSrv = Start-Process python -ArgumentList @("-m", "http.server", "9112", "--bind", "127.0.0.1", "--directory", "$catDir") -PassThru -WindowStyle Hidden
    $ok9112 = $false
    foreach ($i in 1..20) { try { Invoke-RestMethod "http://127.0.0.1:9112/catalog-ok.json" -TimeoutSec 2 | Out-Null; $ok9112 = $true; break } catch { Start-Sleep -Milliseconds 500 } }
    if (-not $ok9112) { Write-Host "server katalog uji :9112 tidak hidup — pemeriksaan katalog akan gagal" -ForegroundColor Yellow }
} catch { Write-Host "python http.server gagal: $($_.Exception.Message)" -ForegroundColor Yellow }
$env:GAMA_AGENT_CATALOG_URL = "http://127.0.0.1:9112/catalog-ok.json"

# 2) Jalankan agent (WinExe tray; mutex single-instance; auto-start HKCU ditulis — runner sekali pakai).
$proc = Start-Process -FilePath $Exe -PassThru -WindowStyle Hidden
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $up = $false
    while ((Get-Date) -lt $deadline) {
        try { $h = Invoke-RestMethod "$base/health" -TimeoutSec 3; $up = $true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $up) { Fail "agent tidak mendengarkan :9111 dalam $TimeoutSec dtk"; throw "down" }

    # 3) Bentuk /health — field wajib kontrak §A (ok, agentVersion, schemaVersion).
    if ($h.ok -ne $true) { Fail "/health ok != true" } else { Ok "/health ok" }
    if (-not $h.agentVersion) { Fail "/health tanpa agentVersion" } else { Ok "/health agentVersion=$($h.agentVersion)" }
    if ($h.schemaVersion -ne 1) { Fail "/health schemaVersion != 1 (amplop v1 beku)" } else { Ok "/health schemaVersion=1" }
    $vbproj = Get-Content (Join-Path $PSScriptRoot "..\src\SpikeTransport\SpikeTransport.vbproj") -Raw
    if ($vbproj -match "<Version>([^<]+)</Version>") {
        if ($Matches[1] -ne $h.agentVersion) { Fail "versi dua sumber tidak sama: vbproj=$($Matches[1]) /health=$($h.agentVersion) (aturan mutlak 5)" } else { Ok "versi vbproj == /health" }
    }

    # 4) /printers — installed[], roles{4 peran}, default.
    $p = Invoke-RestMethod "$base/printers" -TimeoutSec 5
    if ($p.ok -ne $true) { Fail "/printers ok != true" }
    if ($null -eq $p.installed) { Fail "/printers tanpa installed[]" }
    foreach ($r in "CASHIER","DELIVERY","QRLABEL","REPORT") {
        if (-not ($p.roles.PSObject.Properties.Name -contains $r)) { Fail "/printers roles tanpa $r" }
    }
    if ($fail.Count -eq 0) { Ok "/printers bentuk" }

    # 5) /setup/status — state idle + field lama.
    $s = Invoke-RestMethod "$base/setup/status" -TimeoutSec 5
    if ($s.state -ne "idle") { Fail "/setup/status state awal != idle ($($s.state))" } else { Ok "/setup/status idle" }
    foreach ($f in "model","printer","role","message") { if (-not ($s.PSObject.Properties.Name -contains $f)) { Fail "/setup/status tanpa field $f" } }

    # 6) Origin terlarang ditolak (403 FORBIDDEN_ORIGIN) — pagar CSRF.
    try {
        Invoke-WebRequest "$base/setup/status" -Method Post -Headers @{ Origin = "https://evil.example" } -Body "{}" -ContentType "application/json" -TimeoutSec 5 | Out-Null
        Fail "POST dari origin asing tidak ditolak"
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 403) { Ok "origin asing -> 403" } else { Fail "origin asing -> $($_.Exception.Message)" }
    }

    # 7) Route tak dikenal -> 404 NOT_FOUND.
    try { Invoke-WebRequest "$base/nope" -TimeoutSec 5 | Out-Null; Fail "/nope tidak 404" }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 404) { Ok "/nope -> 404" } else { Fail "/nope -> $($_.Exception.Message)" } }

    # 8) /print/test memakai "Microsoft Print to PDF" + PrintToFile (tanpa dialog) — jalur sendiri.
    $t = Invoke-RestMethod "$base/print/test" -Method Post -TimeoutSec 60
    if ($t.ok -ne $true -or -not $t.printed) { Fail "/print/test gagal: $($t | ConvertTo-Json -Compress)" } else { Ok "/print/test -> $($t.printed)" }

    # 9) Cetak 17 fixtures ke printer virtual (PowerPacks → default Windows diganti sesaat oleh
    #    WithRolePrinter; label QR → PrinterSettings.PrinterName). Bukti = berkas keluaran bertambah.
    if ($hasPdf) {
        $fixtures = Get-ChildItem (Join-Path $PSScriptRoot "..\fixtures\*.sample.json")
        if ($fixtures.Count -lt 17) { Fail "fixtures < 17 (ada $($fixtures.Count))" }
        foreach ($f in $fixtures) {
            $body = Get-Content $f.FullName -Raw
            try {
                $r = Invoke-RestMethod "$base/print" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 90
                if ($r.ok -ne $true) { Fail "$($f.Name): $($r | ConvertTo-Json -Compress)" } else { Ok "$($f.Name) -> $($r.jobType)" }
            } catch { Fail "$($f.Name): $($_.Exception.Message)" }
        }
        Start-Sleep -Seconds 3
        # Port berkas tetap MENIMPA per job (isi = job terakhir), dan "Generic / Text Only" merender
        # nota GDI jadi teks polos ratusan byte (run 3: 926 byte untuk split_receipt) → bukti yang
        # benar = berkas ada dan tidak kosong, bukan ambang ukuran.
        $size = (Get-Item $outFile -ErrorAction SilentlyContinue).Length
        if (-not $size -or $size -le 0) { Fail "keluaran printer virtual kosong — job tidak sampai ke spooler" } else { Ok "keluaran printer virtual $size byte (job terakhir)" }
        # Amplop rusak -> BAD_PAYLOAD, bukan 500.
        $bad = Invoke-RestMethod "$base/print" -Method Post -ContentType "application/json" -Body '{"schemaVersion":1}' -TimeoutSec 10
        if ($bad.ok -ne $false -or $bad.error -ne "BAD_PAYLOAD") { Fail "amplop rusak tidak BAD_PAYLOAD: $($bad | ConvertTo-Json -Compress)" } else { Ok "amplop rusak -> BAD_PAYLOAD" }
    } else {
        Fail "printer virtual tidak bisa dibuat — cetak fixtures tidak teruji"
    }

    # 10) PR-12 — /health field aditif (deviceId GUID, osArch, katalog); versi dari assembly.
    if (-not ($h.deviceId -match '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')) { Fail "/health deviceId bukan GUID: $($h.deviceId)" } else { Ok "/health deviceId GUID" }
    if ($h.osArch -notin @("x64", "x86", "arm64")) { Fail "/health osArch aneh: $($h.osArch)" } else { Ok "/health osArch=$($h.osArch)" }
    if ($h.catalogVersion -ne 0 -or $h.catalogSource -notin @("seed", "server")) { Fail "/health katalog awal bukan v0 benih/server: $($h.catalogSource)/$($h.catalogVersion)" } else { Ok "/health katalog v0 ($($h.catalogSource))" }
    $h2 = Invoke-RestMethod "$base/health" -TimeoutSec 5
    if ($h2.deviceId -ne $h.deviceId) { Fail "deviceId berubah antar panggilan" } else { Ok "deviceId stabil" }

    # 11) PR-12 — katalog resep bertanda tangan: sah diterima, diubah/rusak/non-loopback ditolak.
    $rc = Invoke-RestMethod "$base/recipes" -TimeoutSec 5
    if ($rc.ok -ne $true -or -not (@($rc.recipes.model) -contains "TM-U220")) { Fail "/recipes tanpa TM-U220: $($rc | ConvertTo-Json -Compress)" } else { Ok "/recipes memuat TM-U220" }
    $okc = Invoke-RestMethod "$base/catalog/refresh" -Method Post -ContentType "application/json" -Body '{"url":"http://127.0.0.1:9112/catalog-ok.json"}' -TimeoutSec 30
    if ($okc.ok -ne $true -or $okc.source -ne "server" -or $okc.catalogVersion -ne 0) { Fail "refresh katalog sah gagal: $($okc | ConvertTo-Json -Compress)" } else { Ok "refresh katalog sah -> server v0" }
    $rc = Invoke-RestMethod "$base/recipes" -TimeoutSec 5
    if (-not (@($rc.recipes.model) -contains "TEST-PRINTER")) { Fail "/recipes tanpa TEST-PRINTER setelah refresh: $($rc | ConvertTo-Json -Compress)" } else { Ok "/recipes memuat TEST-PRINTER" }
    if (@($rc.recipes.model) -contains "DISABLED-PRINTER") { Fail "/recipes menawarkan resep disabled" } else { Ok "/recipes menyembunyikan resep disabled" }
    $badc = Invoke-RestMethod "$base/catalog/refresh" -Method Post -ContentType "application/json" -Body '{"url":"http://127.0.0.1:9112/catalog-bad.json"}' -TimeoutSec 30
    if ($badc.ok -ne $false -or $badc.error -ne "SIGNATURE_INVALID") { Fail "katalog diubah tidak ditolak: $($badc | ConvertTo-Json -Compress)" } else { Ok "katalog diubah -> SIGNATURE_INVALID" }
    $rc2 = Invoke-RestMethod "$base/recipes" -TimeoutSec 5
    if ((@($rc2.recipes.model) -contains "EVIL-PRINTER") -or -not (@($rc2.recipes.model) -contains "TEST-PRINTER")) { Fail "/recipes berubah setelah katalog ditolak" } else { Ok "/recipes tetap setelah katalog ditolak" }
    $brk = Invoke-RestMethod "$base/catalog/refresh" -Method Post -ContentType "application/json" -Body '{"url":"http://127.0.0.1:9112/catalog-broken.json"}' -TimeoutSec 30
    if ($brk.ok -ne $false -or $brk.error -ne "CATALOG_ENVELOPE_INVALID") { Fail "amplop rusak tidak ditolak: $($brk | ConvertTo-Json -Compress)" } else { Ok "amplop rusak -> CATALOG_ENVELOPE_INVALID" }
    $rej = Invoke-RestMethod "$base/catalog/refresh" -Method Post -ContentType "application/json" -Body '{"url":"https://evil.example/catalog.json"}' -TimeoutSec 30
    if ($rej.ok -ne $false -or $rej.error -ne "CATALOG_URL_REJECTED") { Fail "override alamat non-loopback tidak ditolak: $($rej | ConvertTo-Json -Compress)" } else { Ok "override non-loopback -> CATALOG_URL_REJECTED" }
    $h3 = Invoke-RestMethod "$base/health" -TimeoutSec 5
    if ($h3.catalogSource -ne "server") { Fail "/health catalogSource != server setelah refresh ($($h3.catalogSource))" } else { Ok "/health catalogSource=server" }

    # 12) PR-12 — /setup/printer: model tak dikenal & resep disabled → UNSUPPORTED_MODEL; paket tak ada →
    #     3 percobaan → failed + error terstruktur (DOWNLOAD_FAILED / HASH_MISMATCH), bukan teks bebas.
    foreach ($m in "NOPE", "DISABLED-PRINTER") {
        $u = Invoke-RestMethod "$base/setup/printer" -Method Post -ContentType "application/json" -Body ('{"model":"' + $m + '"}') -TimeoutSec 10
        if ($u.ok -ne $false -or $u.error -ne "UNSUPPORTED_MODEL") { Fail "setup $m tidak UNSUPPORTED_MODEL: $($u | ConvertTo-Json -Compress)" } else { Ok "setup $m -> UNSUPPORTED_MODEL" }
    }
    $st = Invoke-RestMethod "$base/setup/printer" -Method Post -ContentType "application/json" -Body '{"model":"TEST-PRINTER"}' -TimeoutSec 10
    if ($st.ok -ne $true -or $st.state -ne "running") { Fail "setup TEST-PRINTER tidak running: $($st | ConvertTo-Json -Compress)" }
    else {
        $deadline2 = (Get-Date).AddSeconds(150); $final = $null
        while ((Get-Date) -lt $deadline2) { Start-Sleep -Seconds 2; $final = Invoke-RestMethod "$base/setup/status" -TimeoutSec 5; if ($final.state -in @("failed", "done")) { break } }
        if ($null -eq $final -or $final.state -ne "failed" -or $final.error -notin @("DOWNLOAD_FAILED", "HASH_MISMATCH")) { Fail "setup TEST-PRINTER: state=$($final.state) error=$($final.error) msg=$($final.message)" } else { Ok "setup TEST-PRINTER -> failed/$($final.error) (kode terstruktur)" }
    }

    # 13) PR-12/PR-14 — /print/test {printerRole} mencetak ke printer PERAN (bukan PDF).
    if ($hasPdf) {
        $tp = Invoke-RestMethod "$base/print/test" -Method Post -ContentType "application/json" -Body '{"printerRole":"CASHIER"}' -TimeoutSec 60
        if ($tp.ok -ne $true -or $tp.printer -ne $vp) { Fail "/print/test peran gagal: $($tp | ConvertTo-Json -Compress)" } else { Ok "/print/test CASHIER -> $($tp.printer)" }
        $tpb = Invoke-RestMethod "$base/print/test" -Method Post -ContentType "application/json" -Body '{"printerRole":"NOPE"}' -TimeoutSec 60
        if ($tpb.ok -ne $false -or $tpb.error -ne "ROLE_UNMAPPED") { Fail "/print/test peran asing tidak ROLE_UNMAPPED: $($tpb | ConvertTo-Json -Compress)" } else { Ok "/print/test peran asing -> ROLE_UNMAPPED" }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    if ($catSrv -and -not $catSrv.HasExited) { Stop-Process -Id $catSrv.Id -Force }
    # Bersihkan auto-start yang ditulis agent di runner (kerapian; runner sekali pakai).
    try { Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GamaPrintAgent" -ErrorAction SilentlyContinue } catch {}
}

if ($fail.Count -gt 0) { Write-Host "`n$($fail.Count) kegagalan" -ForegroundColor Red; exit 1 }
Write-Host "`nsmoke hijau" -ForegroundColor Green
