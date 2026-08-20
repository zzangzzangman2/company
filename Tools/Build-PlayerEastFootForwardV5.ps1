[CmdletBinding()]
param(
    [string]$BaseFrames = '',
    [string]$SourceFrames = '',
    [string]$OutputRoot = '',
    [ValidateRange(200,220)]
    [int]$FootCutY = 216,
    [ValidateRange(0.65,0.80)]
    [double]$SeamFraction = 0.70,
    [switch]$RigidFeet,
    [switch]$FlatShoes,
    [ValidateRange(0,2)]
    [int]$CanonicalPhase = 1,
    [ValidateRange(0,2)]
    [int]$SwingCanonicalPhase = 0,
    [ValidateRange(224,236)]
    [int]$SupportFloorY = 233,
    [ValidateRange(2,12)]
    [int]$SwingLiftOuter = 6,
    [ValidateRange(4,16)]
    [int]$SwingLiftMiddle = 10
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
    $OutputRoot = Join-Path $projectRoot 'Artifacts\PlayerEastFootForwardV5Candidate'
}

$BaseFrames = [IO.Path]::GetFullPath($BaseFrames)
$SourceFrames = [IO.Path]::GetFullPath($SourceFrames)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$outputFrames = Join-Path $OutputRoot 'Frames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$outputFrames | Out-Null

Add-Type -AssemblyName System.Drawing

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
    if ($maxY -lt 0) { throw 'Cannot process an empty player frame.' }
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
        throw "Expected two separated east-foot runs at y=$CutY, found $($runs.Count)."
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
            if ($point.X -lt $minX) { $minX = $point.X }
            if ($point.X -gt $maxX) { $maxX = $point.X }
            if ($point.Y -lt $minY) { $minY = $point.Y }
            if ($point.Y -gt $maxY) { $maxY = $point.Y }
            for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    if ($dx -eq 0 -and $dy -eq 0) { continue }
                    $nx = $point.X + $dx; $ny = $point.Y + $dy
                    if ($nx -lt 0 -or $nx -ge $Bitmap.Width -or
                        $ny -lt $CutY -or $ny -ge $Bitmap.Height -or
                        $visited[$nx,$ny]) { continue }
                    if ($Bitmap.GetPixel($nx,$ny).A -eq 0) { continue }
                    $visited[$nx,$ny] = $true
                    $queue.Enqueue((New-Object System.Drawing.Point($nx,$ny)))
                }
            }
        }
        $components.Add([pscustomobject]@{
            AnchorX = ($run.Start + $run.End) * 0.5
            MinX = $minX
            MaxX = $maxX
            MinY = $minY
            MaxY = $maxY
            Pixels = $pixels
        })
    }
    $components
}

function Write-FootForwardFrame {
    param([string]$SourcePath,[string]$UpperPath,[string]$BasePath,[string]$DestinationPath)
    $source = New-Object System.Drawing.Bitmap $SourcePath
    $upper = New-Object System.Drawing.Bitmap $UpperPath
    $base = New-Object System.Drawing.Bitmap $BasePath
    try {
        if ($source.Width -ne 256 -or $source.Height -ne 256 -or
            $upper.Width -ne 256 -or $upper.Height -ne 256 -or
            $base.Width -ne 256 -or $base.Height -ne 256) {
            throw 'East-foot player frames must be 256x256.'
        }
        $sourceBounds = Get-AlphaBounds $source
        $upperBounds = Get-AlphaBounds $upper
        $sourceHeight = $sourceBounds.MaxY - $sourceBounds.MinY + 1
        $sourceSeam = [int][Math]::Round($sourceBounds.MinY + $sourceHeight * $SeamFraction)
        $sourceAxis = Get-PelvisAxis $source $sourceBounds $sourceSeam
        $upperAxis = Get-PelvisAxis $upper $upperBounds $sourceSeam
        $axisShift = [int][Math]::Round($upperAxis - $sourceAxis)
        $components = @(Get-FootComponents $source $FootCutY)

        $result = New-Object System.Drawing.Bitmap 256,256,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($result)
            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImageUnscaled($base,0,0)
            }
            finally { $graphics.Dispose() }

            foreach ($component in $components) {
                foreach ($point in $component.Pixels) {
                    $mirroredX = [int][Math]::Round((2.0 * $sourceAxis) - $point.X) + $axisShift
                    if ($mirroredX -ge 0 -and $mirroredX -lt 256) {
                        $result.SetPixel($mirroredX,$point.Y,[System.Drawing.Color]::Transparent)
                    }
                }
            }

            foreach ($component in $components) {
                $targetAnchorX = (2.0 * $sourceAxis) - $component.AnchorX + $axisShift
                $translateX = [int][Math]::Round($targetAnchorX - $component.AnchorX)
                foreach ($point in $component.Pixels) {
                    $destinationX = $point.X + $translateX
                    if ($destinationX -lt 0 -or $destinationX -ge 256) { continue }
                    $result.SetPixel($destinationX,$point.Y,$source.GetPixel($point.X,$point.Y))
                }
            }
            $result.Save($DestinationPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $result.Dispose() }
    }
    finally {
        $source.Dispose()
        $upper.Dispose()
        $base.Dispose()
    }
}

function Write-RigidFootFrame {
    param(
        [string]$CanonicalPath,
        [string]$SwingCanonicalPath,
        [string]$BasePath,
        [int]$Phase,
        [string]$DestinationPath)
    $canonical = New-Object System.Drawing.Bitmap $CanonicalPath
    $swingCanonical = New-Object System.Drawing.Bitmap $SwingCanonicalPath
    $base = New-Object System.Drawing.Bitmap $BasePath
    try {
        if ($canonical.Width -ne 256 -or $canonical.Height -ne 256 -or
            $base.Width -ne 256 -or $base.Height -ne 256) {
            throw 'Rigid east-foot player frames must be 256x256.'
        }
        $canonicalComponents = @(Get-FootComponents $canonical $FootCutY)
        $swingComponents = @(Get-FootComponents $swingCanonical $FootCutY)
        $targetComponents = @(Get-FootComponents $base $FootCutY)
        if ($canonicalComponents.Count -ne 2 -or $targetComponents.Count -ne 2) {
            throw 'Rigid east-foot replacement requires exactly two separated feet.'
        }

        $result = New-Object System.Drawing.Bitmap 256,256,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($result)
            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.DrawImageUnscaled($base,0,0)
            }
            finally { $graphics.Dispose() }

            for ($targetIndex = 0; $targetIndex -lt 2; $targetIndex++) {
                # Canonical east phase 1 owns two rigid shapes: left swing shoe and right support shoe.
                # The second half-cycle swaps their leg ownership without mirroring either shoe.
                $targetComponent = $targetComponents[$targetIndex]
                $isSupport = if ($Phase -lt 3) { $targetIndex -eq 1 } else { $targetIndex -eq 0 }
                if ($FlatShoes -or $isSupport) {
                    $sourceComponent = $canonicalComponents[1]
                    $sourceBitmap = $canonical
                } else {
                    # The swing foot uses a separate mild toe-up shoe instead of the steep
                    # phase-1 dangling shoe, so only the support sole touches the floor.
                    $sourceComponent = $swingComponents[1]
                    $sourceBitmap = $swingCanonical
                }
                $translateX = [int][Math]::Round($targetComponent.AnchorX - $sourceComponent.AnchorX)
                $swingLift = if (($Phase % 3) -eq 1) { $SwingLiftMiddle } else { $SwingLiftOuter }
                $targetBottomY = if ($isSupport) { $SupportFloorY } else { $SupportFloorY-$swingLift }
                $translateY = $targetBottomY - $sourceComponent.MaxY
                $clearFromY = $FootCutY + [Math]::Max(0,$translateY)
                foreach ($point in $targetComponent.Pixels) {
                    if ($point.Y -ge $clearFromY) {
                        $result.SetPixel($point.X,$point.Y,[System.Drawing.Color]::Transparent)
                    }
                }
                foreach ($point in $sourceComponent.Pixels) {
                    $destinationX = $point.X + $translateX
                    $destinationY = $point.Y + $translateY
                    if ($destinationX -lt 0 -or $destinationX -ge 256 -or
                        $destinationY -lt 0 -or $destinationY -ge 256) { continue }
                    $result.SetPixel($destinationX,$destinationY,$sourceBitmap.GetPixel($point.X,$point.Y))
                }
            }
            $result.Save($DestinationPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $result.Dispose() }
    }
    finally {
        $canonical.Dispose()
        $swingCanonical.Dispose()
        $base.Dispose()
    }
}

Get-ChildItem -LiteralPath $BaseFrames -Filter 'player_*_walk_*_v2.png' -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $outputFrames $_.Name) -Force
}
if ((Get-ChildItem -LiteralPath $outputFrames -Filter 'player_*_walk_*_v2.png' -File).Count -ne 48) {
    throw 'Expected exactly 48 base player frames.'
}

if ($RigidFeet) {
    $canonicalPath = Join-Path $SourceFrames "player_east_walk_${CanonicalPhase}_v2.png"
    $swingCanonicalPath = Join-Path $SourceFrames "player_east_walk_${SwingCanonicalPhase}_v2.png"
    for ($phase = 0; $phase -lt 6; $phase++) {
        $basePath = Join-Path $BaseFrames "player_east_walk_${phase}_v2.png"
        $destinationPath = Join-Path $outputFrames "player_east_walk_${phase}_v2.png"
        Write-RigidFootFrame -CanonicalPath $canonicalPath -SwingCanonicalPath $swingCanonicalPath -BasePath $basePath `
            -Phase $phase -DestinationPath $destinationPath
    }
}
else {
    for ($phase = 0; $phase -lt 3; $phase++) {
        $sourcePath = Join-Path $SourceFrames "player_east_walk_${phase}_v2.png"
        $upperPath = Join-Path $SourceFrames "player_east_walk_$($phase+3)_v2.png"
        $basePath = Join-Path $BaseFrames "player_east_walk_$($phase+3)_v2.png"
        $destinationPath = Join-Path $outputFrames "player_east_walk_$($phase+3)_v2.png"
        Write-FootForwardFrame -SourcePath $sourcePath -UpperPath $upperPath `
            -BasePath $basePath -DestinationPath $destinationPath
    }
}

$contactMeasurements = @()
if ($RigidFeet) {
    for ($phase = 0; $phase -lt 6; $phase++) {
        $framePath = Join-Path $outputFrames "player_east_walk_${phase}_v2.png"
        $frame = New-Object System.Drawing.Bitmap $framePath
        try {
            $leftBottom = -1; $rightBottom = -1
            for ($y = $FootCutY; $y -lt $frame.Height; $y++) {
                for ($x = 0; $x -lt $frame.Width; $x++) {
                    if ($frame.GetPixel($x,$y).A -eq 0) { continue }
                    if ($x -lt 128) {
                        if ($y -gt $leftBottom) { $leftBottom = $y }
                    } elseif ($y -gt $rightBottom) {
                        $rightBottom = $y
                    }
                }
            }
            $supportBottom = if ($phase -lt 3) { $rightBottom } else { $leftBottom }
            $swingBottom = if ($phase -lt 3) { $leftBottom } else { $rightBottom }
            $expectedSwingBottom = $SupportFloorY - $(if (($phase % 3) -eq 1) { $SwingLiftMiddle } else { $SwingLiftOuter })
            if ($supportBottom -ne $SupportFloorY -or $swingBottom -ne $expectedSwingBottom) {
                throw "East contact invariant failed at phase ${phase}: support=$supportBottom/$SupportFloorY swing=$swingBottom/$expectedSwingBottom."
            }

            $floorRuns = 0; $insideRun = $false
            for ($x = 0; $x -lt $frame.Width; $x++) {
                $opaque = $frame.GetPixel($x,$SupportFloorY).A -gt 0
                if ($opaque -and -not $insideRun) { $floorRuns++; $insideRun = $true }
                elseif (-not $opaque) { $insideRun = $false }
            }
            if ($floorRuns -ne 1) {
                throw "East single-contact invariant failed at phase ${phase}: floorRuns=$floorRuns/1."
            }
            $contactMeasurements += [ordered]@{
                phase = $phase
                supportBottomY = $supportBottom
                swingBottomY = $swingBottom
                clearancePx = $supportBottom-$swingBottom
                floorRuns = $floorRuns
            }
        }
        finally { $frame.Dispose() }
    }
}

$receipt = [ordered]@{
    schemaVersion = 1
    contract = if ($RigidFeet -and $FlatShoes) {
        'FC-PLAYER-EAST-FLAT-RIGID-FEET-V6-CANDIDATE'
    } elseif ($RigidFeet) {
        'FC-PLAYER-EAST-RIGID-FEET-V6-CANDIDATE'
    } else {
        'FC-PLAYER-EAST-FOOT-FORWARD-V5-CANDIDATE'
    }
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    baseFrames = $BaseFrames
    sourceFrames = $SourceFrames
    directionsChanged = @('east')
    phasesChanged = if ($RigidFeet) { @(0,1,2,3,4,5) } else { @(3,4,5) }
    footCutY = $FootCutY
    rigidFeet = [bool]$RigidFeet
    flatShoes = [bool]$FlatShoes
    canonicalPhase = if ($RigidFeet) { $CanonicalPhase } else { $null }
    swingCanonicalPhase = if ($RigidFeet -and -not $FlatShoes) { $SwingCanonicalPhase } else { $null }
    supportFloorY = if ($RigidFeet) { $SupportFloorY } else { $null }
    swingLiftOuter = if ($RigidFeet) { $SwingLiftOuter } else { $null }
    swingLiftMiddle = if ($RigidFeet) { $SwingLiftMiddle } else { $null }
    contactMeasurements = if ($RigidFeet) { $contactMeasurements } else { $null }
    rule = if ($RigidFeet -and $FlatShoes) {
        'Keep the approved v4 body and leg crossing. Reuse one flat east-facing support sneaker silhouette for both feet in all six phases. Translate the swing shoe vertically as one rigid object; never mirror, rotate, or warp it.'
    } elseif ($RigidFeet) {
        'Keep the approved v4 body and leg crossing. Reuse the same canonical east swing/support shoe silhouettes in all six phases, translating them between ankle anchors without mirroring, warping, or per-frame shape changes.'
    } else {
        'Keep the approved v4 east upper body and mirrored leg crossing; restore each source shoe without horizontal reflection at its mirrored ankle anchor.'
    }
}
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'east-foot-forward-receipt.json'),
    ($receipt | ConvertTo-Json -Depth 5) + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

if ($RigidFeet -and $FlatShoes) {
    Write-Output 'PLAYER_EAST_FLAT_RIGID_FEET_V6: BUILT'
} elseif ($RigidFeet) {
    Write-Output 'PLAYER_EAST_RIGID_FEET_V6: BUILT'
} else {
    Write-Output 'PLAYER_EAST_FOOT_FORWARD_V5: BUILT'
}
Write-Output "Frames: $outputFrames"
