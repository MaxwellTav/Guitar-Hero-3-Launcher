# host\app.ico: the launcher's own icon (a guitar pick with a lightning bolt), drawn here, so the exe
# carries nothing of the game. (The window shows GH3.exe's icon at run time, from the player's copy.)
#   powershell -NoProfile -File make_icon.ps1
Add-Type -AssemblyName System.Drawing

function Draw-Pick([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0
    function P([double]$x, [double]$y) { New-Object System.Drawing.PointF ([float]($x * $s)), ([float]($y * $s)) }

    # the pick: wide rounded top, point at the bottom
    $pick = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pick.AddBezier((P 128 14), (P 196 12), (P 246 34), (P 240 92))
    $pick.AddBezier((P 240 92), (P 232 156), (P 168 214), (P 128 244))
    $pick.AddBezier((P 128 244), (P 88 214), (P 24 156), (P 16 92))
    $pick.AddBezier((P 16 92), (P 10 34), (P 60 12), (P 128 14))
    $pick.CloseFigure()
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush (P 0 0), (P 0 256), ([System.Drawing.Color]::FromArgb(255, 255, 122, 47)), ([System.Drawing.Color]::FromArgb(255, 150, 22, 10))
    $g.FillPath($fill, $pick)
    $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 60, 8, 4)), ([float][Math]::Max(1, 9 * $s))
    $edge.LineJoin = 'Round'
    $g.DrawPath($edge, $pick)

    # a highlight along the top
    $shine = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shine.AddBezier((P 128 30), (P 180 28), (P 222 44), (P 222 84))
    $shine.AddBezier((P 222 84), (P 180 64), (P 76 64), (P 34 84))
    $shine.AddBezier((P 34 84), (P 34 44), (P 76 28), (P 128 30))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 255, 255, 255))), $shine)

    # the bolt
    $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bolt.AddPolygon(@((P 146 44), (P 86 136), (P 122 136), (P 104 214), (P 172 112), (P 136 112), (P 160 44)))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 246, 214))), $bolt)
    $boltEdge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 70, 14, 6)), ([float][Math]::Max(1, 5 * $s))
    $boltEdge.LineJoin = 'Round'
    $g.DrawPath($boltEdge, $bolt)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$entries = foreach ($size in 256, 64, 48, 32, 16) { [pscustomobject]@{ Size = $size; Bytes = (Draw-Pick $size) } }
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $d = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$e.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $e.Bytes.Length
}
foreach ($e in $entries) { $w.Write($e.Bytes) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'host\app.ico'), $out.ToArray())
# the installer wears the same one
$inst = Join-Path $PSScriptRoot 'installer'
if (Test-Path $inst) { Copy-Item (Join-Path $PSScriptRoot 'host\app.ico') (Join-Path $inst 'app.ico') -Force }
Write-Host "app.ico: $($entries.Count) sizes"
