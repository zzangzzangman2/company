[CmdletBinding()]
param([switch]$CompareCandidate, [string]$DeveloperSettings = '', [int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$walkProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$walkPlayer = Join-Path $walkProject 'Artifacts\FastQa\cache\WindowsPlayer\FamilyCompany_FastQa.exe'
$walkProfile = if ($CompareCandidate) { 'candidate' } else { 'default' }
$walkOutput = Join-Path $walkProject ('Artifacts\FastQa\WalkAudit-' + $walkProfile + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $walkOutput | Out-Null
$walkStart = [Diagnostics.ProcessStartInfo]::new()
$walkStart.FileName = $walkPlayer
$walkStart.WorkingDirectory = $walkProject
$walkStart.UseShellExecute = $false
$walkStart.CreateNoWindow = $true
$walkStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$walkStart.Arguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 ' +
    '-familyCompanyOpeningWalkAudit -familyCompanyOpeningWalkArtifacts "' + $walkOutput + '" -logFile "' + $walkOutput + '\player.log"'
if ($CompareCandidate) { $walkStart.Arguments += ' -familyCompanyLegacy2DScaleCandidate' }
if ($DeveloperSettings) {
    $walkSettings = [IO.Path]::GetFullPath($DeveloperSettings)
    if ($walkSettings.Contains('"')) { throw 'Invalid developer settings path.' }
    $walkStart.Arguments += ' -familyCompanyDevSettings "' + $walkSettings + '"'
}
$walkProcess = [Diagnostics.Process]::Start($walkStart)
$walkTimer = [Diagnostics.Stopwatch]::StartNew()
$walkWindowSamples = 0
Write-Host "[WALK AUDIT] profile=$walkProfile pid=$($walkProcess.Id) artifacts=$walkOutput"
try {
    while (-not $walkProcess.WaitForExit(200)) {
        $walkProcess.Refresh()
        $walkWindowSamples++
        if ($walkProcess.MainWindowHandle -ne [IntPtr]::Zero) { throw 'Unexpected visible QA window.' }
        if ($walkTimer.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw 'Walk audit timeout.' }
    }
    $walkProcess.WaitForExit()
    if ($walkProcess.ExitCode -ne 0) { throw "Walk audit capture failed; see $walkOutput\player.log" }
    Get-Content -LiteralPath (Join-Path $walkOutput 'audit-capture.txt')
    if (Select-String -LiteralPath (Join-Path $walkOutput 'player.log') -Pattern 'Exception:|error CS|: FAIL|FAIL_CLOSED' -Quiet) {
        throw 'Walk audit runtime errors.'
    }
    Write-Host "[WALK AUDIT] Captured; MainWindowHandle=0 samples=$walkWindowSamples. This is not a visual PASS."
} finally {
    if (-not $walkProcess.HasExited) { $walkProcess.Kill(); [void]$walkProcess.WaitForExit(10000) }
    $walkProcess.Dispose()
}
