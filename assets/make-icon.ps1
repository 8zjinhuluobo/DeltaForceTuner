$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sizes = @(256, 64, 48, 32, 16)
$images = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $orange = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 122, 26))
    $g.FillRectangle($orange, 0, 0, $s, $s)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $m = [int]($s * 0.20)
    $pts = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point([int]($s / 2), $m)),
        (New-Object System.Drawing.Point(($s - $m), ($s - $m))),
        (New-Object System.Drawing.Point($m, ($s - $m)))
    )
    $g.FillPolygon($white, $pts)
    $dark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(11, 15, 20))
    $m2 = [int]($s * 0.35)
    $pts2 = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point([int]($s / 2), $m2)),
        (New-Object System.Drawing.Point(($s - $m2), ($s - $m2))),
        (New-Object System.Drawing.Point($m2, ($s - $m2)))
    )
    $g.FillPolygon($dark, $pts2)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$images.Add($ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $b = 0
    if ($s -lt 256) { $b = $s }
    $bw.Write([byte]$b)
    $bw.Write([byte]$b)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$images[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($p in $images) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'app.ico'), $out.ToArray())
$bw.Close()
Get-Item (Join-Path $PSScriptRoot 'app.ico') | Select-Object Length, FullName
