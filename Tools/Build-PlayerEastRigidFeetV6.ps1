[CmdletBinding()]
param(
    [string]$BaseFrames = '',
    [string]$SourceFrames = '',
    [string]$OutputRoot = '',
    [ValidateRange(208,220)]
    [int]$FootCutY = 219,
    [ValidateRange(0,2)]
    [int]$CanonicalPhase = 0,
    [ValidateRange(0,2)]
    [int]$SwingCanonicalPhase = 0,
    [ValidateRange(224,236)]
    [int]$SupportFloorY = 233,
    [ValidateRange(2,12)]
    [int]$SwingLiftOuter = 6,
    [ValidateRange(4,16)]
    [int]$SwingLiftMiddle = 10,
    [switch]$FlatShoes
)

$ErrorActionPreference = 'Stop'
$builder = Join-Path $PSScriptRoot 'Build-PlayerEastFootForwardV5.ps1'
$arguments = @{
    FootCutY = $FootCutY
    CanonicalPhase = $CanonicalPhase
    SwingCanonicalPhase = $SwingCanonicalPhase
    SupportFloorY = $SupportFloorY
    SwingLiftOuter = $SwingLiftOuter
    SwingLiftMiddle = $SwingLiftMiddle
    RigidFeet = $true
    FlatShoes = [bool]$FlatShoes
}
if (-not [string]::IsNullOrWhiteSpace($BaseFrames)) { $arguments.BaseFrames = $BaseFrames }
if (-not [string]::IsNullOrWhiteSpace($SourceFrames)) { $arguments.SourceFrames = $SourceFrames }
if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) { $arguments.OutputRoot = $OutputRoot }

& $builder @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
