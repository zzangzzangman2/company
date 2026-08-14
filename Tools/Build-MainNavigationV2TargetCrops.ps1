param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourcePath = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2\Reference\main_navigation_v2_visual_target.png'
$outputRoot = Join-Path $ProjectRoot 'Artifacts\MainNavigationHudV2'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$specs = @(
    @{ Name='visual-target-top-200pct.png'; X=0; Y=0; W=1672; H=180; Scale=2.0 },
    @{ Name='visual-target-bottom-200pct.png'; X=0; Y=748; W=1672; H=193; Scale=2.0 },
    @{ Name='visual-target-investment-150pct.png'; X=286; Y=170; W=1100; H=610; Scale=1.5 }
)

$source = [System.Drawing.Bitmap]::new($sourcePath)
try {
    foreach ($spec in $specs) {
        $width = [int][Math]::Round($spec.W * $spec.Scale)
        $height = [int][Math]::Round($spec.H * $spec.Scale)
        $output = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($output)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $sourceRect = [System.Drawing.Rectangle]::new($spec.X, $spec.Y, $spec.W, $spec.H)
                $destinationRect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
                $graphics.DrawImage($source, $destinationRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }
            $path = Join-Path $outputRoot $spec.Name
            $output.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "TARGET_CROP: $path"
        }
        finally { $output.Dispose() }
    }
}
finally { $source.Dispose() }

Write-Host 'MAIN_NAVIGATION_V2_TARGET_CROPS: PASS mockup-only'
