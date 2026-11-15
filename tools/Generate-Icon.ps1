# Zeichnet das SyncForge-Icon im Windows-11-Fluent-Stil:
# helle Wolke mit Verlauf + blaue Sync-Pfeile (freigestellt), und schreibt
# Assets/AppIcon.png (256 px) sowie Assets/SyncForge.ico (16-256 px).
param([string]$PreviewDir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\SyncForge\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function C([string]$hex, [int]$a = 255) {
    $v = [Convert]::ToInt32($hex.TrimStart('#'), 16)
    [System.Drawing.Color]::FromArgb($a, ($v -shr 16) -band 255, ($v -shr 8) -band 255, $v -band 255)
}

# Wolke als Pfad (Kreise + Grundflaeche), $d = Einzug fuer Kontur/Innenflaeche
function New-CloudRegion([single]$s, [single]$d) {
    $region = [System.Drawing.Region]::new([System.Drawing.RectangleF]::Empty)
    $circles = @(
        @(0.30, 0.56, 0.17),   # links
        @(0.50, 0.42, 0.235),  # oben
        @(0.72, 0.55, 0.175)   # rechts
    )
    foreach ($c in $circles) {
        $r = ($c[2] * $s) - $d
        $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $p.AddEllipse([single]($c[0] * $s - $r), [single]($c[1] * $s - $r), [single](2 * $r), [single](2 * $r))
        $region.Union($p)
    }
    # Grundflaeche mit runder Unterkante
    $x0 = 0.30 * $s; $x1 = 0.72 * $s; $y0 = 0.50 * $s; $y1 = 0.73 * $s - $d
    $rr = (0.23 * $s - $d) - (0.50 * $s - $d) + 0.05 * $s
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $p.AddRectangle([System.Drawing.RectangleF]::new($x0, $y0, $x1 - $x0, $y1 - $y0))
    $region.Union($p)
    return $region
}

function Draw-Arrow($g, [single]$cx, [single]$cy, [single]$r, [single]$startDeg, [single]$sweepDeg, [single]$w, $brush) {
    $pen = [System.Drawing.Pen]::new($brush, $w)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Flat'
    $g.DrawArc($pen, $cx - $r, $cy - $r, 2 * $r, 2 * $r, $startDeg, $sweepDeg)
    # Pfeilspitze am Ende des Bogens (im Uhrzeigersinn)
    $a = ($startDeg + $sweepDeg) * [Math]::PI / 180
    $px = $cx + $r * [Math]::Cos($a); $py = $cy + $r * [Math]::Sin($a)
    $tx = -[Math]::Sin($a); $ty = [Math]::Cos($a)        # Tangente
    $nx = [Math]::Cos($a); $ny = [Math]::Sin($a)         # Radial
    $h = $w * 1.25; $hw = $w * 1.0
    $pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($px + $tx * $h, $py + $ty * $h),
        [System.Drawing.PointF]::new($px + $nx * $hw, $py + $ny * $hw),
        [System.Drawing.PointF]::new($px - $nx * $hw, $py - $ny * $hw)
    )
    $g.FillPolygon($brush, $pts)
}

function New-IconBitmap([int]$size) {
    $s = [single]$size
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $border = [Math]::Max(1.0, $s * 0.022)

    # Wolke: Kontur, dann Innenflaeche mit Verlauf
    $outer = New-CloudRegion $s 0
    $g.FillRegion([System.Drawing.SolidBrush]::new((C '#5B9BD5')), $outer)
    $inner = New-CloudRegion $s $border
    $cloudBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0.18 * $s), [System.Drawing.PointF]::new(0, 0.75 * $s),
        (C '#FFFFFF'), (C '#B9DBF7'))
    $g.FillRegion($cloudBrush, $inner)

    # Sync-Plakette unten rechts: weisse Freistellung, dann Pfeile
    $cx = 0.665 * $s; $cy = 0.69 * $s
    $gap = 0.215 * $s
    $g.FillEllipse([System.Drawing.SolidBrush]::new((C '#FFFFFF')), $cx - $gap, $cy - $gap, 2 * $gap, 2 * $gap)
    $ringR = 0.138 * $s
    $w = [Math]::Max(1.5, 0.056 * $s)
    $arrowBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($cx - $gap, $cy - $gap), [System.Drawing.PointF]::new($cx + $gap, $cy + $gap),
        (C '#2899F5'), (C '#0F5FB8'))
    Draw-Arrow $g $cx $cy $ringR 200 115 $w $arrowBrush
    Draw-Arrow $g $cx $cy $ringR 20 115 $w $arrowBrush

    $g.Dispose()
    return $bmp
}

# BITMAPINFOHEADER + BGRA-Pixel (von unten nach oben) + leere AND-Maske
function ConvertTo-IconDib([System.Drawing.Bitmap]$bmp) {
    $n = $bmp.Width
    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    $maskStride = [int]([Math]::Ceiling($n / 32.0) * 4)
    $bw.Write([uint32]40); $bw.Write([int32]$n); $bw.Write([int32]($n * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($n * $n * 4 + $maskStride * $n))
    $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    for ($y = $n - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $n; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $bw.Write([byte[]]::new($maskStride * $n))
    $bw.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = [System.IO.MemoryStream]::new()
    if ($sz -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Klassischer 32-Bit-DIB-Eintrag: von Taskleiste, Explorer und Ressourcen-Compiler zuverlaessig gelesen
        $bytes = ConvertTo-IconDib $bmp
        $ms.Write($bytes, 0, $bytes.Length)
    }
    if ($PreviewDir) { $bmp.Save((Join-Path $PreviewDir "icon-$sz.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    if ($sz -eq 256) { $bmp.Save((Join-Path $assets 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO-Container (DIB-Eintraege bis 128 px, PNG fuer 256 px)
$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $len = $pngs[$i].Length
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim)
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'SyncForge.ico'), $out.ToArray())
Write-Host "Icon erzeugt in $assets"

# --- MSIX-Grafiken (Assets\Package): Icon zentriert auf transparenter Fläche ---
$pkg = Join-Path $assets 'Package'
New-Item -ItemType Directory -Force $pkg | Out-Null

function Save-Canvas([string]$name, [int]$w, [int]$h, [double]$iconRatio) {
    $iconSize = [int]([Math]::Min($w, $h) * $iconRatio)
    $icon = New-IconBitmap $iconSize
    $canvas = [System.Drawing.Bitmap]::new($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($icon, [int](($w - $iconSize) / 2), [int](($h - $iconSize) / 2), $iconSize, $iconSize)
    $g.Dispose()
    $canvas.Save((Join-Path $pkg $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose(); $icon.Dispose()
}

foreach ($scale in 100, 200) {
    $f = $scale / 100
    Save-Canvas "Square44x44Logo.scale-$scale.png" (44 * $f) (44 * $f) 1.0
    Save-Canvas "Square150x150Logo.scale-$scale.png" (150 * $f) (150 * $f) 0.6
    Save-Canvas "Wide310x150Logo.scale-$scale.png" (310 * $f) (150 * $f) 0.6
    Save-Canvas "StoreLogo.scale-$scale.png" (50 * $f) (50 * $f) 1.0
    Save-Canvas "SplashScreen.scale-$scale.png" (620 * $f) (300 * $f) 0.5
}
# Taskleiste/Startmenü: feste Größen, "unplated" = ohne Kachelhintergrund
foreach ($size in 16, 24, 32, 48, 256) {
    Save-Canvas "Square44x44Logo.targetsize-$size.png" $size $size 1.0
    Save-Canvas "Square44x44Logo.targetsize-${size}_altform-unplated.png" $size $size 1.0
    Save-Canvas "Square44x44Logo.targetsize-${size}_altform-lightunplated.png" $size $size 1.0
}
Write-Host "MSIX-Grafiken erzeugt in $pkg"
