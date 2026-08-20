[CmdletBinding()]
param(
    [string]$PartA = '',
    [string]$PartB = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($PartA)) {
    $PartA = Join-Path $projectRoot 'ArtSources\PlayerWalk2DGenerated\player_walk8dir3half_a_chroma_v5.png'
}
if ([string]::IsNullOrWhiteSpace($PartB)) {
    $PartB = Join-Path $projectRoot 'ArtSources\PlayerWalk2DGenerated\player_walk8dir3half_b_chroma_v5.png'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\Player2DWalkV5HalfCycleCandidate'
}
$PartA = [IO.Path]::GetFullPath($PartA)
$PartB = [IO.Path]::GetFullPath($PartB)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$unassembledRoot = Join-Path $OutputRoot 'UnassembledFrames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$unassembledRoot | Out-Null

$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
Add-Type -AssemblyName System.Drawing
$directionsA = @('south','southwest','west','northwest')
$directionsB = @('north','northeast','east','southeast')
$rowStarts = @(0,256,512,750)
$rowEnds = @(256,512,750,1024)
$targetTop = 12
$uniformScale = 1.00

function Convert-ChromaSheet {
    param([string]$InputPath,[string]$OutputPath)
    if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        throw "Missing half-cycle chroma sheet: $InputPath"
    }
    & $ffmpeg -hide_banner -loglevel error -y -i $InputPath `
        -vf 'colorkey=0x00FF00:0.28:0.0,despill=green:mix=0.65,format=rgba' `
        -frames:v 1 $OutputPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Chroma removal failed: $InputPath"
    }
}

function Measure-AlphaBounds {
    param([System.Drawing.Bitmap]$Bitmap,[int]$Left,[int]$Top,[int]$Width,[int]$Height)
    $minX=$Left+$Width; $maxX=-1; $minY=$Top+$Height; $maxY=-1
    for($y=$Top;$y -lt $Top+$Height;$y++){
        for($x=$Left;$x -lt $Left+$Width;$x++){
            if($Bitmap.GetPixel($x,$y).A -eq 0){continue}
            if($x -lt $minX){$minX=$x}; if($x -gt $maxX){$maxX=$x}
            if($y -lt $minY){$minY=$y}; if($y -gt $maxY){$maxY=$y}
        }
    }
    if($maxX -lt 0){throw "Empty half-cycle cell at $Left,$Top"}
    [pscustomobject]@{MinX=$minX;MaxX=$maxX;MinY=$minY;MaxY=$maxY}
}

function Measure-UpperCenterX {
    param([System.Drawing.Bitmap]$Bitmap,$Bounds)
    $upperBottom=$Bounds.MinY+[int][Math]::Floor(($Bounds.MaxY-$Bounds.MinY+1)*0.60)
    $minX=$Bounds.MaxX; $maxX=$Bounds.MinX
    for($y=$Bounds.MinY;$y -le $upperBottom;$y++){
        for($x=$Bounds.MinX;$x -le $Bounds.MaxX;$x++){
            if($Bitmap.GetPixel($x,$y).A -eq 0){continue}
            if($x -lt $minX){$minX=$x}; if($x -gt $maxX){$maxX=$x}
        }
    }
    ($minX+$maxX)*0.5
}

function Write-HalfFrames {
    param([string]$KeyedPath,[string[]]$Directions)
    $sheet=New-Object System.Drawing.Bitmap $KeyedPath
    try{
        if($sheet.Width -ne 1536 -or $sheet.Height -ne 1024){
            throw "Half-cycle sheet must be 1536x1024: $KeyedPath"
        }
        for($row=0;$row -lt 4;$row++){
            $top=$rowStarts[$row]
            $bandHeight=$rowEnds[$row]-$top
            for($phase=0;$phase -lt 3;$phase++){
                $left=$phase*512
                $bounds=Measure-AlphaBounds $sheet $left $top 512 $bandHeight
                $upperCenter=Measure-UpperCenterX $sheet $bounds
                $destX=[int][Math]::Round(128.0-(($upperCenter-$left)*$uniformScale))
                $destY=$targetTop-[int][Math]::Round(($bounds.MinY-$top)*$uniformScale)
                $height=[int][Math]::Round(($bounds.MaxY-$bounds.MinY+1)*$uniformScale)
                if($targetTop+$height -gt 254){
                    throw "Half-cycle figure clips normalized frame: $($Directions[$row])/P$phase height=$height"
                }
                $frame=New-Object System.Drawing.Bitmap 256,256,
                    ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try{
                    $graphics=[System.Drawing.Graphics]::FromImage($frame)
                    try{
                        $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                        $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                        $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
                        $graphics.Clear([System.Drawing.Color]::Transparent)
                        $sourceRect=New-Object System.Drawing.Rectangle $left,$top,512,$bandHeight
                        $destinationRect=New-Object System.Drawing.Rectangle $destX,$destY,
                            ([int][Math]::Round(512*$uniformScale)),
                            ([int][Math]::Round($bandHeight*$uniformScale))
                        $graphics.DrawImage($sheet,$destinationRect,$sourceRect,[System.Drawing.GraphicsUnit]::Pixel)
                    }
                    finally{$graphics.Dispose()}
                    $path=Join-Path $unassembledRoot "player_$($Directions[$row])_walk_${phase}_v2.png"
                    $frame.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally{$frame.Dispose()}
            }
        }
    }
    finally{$sheet.Dispose()}
}

$keyedA=Join-Path $OutputRoot 'player_walk8dir3half_a_keyed_v5.png'
$keyedB=Join-Path $OutputRoot 'player_walk8dir3half_b_keyed_v5.png'
Convert-ChromaSheet $PartA $keyedA
Convert-ChromaSheet $PartB $keyedB
Write-HalfFrames $keyedA $directionsA
Write-HalfFrames $keyedB $directionsB

# The first half owns the anatomy. The reversed upper-body order supplies the opposite arm swing;
# Build-Player2DWalkAlternatingV4 replaces only the lower body with an exact pelvis reflection.
$directions=@($directionsA+$directionsB)
foreach($direction in $directions){
    $upperMap=@(0,1,2)
    for($offset=0;$offset -lt 3;$offset++){
        $source=Join-Path $unassembledRoot "player_${direction}_walk_$($upperMap[$offset])_v2.png"
        $destination=Join-Path $unassembledRoot "player_${direction}_walk_$($offset+3)_v2.png"
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

$alternatingBuilder=Join-Path $PSScriptRoot 'Build-Player2DWalkAlternatingV4.ps1'
& powershell -NoProfile -ExecutionPolicy Bypass -File $alternatingBuilder `
    -InputFrames $unassembledRoot -OutputRoot $OutputRoot -SeamFraction 0.72 `
    -SeamBlendMode SourceBridge
if($LASTEXITCODE -ne 0){throw "Alternating half-cycle assembly failed: $LASTEXITCODE"}

$receipt=[ordered]@{
    schemaVersion=1
    contract='FC-PLAYER-2D-WALK-V5-HALF-CYCLE-CANDIDATE'
    generatedUtc=[DateTime]::UtcNow.ToString('o')
    sourcePartA=$PartA
    sourcePartB=$PartB
    sourcePartASha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $PartA).Hash
    sourcePartBSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $PartB).Hash
    sourceColumns=3
    runtimePhases=6
    uniformScale=$uniformScale
    rule='P0/P1/P2 authored half-step; P3/P4/P5 use reversed upper timing and reflected lower-body anatomy.'
}
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'half-cycle-source-receipt.json'),
    ($receipt|ConvertTo-Json -Depth 5)+[Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_2D_WALK_V5_HALF_CYCLE: BUILT'
Write-Output "Frames: $(Join-Path $OutputRoot 'Frames')"
