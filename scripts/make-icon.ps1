# 生成应用图标：assets\手机投屏.ico（PNG 帧多尺寸 256/48/32/16）+ assets\手机投屏图标预览.png（512）
# 说明：PNG 帧 ICO 用于 exe 资源、窗口标题栏、托盘均正常；
#       但 GDI+ 的 Icon.ToBitmap() 不兼容 PNG 帧（会抛异常），程序内大图标请引用预览 PNG。
# 用法：powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
$ErrorActionPreference = 'Stop'
$root   = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null
Add-Type -AssemblyName System.Drawing

function New-RoundRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-PhoneIcon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $size / 256.0
    # 蓝紫渐变圆角底
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point($size, $size)),
        [System.Drawing.Color]::FromArgb(255, 96, 54, 191),
        [System.Drawing.Color]::FromArgb(255, 32, 110, 245))
    $g.FillPath($grad, (New-RoundRectPath 0 0 $size $size ([float](40 * $u))))
    # 白色圆角手机机身
    $bw = 104 * $u; $bh = 172 * $u
    $bx = ($size - $bw) / 2; $by = ($size - $bh) / 2 - 4 * $u
    $g.FillPath([System.Drawing.Brushes]::White, (New-RoundRectPath $bx $by $bw $bh ([float](22 * $u))))
    # 深色屏幕
    $sw = 80 * $u; $sh = 130 * $u
    $sx = ($size - $sw) / 2; $sy = $by + 18 * $u
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 32, 52))), $sx, $sy, $sw, $sh)
    # 播放三角
    $px = $sx + $sw * 0.34; $py = $sy + $sh * 0.28; $pw = $sw * 0.32; $ph = $sh * 0.44
    $g.FillPolygon([System.Drawing.Brushes]::White, [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF($px, $py)),
        (New-Object System.Drawing.PointF($px + $pw, $py + $ph / 2)),
        (New-Object System.Drawing.PointF($px, $py + $ph))))
    # 右下角在线指示点
    $dr = 26 * $u; $cx = $size - $dr - 14 * $u; $cy = $size - $dr - 14 * $u
    $g.FillEllipse([System.Drawing.Brushes]::White, $cx - 6 * $u, $cy - 6 * $u, $dr + 12 * $u, $dr + 12 * $u)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 46, 204, 113))), $cx, $cy, $dr, $dr)
    $g.Dispose()
    return $bmp
}

function Save-PngIco([string]$path, [int[]]$sizes) {
    $pngs = @{}
    $blob = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($blob)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($s in $sizes) {
        $b = New-PhoneIcon $s
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $data = $ms.ToArray(); $ms.Dispose(); $b.Dispose()
        $pngs[$s] = $data
        $wh = if ($s -ge 256) { 0 } else { $s }
        $writer.Write([byte]$wh); $writer.Write([byte]$wh)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$data.Length); $writer.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($s in $sizes) { $writer.Write($pngs[$s]) }
    $writer.Flush()
    [IO.File]::WriteAllBytes($path, $blob.ToArray())
    $writer.Dispose(); $blob.Dispose()
}

Save-PngIco (Join-Path $assets '手机投屏.ico') @(256, 48, 32, 16)
$prev = New-PhoneIcon 512
$prev.Save((Join-Path $assets '手机投屏图标预览.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$prev.Dispose()
Write-Host 'OK: assets\手机投屏.ico + assets\手机投屏图标预览.png'
