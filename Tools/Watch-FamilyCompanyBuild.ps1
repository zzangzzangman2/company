[CmdletBinding()]
param(
    [string]$CanonicalProjectPath = 'C:\Users\godho\Documents\Codex\family_company_unity',
    [string]$UnityEditorPath = 'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe',
    [string]$FinalOutputPath = 'C:\Users\godho\Downloads\Family\FamilyCompany_Playtest',
    [string]$AutomationRoot = 'C:\Users\godho\Downloads\Family\FamilyCompany_BuildAutomation',
    [int]$PollSeconds = 3,
    [int]$DebounceSeconds = 12,
    [int]$FailureRetrySeconds = 90,
    [int]$UnityWaitTimeoutMinutes = 120,
    [int]$UnityRetrySeconds = 15
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

if ($PollSeconds -lt 1) { throw 'PollSeconds must be positive.' }
if ($DebounceSeconds -lt 2) { throw 'DebounceSeconds must be at least 2.' }
if ($FailureRetrySeconds -lt 10) { throw 'FailureRetrySeconds must be at least 10.' }

$projectPath = Assert-CanonicalProjectPath $CanonicalProjectPath
[void](Assert-ExactUnityEditor $UnityEditorPath $projectPath)
$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$buildStatusPath = Join-Path $automationPath 'build-status.json'
$watchLockPath = Join-Path $automationPath 'watcher.lock'
$stopRequestPath = Join-Path $automationPath 'stop.request'
$buildScriptPath = Join-Path $PSScriptRoot 'Build-FamilyCompanyWindows.ps1'
$workerScriptPath = Get-NormalizedFullPath $MyInvocation.MyCommand.Path

if (-not (Test-Path -LiteralPath $buildScriptPath -PathType Leaf)) {
    throw "One-shot build script is missing: $buildScriptPath"
}
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
$watchLock = Open-ExclusiveLock $watchLockPath
if ($null -eq $watchLock) {
    Write-Error 'Another Family Company build watcher already owns the watcher lock.'
    exit 24
}

$pendingFingerprint = $null
$pendingSinceUtc = [DateTime]::MinValue
$nextBuildNotBeforeUtc = [DateTime]::MinValue
$lastSuccessfulFingerprint = $null
$existingBuildStatus = Read-JsonIfPresent $buildStatusPath
if ($null -ne $existingBuildStatus -and
    $existingBuildStatus.PSObject.Properties.Name -contains 'lastSuccessfulFingerprint') {
    $lastSuccessfulFingerprint = [string]$existingBuildStatus.lastSuccessfulFingerprint
}

try {
    Remove-Item -LiteralPath $stopRequestPath -Force -ErrorAction SilentlyContinue
    Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
        state = 'Watching'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        workerScript = $workerScriptPath; canonicalProject = $projectPath
        lastSuccessfulFingerprint = $lastSuccessfulFingerprint
    })

    while ($true) {
        if (Test-Path -LiteralPath $stopRequestPath -PathType Leaf) { break }

        try {
            $snapshot = Get-CanonicalBuildSnapshot $projectPath
            $nowUtc = [DateTime]::UtcNow
            if ([string]::Equals(
                    $snapshot.Fingerprint,
                    $lastSuccessfulFingerprint,
                    [StringComparison]::Ordinal)) {
                $pendingFingerprint = $null
                $pendingSinceUtc = [DateTime]::MinValue
                Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                    state = 'Watching'; updatedUtc = $nowUtc.ToString('o'); processId = $PID
                    workerScript = $workerScriptPath; canonicalProject = $projectPath
                    head = $snapshot.Head; branch = $snapshot.Branch
                    currentFingerprint = $snapshot.Fingerprint
                    lastSuccessfulFingerprint = $lastSuccessfulFingerprint
                })
            }
            else {
                if (-not [string]::Equals(
                        $pendingFingerprint,
                        $snapshot.Fingerprint,
                        [StringComparison]::Ordinal)) {
                    $pendingFingerprint = $snapshot.Fingerprint
                    $pendingSinceUtc = $nowUtc
                }

                $stableSeconds = ($nowUtc - $pendingSinceUtc).TotalSeconds
                $canBuild = $stableSeconds -ge $DebounceSeconds -and $nowUtc -ge $nextBuildNotBeforeUtc
                if ($canBuild) {
                    Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                        state = 'InvokingBuild'; updatedUtc = $nowUtc.ToString('o'); processId = $PID
                        workerScript = $workerScriptPath; canonicalProject = $projectPath
                        head = $snapshot.Head; branch = $snapshot.Branch
                        currentFingerprint = $snapshot.Fingerprint
                        lastSuccessfulFingerprint = $lastSuccessfulFingerprint
                    })

                    $powershell = Join-Path $PSHOME 'powershell.exe'
                    $arguments = @(
                        '-NoProfile', '-ExecutionPolicy', 'Bypass',
                        '-File', $buildScriptPath,
                        '-CanonicalProjectPath', $projectPath,
                        '-UnityEditorPath', $UnityEditorPath,
                        '-FinalOutputPath', $FinalOutputPath,
                        '-AutomationRoot', $automationPath,
                        '-UnityWaitTimeoutMinutes', $UnityWaitTimeoutMinutes,
                        '-UnityRetrySeconds', $UnityRetrySeconds)
                    $previousErrorActionPreference = $ErrorActionPreference
                    $ErrorActionPreference = 'Continue'
                    try {
                        & $powershell @arguments 2>&1 | Out-Null
                        $buildExitCode = $LASTEXITCODE
                    }
                    finally {
                        $ErrorActionPreference = $previousErrorActionPreference
                    }
                    $latestBuildStatus = Read-JsonIfPresent $buildStatusPath
                    if ($buildExitCode -eq 0 -and $null -ne $latestBuildStatus -and
                        $latestBuildStatus.PSObject.Properties.Name -contains 'lastSuccessfulFingerprint') {
                        $lastSuccessfulFingerprint = [string]$latestBuildStatus.lastSuccessfulFingerprint
                        $nextBuildNotBeforeUtc = [DateTime]::MinValue
                    }
                    else {
                        $nextBuildNotBeforeUtc = [DateTime]::UtcNow.AddSeconds($FailureRetrySeconds)
                    }
                    $pendingFingerprint = $null
                    $pendingSinceUtc = [DateTime]::MinValue
                }
                else {
                    Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                        state = 'Debouncing'; updatedUtc = $nowUtc.ToString('o'); processId = $PID
                        workerScript = $workerScriptPath; canonicalProject = $projectPath
                        head = $snapshot.Head; branch = $snapshot.Branch
                        currentFingerprint = $snapshot.Fingerprint
                        pendingSinceUtc = $pendingSinceUtc.ToString('o')
                        nextBuildNotBeforeUtc = $nextBuildNotBeforeUtc.ToString('o')
                        lastSuccessfulFingerprint = $lastSuccessfulFingerprint
                    })
                }
            }
        }
        catch {
            Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                state = 'WatchError'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
                workerScript = $workerScriptPath; canonicalProject = $projectPath
                error = $_.Exception.Message; lastSuccessfulFingerprint = $lastSuccessfulFingerprint
            })
            $nextBuildNotBeforeUtc = [DateTime]::UtcNow.AddSeconds($FailureRetrySeconds)
        }

        Start-Sleep -Seconds $PollSeconds
    }
}
finally {
    Remove-Item -LiteralPath $stopRequestPath -Force -ErrorAction SilentlyContinue
    Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
        state = 'Stopped'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        workerScript = $workerScriptPath; canonicalProject = $projectPath
        lastSuccessfulFingerprint = $lastSuccessfulFingerprint
    })
    if ($null -ne $watchLock) {
        $watchLock.Dispose()
        Remove-Item -LiteralPath $watchLockPath -Force -ErrorAction SilentlyContinue
    }
}
