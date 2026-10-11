# Pratinjau jendela Status (P-626) untuk diperiksa mata sebelum pilot: memuat exe
# hasil build di Windows PowerShell 5.1 (.NET Framework, sama dengan agent), membuka
# StatusWindow di luar layar, lalu menyimpan dua gambar: keadaan aktif dan sesudah
# cetakan gagal. Bukan gerbang rilis (langkah CI continue-on-error).
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$OutDir
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$flags = [Reflection.BindingFlags]"NonPublic,Public,Static,Instance"
$windowType = $asm.GetType("GamaPrintAgent.SpikeTransport.StatusWindow", $true)
$captureType = $asm.GetType("GamaPrintAgent.SpikeTransport.StatusCapture", $true)

function Save-Window([string]$name) {
    $ctor = $windowType.GetConstructor([Reflection.BindingFlags]"NonPublic,Instance", $null, [Type[]]@(), $null)   # tanpa Static: .cctor ikut cocok
    $form = $ctor.Invoke(@())
    $form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object Drawing.Point(-3000, -3000)
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap($form.Width, $form.Height)
    $form.DrawToBitmap($bmp, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $form.Close()
    Write-Host "ok    $path ($($form.Width)x$($form.Height))"
}

Save-Window "status-aktif.png"
$captureType.GetMethod("RecordPrint", $flags).Invoke($null, @("CASHIER", $false, "PRINT_FAILED"))
Save-Window "status-cetak-gagal.png"
