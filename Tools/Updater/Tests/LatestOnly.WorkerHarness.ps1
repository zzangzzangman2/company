param([string]$GameDirectory, [string]$InstallRoot, [string]$ResultPath, [string]$Scenario, [switch]$Legacy)
$ErrorActionPreference = 'Stop'
# Local API fault injection only. Production scripts are invoked unchanged; no network or game runs.
function Invoke-RestMethod {
    param($Uri, $Headers, $TimeoutSec)
    if ($Scenario -eq 'network') { throw 'TEST_NETWORK_UNAVAILABLE' }
    if ($Scenario -eq 'missing') { return @{draft=$false;prerelease=$false;tag_name='not-a-game';id=1} }
    if ($Scenario -eq 'draft') { return @{draft=$true;prerelease=$false;tag_name='fc-win-20260906.2';id=2} }
    throw 'Unexpected test scenario.'
}
if ($Legacy) {
    & (Join-Path $PSScriptRoot '../FamilyCompany.Launcher.ps1') -InstallRoot $InstallRoot -UpdateOnly
} else {
    & (Join-Path $PSScriptRoot '../FamilyCompany.InGame.ps1') -GameDirectory $GameDirectory -InstallRoot $InstallRoot -ResultPath $ResultPath
}
exit $LASTEXITCODE
