param(
    [string]$QaRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Artifacts\Family3DPrototypeV3\D3D11QaRun3Visible')
)

$ErrorActionPreference = 'Stop'
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$frameRoot = Join-Path $QaRoot 'frames'
$metadataPath = Join-Path $QaRoot 'frame-metadata.csv'
$receiptPath = Join-Path $QaRoot 'qa-receipt.json'
if (-not (Test-Path -LiteralPath $metadataPath) -or -not (Test-Path -LiteralPath $receiptPath)) {
    throw "Missing V3 QA receipt or frame metadata: $QaRoot"
}

$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if ($receipt.contract -ne 'FC-FAMILY-3D-MOTION-LAB-V3' -or
    -not $receipt.automaticGatesPass -or
    $receipt.status -ne 'AUTO_PASS_VISUAL_REVIEW_REQUIRED') {
    throw "V3 automatic gates are not PASS: $($receipt.status)"
}

$metadata = @(Import-Csv -LiteralPath $metadataPath)
$frames = @(Get-ChildItem -LiteralPath $frameRoot -Filter 'frame_*.png' -File | Sort-Object Name)
if ($metadata.Count -ne [int]$receipt.capturedFrames -or $frames.Count -ne $metadata.Count) {
    throw "Frame count mismatch: metadata=$($metadata.Count) receipt=$($receipt.capturedFrames) png=$($frames.Count)"
}

Add-Type -AssemblyName System.Drawing
for ($index = 0; $index -lt $metadata.Count; $index++) {
    $expectedName = 'frame_{0:D4}.png' -f $index
    if ($frames[$index].Name -ne $expectedName -or [int]$metadata[$index].frame -ne $index) {
        throw "Non-contiguous frame sequence at ${index}: $($frames[$index].Name) / $($metadata[$index].frame)"
    }
    $bitmap = [System.Drawing.Image]::FromFile($frames[$index].FullName)
    try {
        if ($bitmap.Width -ne 1280 -or $bitmap.Height -ne 720) {
            throw "Unexpected frame dimensions at $expectedName`: $($bitmap.Width)x$($bitmap.Height)"
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-FirstDirectionSegment {
    param([string]$Direction)
    $start = -1
    for ($index = 0; $index -lt $metadata.Count; $index++) {
        if ($metadata[$index].direction -eq $Direction) {
            $start = $index
            break
        }
    }
    if ($start -lt 0) { throw "Direction missing from metadata: $Direction" }
    $end = $start
    while ($end + 1 -lt $metadata.Count -and $metadata[$end + 1].direction -eq $Direction) {
        $end++
    }
    return @($metadata[$start..$end])
}

$cycleSeconds = [double]$receipt.cycleSeconds
$marginSeconds = 0.15
$directionFrames = [ordered]@{}
$selectionRows = [System.Collections.Generic.List[object]]::new()
foreach ($direction in @('SW', 'NW', 'NE', 'SE')) {
    $segment = @(Get-FirstDirectionSegment -Direction $direction)
    $segmentStart = [double]$segment[0].motionClock
    $segmentEnd = [double]$segment[-1].motionClock
    $anchors = @($segment | Where-Object {
        [int]$_.pose -eq 0 -and
        [double]$_.phase -lt 0.08 -and
        [double]$_.motionClock -ge $segmentStart + $marginSeconds -and
        [double]$_.motionClock + $cycleSeconds -le $segmentEnd - $marginSeconds
    } | Sort-Object @{ Expression = { [double]$_.motionClock } })
    if ($anchors.Count -eq 0) {
        throw "No complete, turn-safe gait cycle found for $direction"
    }
    $anchor = $anchors[0]
    $anchorClock = [double]$anchor.motionClock
    $chosen = [System.Collections.Generic.List[int]]::new()
    for ($pose = 0; $pose -lt 6; $pose++) {
        $targetClock = $anchorClock + $cycleSeconds * ($pose / 6.0)
        $candidate = $segment |
            Where-Object {
                [int]$_.pose -eq $pose -and
                [double]$_.motionClock -ge $anchorClock - 0.03 -and
                [double]$_.motionClock -le $anchorClock + $cycleSeconds
            } |
            Sort-Object @{ Expression = { [Math]::Abs([double]$_.motionClock - $targetClock) } } |
            Select-Object -First 1
        if ($null -eq $candidate) { throw "Missing $direction P$pose inside selected cycle" }
        $chosen.Add([int]$candidate.frame)
        $selectionRows.Add([pscustomobject]@{
            direction = $direction
            pose = $pose
            frame = [int]$candidate.frame
            motionClock = [double]$candidate.motionClock
            phase = [double]$candidate.phase
        })
    }
    for ($index = 1; $index -lt $chosen.Count; $index++) {
        if ($chosen[$index] -le $chosen[$index - 1]) {
            throw "Non-increasing selected cycle for ${direction}: $($chosen -join ',')"
        }
    }
    $directionFrames[$direction] = @($chosen)
}

$selectionRows | Export-Csv -LiteralPath (Join-Path $QaRoot 'selected-pose-frames-v3.csv') -NoTypeInformation -Encoding UTF8

& $ffmpeg -y -hide_banner -loglevel warning `
    -framerate 30 -start_number 0 -i (Join-Path $frameRoot 'frame_%04d.png') `
    -frames:v $metadata.Count -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart `
    (Join-Path $QaRoot 'family-3d-four-direction-synced-loop-v3.mp4')
if ($LASTEXITCODE -ne 0) { throw "ffmpeg loop encoding failed: $LASTEXITCODE" }

$transitionRows = [System.Collections.Generic.List[object]]::new()
for ($index = 1; $index -lt $metadata.Count; $index++) {
    if ($metadata[$index].direction -ne $metadata[$index - 1].direction) {
        $transitionRows.Add([pscustomobject]@{
            From = $metadata[$index - 1].direction
            To = $metadata[$index].direction
            Frame = [int]$metadata[$index].frame
        })
    }
}
if ($transitionRows.Count -lt 4) { throw "Four direction transitions were not captured" }
$turnArguments = [System.Collections.Generic.List[string]]::new()
$turnArguments.Add('-y')
$turnArguments.Add('-hide_banner')
$turnArguments.Add('-loglevel')
$turnArguments.Add('warning')
$turnFilters = [System.Collections.Generic.List[string]]::new()
$turnLabels = [System.Collections.Generic.List[string]]::new()
$turnLayout = [System.Collections.Generic.List[string]]::new()
$turnInput = 0
for ($row = 0; $row -lt 4; $row++) {
    $transition = $transitionRows[$row]
    for ($offset = -1; $offset -le 5; $offset++) {
        $frame = [Math]::Max(0, [Math]::Min($metadata.Count - 1, $transition.Frame + $offset))
        $turnArguments.Add('-i')
        $turnArguments.Add((Join-Path $frameRoot ('frame_{0:D4}.png' -f $frame)))
        $label = "u$turnInput"
        $turnFilters.Add(
            "[$turnInput`:v]scale=256:144," +
            "drawtext=fontfile='C\:/Windows/Fonts/arialbd.ttf':" +
            "text='$($transition.From)-$($transition.To) f$frame':x=6:y=6:" +
            "fontsize=15:fontcolor=white:borderw=2:bordercolor=black,setsar=1[$label]")
        $turnLabels.Add("[$label]")
        $turnLayout.Add("$((($offset + 1) * 256))_$($row * 144)")
        $turnInput++
    }
}
$turnFilters.Add(($turnLabels -join '') + "xstack=inputs=28:layout=$($turnLayout -join '|'):fill=0x15181D[out]")
$turnArguments.Add('-filter_complex')
$turnArguments.Add(($turnFilters -join ';'))
$turnArguments.Add('-map')
$turnArguments.Add('[out]')
$turnArguments.Add('-frames:v')
$turnArguments.Add('1')
$turnArguments.Add('-update')
$turnArguments.Add('1')
$turnArguments.Add((Join-Path $QaRoot 'four-turns-28frame-v3.png'))
& $ffmpeg @turnArguments
if ($LASTEXITCODE -ne 0) { throw "ffmpeg turn sheet failed: $LASTEXITCODE" }

$lanes = @(
    @{ Id = 'PLAYER'; X = 40 },
    @{ Id = 'FATHER'; X = 315 },
    @{ Id = 'MOTHER'; X = 590 },
    @{ Id = 'OLDER SISTER'; X = 870 }
)
$font = 'C\:/Windows/Fonts/arialbd.ttf'
$tileWidth = 288
$tileHeight = 304

foreach ($entry in $directionFrames.GetEnumerator()) {
    $direction = $entry.Key
    $selectedFrames = $entry.Value
    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('-y')
    $arguments.Add('-hide_banner')
    $arguments.Add('-loglevel')
    $arguments.Add('warning')
    foreach ($lane in $lanes) {
        foreach ($frame in $selectedFrames) {
            $arguments.Add('-i')
            $arguments.Add((Join-Path $frameRoot ('frame_{0:D4}.png' -f $frame)))
        }
    }

    $filters = [System.Collections.Generic.List[string]]::new()
    $labels = [System.Collections.Generic.List[string]]::new()
    $inputIndex = 0
    for ($row = 0; $row -lt $lanes.Count; $row++) {
        for ($pose = 0; $pose -lt 6; $pose++) {
            $label = "t$inputIndex"
            $filters.Add(
                "[$inputIndex`:v]crop=350:360:$($lanes[$row].X):190," +
                "scale=280:288,pad=288:304:4:12:color=0x15181D," +
                "drawtext=fontfile='$font':text='$direction P$pose':x=8:y=8:" +
                "fontsize=20:fontcolor=white:borderw=2:bordercolor=black,setsar=1[$label]")
            $labels.Add("[$label]")
            $inputIndex++
        }
    }

    $layout = [System.Collections.Generic.List[string]]::new()
    for ($row = 0; $row -lt 4; $row++) {
        for ($column = 0; $column -lt 6; $column++) {
            $layout.Add("$($column * $tileWidth)_$($row * $tileHeight)")
        }
    }
    $filters.Add(($labels -join '') + "xstack=inputs=24:layout=$($layout -join '|'):fill=0x15181D[out]")
    $arguments.Add('-filter_complex')
    $arguments.Add(($filters -join ';'))
    $arguments.Add('-map')
    $arguments.Add('[out]')
    $arguments.Add('-frames:v')
    $arguments.Add('1')
    $arguments.Add('-update')
    $arguments.Add('1')
    $arguments.Add((Join-Path $QaRoot ("{0}_24pose_contact_sheet_v3.png" -f $direction)))
    & $ffmpeg @arguments
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg contact sheet failed for $direction`: $LASTEXITCODE" }

    $directionCrop = @{
        SW = @{ PlayerX = 175; Y = 280 }
        NW = @{ PlayerX = 65;  Y = 285 }
        NE = @{ PlayerX = 60;  Y = 215 }
        SE = @{ PlayerX = 175; Y = 205 }
    }[$direction]
    $closeArguments = [System.Collections.Generic.List[string]]::new()
    $closeArguments.Add('-y')
    $closeArguments.Add('-hide_banner')
    $closeArguments.Add('-loglevel')
    $closeArguments.Add('warning')
    foreach ($lane in $lanes) {
        foreach ($frame in $selectedFrames) {
            $closeArguments.Add('-i')
            $closeArguments.Add((Join-Path $frameRoot ('frame_{0:D4}.png' -f $frame)))
        }
    }

    $closeFilters = [System.Collections.Generic.List[string]]::new()
    $closeLabels = [System.Collections.Generic.List[string]]::new()
    $inputIndex = 0
    for ($row = 0; $row -lt $lanes.Count; $row++) {
        for ($pose = 0; $pose -lt 6; $pose++) {
            $label = "c$inputIndex"
            $cropX = $directionCrop.PlayerX + ($row * 275)
            $closeFilters.Add(
                "[$inputIndex`:v]crop=220:240:$cropX`:$($directionCrop.Y)," +
                "scale=440:480,pad=448:504:4:20:color=0x15181D," +
                "drawtext=fontfile='$font':text='$direction P$pose':x=10:y=8:" +
                "fontsize=28:fontcolor=white:borderw=3:bordercolor=black,setsar=1[$label]")
            $closeLabels.Add("[$label]")
            $inputIndex++
        }
    }
    $closeLayout = [System.Collections.Generic.List[string]]::new()
    for ($row = 0; $row -lt 4; $row++) {
        for ($column = 0; $column -lt 6; $column++) {
            $closeLayout.Add("$($column * 448)_$($row * 504)")
        }
    }
    $closeFilters.Add(($closeLabels -join '') + "xstack=inputs=24:layout=$($closeLayout -join '|'):fill=0x15181D[out]")
    $closeArguments.Add('-filter_complex')
    $closeArguments.Add(($closeFilters -join ';'))
    $closeArguments.Add('-map')
    $closeArguments.Add('[out]')
    $closeArguments.Add('-frames:v')
    $closeArguments.Add('1')
    $closeArguments.Add('-update')
    $closeArguments.Add('1')
    $closeArguments.Add((Join-Path $QaRoot ("{0}_24pose_closeup_sheet_v3.png" -f $direction)))
    & $ffmpeg @closeArguments
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg closeup sheet failed for $direction`: $LASTEXITCODE" }
}

Get-ChildItem -LiteralPath $QaRoot -File |
    Where-Object { $_.Name -like '*v3*' } |
    Sort-Object Name |
    Select-Object Name, Length
