# Generates assets/gloam.ico: a "gloaming" disc, gold left half, slate right half.
Add-Type -AssemblyName System.Drawing

$sizes = 16, 32, 48, 256
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, [int]($s * 0.08))
    $d = $s - 2 * $pad
    $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 215, 0))
    $slate = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 119, 136, 153))
    # Left semicircle (gold): start at 90 deg, sweep 180 clockwise.
    $g.FillPie($gold, $pad, $pad, $d, $d, 90, 180)
    # Right semicircle (slate): start at 270 deg, sweep 180.
    $g.FillPie($slate, $pad, $pad, $d, $d, 270, 180)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
}

# Assemble an ICO that stores each image as PNG (supported on Windows Vista+).
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type: icon
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bytes = $pngs[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)         # width
    $bw.Write([Byte]$dim)         # height
    $bw.Write([Byte]0)            # color count
    $bw.Write([Byte]0)            # reserved
    $bw.Write([UInt16]1)          # color planes
    $bw.Write([UInt16]32)         # bits per pixel
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $pngs) { $bw.Write($bytes) }
$bw.Flush()

$dir = Join-Path $PSScriptRoot "..\assets"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $dir "gloam.ico"), $out.ToArray())
Write-Host "Wrote assets/gloam.ico ($($out.Length) bytes)"
