[CmdletBinding()]
param(
    [string]$PartA = '',
    [string]$PartB = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($PartA)) {
    $PartA = Join-Path $projectRoot 'ArtSources\PlayerWalk2DGenerated\player_walk8dir6_a_chroma_v2.png'
}
if ([string]::IsNullOrWhiteSpace($PartB)) {
    $PartB = Join-Path $projectRoot 'ArtSources\PlayerWalk2DGenerated\player_walk8dir6_b_chroma_v2.png'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\Player2DWalkV2Candidate'
}

$PartA = [IO.Path]::GetFullPath($PartA)
$PartB = [IO.Path]::GetFullPath($PartB)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$framesRoot = Join-Path $OutputRoot 'Frames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$framesRoot | Out-Null

$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$directionsA = @('south','southwest','west','northwest')
$directionsB = @('north','northeast','east','southeast')
$rowStarts = @(0,270,510,750)
$rowEnds = @(270,510,750,1024)
$frameSize = 256
$targetTop = 24

Add-Type -AssemblyName System.Drawing

function Convert-ChromaSheet {
    param([string]$InputPath,[string]$OutputPath)
    if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        throw "Missing generated chroma sheet: $InputPath"
    }
    & $ffmpeg -hide_banner -loglevel error -y -i $InputPath `
        -vf 'colorkey=0x00FF00:0.28:0.0,despill=green:mix=0.65,format=rgba' `
        -frames:v 1 $OutputPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Chroma removal failed: $InputPath"
    }
}

function Measure-AlphaBounds {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$Left,
        [int]$Top,
        [int]$Width,
        [int]$Height
    )
    $minX = $Left + $Width
    $maxX = -1
    $minY = $Top + $Height
    $maxY = -1
    for ($y = $Top; $y -lt $Top + $Height; $y++) {
        for ($x = $Left; $x -lt $Left + $Width; $x++) {
            if ($Bitmap.GetPixel($x,$y).A -eq 0) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
    if ($maxX -lt $minX -or $maxY -lt $minY) {
        throw "Empty generated cell at left=$Left top=$Top"
    }
    [pscustomobject]@{ MinX=$minX; MaxX=$maxX; MinY=$minY; MaxY=$maxY }
}

function Measure-UpperCenterX {
    param([System.Drawing.Bitmap]$Bitmap,$Bounds)
    $height = $Bounds.MaxY - $Bounds.MinY + 1
    $upperBottom = $Bounds.MinY + [int][Math]::Floor($height * 0.60)
    $minX = $Bounds.MaxX
    $maxX = $Bounds.MinX
    for ($y = $Bounds.MinY; $y -le $upperBottom; $y++) {
        for ($x = $Bounds.MinX; $x -le $Bounds.MaxX; $x++) {
            if ($Bitmap.GetPixel($x,$y).A -eq 0) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
        }
    }
    if ($maxX -lt $minX) { throw 'Generated cell has no upper-body silhouette.' }
    ($minX + $maxX) * 0.5
}

function Measure-FrameBounds {
    param([System.Drawing.Bitmap]$Bitmap)
    Measure-AlphaBounds -Bitmap $Bitmap -Left 0 -Top 0 -Width $Bitmap.Width -Height $Bitmap.Height
}

function Save-NormalizedPart {
    param(
        [string]$KeyedPath,
        [string[]]$Directions,
        [string]$PartToken
    )
    $sheet = [System.Drawing.Bitmap]::FromFile($KeyedPath)
    try {
        if ($sheet.Width -ne 1536 -or $sheet.Height -ne 1024) {
            throw "Generated sheet must be 1536x1024: $KeyedPath"
        }
        $normalizedSheet = New-Object System.Drawing.Bitmap 1536,1024,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $sheetGraphics = [System.Drawing.Graphics]::FromImage($normalizedSheet)
            try {
                $sheetGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $sheetGraphics.Clear([System.Drawing.Color]::Transparent)
                for ($row = 0; $row -lt 4; $row++) {
                    $bandTop = $rowStarts[$row]
                    $bandHeight = $rowEnds[$row] - $bandTop
                    for ($phase = 0; $phase -lt 6; $phase++) {
                        $cellLeft = $phase * $frameSize
                        $bounds = Measure-AlphaBounds -Bitmap $sheet -Left $cellLeft -Top $bandTop `
                            -Width $frameSize -Height $bandHeight
                        $upperCenter = Measure-UpperCenterX -Bitmap $sheet -Bounds $bounds
                        $destX = [int][Math]::Round(128.0 - ($upperCenter - $cellLeft))
                        $destY = $targetTop - ($bounds.MinY - $bandTop)
                        $height = $bounds.MaxY - $bounds.MinY + 1
                        if ($targetTop + $height -gt 256) {
                            throw "Generated figure is too tall after head lock: $($Directions[$row])/P$phase height=$height"
                        }

                        $frame = New-Object System.Drawing.Bitmap 256,256,
                            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                        try {
                            $graphics = [System.Drawing.Graphics]::FromImage($frame)
                            try {
                                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                                $graphics.Clear([System.Drawing.Color]::Transparent)
                                $sourceRect = New-Object System.Drawing.Rectangle $cellLeft,$bandTop,$frameSize,$bandHeight
                                $destinationRect = New-Object System.Drawing.Rectangle $destX,$destY,$frameSize,$bandHeight
                                $graphics.DrawImage($sheet,$destinationRect,$sourceRect,
                                    [System.Drawing.GraphicsUnit]::Pixel)
                            }
                            finally { $graphics.Dispose() }

                            $frameBounds = Measure-FrameBounds -Bitmap $frame
                            if ($frameBounds.MinX -lt 2 -or $frameBounds.MaxX -gt 253 -or
                                $frameBounds.MinY -lt 2 -or $frameBounds.MaxY -gt 253) {
                                throw "Normalized frame clips canvas: $($Directions[$row])/P$phase " +
                                    "bbox=$($frameBounds.MinX),$($frameBounds.MinY)-$($frameBounds.MaxX),$($frameBounds.MaxY)"
                            }
                            $frameName = "player_$($Directions[$row])_walk_${phase}_v2.png"
                            $framePath = Join-Path $framesRoot $frameName
                            $frame.Save($framePath,[System.Drawing.Imaging.ImageFormat]::Png)
                            $sheetGraphics.DrawImageUnscaled($frame,$phase*256,$row*256)
                            $script:receiptFrames += [pscustomobject]@{
                                direction=$Directions[$row]
                                phase=$phase
                                file=$frameName
                                bbox=@($frameBounds.MinX,$frameBounds.MinY,$frameBounds.MaxX,$frameBounds.MaxY)
                                sourceCell=@($cellLeft,$bandTop,$frameSize,$bandHeight)
                                normalizationShift=@($destX,$destY)
                            }
                        }
                        finally { $frame.Dispose() }
                    }
                }
            }
            finally { $sheetGraphics.Dispose() }
            $sheetPath = Join-Path $OutputRoot "player_pixel_walk8dir6_${PartToken}_v2.png"
            $normalizedSheet.Save($sheetPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $normalizedSheet.Dispose() }
    }
    finally { $sheet.Dispose() }
}

$keyedA = Join-Path $OutputRoot 'player_walk8dir6_a_keyed_v2.png'
$keyedB = Join-Path $OutputRoot 'player_walk8dir6_b_keyed_v2.png'
Convert-ChromaSheet -InputPath $PartA -OutputPath $keyedA
Convert-ChromaSheet -InputPath $PartB -OutputPath $keyedB

$script:receiptFrames = @()
Save-NormalizedPart -KeyedPath $keyedA -Directions $directionsA -PartToken 'a'
Save-NormalizedPart -KeyedPath $keyedB -Directions $directionsB -PartToken 'b'

$receipt = [ordered]@{
    schemaVersion = 1
    contract = 'FC-PLAYER-2D-WALK-V2-CANDIDATE'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    sourcePartA = $PartA
    sourcePartB = $PartB
    sourcePartASha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PartA).Hash
    sourcePartBSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PartB).Hash
    chromaKey = '0x00FF00 similarity=0.28 blend=0.0 despill=green mix=0.65'
    canvas = 256
    directions = @($directionsA + $directionsB)
    phases = 6
    headTopPixels = $targetTop
    frames = $script:receiptFrames
}
$receiptJson = $receipt | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'source-receipt.json'),
    $receiptJson + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_2D_WALK_V2_CANDIDATE: BUILT'
Write-Output "Frames: $framesRoot"
Write-Output "Receipt: $(Join-Path $OutputRoot 'source-receipt.json')"
