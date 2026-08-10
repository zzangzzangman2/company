[CmdletBinding()]
param(
    [string]$AutomationRoot = 'C:\Users\godho\Downloads\FamilyCompany_BuildAutomation',
    [int]$WaitSeconds = 30,
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

if ($WaitSeconds -lt 1) { throw 'WaitSeconds must be positive.' }
$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$stopRequestPath = Join-Path $automationPath 'stop.request'
$workerScriptPath = Get-NormalizedFullPath (Join-Path $PSScriptRoot 'Watch-FamilyCompanyBuild.ps1')
$status = Read-JsonIfPresent $watchStatusPath
if ($null -eq $status -or -not ($status.PSObject.Properties.Name -contains 'processId')) {
    Write-Output 'No Family Company build watcher status was found.'
    exit 0
}

$watcherProcessId = [int]$status.processId
if (-not (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
    Write-Output 'The recorded watcher process is no longer running.'
    exit 0
}

New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
[IO.File]::WriteAllText(
    $stopRequestPath,
    [DateTime]::UtcNow.ToString('o') + [Environment]::NewLine,
    (New-Object Text.UTF8Encoding($false)))
Write-Output "Requested graceful watcher shutdown (PID $watcherProcessId)."

$deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    if (-not (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
        Write-Output 'Family Company build watcher stopped.'
        exit 0
    }
    Start-Sleep -Milliseconds 500
}

if ($Force -and (Test-WatcherProcessIdentity $watcherProcessId $workerScriptPath)) {
    Stop-Process -Id $watcherProcessId -Force
    Write-Output "Force-stopped Family Company build watcher (PID $watcherProcessId)."
    exit 0
}

throw "Watcher did not stop within $WaitSeconds second(s). Re-run with -Force only if a build is not being promoted."
