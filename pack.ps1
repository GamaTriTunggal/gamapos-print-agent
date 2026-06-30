# Bangun installer + paket update (Velopack). Jalankan di Windows, dari ROOT repo.
#
# Prasyarat sekali-saja:
#   dotnet tool install -g vpk --version 1.2.0      (CLI Velopack, samakan versi dgn package 1.2.0)
#
# Pakai:
#   .\pack.ps1 -Version 1.0.0
#
# Hasil: folder .\Releases\ berisi GamaPrintAgent-...-Setup.exe (ikon GamaPOS) + paket update + manifest feed.
# Untuk rilis: upload isi .\Releases\ ke GitHub Releases (lihat README distribusi).

param(
    [Parameter(Mandatory = $true)][string]$Version
)
$ErrorActionPreference = "Stop"

$proj = "src/SpikeTransport/SpikeTransport.vbproj"
$pub  = "publish"
$exe  = "GamaPrintAgent.SpikeTransport.exe"

Write-Host "== 1/2 Publish v$Version ==" -ForegroundColor Cyan
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish $proj -c Release -o $pub

Write-Host "== 2/2 vpk pack v$Version ==" -ForegroundColor Cyan
# --framework net48 → installer memastikan .NET Framework 4.8 ada di PC target.
# (Kalau suatu flag error, cek nama persisnya: vpk pack --help)
vpk pack `
    --packId      GamaPrintAgent `
    --packVersion $Version `
    --packDir     $pub `
    --mainExe     $exe `
    --icon        gamapos.ico `
    --packTitle   "Gama Print Agent" `
    --framework   net48

Write-Host "== Selesai. Artefak di .\Releases\ ==" -ForegroundColor Green
Get-ChildItem .\Releases\ | Select-Object Name, Length
