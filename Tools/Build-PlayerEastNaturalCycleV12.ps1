[CmdletBinding()]
param(
    [string]$V4Frames = '',
    [string]$V3Frames = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($V4Frames)) {
    $V4Frames = Join-Path $projectRoot 'Artifacts\Player2DWalkV4AlternatingCandidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($V3Frames)) {
    $V3Frames = Join-Path $projectRoot 'Artifacts\Player2DWalkV3Candidate\Frames'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'Artifacts\PlayerEastNaturalCycleV12Candidate'
}
$V4Frames = [IO.Path]::GetFullPath($V4Frames)
$V3Frames = [IO.Path]::GetFullPath($V3Frames)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$semanticRoot = Join-Path $OutputRoot 'SemanticShoeBase'

& (Join-Path $PSScriptRoot 'Build-PlayerEastSemanticShoesV12.ps1') `
    -BaseFrames $V4Frames -SourceFrames $V3Frames -OutputRoot $semanticRoot

& (Join-Path $PSScriptRoot 'Build-PlayerEastContinuousCycleV11.ps1') `
    -BaseFrames (Join-Path $semanticRoot 'Frames') -OutputRoot $OutputRoot `
    -Contract 'FC-PLAYER-EAST-NATURAL-CYCLE-V12-CANDIDATE'

$legacySheet = Join-Path $OutputRoot 'player_east_continuous_cycle_v11_contactsheet.png'
$finalSheet = Join-Path $OutputRoot 'player_east_natural_cycle_v12_contactsheet.png'
Copy-Item -LiteralPath $legacySheet -Destination $finalSheet -Force

$receipt = [ordered]@{
    schemaVersion = 1
    contract = 'FC-PLAYER-EAST-NATURAL-CYCLE-V12-CANDIDATE'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    v4Frames = $V4Frames
    v3Frames = $V3Frames
    outputFrames = Join-Path $OutputRoot 'Frames'
    directionsChanged = @('east')
    phases = 6
    semanticShoeRule = 'Remove only footwear pixels from V4 P3/P4/P5. Preserve every navy trouser, calf, knee, hip, torso, arm, head, and face pixel. Attach the matching unmirrored V3 shoe to its reflected collar with dy=0.'
    ownerMap = @('P3 L=P0R / R=P0L','P4 L=P1R / R=P1L','P5 L=P2R / R=P2L')
    collarTranslationX = @(@(-52,62),@(-34,31),@(-48,63))
    lowerDonorByPhase = @(0,1,5,3,4,2)
    gaitRule = 'A stance moves monotonically -X while B swings +X, then B stance moves -X while A swings +X. P2/P3 and P5/P0 are the symmetric contact dwell pairs.'
    groundY = 233
    wholeFrameVerticalShiftByPhase = @(1,0,0,1,0,0)
    generatedImageUsedInShippingFrames = $false
}
[IO.File]::WriteAllText(
    (Join-Path $OutputRoot 'natural-cycle-receipt.json'),
    ($receipt | ConvertTo-Json -Depth 6) + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))

Write-Output 'PLAYER_EAST_NATURAL_CYCLE_V12: BUILT'
Write-Output "Frames: $(Join-Path $OutputRoot 'Frames')"
Write-Output "Contact sheet: $finalSheet"
