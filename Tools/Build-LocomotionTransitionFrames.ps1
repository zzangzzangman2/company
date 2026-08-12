param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Remove-DisconnectedAlphaIslands {
    param([System.Drawing.Bitmap]$Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $solid = New-Object 'bool[]' ($width * $height)
    $visited = New-Object 'bool[]' ($width * $height)
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $isMagentaResidue = $pixel.R -gt 160 -and $pixel.B -gt 130 -and $pixel.G -lt 110 -and
                                ((($pixel.R + $pixel.B) / 2) - $pixel.G) -gt 70
            if ($pixel.A -le 24 -or $isMagentaResidue) {
                $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                $solid[$y * $width + $x] = $false
            }
            else {
                # Runtime pixel sprites use hard alpha so filtering cannot leave
                # magenta/grey halos around the silhouette.
                $Bitmap.SetPixel(
                    $x,
                    $y,
                    [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B))
                $solid[$y * $width + $x] = $true
            }
        }
    }

    $components = New-Object System.Collections.Generic.List[object]
    $offsets = @(@(-1, 0), @(1, 0), @(0, -1), @(0, 1), @(-1, -1), @(1, -1), @(-1, 1), @(1, 1))
    for ($index = 0; $index -lt $solid.Length; $index++) {
        if (-not $solid[$index] -or $visited[$index]) { continue }
        $queue = New-Object 'System.Collections.Generic.Queue[int]'
        $pixels = New-Object 'System.Collections.Generic.List[int]'
        $queue.Enqueue($index)
        $visited[$index] = $true
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            $pixels.Add($current)
            $currentX = $current % $width
            $currentY = [Math]::Floor($current / $width)
            foreach ($offset in $offsets) {
                $nextX = $currentX + $offset[0]
                $nextY = $currentY + $offset[1]
                if ($nextX -lt 0 -or $nextX -ge $width -or $nextY -lt 0 -or $nextY -ge $height) {
                    continue
                }
                $next = $nextY * $width + $nextX
                if (-not $solid[$next] -or $visited[$next]) { continue }
                $visited[$next] = $true
                $queue.Enqueue($next)
            }
        }
        $components.Add($pixels)
    }
    if ($components.Count -eq 0) { return }
    $largestComponent = $components | Sort-Object Count -Descending | Select-Object -First 1
    foreach ($component in $components) {
        if ([object]::ReferenceEquals($component, $largestComponent)) { continue }
        foreach ($pixelIndex in $component) {
            $x = $pixelIndex % $width
            $y = [Math]::Floor($pixelIndex / $width)
            $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
        }
    }
}

function Align-FootAnchor {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$TargetX = 128,
        [int]$TargetBottomY = 247
    )

    $maxY = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -gt 24) {
                $maxY = [Math]::Max($maxY, $y)
            }
        }
    }
    if ($maxY -lt 0) {
        throw 'Cannot align an empty locomotion frame.'
    }

    # The lowest 16 visible rows contain the planted foot/feet.  Use their
    # horizontal centre as the sprite pivot instead of the full-body bounds so
    # wide hair, skirts, and directional poses cannot make the actor slide.
    $footMinX = $Bitmap.Width
    $footMaxX = -1
    $footBandTop = [Math]::Max(0, $maxY - 15)
    for ($y = $footBandTop; $y -le $maxY; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -gt 24) {
                $footMinX = [Math]::Min($footMinX, $x)
                $footMaxX = [Math]::Max($footMaxX, $x)
            }
        }
    }
    if ($footMaxX -lt 0) {
        throw 'Cannot find a visible foot anchor in locomotion frame.'
    }

    $footCentreX = ($footMinX + $footMaxX) / 2.0
    $offsetX = [int][Math]::Round($TargetX - $footCentreX)
    $offsetY = $TargetBottomY - $maxY
    $aligned = New-Object System.Drawing.Bitmap $Bitmap.Width, $Bitmap.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($aligned)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImageUnscaled($Bitmap, $offsetX, $offsetY)
    }
    finally {
        $graphics.Dispose()
    }
    return $aligned
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $projectRoot 'Assets\Art\Characters\Family\LocomotionTransitionsV1'
$chromaHelper = Join-Path $env:USERPROFILE '.codex\skills\.system\imagegen\scripts\remove_chroma_key.py'
if (-not (Test-Path -LiteralPath $chromaHelper)) {
    throw "Missing imagegen chroma helper: $chromaHelper"
}
$members = @(
    @{ Id = 'player' },
    @{ Id = 'older_sister' },
    @{ Id = 'father' },
    @{ Id = 'mother' }
)
$clips = @(
    @{ Id = 'turn_in_place'; SourcePrefix = '' },
    @{ Id = 'walk_start'; SourcePrefix = 'walk_start_' },
    @{ Id = 'walk_stop'; SourcePrefix = 'walk_stop_' },
    @{ Id = 'short_shuffle'; SourcePrefix = 'short_shuffle_' }
)
$directions = @('south', 'southwest', 'west', 'northwest', 'north', 'northeast', 'east', 'southeast')

[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null

foreach ($member in $members) {
foreach ($clip in $clips) {
    $sourceFile = $clip.SourcePrefix + $member.Id + '.png'
    $sourcePath = Join-Path $SourceRoot $sourceFile
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Missing generated transition source: $sourcePath"
    }
    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        $normalized = New-Object System.Drawing.Bitmap 1024, 1024, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($normalized)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($source, 0, 0, 1024, 1024)
            }
            finally {
                $graphics.Dispose()
            }

            $key = $normalized.GetPixel(0, 0)
            $memberRoot = Join-Path $outputRoot $member.Id
            $sourceOutput = Join-Path $memberRoot 'Source'
            $frameOutput = Join-Path $memberRoot 'Frames'
            [System.IO.Directory]::CreateDirectory($sourceOutput) | Out-Null
            [System.IO.Directory]::CreateDirectory($frameOutput) | Out-Null
            if ($clip.Id -eq 'turn_in_place') {
                Get-ChildItem -LiteralPath $sourceOutput -Filter ($member.Id + '_locomotion_transitions_4x4_*_v1.png') -ErrorAction SilentlyContinue |
                    Remove-Item -Force
                Get-ChildItem -LiteralPath $frameOutput -Filter ($member.Id + '_*_transition_?.png') -ErrorAction SilentlyContinue |
                    Remove-Item -Force
            }
            $normalizedPath = Join-Path $sourceOutput (
                $member.Id + '_' + $clip.Id + '_4x4_chroma_v1.png')
            $alphaPath = Join-Path $sourceOutput (
                $member.Id + '_' + $clip.Id + '_4x4_alpha_v1.png')
            $normalized.Save($normalizedPath, [System.Drawing.Imaging.ImageFormat]::Png)

            & python $chromaHelper --input $normalizedPath --out $alphaPath --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 120 --despill --force
            if ($LASTEXITCODE -ne 0) {
                throw "Chroma removal failed for $($member.Id) with exit code $LASTEXITCODE"
            }
            $alphaSheet = [System.Drawing.Bitmap]::FromFile($alphaPath)
            try {
            for ($row = 0; $row -lt 4; $row++) {
                for ($column = 0; $column -lt 4; $column++) {
                    $directionIndex = $row * 2 + [Math]::Floor($column / 2)
                    $pose = if (($column % 2) -eq 0) { 'a' } else { 'b' }
                    $frame = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    try {
                        $drawing = [System.Drawing.Graphics]::FromImage($frame)
                        try {
                            $drawing.DrawImage(
                                $alphaSheet,
                                (New-Object System.Drawing.Rectangle 0, 0, 256, 256),
                                (New-Object System.Drawing.Rectangle ($column * 256), ($row * 256), 256, 256),
                                [System.Drawing.GraphicsUnit]::Pixel)
                        }
                        finally {
                            $drawing.Dispose()
                        }

                        Remove-DisconnectedAlphaIslands -Bitmap $frame
                        $alignedFrame = Align-FootAnchor -Bitmap $frame
                        $frame.Dispose()
                        $frame = $alignedFrame
                        $visiblePixels = 0
                        for ($y = 0; $y -lt 256; $y++) {
                            for ($x = 0; $x -lt 256; $x++) {
                                if ($frame.GetPixel($x, $y).A -gt 0) { $visiblePixels++ }
                            }
                        }
                        if ($visiblePixels -lt 1000) {
                            throw "$($member.Id) $($directions[$directionIndex]) $pose has too few visible pixels: $visiblePixels"
                        }
                        $framePath = Join-Path $frameOutput (
                            $member.Id + '_' + $directions[$directionIndex] + '_' + $clip.Id + '_' + $pose + '.png')
                        $frame.Save($framePath, [System.Drawing.Imaging.ImageFormat]::Png)
                    }
                    finally {
                        $frame.Dispose()
                    }
                }
            }
            }
            finally {
                $alphaSheet.Dispose()
            }
        }
        finally {
            $normalized.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}
}

Write-Host "LOCOMOTION_TRANSITION_FRAME_BUILD_PASS | members=4 clips=4 frames=256 output=$outputRoot"
