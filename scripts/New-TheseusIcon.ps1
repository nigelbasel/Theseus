<#
.SYNOPSIS
    Generates the Theseus application icon.

.DESCRIPTION
    Draws a square spiral - the thread through the Labyrinth - and writes a multi-resolution .ico.
    The icon is generated rather than hand-drawn so it can be regenerated and adjusted, and so the
    smallest sizes are drawn at their own scale instead of being downsampled into mush.

.PARAMETER Path
    Where to write the icon. Defaults to the application's Assets folder.
#>
[CmdletBinding()]
param(
    [string] $Path
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $Path) {
    $repository = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
    $Path = Join-Path $repository 'src\Theseus.App\Assets\theseus.ico'
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$background = [System.Drawing.ColorTranslator]::FromHtml('#0A9C85')
$thread = [System.Drawing.Color]::White

function Get-SpiralPoints {
    param([int] $Turns)

    $points = New-Object System.Collections.ArrayList
    $x = 0.0
    $y = 0.0
    [void]$points.Add(@($x, $y))

    $directions = @(@(1, 0), @(0, -1), @(-1, 0), @(0, 1))
    $index = 0
    $length = 1

    while ($length -le $Turns) {
        for ($repeat = 0; $repeat -lt 2; $repeat++) {
            $direction = $directions[$index % 4]
            $x += $direction[0] * $length
            $y += $direction[1] * $length
            [void]$points.Add(@($x, $y))
            $index++
        }
        $length++
    }

    return $points
}

function New-IconBitmap {
    param([int] $Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    # A filled tile rather than a bare glyph: the taskbar and title bar sit on unpredictable
    # backgrounds, and a transparent mark disappears against one that happens to match it.
    $radius = [Math]::Max(2, [int]($Size * 0.22))
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $tile.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $tile.AddArc($Size - $diameter - 1, 0, $diameter, $diameter, 270, 90)
    $tile.AddArc($Size - $diameter - 1, $Size - $diameter - 1, $diameter, $diameter, 0, 90)
    $tile.AddArc(0, $Size - $diameter - 1, $diameter, $diameter, 90, 90)
    $tile.CloseFigure()

    $brush = New-Object System.Drawing.SolidBrush($background)
    $graphics.FillPath($brush, $tile)

    # Fewer turns at small sizes: three concentric passes read as a spiral at 16 px, five read as a blob.
    $turns = if ($Size -le 24) { 3 } elseif ($Size -le 48) { 4 } else { 5 }
    $points = Get-SpiralPoints -Turns $turns

    $minX = ($points | ForEach-Object { $_[0] } | Measure-Object -Minimum).Minimum
    $maxX = ($points | ForEach-Object { $_[0] } | Measure-Object -Maximum).Maximum
    $minY = ($points | ForEach-Object { $_[1] } | Measure-Object -Minimum).Minimum
    $maxY = ($points | ForEach-Object { $_[1] } | Measure-Object -Maximum).Maximum

    $spanX = [Math]::Max(1, $maxX - $minX)
    $spanY = [Math]::Max(1, $maxY - $minY)
    $span = [Math]::Max($spanX, $spanY)

    $padding = if ($Size -le 32) { $Size * 0.14 } else { $Size * 0.18 }
    $usable = $Size - (2 * $padding)
    $scale = $usable / $span

    # The stroke is a fraction of the gap between the spiral's arms, not of the icon. Sizing it from
    # the icon makes the arms touch as soon as the turn count rises, and the mark fills in solid.
    $stroke = [Math]::Max(1.0, $scale * 0.42)

    $offsetX = $padding + (($usable - ($spanX * $scale)) / 2)
    $offsetY = $padding + (($usable - ($spanY * $scale)) / 2)

    $projected = @()
    foreach ($point in $points) {
        $projected += New-Object System.Drawing.PointF(
            [float]($offsetX + (($point[0] - $minX) * $scale)),
            [float]($offsetY + (($point[1] - $minY) * $scale)))
    }

    $pen = New-Object System.Drawing.Pen($thread, [float]$stroke)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($pen, $projected)

    $pen.Dispose()
    $brush.Dispose()
    $tile.Dispose()
    $graphics.Dispose()

    return $bitmap
}

$directory = Split-Path -Parent $Path
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

# Each size is stored as a PNG inside the .ico, which Windows has supported since Vista and which
# keeps the 256 px entry from bloating the file.
$payloads = @()
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $payloads += , @{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
    $bitmap.Dispose()
}

$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($output)

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$payloads.Count)

$offset = 6 + (16 * $payloads.Count)
foreach ($payload in $payloads) {
    $dimension = if ($payload.Size -ge 256) { 0 } else { $payload.Size }
    $writer.Write([Byte]$dimension)
    $writer.Write([Byte]$dimension)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$payload.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $payload.Bytes.Length
}

foreach ($payload in $payloads) {
    $writer.Write($payload.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($Path, $output.ToArray())
$writer.Dispose()
$output.Dispose()

"wrote $Path ($((Get-Item $Path).Length) bytes, $($payloads.Count) sizes)"
