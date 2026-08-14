[CmdletBinding()]
param(
    [string]$CanonicalProjectPath = '',
    [string]$UnityEditorPath = '',
    [string]$TargetPath = '',
    [string]$DeploymentRoot = '',
    [string]$AutomationRoot = '',
    [string]$GlobalBuildLockPath = '',
    [string]$RequiredBranch = 'codex/integration-p0-qa',
    [string]$TestStatus = 'Unity pre-build validators run during Release build; final player smoke pending',
    [int]$PollSeconds = 3,
    [int]$DebounceSeconds = 12,
    [int]$FailureRetrySeconds = 90,
    [int]$PlayerRetrySeconds = 5,
    [int]$MaximumIterations = 0
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

if ($PollSeconds -lt 1) { throw 'PollSeconds must be positive.' }
if ($DebounceSeconds -lt 2) { throw 'DebounceSeconds must be at least 2.' }
if ($FailureRetrySeconds -lt 10) { throw 'FailureRetrySeconds must be at least 10.' }
if ($PlayerRetrySeconds -lt 2) { throw 'PlayerRetrySeconds must be at least 2.' }
if ($MaximumIterations -lt 0) { throw 'MaximumIterations cannot be negative.' }

$defaults = Get-FamilyCompanyDeployDefaults
if ([string]::IsNullOrWhiteSpace($CanonicalProjectPath)) { $CanonicalProjectPath = $defaults.CanonicalProjectPath }
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) { $UnityEditorPath = $defaults.UnityEditorPath }
if ([string]::IsNullOrWhiteSpace($TargetPath)) { $TargetPath = $defaults.TargetPath }
if ([string]::IsNullOrWhiteSpace($DeploymentRoot)) { $DeploymentRoot = $defaults.DeploymentRoot }
if ([string]::IsNullOrWhiteSpace($AutomationRoot)) { $AutomationRoot = $defaults.AutomationRoot }
if ([string]::IsNullOrWhiteSpace($GlobalBuildLockPath)) { $GlobalBuildLockPath = $defaults.GlobalBuildLockPath }

$automationPath = Get-NormalizedFullPath $AutomationRoot
$watchStatusPath = Join-Path $automationPath 'watch-status.json'
$watchLockPath = Join-Path $automationPath 'watcher.lock'
$stopRequestPath = Join-Path $automationPath 'stop.request'
$logDirectory = Join-Path $automationPath 'logs'
$logPath = Join-Path $logDirectory "watch-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$PID.log"
$workerScriptPath = Get-NormalizedFullPath $MyInvocation.MyCommand.Path
$deployScriptPath = Join-Path $PSScriptRoot 'Deploy-FamilyCompanyWindows.ps1'

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$watchLock = Open-ExclusiveLock $watchLockPath
if ($null -eq $watchLock) {
    Add-BuildLogLine $logPath 'DUPLICATE_WATCHER_REJECTED: watcher.lock is already owned.'
    Write-Error "Another Family Company deployment watcher owns: $watchLockPath" -ErrorAction Continue
    exit 24
}

$projectPath = $null
$pendingHead = $null
$pendingSinceUtc = [DateTime]::MinValue
$nextAttemptUtc = [DateTime]::MinValue
$lastLoggedState = ''
$iteration = 0
try {
    $projectPath = Assert-CanonicalProjectPath $CanonicalProjectPath
    $unityEditor = Assert-ExactUnityEditor $UnityEditorPath $projectPath
    $target = Get-NormalizedFullPath $TargetPath
    $deploymentPath = Get-NormalizedFullPath $DeploymentRoot
    $globalLockPath = Get-NormalizedFullPath $GlobalBuildLockPath
    if (-not (Test-Path -LiteralPath $deployScriptPath -PathType Leaf)) {
        throw "Deployment script is missing: $deployScriptPath"
    }
    Remove-Item -LiteralPath $stopRequestPath -Force -ErrorAction SilentlyContinue
    Add-BuildLogLine $logPath (
        "WATCH_START pid=$PID project=$projectPath target=$target watcherLock=$watchLockPath " +
        "globalBuildLock=$globalLockPath debounceSeconds=$DebounceSeconds")

    while ($true) {
        if (Test-Path -LiteralPath $stopRequestPath -PathType Leaf) { break }
        $iteration++
        $stateName = 'Watching'
        $detail = ''
        $repositoryState = $null
        try {
            $repositoryState = Get-FamilyCompanyRepositoryState $projectPath
            $nowUtc = [DateTime]::UtcNow
            if ($repositoryState.HasConflicts) {
                $stateName = 'HeldMergeConflict'
                $detail = $repositoryState.Conflicts
                $pendingHead = $null
            }
            elseif ($repositoryState.IsDirty) {
                $stateName = 'HeldDirtyWorktree'
                $detail = 'Commit or remove all tracked and untracked changes before deployment.'
                $pendingHead = $null
            }
            elseif (-not [string]::Equals($repositoryState.Branch, $RequiredBranch, [StringComparison]::Ordinal)) {
                $stateName = 'HeldWrongBranch'
                $detail = "Expected '$RequiredBranch', found '$($repositoryState.Branch)'."
                $pendingHead = $null
            }
            else {
                $deployedCommit = Get-FamilyCompanyDeployedCommit $target
                if ([string]::Equals($deployedCommit, $repositoryState.Head, [StringComparison]::Ordinal)) {
                    $stateName = 'WatchingUpToDate'
                    $detail = "HEAD $($repositoryState.Head) is deployed."
                    $pendingHead = $null
                }
                else {
                    if (-not [string]::Equals($pendingHead, $repositoryState.Head, [StringComparison]::Ordinal)) {
                        $pendingHead = $repositoryState.Head
                        $pendingSinceUtc = $nowUtc
                    }
                    $stableSeconds = ($nowUtc - $pendingSinceUtc).TotalSeconds
                    if ($stableSeconds -lt $DebounceSeconds) {
                        $stateName = 'DebouncingCommittedHead'
                        $detail = "Stable for $([Math]::Round($stableSeconds, 1)) of $DebounceSeconds second(s)."
                    }
                    elseif ($nowUtc -lt $nextAttemptUtc) {
                        $stateName = 'WaitingToRetry'
                        $detail = "Next attempt UTC: $($nextAttemptUtc.ToString('o'))"
                    }
                    else {
                        $stateName = 'InvokingDeployment'
                        $detail = "Deploying committed HEAD $($repositoryState.Head)."
                        Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                            state = $stateName; detail = $detail; updatedUtc = $nowUtc.ToString('o'); processId = $PID
                            workerScript = $workerScriptPath; canonicalProject = $projectPath; targetPath = $target
                            head = $repositoryState.Head; branch = $repositoryState.Branch; logPath = $logPath
                            watcherLockPath = $watchLockPath; globalBuildLockPath = $globalLockPath
                        })
                        Add-BuildLogLine $logPath "INVOKE_DEPLOY: head=$($repositoryState.Head)"
                        $powershell = Get-FamilyCompanyPowerShellHost
                        $arguments = @(
                            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $deployScriptPath,
                            '-CanonicalProjectPath', $projectPath, '-UnityEditorPath', $unityEditor,
                            '-TargetPath', $target, '-DeploymentRoot', $deploymentPath,
                            '-AutomationRoot', $automationPath, '-GlobalBuildLockPath', $globalLockPath,
                            '-RequiredBranch', $RequiredBranch, '-TestStatus', $TestStatus)
                        $previousErrorActionPreference = $ErrorActionPreference
                        $ErrorActionPreference = 'Continue'
                        try {
                            & $powershell @arguments 2>&1 | ForEach-Object { Add-BuildLogLine $logPath "DEPLOY: $_" }
                            $deployExitCode = $LASTEXITCODE
                        }
                        finally {
                            $ErrorActionPreference = $previousErrorActionPreference
                        }
                        if ($deployExitCode -eq 0) {
                            $pendingHead = $null
                            $nextAttemptUtc = [DateTime]::MinValue
                            $stateName = 'WatchingUpToDate'
                            $detail = 'Deployment completed or the target was already current.'
                        }
                        elseif ($deployExitCode -eq 34) {
                            $stateName = 'AwaitingPlayerExit'
                            $detail = 'FamilyCompany.exe is running; candidate is retained and will be promoted after exit.'
                            $nextAttemptUtc = [DateTime]::UtcNow.AddSeconds($PlayerRetrySeconds)
                        }
                        elseif ($deployExitCode -in @(31, 32, 35, 36)) {
                            $stateName = 'HeldByRepositoryState'
                            $detail = "Deployment exited with repository hold code $deployExitCode."
                            $pendingHead = $null
                        }
                        else {
                            $stateName = 'DeploymentFailed'
                            $detail = "Deployment exited with code $deployExitCode; retry delayed."
                            $nextAttemptUtc = [DateTime]::UtcNow.AddSeconds($FailureRetrySeconds)
                            $pendingHead = $null
                        }
                    }
                }
            }

            Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                state = $stateName; detail = $detail; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
                workerScript = $workerScriptPath; canonicalProject = $projectPath; targetPath = $target
                head = if ($null -eq $repositoryState) { $null } else { $repositoryState.Head }
                branch = if ($null -eq $repositoryState) { $null } else { $repositoryState.Branch }
                pendingSinceUtc = if ($pendingSinceUtc -eq [DateTime]::MinValue) { $null } else { $pendingSinceUtc.ToString('o') }
                nextAttemptUtc = if ($nextAttemptUtc -eq [DateTime]::MinValue) { $null } else { $nextAttemptUtc.ToString('o') }
                logPath = $logPath; watcherLockPath = $watchLockPath; globalBuildLockPath = $globalLockPath
            })
            if (-not [string]::Equals($lastLoggedState, $stateName, [StringComparison]::Ordinal)) {
                Add-BuildLogLine $logPath "STATE $stateName $detail"
                $lastLoggedState = $stateName
            }
        }
        catch {
            $stateName = 'WatchError'
            $detail = $_.Exception.Message
            Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
                state = $stateName; detail = $detail; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
                workerScript = $workerScriptPath; canonicalProject = $projectPath; logPath = $logPath
                watcherLockPath = $watchLockPath; globalBuildLockPath = $GlobalBuildLockPath
            })
            if (-not [string]::Equals($lastLoggedState, $stateName, [StringComparison]::Ordinal)) {
                Add-BuildLogLine $logPath "STATE $stateName $detail"
                $lastLoggedState = $stateName
            }
            $nextAttemptUtc = [DateTime]::UtcNow.AddSeconds($FailureRetrySeconds)
        }

        if ($MaximumIterations -gt 0 -and $iteration -ge $MaximumIterations) { break }
        Start-Sleep -Seconds $PollSeconds
    }
}
finally {
    Remove-Item -LiteralPath $stopRequestPath -Force -ErrorAction SilentlyContinue
    Write-JsonAtomically $watchStatusPath ([pscustomobject]@{
        state = 'Stopped'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        workerScript = $workerScriptPath; canonicalProject = $projectPath; logPath = $logPath
        watcherLockPath = $watchLockPath; globalBuildLockPath = $GlobalBuildLockPath
    })
    Add-BuildLogLine $logPath 'WATCH_STOP'
    if ($null -ne $watchLock) {
        $watchLock.Dispose()
        Remove-Item -LiteralPath $watchLockPath -Force -ErrorAction SilentlyContinue
    }
}
