[CmdletBinding()]
param(
    [string]$AutomationRoot = '',
    [int]$PollSeconds = 3,
    [int]$DebounceSeconds = 12,
    [int]$FailureRetrySeconds = 90,
    [int]$PlayerRetrySeconds = 5
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$defaults = Get-FamilyCompanyDeployDefaults
if ([string]::IsNullOrWhiteSpace($AutomationRoot)) { $AutomationRoot = $defaults.AutomationRoot }
$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$watchLockPath = Join-Path $automationPath 'watcher.lock'
$logDirectory = Join-Path $automationPath 'logs'
$workerScriptPath = Get-NormalizedFullPath (Join-Path $PSScriptRoot 'Watch-FamilyCompanyDeploy.ps1')

$existing = Read-JsonIfPresent $watchStatusPath
if ($null -ne $existing -and $existing.PSObject.Properties.Name -contains 'processId') {
    $existingProcessId = [int]$existing.processId
    if (Test-WatcherProcessIdentity $existingProcessId $workerScriptPath) {
        Write-Output "Family Company deployment watcher is already running (PID $existingProcessId)."
        Write-Output "Watcher lock: $watchLockPath"
        Write-Output "Global Unity/build lock: $($defaults.GlobalBuildLockPath)"
        Write-Output "Status: $watchStatusPath"
        Write-Output "Logs: $logDirectory"
        exit 0
    }
}

New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
$powershell = Get-FamilyCompanyPowerShellHost
$argumentList = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $workerScriptPath + '"'),
    '-AutomationRoot', ('"' + $automationPath + '"'),
    '-PollSeconds', $PollSeconds, '-DebounceSeconds', $DebounceSeconds,
    '-FailureRetrySeconds', $FailureRetrySeconds, '-PlayerRetrySeconds', $PlayerRetrySeconds)
$process = Start-Process -FilePath $powershell -ArgumentList $argumentList -WindowStyle Hidden -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds(8)
$started = $false
while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 200
    $status = Read-JsonIfPresent $watchStatusPath
    if ($null -ne $status -and $status.PSObject.Properties.Name -contains 'processId' -and
        [int]$status.processId -eq $process.Id -and
        (Test-WatcherProcessIdentity $process.Id $workerScriptPath)) {
        $started = $true
        break
    }
    if ($process.HasExited) { break }
}
if (-not $started) {
    throw "Deployment watcher failed to start or exited before publishing status (PID $($process.Id))."
}

Write-Output "Started Family Company deployment watcher (PID $($process.Id))."
Write-Output "Target: $($defaults.TargetPath)"
Write-Output "Watcher lock: $watchLockPath"
Write-Output "Global Unity/build lock: $($defaults.GlobalBuildLockPath)"
Write-Output "Status: $watchStatusPath"
Write-Output "Logs: $logDirectory"
Write-Output 'Stop with STOP_WINDOWS_DEPLOY_WATCH.cmd.'
