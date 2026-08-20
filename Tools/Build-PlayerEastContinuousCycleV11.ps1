[CmdletBinding()]
param(
    [string]$BaseFrames = '',
    [string]$OutputRoot = '',
    [string]$Contract = 'FC-PLAYER-EAST-CONTINUOUS-CYCLE-V11-CANDIDATE',
    [ValidateRange(168,176)]
    [int]$LowerCutY = 171,
    [ValidateRange(230,234)]
    [int]$GroundY = 233
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($BaseFrames)) {
    # V5 is the last east candidate whose shoes remain joined to their authored
    # ankles. V6-V10 moved partial foot components and must not be used here.
    $BaseFrames = Join-Path $projectRoot 'Artifacts\PlayerEastFootForwardV5Candidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\PlayerEastContinuousCycleV11Candidate'
}

$BaseFrames = [IO.Path]::GetFullPath($BaseFrames)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$outputFrames = Join-Path $OutputRoot 'Frames'
New-Item -ItemType Directory -Force -Path $OutputRoot,$outputFrames | Out-Null

Add-Type -AssemblyName System.Drawing

# The generated source repeats each half-cycle as wide -> passing -> wide.
# Swapping only the two closing lower-body poses produces a continuous cycle:
# A contact -> A passing -> B contact -> B contact -> B passing -> A contact.
# Crucially, each donor is copied as one authored lower body; no shoe, ankle, or
# lower-leg component is translated independently.
$lowerDonorByPhase = @(0,1,5,3,4,2)

function Get-AlphaMaxY {
    param([System.Drawing.Bitmap]$Bitmap)
    for ($y = $Bitmap.Height-1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x,$y).A -gt 0) { return $y }
        }
    }
    throw 'Cannot ground an empty continuous east-walk frame.'
}

function Write-ContinuousFrame {
    param(
        [string]$UpperPath,
        [string]$LowerPath,
        [string]$DestinationPath,
        [int]$CutY,
        [int]$VerticalShift)

    $upper = New-Object System.Drawing.Bitmap $UpperPath
    $lower = New-Object System.Drawing.Bitmap $LowerPath
    try {
        if ($upper.Width -ne 256 -or $upper.Height -ne 256 -or
            $lower.Width -ne 256 -or $lower.Height -ne 256) {
            throw 'Continuous east-walk frames must be 256x256.'
        }

        $result = New-Object System.Drawing.Bitmap 256,256,
            ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            for ($y = 0; $y -lt 256; $y++) {
                $sourceY = $y-$VerticalShift
                if ($sourceY -lt 0 -or $sourceY -ge 256) { continue }
                $source = if ($sourceY -lt $CutY) { $upper } else { $lower }
                for ($x = 0; $x -lt 256; $x++) {
                    $result.SetPixel($x,$y,$source.GetPixel($x,$sourceY))
                }
            }
            $result.Save($DestinationPath,[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $result.Dispose() }
    }
    finally {
        $upper.Dispose()
        $lower.Dispose()
    }
}

$baseFiles = @(Get-ChildItem -LiteralPath $BaseFrames -Filter 'player_*_walk_*_v2.png' -File)
if ($baseFiles.Count -ne 48) {
    throw "Expected exactly 48 base player frames, found $($baseFiles.Count)."
}
foreach ($file in $baseFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $outputFrames $file.Name) -Force
}

for ($phase = 0; $phase -lt 6; $phase++) {
    $upperPath = Join-Path $BaseFrames "player_east_walk_${phase}_v2.png"
    $donorPhase = $lowerDonorByPhase[$phase]
    $lowerPath = Join-Path $BaseFrames "player_east_walk_${donorPhase}_v2.png"
    $destinationPath = Join-Path $outputFrames "player_east_walk_${phase}_v2.png"
    $lower = New-Object System.Drawing.Bitmap $lowerPath
    try { $verticalShift = $GroundY-(Get-AlphaMaxY $lower) }
    finally { $lower.Dispose() }
    if ($verticalShift -lt 0 -or $verticalShift -gt 1) {
        throw "Unexpected whole-frame ground shift at phase ${phase}: $verticalShift."
    }
    Write-ContinuousFrame -UpperPath $upperPath -LowerPath $lowerPath `
        -DestinationPath $destinationPath -CutY $LowerCutY -VerticalShift $verticalShift
}

# Exact seam and source-preservation gates. These make it impossible for a
# future edit to reintroduce V10's partial-foot translation under this contract.
for ($phase = 0; $phase -lt 6; $phase++) {
    $upperPath = Join-Path $BaseFrames "player_east_walk_${phase}_v2.png"
    $lowerPath = Join-Path $BaseFrames "player_east_walk_$($lowerDonorByPhase[$phase])_v2.png"
    $resultPath = Join-Path $outputFrames "player_east_walk_${phase}_v2.png"
    $upper = New-Object System.Drawing.Bitmap $upperPath
    $lower = New-Object System.Drawing.Bitmap $lowerPath
    $result = New-Object System.Drawing.Bitmap $resultPath
    try {
        $verticalShift = $GroundY-(Get-AlphaMaxY $lower)
        for ($y = 0; $y -lt 256; $y++) {
            $sourceY = $y-$verticalShift
            for ($x = 0; $x -lt 256; $x++) {
                $expectedArgb = 0
                if ($sourceY -ge 0 -and $sourceY -lt 256) {
                    $expected = if ($sourceY -lt $LowerCutY) { $upper } else { $lower }
                    $expectedArgb = $expected.GetPixel($x,$sourceY).ToArgb()
                }
                if ($result.GetPixel($x,$y).ToArgb() -ne $expectedArgb) {
                    throw "Source-preservation invariant failed at phase ${phase}, pixel ${x},${y}."
                }
            }
        }
        if ((Get-AlphaMaxY $result) -ne $GroundY) {
            throw "Ground invariant failed at phase ${phase}."
        }
    }
    finally {
        $upper.Dispose()
        $lower.Dispose()
        $result.Dispose()
    }
}

$sheetPath = Join-Path $OutputRoot 'player_east_continuous_cycle_v11_contactsheet.png'
$sheet = New-Object System.Drawing.Bitmap 1536,256,
    ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($sheet)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.Clear([System.Drawing.Color]::Transparent)
        for ($phase = 0; $phase -lt 6; $phase++) {
            $path = Join-Path $outputFrames "player_east_walk_${phase}_v2.png"
            $frame = New-Object System.Drawing.Bitmap $path
            try { $graphics.DrawImageUnscaled($frame,$phase*256,0) }
            finally { $frame.Dispose() }
        }
    }
    finally { $graphics.Dispose() }
    $sheet.Save($sheetPath,[System.Drawing.Imaging.ImageFormat]::Png)
}
finally { $sheet.Dispose() }

$receipt = [ordered]@{
    schemaVersion = 1
    contract = $Contract
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    baseFrames = $BaseFrames
    directionsChanged = @('east')
    phasesChanged = @(2,5)
    lowerCutY = $LowerCutY
    groundY = $GroundY
    lowerDonorByPhase = $lowerDonorByPhase
    wholeFrameVerticalShiftByPhase = @(1,0,0,1,0,0)
    gaitOrder = @('A-contact','A-passing','B-contact','B-contact','B-passing','A-contact')
    rule = 'Preserve every phase upper body above the waist seam. Reorder complete authored lower bodies to make the two anatomical legs pass continuously. Normalize only the complete 256x256 character frame to the shared ground row. Never translate or paste an isolated shoe, ankle, or lower-leg fragment.'
}
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'continuous-cycle-receipt.json'),
    ($receipt | ConvertTo-Json -Depth 5) + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_EAST_CONTINUOUS_CYCLE_V11: BUILT'
Write-Output "Frames: $outputFrames"
Write-Output "Contact sheet: $sheetPath"
