[CmdletBinding()]
param(
    [string]$BaseFrames = '',
    [string]$SourceFrames = '',
    [string]$OutputRoot = '',
    [ValidateRange(212,220)]
    [int]$FootProbeY = 216
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($BaseFrames)) {
    $BaseFrames = Join-Path $projectRoot 'Artifacts\Player2DWalkV4AlternatingCandidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($SourceFrames)) {
    $SourceFrames = Join-Path $projectRoot 'Artifacts\Player2DWalkV3Candidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\PlayerEastSemanticShoesV12Candidate'
}
$BaseFrames = [IO.Path]::GetFullPath($BaseFrames)
$SourceFrames = [IO.Path]::GetFullPath($SourceFrames)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$outputFrames = Join-Path $OutputRoot 'Frames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$outputFrames | Out-Null

Add-Type -AssemblyName System.Drawing

function Test-PantsPixel {
    param([System.Drawing.Color]$Color)
    if ($Color.A -eq 0) { return $false }
    # The authored trousers are the only blue-dominant object below y=198.
    # A strict margin keeps neutral black shoe outlines in the footwear mask.
    return $Color.B -ge 24 -and
        $Color.B -ge ($Color.R + 7) -and
        $Color.B -ge ($Color.G + 4)
}

function Test-ShoeCorePixel {
    param([System.Drawing.Color]$Color)
    if ($Color.A -eq 0) { return $false }
    $red = $Color.R -ge 46 -and
        $Color.R -ge ($Color.G + 12) -and
        $Color.R -ge ($Color.B + 6)
    $white = $Color.R -ge 105 -and $Color.G -ge 82 -and $Color.B -ge 82 -and
        ([Math]::Max($Color.R,[Math]::Max($Color.G,$Color.B)) -
         [Math]::Min($Color.R,[Math]::Min($Color.G,$Color.B))) -le 90
    return $red -or $white
}

function Get-FootComponents {
    param([System.Drawing.Bitmap]$Bitmap,[int]$CutY)
    $visited = New-Object 'bool[,]' $Bitmap.Width,$Bitmap.Height
    $components = New-Object System.Collections.Generic.List[object]
    $runs = New-Object System.Collections.Generic.List[object]
    $runStart = -1
    for ($x = 0; $x -lt $Bitmap.Width; $x++) {
        if ($Bitmap.GetPixel($x,$CutY).A -gt 0) {
            if ($runStart -lt 0) { $runStart = $x }
        }
        elseif ($runStart -ge 0) {
            $runs.Add([pscustomobject]@{Start=$runStart;End=$x-1})
            $runStart = -1
        }
    }
    if ($runStart -ge 0) { $runs.Add([pscustomobject]@{Start=$runStart;End=$Bitmap.Width-1}) }
    if ($runs.Count -ne 2) {
        throw "Expected two separated foot runs at y=$CutY, found $($runs.Count)."
    }

    foreach ($run in $runs) {
        $queue = New-Object 'System.Collections.Generic.Queue[System.Drawing.Point]'
        for ($x = $run.Start; $x -le $run.End; $x++) {
            if (-not $visited[$x,$CutY]) {
                $visited[$x,$CutY] = $true
                $queue.Enqueue((New-Object System.Drawing.Point($x,$CutY)))
            }
        }
        $pixels = New-Object 'System.Collections.Generic.List[System.Drawing.Point]'
        $minX = $Bitmap.Width; $maxX = -1; $minY = $Bitmap.Height; $maxY = -1
        while ($queue.Count -gt 0) {
            $point = $queue.Dequeue()
            $pixels.Add($point)
            $minX = [Math]::Min($minX,$point.X); $maxX = [Math]::Max($maxX,$point.X)
            $minY = [Math]::Min($minY,$point.Y); $maxY = [Math]::Max($maxY,$point.Y)
            for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    if ($dx -eq 0 -and $dy -eq 0) { continue }
                    $nx = $point.X+$dx; $ny = $point.Y+$dy
                    if ($nx -lt 0 -or $nx -ge $Bitmap.Width -or
                        $ny -lt $CutY -or $ny -ge $Bitmap.Height -or
                        $visited[$nx,$ny] -or $Bitmap.GetPixel($nx,$ny).A -eq 0) { continue }
                    $visited[$nx,$ny] = $true
                    $queue.Enqueue((New-Object System.Drawing.Point($nx,$ny)))
                }
            }
        }
        $components.Add([pscustomobject]@{
            AnchorX = ($run.Start+$run.End)*0.5
            MinX=$minX; MaxX=$maxX; MinY=$minY; MaxY=$maxY; Pixels=$pixels
        })
    }
    $components
}

function Get-SemanticShoe {
    param([System.Drawing.Bitmap]$Bitmap,$FootComponent)
    $regionMinX = [Math]::Max(0,$FootComponent.MinX-5)
    $regionMaxX = [Math]::Min($Bitmap.Width-1,$FootComponent.MaxX+5)
    $regionMinY = [Math]::Max(198,$FootComponent.MinY-18)
    $regionMaxY = [Math]::Min($Bitmap.Height-1,$FootComponent.MaxY+2)

    $core = New-Object System.Collections.Generic.List[System.Drawing.Point]
    $coreMinY = $Bitmap.Height
    for ($y = $regionMinY; $y -le $regionMaxY; $y++) {
        for ($x = $regionMinX; $x -le $regionMaxX; $x++) {
            if (Test-ShoeCorePixel $Bitmap.GetPixel($x,$y)) {
                $core.Add((New-Object System.Drawing.Point($x,$y)))
                $coreMinY = [Math]::Min($coreMinY,$y)
            }
        }
    }
    if ($core.Count -lt 20) { throw 'Could not find a semantic red/white shoe core.' }

    # Flood through every non-blue opaque footwear pixel from the colored core.
    # The top clamp prevents a neutral pant outline from climbing the shin.
    $maskTop = [Math]::Max($regionMinY,$coreMinY-2)
    $visited = New-Object 'bool[,]' $Bitmap.Width,$Bitmap.Height
    $queue = New-Object 'System.Collections.Generic.Queue[System.Drawing.Point]'
    foreach ($point in $core) {
        if ($point.Y -lt $maskTop -or $visited[$point.X,$point.Y]) { continue }
        $visited[$point.X,$point.Y] = $true
        $queue.Enqueue($point)
    }
    $pixels = New-Object System.Collections.Generic.List[System.Drawing.Point]
    $minX = $Bitmap.Width; $maxX = -1; $minY = $Bitmap.Height; $maxY = -1
    while ($queue.Count -gt 0) {
        $point = $queue.Dequeue()
        $pixels.Add($point)
        $minX=[Math]::Min($minX,$point.X);$maxX=[Math]::Max($maxX,$point.X)
        $minY=[Math]::Min($minY,$point.Y);$maxY=[Math]::Max($maxY,$point.Y)
        for ($dy=-1;$dy-le1;$dy++) {
            for ($dx=-1;$dx-le1;$dx++) {
                if ($dx-eq0 -and $dy-eq0) { continue }
                $nx=$point.X+$dx;$ny=$point.Y+$dy
                if ($nx-lt$regionMinX -or $nx-gt$regionMaxX -or
                    $ny-lt$maskTop -or $ny-gt$regionMaxY -or $visited[$nx,$ny]) { continue }
                $pixel=$Bitmap.GetPixel($nx,$ny)
                if ($pixel.A-eq0 -or (Test-PantsPixel $pixel)) { continue }
                $visited[$nx,$ny]=$true
                $queue.Enqueue((New-Object System.Drawing.Point($nx,$ny)))
            }
        }
    }

    $collarXs = New-Object System.Collections.Generic.List[double]
    foreach ($point in $core) {
        if ($point.Y -le $coreMinY+3) { $collarXs.Add($point.X) }
    }
    if ($collarXs.Count -eq 0) { throw 'Could not resolve the shoe collar anchor.' }
    $anchorX = ($collarXs | Measure-Object -Average).Average
    [pscustomobject]@{
        AnchorX=$anchorX; CoreMinY=$coreMinY
        MinX=$minX;MaxX=$maxX;MinY=$minY;MaxY=$maxY;Pixels=$pixels
    }
}

function Write-SemanticShoeFrame {
    param(
        [string]$BasePath,
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$DebugPath,
        [int]$Phase)
    $base = New-Object System.Drawing.Bitmap $BasePath
    $source = New-Object System.Drawing.Bitmap $SourcePath
    try {
        $feet = @(Get-FootComponents $base $FootProbeY)
        $shoes = @(
            (Get-SemanticShoe $base $feet[0]),
            (Get-SemanticShoe $base $feet[1]))
        $sourceFeet = @(Get-FootComponents $source $FootProbeY)
        $sourceShoes = @(
            (Get-SemanticShoe $source $sourceFeet[0]),
            (Get-SemanticShoe $source $sourceFeet[1]))
        $result = $base.Clone(
            (New-Object System.Drawing.Rectangle(0,0,256,256)),
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $debug = New-Object System.Drawing.Bitmap 256,256,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $debugGraphics=[System.Drawing.Graphics]::FromImage($debug)
            try {
                $debugGraphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $debugGraphics.DrawImageUnscaled($base,0,0)
            }
            finally{$debugGraphics.Dispose()}

            foreach ($shoe in $shoes) {
                foreach ($point in $shoe.Pixels) {
                    # Use transparent black so alpha-blind debug tools cannot
                    # display the removed mirrored shoe as a white silhouette.
                    $result.SetPixel($point.X,$point.Y,[System.Drawing.Color]::FromArgb(0,0,0,0))
                    $debug.SetPixel($point.X,$point.Y,[System.Drawing.Color]::FromArgb(220,255,0,255))
                }
                $anchor = [int][Math]::Round($shoe.AnchorX)
                for ($dy=-2;$dy-le2;$dy++) {
                    for ($dx=-2;$dx-le2;$dx++) {
                        $x=$anchor+$dx;$y=$shoe.CoreMinY+$dy
                        if ($x-ge0-and$x-lt256-and$y-ge0-and$y-lt256) {
                            $debug.SetPixel($x,$y,[System.Drawing.Color]::Cyan)
                        }
                    }
                }
            }

            # V4 target-left is the pelvis reflection of V3 source-right and
            # target-right is source-left.  Translate the clean authored shoe
            # to the reflected collar; do not mirror or move any trouser pixel.
            # The offsets are the rounded collar-boundary centroids measured
            # from the exact V3/V4 source pair, never a screen-side guess.
            $translationByPhase = @{
                3 = @(-52,62)
                4 = @(-34,31)
                5 = @(-48,63)
            }
            # At the crossing frame, the right swing shoe is behind the left
            # support shoe. Draw back-to-front; the wide poses do not overlap.
            $order = if ($Phase -eq 4) { @(1,0) } else { @(0,1) }
            foreach ($targetIndex in $order) {
                $sourceIndex = 1-$targetIndex
                $sourceShoe = $sourceShoes[$sourceIndex]
                $translateX = $translationByPhase[$Phase][$targetIndex]
                foreach ($point in $sourceShoe.Pixels) {
                    $destinationX=$point.X+$translateX
                    if ($destinationX-lt0-or$destinationX-ge256) { continue }
                    $existing=$base.GetPixel($destinationX,$point.Y)
                    if (Test-PantsPixel $existing) { continue }
                    $result.SetPixel($destinationX,$point.Y,$source.GetPixel($point.X,$point.Y))
                }
            }
            $result.Save($DestinationPath,[System.Drawing.Imaging.ImageFormat]::Png)
            $debug.Save($DebugPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $result.Dispose(); $debug.Dispose() }
    }
    finally { $base.Dispose(); $source.Dispose() }
}

$baseFiles=@(Get-ChildItem -LiteralPath $BaseFrames -Filter 'player_*_walk_*_v2.png' -File)
if($baseFiles.Count-ne48){throw "Expected 48 base frames, found $($baseFiles.Count)."}
foreach($file in $baseFiles){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $outputFrames $file.Name) -Force}

for($phase=3;$phase-le5;$phase++){
    $basePath=Join-Path $BaseFrames "player_east_walk_${phase}_v2.png"
    $sourcePath=Join-Path $SourceFrames "player_east_walk_$($phase-3)_v2.png"
    Write-SemanticShoeFrame -BasePath $basePath `
        -SourcePath $sourcePath `
        -DestinationPath (Join-Path $outputFrames "player_east_walk_${phase}_v2.png") `
        -DebugPath (Join-Path $OutputRoot "player_east_walk_${phase}_semantic_mask.png") -Phase $phase
}

$receipt=[ordered]@{
    schemaVersion=1
    contract='FC-PLAYER-EAST-SEMANTIC-SHOES-V12-CANDIDATE'
    generatedUtc=[DateTime]::UtcNow.ToString('o')
    baseFrames=$BaseFrames
    sourceFrames=$SourceFrames
    directionsChanged=@('east')
    phasesChanged=@(3,4,5)
    footProbeY=$FootProbeY
    ownerMap=@('P3 L=P0R / R=P0L','P4 L=P1R / R=P1L','P5 L=P2R / R=P2L')
    collarTranslationX=@(@(-52,62),@(-34,31),@(-48,63))
    rule='Keep the reflected V4 leg geometry intact. Select only red/white/neutral footwear pixels, exclude blue trousers, remove the complete mirrored shoe, and translate the matching clean V3 shoe to the reflected collar anchor without mirroring it. Never transform the shin or calf.'
}
[IO.File]::WriteAllText((Join-Path $OutputRoot 'semantic-shoes-receipt.json'),
    ($receipt|ConvertTo-Json -Depth 5)+[Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_EAST_SEMANTIC_SHOES_V12: BUILT'
Write-Output "Frames: $outputFrames"
