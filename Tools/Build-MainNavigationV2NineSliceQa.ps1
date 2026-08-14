param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ProjectRoot 'Artifacts\MainNavigationHudV2\main-navigation-v2-nine-slice-qa.png'
}

function New-CheckerBrushes {
    return @(
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 238, 244, 240)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 211, 225, 219))
    )
}

function Draw-Checkerboard {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Rectangle]$Bounds,
        [int]$Cell = 12
    )

    $brushes = New-CheckerBrushes
    try {
        for ($y = $Bounds.Top; $y -lt $Bounds.Bottom; $y += $Cell) {
            for ($x = $Bounds.Left; $x -lt $Bounds.Right; $x += $Cell) {
                $index = (([int](($x - $Bounds.Left) / $Cell)) + ([int](($y - $Bounds.Top) / $Cell))) % 2
                $width = [Math]::Min($Cell, $Bounds.Right - $x)
                $height = [Math]::Min($Cell, $Bounds.Bottom - $y)
                $Graphics.FillRectangle($brushes[$index], $x, $y, $width, $height)
            }
        }
    }
    finally {
        foreach ($brush in $brushes) { $brush.Dispose() }
    }
}

function Get-AxisSegments {
    param([int]$SourceSize, [int]$DestinationSize, [int]$NearBorder, [int]$FarBorder)

    $scale = 1.0
    $sum = $NearBorder + $FarBorder
    if ($sum -gt 0 -and $DestinationSize -lt $sum) {
        $scale = $DestinationSize / [double]$sum
    }

    $destinationNear = [int][Math]::Round($NearBorder * $scale)
    $destinationFar = [int][Math]::Round($FarBorder * $scale)
    if ($destinationNear + $destinationFar -gt $DestinationSize) {
        $destinationFar = [Math]::Max(0, $DestinationSize - $destinationNear)
    }

    return @(
        [pscustomobject]@{ SourceStart = 0; SourceLength = $NearBorder; DestinationStart = 0; DestinationLength = $destinationNear },
        [pscustomobject]@{ SourceStart = $NearBorder; SourceLength = [Math]::Max(0, $SourceSize - $NearBorder - $FarBorder); DestinationStart = $destinationNear; DestinationLength = [Math]::Max(0, $DestinationSize - $destinationNear - $destinationFar) },
        [pscustomobject]@{ SourceStart = $SourceSize - $FarBorder; SourceLength = $FarBorder; DestinationStart = $DestinationSize - $destinationFar; DestinationLength = $destinationFar }
    )
}

function Draw-NineSlice {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Image]$Image,
        [System.Drawing.Rectangle]$Destination,
        [int]$Left,
        [int]$Bottom,
        [int]$Right,
        [int]$Top
    )

    $xSegments = Get-AxisSegments $Image.Width $Destination.Width $Left $Right
    $ySegments = Get-AxisSegments $Image.Height $Destination.Height $Top $Bottom
    foreach ($x in $xSegments) {
        foreach ($y in $ySegments) {
            if ($x.SourceLength -le 0 -or $y.SourceLength -le 0 -or $x.DestinationLength -le 0 -or $y.DestinationLength -le 0) { continue }
            $source = [System.Drawing.Rectangle]::new($x.SourceStart, $y.SourceStart, $x.SourceLength, $y.SourceLength)
            $destinationSlice = [System.Drawing.Rectangle]::new(
                $Destination.X + $x.DestinationStart,
                $Destination.Y + $y.DestinationStart,
                $x.DestinationLength,
                $y.DestinationLength)
            $Graphics.DrawImage($Image, $destinationSlice, $source, [System.Drawing.GraphicsUnit]::Pixel)
        }
    }
}

$assetRoot = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2\Frames'
$markerRoot = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2\Markers'
$items = @(
    @{ Name='top HUD 1864x68'; File=(Join-Path $assetRoot 'top_hud_backplate_v2.png'); X=92; Y=58; W=1864; H=68; Border=@(80,52,80,52) },
    @{ Name='company badge 470x56'; File=(Join-Path $assetRoot 'company_badge_v2.png'); X=92; Y=172; W=470; H=56; Border=@(250,80,120,80) },
    @{ Name='time badge 520x56'; File=(Join-Path $assetRoot 'time_badge_v2.png'); X=610; Y=172; W=520; H=56; Border=@(170,82,116,82) },
    @{ Name='normal 96x50'; File=(Join-Path $assetRoot 'speed_normal_v2.png'); X=1190; Y=175; W=96; H=50; Border=@(70,44,70,44) },
    @{ Name='hover 96x50'; File=(Join-Path $assetRoot 'speed_hover_v2.png'); X=1330; Y=175; W=96; H=50; Border=@(70,46,70,46) },
    @{ Name='selected 96x50'; File=(Join-Path $assetRoot 'speed_selected_v2.png'); X=1470; Y=175; W=96; H=50; Border=@(70,36,70,36) },
    @{ Name='pressed 96x50'; File=(Join-Path $assetRoot 'speed_pressed_v2.png'); X=1610; Y=175; W=96; H=50; Border=@(70,46,70,46) },
    @{ Name='bottom dock 1120x100'; File=(Join-Path $assetRoot 'bottom_dock_v2.png'); X=464; Y=286; W=1120; H=100; Border=@(120,82,120,82) },
    @{ Name='tab normal 200x82'; File=(Join-Path $assetRoot 'tab_normal_v2.png'); X=520; Y=445; W=200; H=82; Border=@(104,70,104,70) },
    @{ Name='tab hover 200x82'; File=(Join-Path $assetRoot 'tab_hover_v2.png'); X=756; Y=445; W=200; H=82; Border=@(104,92,104,92) },
    @{ Name='tab selected 200x82'; File=(Join-Path $assetRoot 'tab_selected_v2.png'); X=992; Y=445; W=200; H=82; Border=@(104,70,104,70) },
    @{ Name='tab pressed 200x82'; File=(Join-Path $assetRoot 'tab_pressed_v2.png'); X=1228; Y=445; W=200; H=82; Border=@(104,66,104,66) },
    @{ Name='modal 1120x660'; File=(Join-Path $assetRoot 'modal_frame_v2.png'); X=72; Y=602; W=1120; H=660; Border=@(132,132,132,132) },
    @{ Name='header 760x104 (narrow stress)'; File=(Join-Path $assetRoot 'modal_header_v2.png'); X=1230; Y=602; W=760; H=104; Border=@(150,92,150,92) },
    @{ Name='card normal 505x232'; File=(Join-Path $assetRoot 'card_normal_v2.png'); X=1220; Y=768; W=505; H=232; Border=@(142,112,142,112) },
    @{ Name='card hover 240x120 (stress)'; File=(Join-Path $assetRoot 'card_hover_v2.png'); X=1740; Y=768; W=240; H=120; Border=@(142,112,142,112) },
    @{ Name='card disabled 506x136'; File=(Join-Path $assetRoot 'card_disabled_v2.png'); X=1220; Y=1050; W=506; H=136; Border=@(142,112,142,112) },
    @{ Name='card featured 1060x178'; File=(Join-Path $assetRoot 'card_featured_v2.png'); X=72; Y=1328; W=1060; H=178; Border=@(188,132,188,132) },
    @{ Name='card featured hover 1060x178'; File=(Join-Path $assetRoot 'card_featured_hover_v2.png'); X=72; Y=1570; W=1060; H=178; Border=@(188,132,188,132) },
    @{ Name='close N 150x54'; File=(Join-Path $assetRoot 'close_normal_v2.png'); X=1220; Y=1336; W=150; H=54; Border=@(110,110,110,110) },
    @{ Name='close H 150x54'; File=(Join-Path $assetRoot 'close_hover_v2.png'); X=1420; Y=1336; W=150; H=54; Border=@(110,110,110,110) },
    @{ Name='close P 150x54'; File=(Join-Path $assetRoot 'close_pressed_v2.png'); X=1620; Y=1336; W=150; H=54; Border=@(110,110,110,110) },
    @{ Name='badge 126x28'; File=(Join-Path $markerRoot 'notification_badge_v2.png'); X=1220; Y=1446; W=126; H=28; Border=@(82,54,82,54) },
    @{ Name='ribbon 116x28'; File=(Join-Path $markerRoot 'coming_soon_ribbon_v2.png'); X=1420; Y=1446; W=116; H=28; Border=@(102,54,102,54) }
)

$canvas = [System.Drawing.Bitmap]::new(2048, 1800, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 255, 248, 230))
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $font = [System.Drawing.Font]::new('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
        $titleFont = [System.Drawing.Font]::new('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
        $ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 59, 58))
        $outline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(170, 47, 119, 113), 1)
        try {
            $graphics.DrawString('Main Navigation V2 — actual target-size 9-slice QA', $titleFont, $ink, 24, 12)
            foreach ($item in $items) {
                if (-not (Test-Path -LiteralPath $item.File)) { throw "Missing V2 asset: $($item.File)" }
                $destination = [System.Drawing.Rectangle]::new($item.X, $item.Y, $item.W, $item.H)
                Draw-Checkerboard $graphics $destination 10
                $image = [System.Drawing.Image]::FromFile($item.File)
                try {
                    Draw-NineSlice $graphics $image $destination $item.Border[0] $item.Border[1] $item.Border[2] $item.Border[3]
                }
                finally { $image.Dispose() }
                $graphics.DrawRectangle($outline, $destination)
                $graphics.DrawString($item.Name, $font, $ink, $item.X, [Math]::Max(38, $item.Y - 24))
            }
        }
        finally {
            $font.Dispose()
            $titleFont.Dispose()
            $ink.Dispose()
            $outline.Dispose()
        }
    }
    finally { $graphics.Dispose() }

    $directory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $canvas.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally { $canvas.Dispose() }

Write-Host "MAIN_NAVIGATION_V2_NINE_SLICE_QA: PASS $OutputPath"
