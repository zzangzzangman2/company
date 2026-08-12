[CmdletBinding()]
param(
    [string]$AutomationRoot = 'C:\Users\godho\Downloads\Family\FamilyCompany_BuildAutomation',
    [int]$PollSeconds = 3,
    [int]$DebounceSeconds = 12,
    [int]$FailureRetrySeconds = 90
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$workerScriptPath = Get-NormalizedFullPath (Join-Path $PSScriptRoot 'Watch-FamilyCompanyBuild.ps1')
$existing = Read-JsonIfPresent $watchStatusPath
if ($null -ne $existing -and $existing.PSObject.Properties.Name -contains 'processId') {
    $existingProcessId = [int]$existing.processId
    if (Test-WatcherProcessIdentity $existingProcessId $workerScriptPath) {
        Write-Output "Family Company build watcher is already running (PID $existingProcessId)."
        exit 0
    }
}

New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
$powershell = Join-Path $PSHOME 'powershell.exe'
$argumentList = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', ('"' + $workerScriptPath + '"'),
    '-AutomationRoot', ('"' + $automationPath + '"'),
    '-PollSeconds', $PollSeconds,
    '-DebounceSeconds', $DebounceSeconds,
    '-FailureRetrySeconds', $FailureRetrySeconds)
$process = Start-Process -FilePath $powershell -ArgumentList $argumentList -WindowStyle Hidden -PassThru
Write-Output "Started Family Company build watcher (PID $($process.Id))."
