[CmdletBinding()]
param(
    [string]$InputFrames = '',
    [string]$OutputRoot = '',
    [ValidateRange(0.65,0.80)]
    [double]$SeamFraction = 0.70,
    [ValidateSet('AuthoredUpper','SourceBridge')]
    [string]$SeamBlendMode = 'AuthoredUpper'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($InputFrames)) {
    $InputFrames = Join-Path $projectRoot 'Artifacts\Player2DWalkV3Candidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\Player2DWalkV4AlternatingCandidate'
}
$InputFrames = [IO.Path]::GetFullPath($InputFrames)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$outputFrames = Join-Path $OutputRoot 'Frames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$outputFrames | Out-Null

Add-Type -AssemblyName System.Drawing
$directions = @('south','southwest','west','northwest','north','northeast','east','southeast')

function Get-AlphaBounds {
    param([System.Drawing.Bitmap]$Bitmap)
    $minX = $Bitmap.Width; $maxX = -1; $minY = $Bitmap.Height; $maxY = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x,$y).A -eq 0) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
    if ($maxY -lt 0) { throw 'Cannot alternate an empty frame.' }
    [pscustomobject]@{MinX=$minX;MaxX=$maxX;MinY=$minY;MaxY=$maxY}
}

function Get-PelvisAxis {
    param([System.Drawing.Bitmap]$Bitmap,$Bounds,[int]$SeamY)
    $minX = $Bitmap.Width; $maxX = -1
    $corridorLeft = [Math]::Max(0,128-52)
    $corridorRight = [Math]::Min($Bitmap.Width-1,128+52)
    for ($y = $SeamY; $y -le [Math]::Min($SeamY+5,$Bounds.MaxY); $y++) {
        for ($x = $corridorLeft; $x -le $corridorRight; $x++) {
            if ($Bitmap.GetPixel($x,$y).A -eq 0) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
        }
    }
    if ($maxX -lt 0) { return 128.0 }
    ($minX + $maxX) * 0.5
}

function Write-AlternatingFrame {
    param([string]$SourcePath,[string]$UpperPath,[string]$DestinationPath)
    $source = New-Object System.Drawing.Bitmap $SourcePath
    $upper = New-Object System.Drawing.Bitmap $UpperPath
    try {
        if ($source.Width -ne 256 -or $source.Height -ne 256 -or
            $upper.Width -ne 256 -or $upper.Height -ne 256) {
            throw 'Alternating player frames must be 256x256.'
        }
        $sourceBounds = Get-AlphaBounds $source
        $upperBounds = Get-AlphaBounds $upper
        $sourceHeight = $sourceBounds.MaxY - $sourceBounds.MinY + 1
        $sourceSeam = [int][Math]::Round($sourceBounds.MinY + $sourceHeight * $SeamFraction)
        $sourceAxis = Get-PelvisAxis $source $sourceBounds $sourceSeam
        $upperAxis = Get-PelvisAxis $upper $upperBounds $sourceSeam
        $axisShift = [int][Math]::Round($upperAxis - $sourceAxis)

        $result = New-Object System.Drawing.Bitmap 256,256,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($result)
            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $baseFrame = if ($SeamBlendMode -eq 'AuthoredUpper') { $upper } else { $source }
                $graphics.DrawImageUnscaled($baseFrame,0,0)
            }
            finally { $graphics.Dispose() }

            if ($SeamBlendMode -eq 'SourceBridge') {
                # Research sheets with unrelated waist contours keep a narrow source-owned bridge.
                # The approved v3/v4 path uses AuthoredUpper so every arm/torso pixel stays authored.
                $upperCutoff = [Math]::Max(0,$sourceSeam-12)
                for ($y = 0; $y -lt $upperCutoff; $y++) {
                    for ($x = 0; $x -lt 256; $x++) {
                        $result.SetPixel($x,$y,$upper.GetPixel($x,$y))
                    }
                }
            }

            for ($y = $sourceSeam; $y -lt 256; $y++) {
                for ($x = 0; $x -lt 256; $x++) {
                    $result.SetPixel($x,$y,[System.Drawing.Color]::Transparent)
                }
            }

            for ($y = $sourceSeam; $y -le $sourceBounds.MaxY; $y++) {
                $destinationY = $y
                if ($destinationY -lt 0 -or $destinationY -ge 256) { continue }
                for ($x = 0; $x -lt 256; $x++) {
                    $pixel = $source.GetPixel($x,$y)
                    if ($pixel.A -eq 0) { continue }
                    $destinationX = [int][Math]::Round((2.0 * $sourceAxis) - $x) + $axisShift
                    if ($destinationX -lt 0 -or $destinationX -ge 256) { continue }
                    $result.SetPixel($destinationX,$destinationY,$pixel)
                }
            }
            $result.Save($DestinationPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $result.Dispose() }
    }
    finally {
        $source.Dispose()
        $upper.Dispose()
    }
}

foreach ($direction in $directions) {
    for ($phase = 0; $phase -lt 3; $phase++) {
        $sourcePath = Join-Path $InputFrames "player_${direction}_walk_${phase}_v2.png"
        $upperPath = Join-Path $InputFrames "player_${direction}_walk_$($phase+3)_v2.png"
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $upperPath -PathType Leaf)) {
            throw "Missing source half-cycle for $direction phase $phase."
        }
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $outputFrames ([IO.Path]::GetFileName($sourcePath))) -Force
        Write-AlternatingFrame -SourcePath $sourcePath -UpperPath $upperPath `
            -DestinationPath (Join-Path $outputFrames "player_${direction}_walk_$($phase+3)_v2.png")
    }
}

for ($part = 0; $part -lt 2; $part++) {
    $sheet = New-Object System.Drawing.Bitmap 1536,1024,
        ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.Clear([System.Drawing.Color]::Transparent)
            for ($row = 0; $row -lt 4; $row++) {
                $direction = $directions[$part*4+$row]
                for ($phase = 0; $phase -lt 6; $phase++) {
                    $path = Join-Path $outputFrames "player_${direction}_walk_${phase}_v2.png"
                    $frame = New-Object System.Drawing.Bitmap $path
                    try { $graphics.DrawImageUnscaled($frame,$phase*256,$row*256) }
                    finally { $frame.Dispose() }
                }
            }
        }
        finally { $graphics.Dispose() }
        $token = if ($part -eq 0) { 'a' } else { 'b' }
        $sheet.Save((Join-Path $OutputRoot "player_pixel_walk8dir6_${token}_v4.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $sheet.Dispose() }
}

$receipt = [ordered]@{
    schemaVersion = 1
    contract = 'FC-PLAYER-2D-WALK-V4-ALTERNATING-CANDIDATE'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    inputFrames = $InputFrames
    inputReceipt = Join-Path ([IO.Path]::GetDirectoryName($InputFrames)) 'source-receipt.json'
    directions = $directions
    phases = 6
    lowerBodySeamFraction = $SeamFraction
    seamBlendMode = $SeamBlendMode
    halfCycleRule = 'P3/P4/P5 keep generated upper body and use pelvis-axis-reflected lower body from P0/P1/P2.'
}
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'alternating-receipt.json'),
    ($receipt | ConvertTo-Json -Depth 5) + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_2D_WALK_V4_ALTERNATING: BUILT'
Write-Output "Frames: $outputFrames"
