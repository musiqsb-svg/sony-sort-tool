# 아이콘 만들기: 주황 바탕에 흰 폴더 + 날짜 칸 3개 (소니 로고는 쓰지 않음)
#   app.ico        프로그램
#   setup.ico      설치 파일 (오른쪽 아래 ↓ 표시)
#   uninstall.ico  제거 파일 (회색 + 오른쪽 아래 × 표시)
# 16~256px 여러 크기를 PNG로 그려 ICO 하나에 담음
Add-Type -AssemblyName System.Drawing
$sizes = 256, 64, 48, 32, 24, 16
$top    = [Drawing.Color]::FromArgb(251, 146, 60)     # 바탕 위쪽
$bottom = [Drawing.Color]::FromArgb(234, 88, 12)      # 바탕 아래쪽
$slotColor = [Drawing.Color]::FromArgb(234, 88, 12)   # 날짜 칸

function New-RoundPath($x, $y, $w, $h, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = [single]($r * 2)
    $p.AddArc([single]$x, [single]$y, $d, $d, 180, 90)
    $p.AddArc([single]($x + $w - $d), [single]$y, $d, $d, 270, 90)
    $p.AddArc([single]($x + $w - $d), [single]($y + $h - $d), $d, $d, 0, 90)
    $p.AddArc([single]$x, [single]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure(); $p
}

function New-Frame([int]$n, [string]$mode) {
    $bmp = New-Object Drawing.Bitmap $n, $n
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $f = $n / 256.0
    $c1 = $top; $c2 = $bottom; $c3 = $slotColor
    if ($mode -eq 'uninstall') { $c1 = [Drawing.Color]::FromArgb(176, 182, 192); $c2 = $c3 = [Drawing.Color]::FromArgb(120, 127, 140) }
    # 바탕
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Point 0, 0), (New-Object Drawing.Point 0, $n), $c1, $c2
    $g.FillPath($bg, (New-RoundPath (8*$f) (8*$f) (240*$f) (240*$f) (52*$f)))
    # 폴더 (탭 + 몸통)
    $white = [Drawing.Brushes]::White
    $g.FillPath($white, (New-RoundPath (48*$f) (70*$f) (72*$f) (40*$f) (10*$f)))
    $g.FillPath($white, (New-RoundPath (48*$f) (88*$f) (160*$f) (110*$f) (14*$f)))
    # 날짜별로 나뉜 칸 3개
    $slot = New-Object Drawing.SolidBrush $c3
    if ($n -ge 24) {
        foreach ($i in 0..2) { $g.FillPath($slot, (New-RoundPath ((68 + $i * 42)*$f) (118*$f) (34*$f) (56*$f) (6*$f))) }
    } else {
        $g.FillRectangle($slot, [single](70*$f), [single](128*$f), [single](116*$f), [single](36*$f))
    }
    # 설치 ↓ · 제거 × 표시 (오른쪽 아래 동그라미)
    if ($mode -ne 'app') {
        $cx = 192*$f; $cy = 192*$f; $r = 60*$f
        $g.FillEllipse($white, [single]($cx - $r - 10*$f), [single]($cy - $r - 10*$f), [single](2*($r + 10*$f)), [single](2*($r + 10*$f)))
        $badge = if ($mode -eq 'setup') { [Drawing.Color]::FromArgb(17, 24, 39) } else { [Drawing.Color]::FromArgb(220, 38, 38) }
        $g.FillEllipse((New-Object Drawing.SolidBrush $badge), [single]($cx - $r), [single]($cy - $r), [single](2*$r), [single](2*$r))
        $pen = New-Object Drawing.Pen ([Drawing.Color]::White), ([single][Math]::Max(1.5, 16*$f))
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
        if ($mode -eq 'setup') {
            $g.DrawLine($pen, [single]$cx, [single]($cy - 30*$f), [single]$cx, [single]($cy + 28*$f))
            $g.DrawLines($pen, [Drawing.PointF[]]@((New-Object Drawing.PointF ([single]($cx - 26*$f)), ([single]($cy + 4*$f))),
                                                   (New-Object Drawing.PointF ([single]$cx), ([single]($cy + 30*$f))),
                                                   (New-Object Drawing.PointF ([single]($cx + 26*$f)), ([single]($cy + 4*$f)))))
        } else {
            $g.DrawLine($pen, [single]($cx - 24*$f), [single]($cy - 24*$f), [single]($cx + 24*$f), [single]($cy + 24*$f))
            $g.DrawLine($pen, [single]($cx + 24*$f), [single]($cy - 24*$f), [single]($cx - 24*$f), [single]($cy + 24*$f))
        }
    }
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}

foreach ($mode in 'app', 'setup', 'uninstall') {
    $out = Join-Path $PSScriptRoot "$mode.ico"
    $frames = foreach ($s in $sizes) { , (New-Frame $s $mode) }
    $fs = [IO.File]::Create($out); $w = New-Object IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $b = [byte]($(if ($s -ge 256) { 0 } else { $s }))
        $w.Write($b); $w.Write($b); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($fr in $frames) { $w.Write($fr) }
    $w.Dispose()
    "icon: $out ($((Get-Item $out).Length) bytes)"
}
