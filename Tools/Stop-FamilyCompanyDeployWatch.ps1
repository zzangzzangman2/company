[CmdletBinding()]
param(
    [string]$AutomationRoot = '',
    [int]$WaitSeconds = 30,
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$defaults = Get-FamilyCompanyDeployDefaults
if ([string]::IsNullOrWhiteSpace($AutomationRoot)) { $AutomationRoot = $defaults.AutomationRoot }
if ($WaitSeconds -lt 1) { throw 'WaitSeconds must be positive.' }
$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$watchLockPath = Join-Path $automationPath 'watcher.lock'
$stopRequestPath = Join-Path $automationPath 'stop.request'
$workerScriptPath = Get-NormalizedFullPath (Join-Path $PSScriptRoot 'Watch-FamilyCompanyDeploy.ps1')
$status = Read-JsonIfPresent $watchStatusPath
if ($null -eq $status -or -not ($status.PSObject.Properties.Name -contains 'processId')) {
    Write-Output 'No Family Company deployment watcher status was found.'
    exit 0
}

$watcherProcessId = [int]$status.processId
if (-not (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
    Write-Output 'The recorded deployment watcher is no longer running.'
    exit 0
}

New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
[IO.File]::WriteAllText(
    $stopRequestPath,
    [DateTime]::UtcNow.ToString('o') + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))
Write-Output "Requested graceful deployment watcher shutdown (PID $watcherProcessId)."
Write-Output "Watcher lock: $watchLockPath"
Write-Output "Status: $watchStatusPath"

$deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    if (-not (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
        Write-Output 'Family Company deployment watcher stopped.'
        exit 0
    }
    Start-Sleep -Milliseconds 500
}

if ($Force -and (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
    Stop-Process -Id $watcherProcessId -Force
    Write-Output "Force-stopped only the verified deployment watcher process (PID $watcherProcessId)."
    exit 0
}

throw "Watcher did not stop within $WaitSeconds second(s). No Unity or game process was terminated."
