<#
.SYNOPSIS
Proves that a family walk row really contains two steps, by anatomy and not by change rate.

.DESCRIPTION
Contract: FC-WALK-TWOSTEP-GATE-V1, enforcing the six-frame contract in
Docs/FAMILY_WALK_ART_GUARDRAILS.md.

Every previous gate scored how much a frame changed. Clothes, arms and hair change too, so a row
where the same leg leads all six frames passed every one of them. This tool never scores change. It
asks the two questions that a one-legged shuffle cannot answer:

  1. PELVIS-MIRROR - frames 3/4/5 must be the pelvis-axis reflection of frames 0/1/2 in the lower
     body. The guardrail builds the second half cycle by reflecting the first, so the reflected
     distance must be far smaller than the unreflected one. If frame 3 is a copy of frame 0 with a
     different shirt, the unreflected distance wins and the row fails.
  2. LEAD-SWAP - in the two contact frames the forward shoe must sit on opposite sides of the pelvis
     axis. This is the literal statement "the left foot leads once and the right foot leads once".

Both checks look only below the pelvis, so no amount of upper-body variation can buy a pass. Both
are structural: a row assembled by reflection satisfies them by construction, and a row that repeats
one leg cannot satisfy them at all.

.EXAMPLE
.\Tools\Verify-FamilyWalkTwoStep.ps1
.EXAMPLE
.\Tools\Verify-FamilyWalkTwoStep.ps1 -Source artsources -Member father
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [ValidateSet('runtime', 'artsources')]
    [string]$Source = 'runtime',
    [string]$Member = '',
    [string]$Direction = '',
    # Load frames from an explicit directory instead of the tracked locations.
    [string]$FrameDirectory = '',
    # The row under test is the marker review copy, so the leg markers must be PRESENT.
    [switch]$MarkerCopy,
    # Permanent cyan/magenta anatomy-marker copy of the row under test, same filenames. When present
    # it adds two things: the marker identity swap between the contact frames, and proof that the
    # copy is the same body (identical alpha silhouette). Neither replaces the silhouette gates.
    [string]$MarkerDirectory = '',
    # Prove the gate in both directions: a synthetically reflected row must pass and a synthetic
    # one-leg repeat plus a whole-frame mirror must fail. The negative control is synthesized so
    # fixing the tracked shipping row cannot silently invalidate the checker self-test.
    [switch]$SelfTest,
    # A reflected half cycle must land far closer than an unreflected copy.
    [double]$MaxMirrorToSameRatio = 0.5,
    # ...and it must actually be close, not merely closer.
    [double]$MaxMirrorAreaFraction = 0.20
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Windows PowerShell 5.1 does not populate $PSScriptRoot while evaluating a script parameter's
# default expression. Resolve it from the script body so the documented command works in both
# Windows PowerShell and PowerShell 7.
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}
else {
    $ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
}
if (-not [string]::IsNullOrWhiteSpace($MarkerDirectory)) {
    $MarkerDirectory = (Resolve-Path -LiteralPath $MarkerDirectory).Path
}

# Conservative structural comparison band. The father identity renderer replaces trousers from
# 0.64, but the two-step gate intentionally compares from 0.74 so the fixed waistband/seam cannot
# score or penalize lower-leg reflection.
$LowerBodyStart = @{
    player       = 0.72
    older_sister = 0.66
    father       = 0.74
    mother       = 0.70
}
$Members = @('player', 'older_sister', 'father', 'mother')
$Directions = @('south', 'southwest', 'west', 'northwest', 'north', 'northeast', 'east', 'southeast')
$ShoeBandRows = 12

function Get-Silhouette {
    param([string]$Path)
    $bitmap = New-Object System.Drawing.Bitmap $Path
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $data = $bitmap.LockBits(
            (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($width * $height * 4)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        }
        finally {
            $bitmap.UnlockBits($data)
        }
    }
    finally {
        $bitmap.Dispose()
    }

    $mask = New-Object 'bool[]' ($width * $height)
    $minX = [int]::MaxValue; $maxX = -1; $minY = [int]::MaxValue; $maxY = -1
    for ($y = 0; $y -lt $height; $y++) {
        $rowBase = $y * $width
        for ($x = 0; $x -lt $width; $x++) {
            if ([int]$bytes[(($rowBase + $x) * 4) + 3] -le 8) { continue }
            $mask[$rowBase + $x] = $true
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
    if ($maxY -lt 0) { throw "Frame is empty: $Path" }
    return @{
        Width = $width; Height = $height; Mask = $mask
        MinX = $minX; MaxX = $maxX; MinY = $minY; MaxY = $maxY
    }
}

function Get-LowerBody {
    param($Silhouette, [double]$StartFraction)
    $bodyHeight = $Silhouette.MaxY - $Silhouette.MinY + 1
    $startY = [int][Math]::Round($Silhouette.MinY + ($bodyHeight * $StartFraction))
    if ($startY -gt $Silhouette.MaxY) { $startY = $Silhouette.MaxY }

    # Pelvis axis: horizontal centre of the seam rows, matching the builder's reflection anchor.
    $seamMin = [int]::MaxValue; $seamMax = -1
    for ($y = $startY; $y -le [Math]::Min($startY + 4, $Silhouette.MaxY); $y++) {
        $rowBase = $y * $Silhouette.Width
        for ($x = 0; $x -lt $Silhouette.Width; $x++) {
            if (-not $Silhouette.Mask[$rowBase + $x]) { continue }
            if ($x -lt $seamMin) { $seamMin = $x }
            if ($x -gt $seamMax) { $seamMax = $x }
        }
    }
    if ($seamMax -lt 0) { throw 'Lower body seam is empty.' }

    $area = 0
    for ($y = $startY; $y -le $Silhouette.MaxY; $y++) {
        $rowBase = $y * $Silhouette.Width
        for ($x = 0; $x -lt $Silhouette.Width; $x++) {
            if ($Silhouette.Mask[$rowBase + $x]) { $area++ }
        }
    }
    return @{
        StartY = $startY
        EndY = $Silhouette.MaxY
        Axis = (($seamMin + $seamMax) / 2.0)
        Area = $area
    }
}

function Get-HeadBand {
    param($Silhouette, [double]$Fraction = 0.22)
    $bodyHeight = $Silhouette.MaxY - $Silhouette.MinY + 1
    $endY = [int][Math]::Round($Silhouette.MinY + ($bodyHeight * $Fraction))
    # The head comparison must align on the head itself. Using the whole-body bounding-box centre
    # makes a valid lower-body reflection shift the comparison axis whenever the contact stride is
    # asymmetric, falsely reporting an unchanged head as HEAD-MIRRORED.
    $bandMin = [int]::MaxValue; $bandMax = -1
    for ($y = $Silhouette.MinY; $y -le $endY; $y++) {
        $rowBase = $y * $Silhouette.Width
        for ($x = 0; $x -lt $Silhouette.Width; $x++) {
            if (-not $Silhouette.Mask[$rowBase + $x]) { continue }
            if ($x -lt $bandMin) { $bandMin = $x }
            if ($x -gt $bandMax) { $bandMax = $x }
        }
    }
    if ($bandMax -lt 0) { throw 'Head band is empty.' }
    return @{
        StartY = $Silhouette.MinY
        EndY = $endY
        Axis = (($bandMin + $bandMax) / 2.0)
    }
}

function Read-Pixels {
    param([string]$Path)
    $bitmap = New-Object System.Drawing.Bitmap $Path
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $data = $bitmap.LockBits(
            (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($width * $height * 4)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        }
        finally { $bitmap.UnlockBits($data) }
    }
    finally { $bitmap.Dispose() }
    return @{ Width = $width; Height = $height; Bytes = $bytes }
}

# Residue is defined against the marker copy, never against a hue band. An absolute cyan/magenta
# test cannot work here: father's shirt is teal (155,807 whole-body false positives) and mother's
# skirt is teal below the pelvis too (18,511 more). The only palette-independent definition is
# "a shipping pixel wearing a colour that the marker pass introduced", so the marker colours are
# learned from the pixels where the two copies actually differ.
function Get-MarkerColours {
    param([string]$ShippingPath, [string]$MarkerPath)
    $shipping = Read-Pixels -Path $ShippingPath
    $marker = Read-Pixels -Path $MarkerPath
    if ($shipping.Width -ne $marker.Width -or $shipping.Height -ne $marker.Height) { return $null }
    $colours = @{}
    for ($index = 0; $index -lt $shipping.Bytes.Length; $index += 4) {
        if ([int]$marker.Bytes[$index + 3] -le 8) { continue }
        if ([int]$shipping.Bytes[$index] -eq [int]$marker.Bytes[$index] -and
            [int]$shipping.Bytes[$index + 1] -eq [int]$marker.Bytes[$index + 1] -and
            [int]$shipping.Bytes[$index + 2] -eq [int]$marker.Bytes[$index + 2]) { continue }
        $key = ([int]$marker.Bytes[$index + 2] -shl 16) -bor
               ([int]$marker.Bytes[$index + 1] -shl 8) -bor
               [int]$marker.Bytes[$index]
        if ($colours.ContainsKey($key)) { $colours[$key]++ } else { $colours[$key] = 1 }
    }
    # Keep the colours the marker pass actually painted, not stray antialiasing.
    return @($colours.GetEnumerator() | Where-Object { $_.Value -ge 25 } | ForEach-Object { $_.Key })
}

function Measure-MarkerResidue {
    param([string]$Path, $Lower, $MarkerColours, [int]$Tolerance = 24)
    if ($null -eq $MarkerColours -or $MarkerColours.Count -eq 0) { return 0 }
    $image = Read-Pixels -Path $Path
    $residue = 0
    for ($y = $Lower.StartY; $y -le $Lower.EndY; $y++) {
        $rowBase = $y * $image.Width
        for ($x = 0; $x -lt $image.Width; $x++) {
            $index = ($rowBase + $x) * 4
            if ([int]$image.Bytes[$index + 3] -le 8) { continue }
            $b = [int]$image.Bytes[$index]
            $g = [int]$image.Bytes[$index + 1]
            $r = [int]$image.Bytes[$index + 2]
            foreach ($colour in $MarkerColours) {
                $dr = [Math]::Abs((($colour -shr 16) -band 0xFF) - $r)
                if ($dr -gt $Tolerance) { continue }
                $dg = [Math]::Abs((($colour -shr 8) -band 0xFF) - $g)
                if ($dg -gt $Tolerance) { continue }
                $db = [Math]::Abs(($colour -band 0xFF) - $b)
                if ($db -gt $Tolerance) { continue }
                $residue++
                break
            }
        }
    }
    return $residue
}

function Measure-AlphaMismatch {
    param([string]$FirstPath, [string]$SecondPath)
    $first = Get-Silhouette -Path $FirstPath
    $second = Get-Silhouette -Path $SecondPath
    if ($first.Width -ne $second.Width -or $first.Height -ne $second.Height) { return -1 }
    $mismatch = 0
    for ($index = 0; $index -lt $first.Mask.Length; $index++) {
        if ($first.Mask[$index] -ne $second.Mask[$index]) { $mismatch++ }
    }
    return $mismatch
}

function Test-LowerBodyPixel {
    param($Silhouette, [int]$X, [int]$Y)
    if ($X -lt 0 -or $X -ge $Silhouette.Width -or $Y -lt 0 -or $Y -ge $Silhouette.Height) { return $false }
    return $Silhouette.Mask[($Y * $Silhouette.Width) + $X]
}

# Both distances align the two frames on their own pelvis axis first, so a one pixel horizontal
# drift cannot decide the verdict; only the arrangement of the legs can. Iterate each integer
# pixel in the first frame exactly once, then reflect that discrete coordinate. Rounding
# (axis + offset) and (axis - offset) independently duplicates/skips columns at half-pixel axes
# and compares a pixel with the neighbour of its true mirror.
function Measure-LowerBodyDistances {
    param($FirstSilhouette, $FirstLower, $SecondSilhouette, $SecondLower)
    $same = 0
    $mirror = 0
    $span = 80
    $rows = [Math]::Min($FirstLower.EndY - $FirstLower.StartY, $SecondLower.EndY - $SecondLower.StartY)
    for ($row = 0; $row -le $rows; $row++) {
        $firstY = $FirstLower.StartY + $row
        $secondY = $SecondLower.StartY + $row
        $firstMinX = [int][Math]::Ceiling($FirstLower.Axis - $span)
        $firstMaxX = [int][Math]::Floor($FirstLower.Axis + $span)
        for ($firstX = $firstMinX; $firstX -le $firstMaxX; $firstX++) {
            $relativeX = $firstX - $FirstLower.Axis
            $sameX = [int][Math]::Round($SecondLower.Axis + $relativeX)
            $mirrorX = [int][Math]::Round($SecondLower.Axis - $relativeX)
            $a = Test-LowerBodyPixel $FirstSilhouette $firstX $firstY
            $b = Test-LowerBodyPixel $SecondSilhouette $sameX $secondY
            $m = Test-LowerBodyPixel $SecondSilhouette $mirrorX $secondY
            if ($a -ne $b) { $same++ }
            if ($a -ne $m) { $mirror++ }
        }
    }
    return @{ Same = $same; Mirror = $mirror }
}

# Forward shoe side, or $null when the two shoes overlap and cannot be separated.
function Get-LeadShoeSide {
    param($Silhouette, $Lower)
    $bandTop = [Math]::Max($Lower.StartY, $Silhouette.MaxY - $ShoeBandRows + 1)
    $width = $Silhouette.Width
    $visited = @{}
    $clusters = New-Object System.Collections.Generic.List[object]
    for ($y = $bandTop; $y -le $Silhouette.MaxY; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            if (-not $Silhouette.Mask[($y * $width) + $x]) { continue }
            $key = ($y * $width) + $x
            if ($visited.ContainsKey($key)) { continue }
            $queue = New-Object 'System.Collections.Generic.Queue[int]'
            $queue.Enqueue($key)
            $visited[$key] = $true
            $count = 0; $sumX = 0.0
            while ($queue.Count -gt 0) {
                $current = $queue.Dequeue()
                $currentX = $current % $width
                $currentY = [Math]::Floor($current / $width)
                $count++
                $sumX += $currentX
                for ($dy = -1; $dy -le 1; $dy++) {
                    for ($dx = -1; $dx -le 1; $dx++) {
                        if ($dx -eq 0 -and $dy -eq 0) { continue }
                        $nextX = $currentX + $dx
                        $nextY = $currentY + $dy
                        if ($nextX -lt 0 -or $nextX -ge $width) { continue }
                        if ($nextY -lt $bandTop -or $nextY -gt $Silhouette.MaxY) { continue }
                        if (-not $Silhouette.Mask[($nextY * $width) + $nextX]) { continue }
                        $nextKey = ($nextY * $width) + $nextX
                        if ($visited.ContainsKey($nextKey)) { continue }
                        $visited[$nextKey] = $true
                        $queue.Enqueue($nextKey)
                    }
                }
            }
            if ($count -ge 20) {
                [void]$clusters.Add([pscustomobject]@{ Count = $count; CentreX = ($sumX / $count) })
            }
        }
    }
    if ($clusters.Count -lt 2) { return $null }
    $lead = $clusters | Sort-Object { [Math]::Abs($_.CentreX - $Lower.Axis) } -Descending | Select-Object -First 1
    $delta = $lead.CentreX - $Lower.Axis
    if ([Math]::Abs($delta) -lt 1.5) { return $null }
    if ($delta -gt 0) { return 1 }
    return -1
}

function Get-AnatomyMarkerStats {
    param([string]$Path, $Silhouette, $Lower)
    $bitmap = New-Object System.Drawing.Bitmap $Path
    try {
        if ($bitmap.Width -ne $Silhouette.Width -or $bitmap.Height -ne $Silhouette.Height) {
            throw "Marker dimensions do not match the rendered frame: $Path"
        }
        $data = $bitmap.LockBits(
            (New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($bitmap.Width * $bitmap.Height * 4)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        }
        finally { $bitmap.UnlockBits($data) }
    }
    finally { $bitmap.Dispose() }

    $markers = @{
        cyan = [ordered]@{ Count = 0; MinY = [int]::MaxValue; MaxY = -1; FootCount = 0; FootSumX = 0.0; FootCentreX = [double]::NaN; VerticalSpan = 0 }
        magenta = [ordered]@{ Count = 0; MinY = [int]::MaxValue; MaxY = -1; FootCount = 0; FootSumX = 0.0; FootCentreX = [double]::NaN; VerticalSpan = 0 }
    }
    for ($y = $Lower.StartY; $y -le $Lower.EndY; $y++) {
        for ($x = 0; $x -lt $Silhouette.Width; $x++) {
            $index = (($y * $Silhouette.Width + $x) * 4)
            if ([int]$bytes[$index + 3] -le 8) { continue }
            $blue = [int]$bytes[$index]
            $green = [int]$bytes[$index + 1]
            $red = [int]$bytes[$index + 2]
            $name = if ($red -le 40 -and $green -ge 180 -and $blue -ge 180) {
                'cyan'
            }
            elseif ($red -ge 180 -and $green -le 40 -and $blue -ge 110) {
                'magenta'
            }
            else { '' }
            if ($name.Length -eq 0) { continue }
            $marker = $markers[$name]
            $marker.Count++
            if ($y -lt $marker.MinY) { $marker.MinY = $y }
            if ($y -gt $marker.MaxY) { $marker.MaxY = $y }
        }
    }
    foreach ($name in @('cyan', 'magenta')) {
        $marker = $markers[$name]
        if ($marker.Count -eq 0) { continue }
        $footTop = [Math]::Max($Lower.StartY, $marker.MaxY - 13)
        for ($y = $footTop; $y -le $marker.MaxY; $y++) {
            for ($x = 0; $x -lt $Silhouette.Width; $x++) {
                $index = (($y * $Silhouette.Width + $x) * 4)
                if ([int]$bytes[$index + 3] -le 8) { continue }
                $blue = [int]$bytes[$index]
                $green = [int]$bytes[$index + 1]
                $red = [int]$bytes[$index + 2]
                $matches = if ($name -eq 'cyan') {
                    $red -le 40 -and $green -ge 180 -and $blue -ge 180
                }
                else {
                    $red -ge 180 -and $green -le 40 -and $blue -ge 110
                }
                if (-not $matches) { continue }
                $marker.FootCount++
                $marker.FootSumX += $x
            }
        }
    }
    foreach ($name in @('cyan', 'magenta')) {
        $marker = $markers[$name]
        $marker.FootCentreX = if ($marker.FootCount -gt 0) {
            $marker.FootSumX / $marker.FootCount
        }
        else { [double]::NaN }
        $marker.VerticalSpan = if ($marker.Count -gt 0) { $marker.MaxY - $marker.MinY + 1 } else { 0 }
    }
    return $markers
}

function Get-LeadMarkerColour {
    param($Stats, [double]$Axis)
    if ($Stats.cyan.FootCount -lt 8 -or $Stats.magenta.FootCount -lt 8) { return $null }
    $cyanDistance = [Math]::Abs($Stats.cyan.FootCentreX - $Axis)
    $magentaDistance = [Math]::Abs($Stats.magenta.FootCentreX - $Axis)
    if ([Math]::Abs($cyanDistance - $magentaDistance) -lt 0.75) { return $null }
    return $(if ($cyanDistance -gt $magentaDistance) { 'cyan' } else { 'magenta' })
}

function Get-PassingMarkerColour {
    param($Stats)
    if ($Stats.cyan.Count -lt 30 -or $Stats.magenta.Count -lt 30) { return $null }
    if ([Math]::Abs($Stats.cyan.MaxY - $Stats.magenta.MaxY) -lt 1) { return $null }
    return $(if ($Stats.cyan.MaxY -lt $Stats.magenta.MaxY) { 'cyan' } else { 'magenta' })
}

function Get-FramePath {
    param([string]$MemberId, [string]$DirectionId, [int]$Phase)
    if ($FrameDirectory) {
        $explicit = Join-Path $FrameDirectory "${MemberId}_${DirectionId}_walk_${Phase}.png"
        if (Test-Path -LiteralPath $explicit) { return $explicit }
        return $null
    }
    if ($Source -eq 'artsources') {
        return Join-Path $ProjectRoot (
            "ArtSources\FamilyWalkHalfCyclesV2\$MemberId\$DirectionId\${MemberId}_${DirectionId}_half_${Phase}.png")
    }
    $found = Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets\Art\Characters') -Recurse -Filter (
        "${MemberId}_${DirectionId}_walk_${Phase}.png") -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'HighMotion' } |
        Select-Object -First 1
    if ($null -eq $found) { return $null }
    return $found.FullName
}

if ($SelfTest) {
    $selfMember = 'father'
    $selfDirection = 'east'
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("fc-walk-twostep-selftest-" + [Guid]::NewGuid().ToString('N'))
    $repeatTemp = "$temp-repeat"
    $cheatTemp = "$temp-cheat"
    [void][IO.Directory]::CreateDirectory($temp)
    [void][IO.Directory]::CreateDirectory($repeatTemp)
    [void][IO.Directory]::CreateDirectory($cheatTemp)
    try {
        $startFraction = $LowerBodyStart[$selfMember]
        for ($phase = 0; $phase -lt 3; $phase++) {
            $sourcePath = Get-FramePath -MemberId $selfMember -DirectionId $selfDirection -Phase $phase
            if ($null -eq $sourcePath) { throw "Self-test needs $selfMember/$selfDirection frames." }
            Copy-Item -LiteralPath $sourcePath -Destination (
                Join-Path $temp "${selfMember}_${selfDirection}_walk_${phase}.png")
            Copy-Item -LiteralPath $sourcePath -Destination (
                Join-Path $repeatTemp "${selfMember}_${selfDirection}_walk_${phase}.png")
            Copy-Item -LiteralPath $sourcePath -Destination (
                Join-Path $repeatTemp "${selfMember}_${selfDirection}_walk_$($phase + 3).png")
            Copy-Item -LiteralPath $sourcePath -Destination (
                Join-Path $cheatTemp "${selfMember}_${selfDirection}_walk_${phase}.png")

            # The cheat candidate: reflect the entire frame, head included.
            $cheat = New-Object System.Drawing.Bitmap $sourcePath
            try {
                $cheat.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
                $cheat.Save(
                    (Join-Path $cheatTemp "${selfMember}_${selfDirection}_walk_$($phase + 3).png"),
                    [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $cheat.Dispose() }

            # Build the opposite half cycle the way the guardrail contract says it must be built:
            # reflect the lower body about the pelvis axis, leave the upper body untouched.
            $silhouette = Get-Silhouette -Path $sourcePath
            $lower = Get-LowerBody -Silhouette $silhouette -StartFraction $startFraction
            $bitmap = New-Object System.Drawing.Bitmap $sourcePath
            try {
                $reflected = New-Object System.Drawing.Bitmap $bitmap.Width, $bitmap.Height, (
                    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try {
                    for ($y = 0; $y -lt $bitmap.Height; $y++) {
                        for ($x = 0; $x -lt $bitmap.Width; $x++) {
                            if ($y -lt $lower.StartY) {
                                $reflected.SetPixel($x, $y, $bitmap.GetPixel($x, $y))
                                continue
                            }
                            $sourceX = [int][Math]::Round((2.0 * $lower.Axis) - $x)
                            if ($sourceX -ge 0 -and $sourceX -lt $bitmap.Width) {
                                $reflected.SetPixel($x, $y, $bitmap.GetPixel($sourceX, $y))
                            }
                        }
                    }
                    $reflected.Save(
                        (Join-Path $temp "${selfMember}_${selfDirection}_walk_$($phase + 3).png"),
                        [System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally { $reflected.Dispose() }
            }
            finally { $bitmap.Dispose() }
        }

        # Child runs get their own process so their exit code is unambiguous: `exit` inside a
        # dot-invoked script would end this one instead of reporting a verdict.
        $powershell = (Get-Process -Id $PID).Path
        & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ProjectRoot $ProjectRoot -Member $selfMember -Direction $selfDirection `
            -FrameDirectory $temp | Out-Host
        $reflectedExit = $LASTEXITCODE
        & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ProjectRoot $ProjectRoot -Member $selfMember -Direction $selfDirection `
            -FrameDirectory $repeatTemp | Out-Host
        $repeatExit = $LASTEXITCODE
        & $powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ProjectRoot $ProjectRoot -Member $selfMember -Direction $selfDirection `
            -FrameDirectory $cheatTemp | Out-Host
        $cheatExit = $LASTEXITCODE

        Write-Host ''
        if ($cheatExit -eq 0) {
            Write-Host ('FAMILY_WALK_TWO_STEP_GATE_SELFTEST: FAIL | a whole-frame mirror was ' +
                'accepted')
            exit 1
        }
        if ($reflectedExit -ne 0) {
            Write-Host ("FAMILY_WALK_TWO_STEP_GATE_SELFTEST: FAIL | a correctly reflected row was " +
                "rejected (exit $reflectedExit)")
            exit 1
        }
        if ($repeatExit -eq 0) {
            Write-Host ("FAMILY_WALK_TWO_STEP_GATE_SELFTEST: FAIL | the synthetic one-leg row was " +
                'accepted')
            exit 1
        }
        Write-Host ('FAMILY_WALK_TWO_STEP_GATE_SELFTEST: PASS | reflected row accepted, synthetic ' +
            'one-leg row rejected, whole-frame mirror rejected')
        exit 0
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $repeatTemp -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $cheatTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$targetMembers = $Members
if ($Member) { $targetMembers = @($Member) }
$targetDirections = $Directions
if ($Direction) { $targetDirections = @($Direction) }

$rowFailures = New-Object System.Collections.Generic.List[string]
$rowsChecked = 0

foreach ($memberId in $targetMembers) {
    # Employees and probe members have no authored split; 0.70 keeps the window on the legs so the
    # same gate can be pointed at any candidate row while hunting for a reference that passes.
    $startFraction = 0.70
    if ($LowerBodyStart.ContainsKey($memberId)) { $startFraction = $LowerBodyStart[$memberId] }
    foreach ($directionId in $targetDirections) {
        $paths = @()
        for ($phase = 0; $phase -lt 6; $phase++) {
            $paths += Get-FramePath -MemberId $memberId -DirectionId $directionId -Phase $phase
        }
        if ($paths -contains $null) {
            Write-Host ("SKIP   {0,-13} {1,-10} frames are absent" -f $memberId, $directionId)
            continue
        }

        $silhouettes = @()
        $lowers = @()
        for ($phase = 0; $phase -lt 6; $phase++) {
            $silhouette = Get-Silhouette -Path $paths[$phase]
            $silhouettes += $silhouette
            $lowers += (Get-LowerBody -Silhouette $silhouette -StartFraction $startFraction)
        }

        $rowsChecked++
        $rowFailed = $false
        $notes = New-Object System.Collections.Generic.List[string]

        foreach ($pair in @(@(0, 3), @(1, 4), @(2, 5))) {
            $first = $pair[0]; $second = $pair[1]
            $distance = Measure-LowerBodyDistances `
                -FirstSilhouette $silhouettes[$first] -FirstLower $lowers[$first] `
                -SecondSilhouette $silhouettes[$second] -SecondLower $lowers[$second]
            $area = [Math]::Max(1, $lowers[$first].Area)
            $ratio = if ($distance.Same -eq 0) { 999.0 } else { $distance.Mirror / [double]$distance.Same }
            $areaFraction = $distance.Mirror / [double]$area
            $pairPassed = ($ratio -le $MaxMirrorToSameRatio) -and ($areaFraction -le $MaxMirrorAreaFraction)
            if (-not $pairPassed) { $rowFailed = $true }
            $notes.Add(("{0}<->{1} mirror={2} same={3} ratio={4:F2} mirror/area={5:F2} {6}" -f
                $first, $second, $distance.Mirror, $distance.Same, $ratio, $areaFraction,
                $(if ($pairPassed) { 'ok' } else { 'MIRROR-FAIL' })))
        }

        # Closes the one cheat that satisfies both leg invariants for free: mirroring the WHOLE
        # frame. That flips the legs correctly and also flips the face, hair and clothes, which the
        # guardrail forbids and a player sees instantly. The head must stay unmirrored, so for the
        # head band the unreflected distance has to be the smaller one.
        $headFirst = Get-HeadBand -Silhouette $silhouettes[0]
        $headSecond = Get-HeadBand -Silhouette $silhouettes[3]
        $headDistance = Measure-LowerBodyDistances `
            -FirstSilhouette $silhouettes[0] -FirstLower $headFirst `
            -SecondSilhouette $silhouettes[3] -SecondLower $headSecond
        if ($headDistance.Mirror -lt ($headDistance.Same * 0.8)) {
            $headFailure = ("head-stable=HEAD-MIRRORED (mirror={0} same={1}; the whole frame was " +
                'reflected, not just the lower body)') -f @(
                    $headDistance.Mirror,
                    $headDistance.Same)
            $notes.Add($headFailure)
            $rowFailed = $true
        }
        else {
            $notes.Add(("head-stable=ok (mirror={0} same={1})" -f
                $headDistance.Mirror, $headDistance.Same))
        }

        # Invariant 4: the marker pass never leaves its colours on shipping pixels. Only decidable
        # with the marker copy present, so without one this stays silent instead of guessing.
        if ($MarkerDirectory -and -not $MarkerCopy) {
            $residueTotal = 0
            $residueFrames = 0
            $colourCount = 0
            for ($phase = 0; $phase -lt 6; $phase++) {
                $markerPath = Join-Path $MarkerDirectory (
                    "${memberId}_${directionId}_walk_${phase}.png")
                if (-not (Test-Path -LiteralPath $markerPath)) { continue }
                $markerColours = Get-MarkerColours -ShippingPath $paths[$phase] -MarkerPath $markerPath
                if ($null -eq $markerColours) { continue }
                $colourCount = [Math]::Max($colourCount, $markerColours.Count)
                $residue = Measure-MarkerResidue -Path $paths[$phase] -Lower $lowers[$phase] `
                    -MarkerColours $markerColours
                $residueTotal += $residue
                if ($residue -gt 0) { $residueFrames++ }
            }
            if ($residueTotal -gt 0) {
                $notes.Add((("marker-clean=MARKER-RESIDUE ({0} px in {1}/6 frames below the pelvis " +
                    'wear one of the {2} marker colours; shipping pixels must never carry them)') -f
                    $residueTotal, $residueFrames, $colourCount))
                $rowFailed = $true
            }
            else {
                $notes.Add(("marker-clean=ok (0 px of {0} marker colours)" -f $colourCount))
            }
        }

        if ($MarkerDirectory) {
            $mismatchTotal = 0
            $missing = $false
            for ($phase = 0; $phase -lt 6; $phase++) {
                $markerPath = Join-Path $MarkerDirectory (
                    "${memberId}_${directionId}_walk_${phase}.png")
                if (-not (Test-Path -LiteralPath $markerPath)) { $missing = $true; break }
                $mismatch = Measure-AlphaMismatch -FirstPath $paths[$phase] -SecondPath $markerPath
                if ($mismatch -lt 0) { $mismatchTotal = -1; break }
                $mismatchTotal += $mismatch
            }
            if ($missing) {
                $notes.Add('marker-silhouette=MARKER-COPY-ABSENT')
                $rowFailed = $true
            }
            elseif ($mismatchTotal -ne 0) {
                $notes.Add(("marker-silhouette=SILHOUETTE-DRIFT ({0} alpha pixels differ; the " +
                    'marker copy is not the same body)') -f $mismatchTotal)
                $rowFailed = $true
            }
            else {
                $notes.Add('marker-silhouette=ok (identical alpha)')
            }
        }

        $leadZero = Get-LeadShoeSide -Silhouette $silhouettes[0] -Lower $lowers[0]
        $leadThree = Get-LeadShoeSide -Silhouette $silhouettes[3] -Lower $lowers[3]
        if ($null -eq $leadZero -or $null -eq $leadThree) {
            $notes.Add('lead-swap=unmeasurable (contact shoes are not separable)')
            $rowFailed = $true
        }
        elseif ($leadZero -eq $leadThree) {
            $notes.Add(("lead-swap=LEAD-FAIL (frames 0 and 3 both lead with the {0} side)" -f
                $(if ($leadZero -gt 0) { 'right-of-pelvis' } else { 'left-of-pelvis' })))
            $rowFailed = $true
        }
        else {
            $notes.Add('lead-swap=ok')
        }

        if ($MarkerDirectory -or $MarkerCopy) {
            $markerStats = @()
            $markersValid = $true
            for ($phase = 0; $phase -lt 6; $phase++) {
                $markerPath = if ($MarkerCopy) {
                    $paths[$phase]
                }
                else {
                    Join-Path $MarkerDirectory "${memberId}_${directionId}_walk_${phase}.png"
                }
                if (-not (Test-Path -LiteralPath $markerPath)) {
                    $notes.Add("anatomy-markers=MISSING phase=$phase")
                    $markersValid = $false
                    $rowFailed = $true
                    break
                }
                $stats = Get-AnatomyMarkerStats -Path $markerPath `
                    -Silhouette $silhouettes[$phase] -Lower $lowers[$phase]
                $lowerHeight = $lowers[$phase].EndY - $lowers[$phase].StartY + 1
                foreach ($colour in @('cyan', 'magenta')) {
                    $marker = $stats[$colour]
                    if ($marker.Count -lt 30 -or $marker.FootCount -lt 8 -or
                        $marker.VerticalSpan -lt ($lowerHeight * 0.30) -or
                        $marker.MinY -gt ($lowers[$phase].StartY + $lowerHeight * 0.55)) {
                        $notes.Add(
                            "anatomy-markers=IDENTITY-FAIL phase=$phase colour=$colour " +
                            "count=$($marker.Count) foot=$($marker.FootCount) span=$($marker.VerticalSpan)")
                        $markersValid = $false
                        $rowFailed = $true
                    }
                }
                $markerStats += $stats
            }
            if ($markersValid) {
                $leadMarkerZero = Get-LeadMarkerColour -Stats $markerStats[0] -Axis $lowers[0].Axis
                $leadMarkerThree = Get-LeadMarkerColour -Stats $markerStats[3] -Axis $lowers[3].Axis
                $passingMarkerTwo = Get-PassingMarkerColour -Stats $markerStats[2]
                $passingMarkerFive = Get-PassingMarkerColour -Stats $markerStats[5]
                if ($null -eq $leadMarkerZero -or $null -eq $leadMarkerThree) {
                    $notes.Add('anatomy-markers=CONTACT-UNMEASURABLE')
                    $rowFailed = $true
                }
                elseif ($leadMarkerZero -eq $leadMarkerThree) {
                    $notes.Add("anatomy-markers=CONTACT-IDENTITY-FAIL ($leadMarkerZero leads 0 and 3)")
                    $rowFailed = $true
                }
                elseif ($null -eq $passingMarkerTwo -or $null -eq $passingMarkerFive) {
                    $notes.Add('anatomy-markers=PASS-UNMEASURABLE')
                    $rowFailed = $true
                }
                elseif ($passingMarkerTwo -eq $passingMarkerFive) {
                    $notes.Add("anatomy-markers=PASS-IDENTITY-FAIL ($passingMarkerTwo passes 2 and 5)")
                    $rowFailed = $true
                }
                else {
                    $notes.Add(
                        "anatomy-markers=ok (contact 0=$leadMarkerZero 3=$leadMarkerThree; " +
                        "passing 2=$passingMarkerTwo 5=$passingMarkerFive)")
                }
            }
        }

        $status = if ($rowFailed) { 'FAIL  ' } else { 'PASS  ' }
        Write-Host ("{0} {1,-13} {2,-10} {3}" -f $status, $memberId, $directionId, ($notes -join ' | '))
        if ($rowFailed) { $rowFailures.Add("$memberId/$directionId") }
    }
}

Write-Host ''
if ($rowFailures.Count -gt 0) {
    Write-Host ("FAMILY_WALK_TWO_STEP_GATE: FAIL | contract=FC-WALK-TWOSTEP-GATE-V1 source={0} rows={1} failed={2}" -f
        $Source, $rowsChecked, $rowFailures.Count)
    Write-Host ("  failing rows: {0}" -f ($rowFailures -join ', '))
    exit 1
}

if ($MarkerDirectory -or $MarkerCopy) {
    Write-Host ("FAMILY_WALK_ANATOMY_MARKER_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 " +
        "source={0} rows={1}" -f $Source, $rowsChecked)
}
Write-Host ("FAMILY_WALK_TWO_STEP_GATE: PASS | contract=FC-WALK-TWOSTEP-GATE-V1 source={0} rows={1}" -f
    $Source, $rowsChecked)
