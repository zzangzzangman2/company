param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetRoot = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2'
$outputRoot = Join-Path $ProjectRoot 'Artifacts\MainNavigationHudV2'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$files = Get-ChildItem -LiteralPath $assetRoot -Filter '*.png' -File -Recurse |
    Where-Object { $_.FullName -notlike '*\Reference\*' } |
    Sort-Object FullName

$runtimeSizes = @{
    'top_hud_backplate_v2.png' = @(1864, 68)
    'company_badge_v2.png' = @(470, 56)
    'time_badge_v2.png' = @(520, 56)
    'speed_normal_v2.png' = @(96, 50)
    'speed_hover_v2.png' = @(96, 50)
    'speed_selected_v2.png' = @(96, 50)
    'speed_pressed_v2.png' = @(96, 50)
    'bottom_dock_v2.png' = @(1120, 100)
    'tab_normal_v2.png' = @(200, 82)
    'tab_hover_v2.png' = @(200, 82)
    'tab_selected_v2.png' = @(200, 82)
    'tab_pressed_v2.png' = @(200, 82)
    'modal_frame_v2.png' = @(1120, 660)
    'modal_header_v2.png' = @(1060, 104)
    'card_normal_v2.png' = @(505, 232)
    'card_hover_v2.png' = @(505, 232)
    'card_disabled_v2.png' = @(506, 136)
    'card_featured_v2.png' = @(1040, 178)
    'card_featured_hover_v2.png' = @(1040, 178)
    'close_normal_v2.png' = @(150, 54)
    'close_hover_v2.png' = @(150, 54)
    'close_pressed_v2.png' = @(150, 54)
    'notification_badge_v2.png' = @(126, 28)
    'coming_soon_ribbon_v2.png' = @(116, 28)
}

function Get-DrawSize([IO.FileInfo]$file, [System.Drawing.Image]$image, [string]$mode) {
    if ($mode -eq 'runtime') {
        if ($runtimeSizes.ContainsKey($file.Name)) { return $runtimeSizes[$file.Name] }
        if ($file.FullName -like '*\Icons\Bottom\*') { return @(84, 84) }
        if ($file.FullName -like '*\Icons\Investment\*') { return @(120, 120) }
    }
    $scale = if ($mode -eq '100pct') { 1.0 } else { 0.5 }
    return @([Math]::Max(1, [int]($image.Width * $scale)), [Math]::Max(1, [int]($image.Height * $scale)))
}

function Build-Sheet([string]$mode, [string]$fileName) {
    $sheetWidth = 4096
    $margin = 32
    $labelHeight = 34
    $gap = 28
    $items = @()
    $x = $margin
    $y = $margin
    $rowHeight = 0
    foreach ($file in $files) {
        $image = [System.Drawing.Image]::FromFile($file.FullName)
        try { $size = Get-DrawSize $file $image $mode }
        finally { $image.Dispose() }
        $w = [int]$size[0]
        $h = [int]$size[1]
        $cellWidth = [Math]::Max($w, 440)
        if ($x + $cellWidth + $margin -gt $sheetWidth) {
            $x = $margin
            $y += $rowHeight + $gap
            $rowHeight = 0
        }
        $items += [pscustomobject]@{ File=$file; X=$x; Y=$y; W=$w; H=$h; CellW=$cellWidth }
        $x += $cellWidth + $gap
        $rowHeight = [Math]::Max($rowHeight, $labelHeight + $h)
    }
    $sheetHeight = $y + $rowHeight + $margin
    $sheet = [System.Drawing.Bitmap]::new($sheetWidth, $sheetHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 245, 247, 244))
            $tile = 24
            $brushA = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 238, 241, 237))
            $brushB = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 221, 229, 224))
            try {
                for ($cy=0; $cy -lt $sheetHeight; $cy += $tile) {
                    for ($cx=0; $cx -lt $sheetWidth; $cx += $tile) {
                        $brush = if ((($cx / $tile) + ($cy / $tile)) % 2 -eq 0) { $brushA } else { $brushB }
                        $graphics.FillRectangle($brush, $cx, $cy, $tile, $tile)
                    }
                }
            }
            finally { $brushA.Dispose(); $brushB.Dispose() }
            $font = [System.Drawing.Font]::new('Segoe UI', 18, [System.Drawing.FontStyle]::Bold)
            $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 59, 58))
            try {
                foreach ($item in $items) {
                    $relative = [IO.Path]::GetRelativePath($assetRoot, $item.File.FullName).Replace('\', '/')
                    $graphics.DrawString($relative, $font, $textBrush, $item.X, $item.Y)
                    $image = [System.Drawing.Image]::FromFile($item.File.FullName)
                    try {
                        $graphics.DrawImage($image, $item.X, $item.Y + $labelHeight, $item.W, $item.H)
                    }
                    finally { $image.Dispose() }
                }
            }
            finally { $font.Dispose(); $textBrush.Dispose() }
        }
        finally { $graphics.Dispose() }
        $destination = Join-Path $outputRoot $fileName
        $sheet.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output "$mode | $destination | ${sheetWidth}x${sheetHeight}"
    }
    finally { $sheet.Dispose() }
}

Build-Sheet '100pct' 'main-navigation-v2-assets-100pct.png'
Build-Sheet '50pct' 'main-navigation-v2-assets-50pct.png'
Build-Sheet 'runtime' 'main-navigation-v2-assets-runtime-scale.png'
