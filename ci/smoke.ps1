# Smoke test Gama Print Agent di CI (windows-latest).
# Menjalankan exe hasil build sebagai proses latar, menunggu :9111, memeriksa bentuk balasan
# endpoint, memutar semua fixtures ke POST /print dengan semua peran dipetakan ke
# "Microsoft Print to PDF", lalu mematikan proses. Exit 1 bila ada yang meleset.
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

# 1) printers.json: semua peran -> PDF (folder data stabil agent, bukan samping exe).
$dataDir = Join-Path $env:LOCALAPPDATA "GamaPrintAgent"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$pdf = "Microsoft Print to PDF"
@{ CASHIER = $pdf; DELIVERY = $pdf; QRLABEL = $pdf; REPORT = $pdf } | ConvertTo-Json | Set-Content (Join-Path $dataDir "printers.json") -Encoding UTF8
$hasPdf = (Get-Printer -ErrorAction SilentlyContinue | Where-Object Name -eq $pdf) -ne $null
Write-Host "printer '$pdf' tersedia di runner: $hasPdf"

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

    # 8) Cetak: hanya bila printer PDF ada di runner (PowerPacks mencetak ke default Windows yang
    #    diganti sesaat oleh WithRolePrinter → butuh printer nyata; PDF writer memenuhi).
    if ($hasPdf) {
        $t = Invoke-RestMethod "$base/print/test" -Method Post -TimeoutSec 60
        if ($t.ok -ne $true -or -not $t.printed) { Fail "/print/test gagal: $($t | ConvertTo-Json -Compress)" } else { Ok "/print/test -> $($t.printed)" }

        $fixtures = Get-ChildItem (Join-Path $PSScriptRoot "..\fixtures\*.sample.json")
        if ($fixtures.Count -lt 18) { Fail "fixtures < 18 (ada $($fixtures.Count))" }
        foreach ($f in $fixtures) {
            $body = Get-Content $f.FullName -Raw
            try {
                $r = Invoke-RestMethod "$base/print" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 90
                if ($r.ok -ne $true) { Fail "$($f.Name): $($r | ConvertTo-Json -Compress)" } else { Ok "$($f.Name) -> $($r.jobType)" }
            } catch { Fail "$($f.Name): $($_.Exception.Message)" }
        }
        # Amplop rusak -> BAD_PAYLOAD, bukan 500.
        $bad = Invoke-RestMethod "$base/print" -Method Post -ContentType "application/json" -Body '{"schemaVersion":1}' -TimeoutSec 10
        if ($bad.ok -ne $false -or $bad.error -ne "BAD_PAYLOAD") { Fail "amplop rusak tidak BAD_PAYLOAD: $($bad | ConvertTo-Json -Compress)" } else { Ok "amplop rusak -> BAD_PAYLOAD" }
    } else {
        Write-Host "SKIP  cetak fixtures: printer '$pdf' tidak ada di runner" -ForegroundColor Yellow
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    # Bersihkan auto-start yang ditulis agent di runner (kerapian; runner sekali pakai).
    try { Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "GamaPrintAgent" -ErrorAction SilentlyContinue } catch {}
}

if ($fail.Count -gt 0) { Write-Host "`n$($fail.Count) kegagalan" -ForegroundColor Red; exit 1 }
Write-Host "`nsmoke hijau" -ForegroundColor Green
