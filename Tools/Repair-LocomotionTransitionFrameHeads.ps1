<#
.SYNOPSIS
Re-splits the vendored locomotion transition sheets without clipping overflowing heads.

.DESCRIPTION
Build-LocomotionTransitionFrames.ps1 cut each 4x4 sheet on exact 256 px cell borders. The
generated art does not respect those borders: rows 1 and 3 were drawn tall enough that the top of
the head sits above its own cell, so 64 of the 256 transition frames lost their hair and rendered a
flat-topped skull whenever an actor stood still, turned in place, or started/stopped a walk.

The lost pixels are still present in the vendored alpha sheets, one cell higher. This tool re-splits
those sheets with an upward overflow band, drops everything that is not part of the tallest
connected silhouette in the band (which is how the neighbouring cell's feet are discarded), and then
applies the same planted-foot alignment the original splitter used. Frames that were never clipped
come out byte-for-byte identical.

Run with -WhatIf style verification first: -VerifyOnly reports what would change and writes nothing.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [int]$OverflowRows = 64,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$CellSize = 256
$TargetX = 128
$TargetBottomY = 247
$Members = @('player', 'older_sister', 'father', 'mother')
$Clips = @('turn_in_place', 'walk_start', 'walk_stop', 'short_shuffle')
$Directions = @('south', 'southwest', 'west', 'northwest', 'north', 'northeast', 'east', 'southeast')

function Read-Argb {
    param([string]$Path)
    $bitmap = New-Object System.Drawing.Bitmap $Path
    try {
        $rect = New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height
        $data = $bitmap.LockBits(
            $rect,
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($bitmap.Width * $bitmap.Height * 4)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        }
        finally {
            $bitmap.UnlockBits($data)
        }
        return @{ Width = $bitmap.Width; Height = $bitmap.Height; Bytes = $bytes }
    }
    finally {
        $bitmap.Dispose()
    }
}

# Same rejection rule as Build-LocomotionTransitionFrames.ps1: soft alpha and magenta chroma
# residue are not part of the silhouette, and everything kept becomes hard alpha.
function Test-SolidPixel {
    param([int]$B, [int]$G, [int]$R, [int]$A)
    if ($A -le 24) { return $false }
    $isMagentaResidue = $R -gt 160 -and $B -gt 130 -and $G -lt 110 -and
                        (((($R + $B) / 2) - $G) -gt 70)
    return -not $isMagentaResidue
}

function Get-RepairedCell {
    param($Sheet, [int]$Row, [int]$Column)

    $sheetWidth = $Sheet.Width
    $bytes = $Sheet.Bytes
    $cellTop = $Row * $CellSize
    $bandTop = [Math]::Max(0, $cellTop - $OverflowRows)
    $bandHeight = ($cellTop + $CellSize) - $bandTop
    $cellLeft = $Column * $CellSize

    $solid = New-Object 'bool[]' ($CellSize * $bandHeight)
    $colour = New-Object 'int[]' ($CellSize * $bandHeight)
    for ($y = 0; $y -lt $bandHeight; $y++) {
        $sourceRow = ($bandTop + $y) * $sheetWidth
        for ($x = 0; $x -lt $CellSize; $x++) {
            $index = ($sourceRow + $cellLeft + $x) * 4
            $b = [int]$bytes[$index]
            $g = [int]$bytes[$index + 1]
            $r = [int]$bytes[$index + 2]
            $a = [int]$bytes[$index + 3]
            if (Test-SolidPixel -B $b -G $g -R $r -A $a) {
                $target = $y * $CellSize + $x
                $solid[$target] = $true
                $colour[$target] = ($r -shl 16) -bor ($g -shl 8) -bor $b
            }
        }
    }

    # Keep only the largest connected silhouette. The neighbouring cell's feet reach into the
    # overflow band but never touch this body, so they drop out here exactly as the original
    # island filter intended.
    $visited = New-Object 'bool[]' ($CellSize * $bandHeight)
    $best = $null
    $bestCount = 0
    $queue = New-Object 'System.Collections.Generic.Queue[int]'
    for ($seed = 0; $seed -lt $solid.Length; $seed++) {
        if (-not $solid[$seed] -or $visited[$seed]) { continue }
        $component = New-Object 'System.Collections.Generic.List[int]'
        $queue.Clear()
        $queue.Enqueue($seed)
        $visited[$seed] = $true
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            [void]$component.Add($current)
            $currentX = $current % $CellSize
            $currentY = [Math]::Floor($current / $CellSize)
            for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    if ($dx -eq 0 -and $dy -eq 0) { continue }
                    $nextX = $currentX + $dx
                    $nextY = $currentY + $dy
                    if ($nextX -lt 0 -or $nextX -ge $CellSize -or
                        $nextY -lt 0 -or $nextY -ge $bandHeight) { continue }
                    $next = $nextY * $CellSize + $nextX
                    if (-not $solid[$next] -or $visited[$next]) { continue }
                    $visited[$next] = $true
                    $queue.Enqueue($next)
                }
            }
        }
        if ($component.Count -gt $bestCount) {
            $bestCount = $component.Count
            $best = $component
        }
    }
    if ($null -eq $best) { throw "Cell r$Row c$Column is empty." }

    $minY = [int]::MaxValue
    $maxY = -1
    foreach ($pixel in $best) {
        $y = [Math]::Floor($pixel / $CellSize)
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
    }

    # Planted-foot pivot: horizontal centre of the lowest 16 visible rows, as in the original.
    $footBandTop = [Math]::Max(0, $maxY - 15)
    $footMinX = [int]::MaxValue
    $footMaxX = -1
    foreach ($pixel in $best) {
        $y = [Math]::Floor($pixel / $CellSize)
        if ($y -lt $footBandTop) { continue }
        $x = $pixel % $CellSize
        if ($x -lt $footMinX) { $footMinX = $x }
        if ($x -gt $footMaxX) { $footMaxX = $x }
    }
    if ($footMaxX -lt 0) { throw "Cell r$Row c$Column has no visible foot anchor." }

    $offsetX = [int][Math]::Round($TargetX - (($footMinX + $footMaxX) / 2.0))
    $offsetY = $TargetBottomY - $maxY

    $frame = New-Object System.Drawing.Bitmap $CellSize, $CellSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = New-Object System.Drawing.Rectangle 0, 0, $CellSize, $CellSize
    $data = $frame.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $out = New-Object byte[] ($CellSize * $CellSize * 4)
        $clippedTop = 0
        foreach ($pixel in $best) {
            $x = ($pixel % $CellSize) + $offsetX
            $y = [Math]::Floor($pixel / $CellSize) + $offsetY
            if ($y -lt 0) { $clippedTop++; continue }
            if ($x -lt 0 -or $x -ge $CellSize -or $y -ge $CellSize) { continue }
            $index = ($y * $CellSize + $x) * 4
            $packed = $colour[$pixel]
            $out[$index] = [byte]($packed -band 0xFF)
            $out[$index + 1] = [byte](($packed -shr 8) -band 0xFF)
            $out[$index + 2] = [byte](($packed -shr 16) -band 0xFF)
            $out[$index + 3] = 255
        }
        [System.Runtime.InteropServices.Marshal]::Copy($out, 0, $data.Scan0, $out.Length)
    }
    finally {
        $frame.UnlockBits($data)
    }

    return @{
        Frame = $frame
        RecoveredRows = [Math]::Max(0, ($cellTop - $bandTop) - $minY)
        SilhouetteHeight = ($maxY - $minY + 1)
        ClippedTopPixels = $clippedTop
    }
}

$outputRoot = Join-Path $ProjectRoot 'Assets\Art\Characters\Family\LocomotionTransitionsV1'
$repaired = 0
$unchanged = 0
$stillClipped = @()

foreach ($member in $Members) {
    foreach ($clip in $Clips) {
        $sheetPath = Join-Path $outputRoot "$member\Source\${member}_${clip}_4x4_alpha_v1.png"
        if (-not (Test-Path -LiteralPath $sheetPath)) {
            throw "Missing vendored transition sheet: $sheetPath"
        }
        $sheet = Read-Argb -Path $sheetPath
        for ($row = 0; $row -lt 4; $row++) {
            for ($column = 0; $column -lt 4; $column++) {
                $directionIndex = $row * 2 + [Math]::Floor($column / 2)
                $pose = if (($column % 2) -eq 0) { 'a' } else { 'b' }
                $name = "${member}_$($Directions[$directionIndex])_${clip}_${pose}.png"
                $framePath = Join-Path $outputRoot "$member\Frames\$name"
                $result = Get-RepairedCell -Sheet $sheet -Row $row -Column $column
                try {
                    if ($result.ClippedTopPixels -gt 0) {
                        $stillClipped += "$name (+$($result.ClippedTopPixels)px above canvas)"
                    }
                    if ($result.RecoveredRows -gt 0) {
                        $repaired++
                        Write-Host ("REPAIR {0,-52} recoveredRows={1,3} height={2,3}" -f
                            $name, $result.RecoveredRows, $result.SilhouetteHeight)
                    }
                    else {
                        $unchanged++
                    }
                    if (-not $VerifyOnly) {
                        $result.Frame.Save($framePath, [System.Drawing.Imaging.ImageFormat]::Png)
                    }
                }
                finally {
                    $result.Frame.Dispose()
                }
            }
        }
    }
}

if ($stillClipped.Count -gt 0) {
    Write-Host "LOCOMOTION_TRANSITION_HEAD_REPAIR: FAIL"
    $stillClipped | ForEach-Object { Write-Host "  still clipped: $_" }
    exit 1
}

$mode = if ($VerifyOnly) { 'verify' } else { 'write' }
Write-Host ("LOCOMOTION_TRANSITION_HEAD_REPAIR: PASS | mode={0} repaired={1} unchanged={2} total={3}" -f
    $mode, $repaired, $unchanged, ($repaired + $unchanged))
