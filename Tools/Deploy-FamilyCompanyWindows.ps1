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
    [int]$UnityWaitTimeoutMinutes = 120,
    [int]$UnityRetrySeconds = 15,
    [switch]$DryRun
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$defaults = Get-FamilyCompanyDeployDefaults
if ([string]::IsNullOrWhiteSpace($CanonicalProjectPath)) { $CanonicalProjectPath = $defaults.CanonicalProjectPath }
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) { $UnityEditorPath = $defaults.UnityEditorPath }
if ([string]::IsNullOrWhiteSpace($TargetPath)) { $TargetPath = $defaults.TargetPath }
if ([string]::IsNullOrWhiteSpace($DeploymentRoot)) { $DeploymentRoot = $defaults.DeploymentRoot }
if ([string]::IsNullOrWhiteSpace($AutomationRoot)) { $AutomationRoot = $defaults.AutomationRoot }
if ([string]::IsNullOrWhiteSpace($GlobalBuildLockPath)) { $GlobalBuildLockPath = $defaults.GlobalBuildLockPath }

$projectPath = $null
$unityEditor = $null
$target = Get-NormalizedFullPath $TargetPath
$deploymentPath = Get-NormalizedFullPath $DeploymentRoot
$automationPath = Get-NormalizedFullPath $AutomationRoot
$globalLockPath = Get-NormalizedFullPath $GlobalBuildLockPath
$statusPath = Join-Path $automationPath 'deploy-status.json'
$logDirectory = Join-Path $automationPath 'logs'
$deployLockPath = Join-Path $automationPath 'deploy.lock'
$buildScriptPath = Join-Path $PSScriptRoot 'Build-FamilyCompanyWindows.ps1'
$runnerTemplatePath = Join-Path $PSScriptRoot 'DeployTemplates\RUN_WINDOWS.cmd'
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$logPath = Join-Path $logDirectory "deploy-$timestamp-$PID.log"

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$deployLock = Open-ExclusiveLock $deployLockPath
if ($null -eq $deployLock) {
    Add-BuildLogLine $logPath 'SKIPPED_LOCKED: another deployment process owns the deployment lock.'
    Write-Output "Deployment skipped: lock is owned by another process: $deployLockPath"
    exit 33
}

$candidatePath = $null
$preserveCandidate = $false
$repositoryState = $null
$buildStartedUtc = $null
try {
    $projectPath = Assert-CanonicalProjectPath $CanonicalProjectPath
    $unityEditor = Assert-ExactUnityEditor $UnityEditorPath $projectPath
    $repositoryState = Assert-FamilyCompanyDeployableHead $projectPath $RequiredBranch
    $deployedCommit = Get-FamilyCompanyDeployedCommit $target
    Add-BuildLogLine $logPath (
        "START branch=$($repositoryState.Branch) head=$($repositoryState.Head) " +
        "deployed=$deployedCommit dryRun=$DryRun target=$target globalBuildLock=$globalLockPath")

    if ([string]::Equals($deployedCommit, $repositoryState.Head, [StringComparison]::Ordinal)) {
        Write-JsonAtomically $statusPath ([pscustomobject]@{
            state = 'SkippedUnchanged'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
            branch = $repositoryState.Branch; head = $repositoryState.Head; targetPath = $target
            logPath = $logPath; deployLockPath = $deployLockPath; globalBuildLockPath = $globalLockPath
        })
        Add-BuildLogLine $logPath 'SKIPPED_UNCHANGED: the committed HEAD is already deployed.'
        Write-Output "No deployment needed; HEAD $($repositoryState.Head) is already deployed."
        exit 0
    }

    if ($DryRun) {
        Write-JsonAtomically $statusPath ([pscustomobject]@{
            state = 'DryRunPassed'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
            branch = $repositoryState.Branch; head = $repositoryState.Head; targetPath = $target
            deploymentRoot = $deploymentPath; unityEditor = $unityEditor; unityVersion = $defaults.UnityVersion
            testStatus = $TestStatus; logPath = $logPath; deployLockPath = $deployLockPath
            globalBuildLockPath = $globalLockPath; downloadsModified = $false
        })
        Add-BuildLogLine $logPath 'DRY_RUN_PASS: clean committed HEAD, exact Unity and deployment paths validated; no target files changed.'
        Write-Output 'DRY RUN PASS: no Windows player was built and the deployment target was not modified.'
        Write-Output "Source: $($repositoryState.Branch) $($repositoryState.Head)"
        Write-Output "Unity: $unityEditor ($($defaults.UnityVersion))"
        Write-Output "Target: $target"
        Write-Output "Deploy lock: $deployLockPath"
        Write-Output "Global Unity/build lock: $globalLockPath"
        Write-Output "Log: $logPath"
        exit 0
    }

    New-Item -ItemType Directory -Path $deploymentPath -Force | Out-Null
    $previousStatus = Read-JsonIfPresent $statusPath
    if ($null -ne $previousStatus -and
        $previousStatus.PSObject.Properties.Name -contains 'pendingCandidatePath') {
        $previousCandidate = [string]$previousStatus.pendingCandidatePath
        $previousHead = if ($previousStatus.PSObject.Properties.Name -contains 'head') { [string]$previousStatus.head } else { '' }
        if (-not [string]::IsNullOrWhiteSpace($previousCandidate) -and
            (Test-Path -LiteralPath $previousCandidate -PathType Container) -and
            [string]::Equals($previousHead, $repositoryState.Head, [StringComparison]::Ordinal)) {
            try {
                [void](Assert-FamilyCompanyDeployCandidate -BuildDirectory $previousCandidate -ExpectedCommit $repositoryState.Head -ExpectedBranch $repositoryState.Branch -ExpectedUnityVersion $defaults.UnityVersion)
                $candidatePath = Get-NormalizedFullPath $previousCandidate
                Add-BuildLogLine $logPath "REUSE_PENDING_CANDIDATE: $candidatePath"
            }
            catch {
                Add-BuildLogLine $logPath "DISCARD_INVALID_PENDING_CANDIDATE: $($_.Exception.Message)"
            }
        }
        if ($null -eq $candidatePath -and
            -not [string]::IsNullOrWhiteSpace($previousCandidate) -and
            (Test-PathDescendsFrom $previousCandidate $deploymentPath) -and
            (Test-Path -LiteralPath $previousCandidate)) {
            Remove-Item -LiteralPath $previousCandidate -Recurse -Force
        }
    }

    if ($null -eq $candidatePath) {
        $shortHead = $repositoryState.Head.Substring(0, [Math]::Min(12, $repositoryState.Head.Length))
        $candidatePath = Join-Path $deploymentPath "candidate.$shortHead.$timestamp.$PID"
        $buildAutomationRoot = Join-Path $automationPath 'candidate-build'
        $buildStartedUtc = [DateTime]::UtcNow
        Write-JsonAtomically $statusPath ([pscustomobject]@{
            state = 'BuildingCandidate'; updatedUtc = $buildStartedUtc.ToString('o'); processId = $PID
            branch = $repositoryState.Branch; head = $repositoryState.Head; targetPath = $target
            pendingCandidatePath = $candidatePath; logPath = $logPath
            deployLockPath = $deployLockPath; globalBuildLockPath = $globalLockPath
        })
        Add-BuildLogLine $logPath "BUILD_CANDIDATE: $candidatePath"
        $powershell = Get-FamilyCompanyPowerShellHost
        $arguments = @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $buildScriptPath,
            '-CanonicalProjectPath', $projectPath,
            '-UnityEditorPath', $unityEditor,
            '-FinalOutputPath', $candidatePath,
            '-AutomationRoot', $buildAutomationRoot,
            '-StagingRoot', $deploymentPath,
            '-GlobalBuildLockPath', $globalLockPath,
            '-ExpectedHead', $repositoryState.Head,
            '-IgnoredPlayerExecutablePath', (Join-Path $target $defaults.ExecutableName),
            '-RequireClean', '-AllowCustomOutput',
            '-UnityWaitTimeoutMinutes', $UnityWaitTimeoutMinutes,
            '-UnityRetrySeconds', $UnityRetrySeconds)
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & $powershell @arguments 2>&1 | ForEach-Object { Add-BuildLogLine $logPath "BUILD: $_" }
            $buildExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        if ($buildExitCode -ne 0) {
            throw "BUILD_FAILED: candidate build exited with code $buildExitCode."
        }

        $completedState = Assert-FamilyCompanyDeployableHead $projectPath $RequiredBranch $repositoryState.Head
        $buildCompletedUtc = [DateTime]::UtcNow
        [void](Write-FamilyCompanyDeployManifest -BuildDirectory $candidatePath -RepositoryState $completedState -BuildStartedUtc $buildStartedUtc -BuildCompletedUtc $buildCompletedUtc -UnityVersion $defaults.UnityVersion -TestStatus $TestStatus)
        Install-FamilyCompanyDeployRunner $candidatePath $runnerTemplatePath
        [void](Assert-FamilyCompanyDeployCandidate -BuildDirectory $candidatePath -ExpectedCommit $repositoryState.Head -ExpectedBranch $repositoryState.Branch -ExpectedUnityVersion $defaults.UnityVersion)
        Add-BuildLogLine $logPath 'CANDIDATE_VALIDATED: EXE, Data, UnityPlayer.dll, build info, manifests and runner are complete.'
    }

    $preserveCandidate = $true
    $targetExecutable = Join-Path $target $defaults.ExecutableName
    if (Test-FamilyCompanyPlayerRunning $targetExecutable) {
        Write-JsonAtomically $statusPath ([pscustomobject]@{
            state = 'AwaitingPlayerExit'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
            branch = $repositoryState.Branch; head = $repositoryState.Head; targetPath = $target
            pendingCandidatePath = $candidatePath; testStatus = $TestStatus; logPath = $logPath
            deployLockPath = $deployLockPath; globalBuildLockPath = $globalLockPath
        })
        Add-BuildLogLine $logPath 'DEPLOYMENT_PENDING: target FamilyCompany.exe is running; it was not terminated.'
        Write-Output 'Deployment pending: FamilyCompany.exe is running and was not terminated.'
        Write-Output "Candidate retained: $candidatePath"
        exit 34
    }

    [void](Assert-FamilyCompanyDeployableHead $projectPath $RequiredBranch $repositoryState.Head)
    $promotion = Publish-FamilyCompanyDeployCandidate -CandidatePath $candidatePath -TargetPath $target -ExpectedCommit $repositoryState.Head -ExpectedBranch $repositoryState.Branch -ExpectedUnityVersion $defaults.UnityVersion
    $candidatePath = $null
    $preserveCandidate = $false
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'Succeeded'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        branch = $repositoryState.Branch; head = $repositoryState.Head; targetPath = $promotion.TargetPath
        lastKnownGoodPath = $promotion.LastKnownGoodPath; testStatus = $TestStatus; logPath = $logPath
        deployLockPath = $deployLockPath; globalBuildLockPath = $globalLockPath
    })
    Add-BuildLogLine $logPath "PASS: deployed $($promotion.TargetPath); lastKnownGood=$($promotion.LastKnownGoodPath)"
    Write-Output "Deployment succeeded: $($promotion.TargetPath)"
    if ($null -ne $promotion.LastKnownGoodPath) {
        Write-Output "Last-known-good: $($promotion.LastKnownGoodPath)"
    }
    exit 0
}
catch {
    $message = $_.Exception.Message
    $exitCode = 1
    $stateName = 'Failed'
    if ($message.StartsWith('MERGE_CONFLICT:', [StringComparison]::Ordinal)) { $exitCode = 32; $stateName = 'HeldMergeConflict' }
    elseif ($message.StartsWith('DIRTY_WORKTREE:', [StringComparison]::Ordinal)) { $exitCode = 31; $stateName = 'HeldDirtyWorktree' }
    elseif ($message.StartsWith('WRONG_BRANCH:', [StringComparison]::Ordinal)) { $exitCode = 35; $stateName = 'HeldWrongBranch' }
    elseif ($message.StartsWith('HEAD_CHANGED:', [StringComparison]::Ordinal)) { $exitCode = 36; $stateName = 'HeldHeadChanged' }
    Add-BuildLogLine $logPath "${stateName}: $message"
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = $stateName; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        branch = if ($null -eq $repositoryState) { $null } else { $repositoryState.Branch }
        head = if ($null -eq $repositoryState) { $null } else { $repositoryState.Head }
        targetPath = $target; pendingCandidatePath = if ($preserveCandidate) { $candidatePath } else { $null }
        error = $message; logPath = $logPath; deployLockPath = $deployLockPath
        globalBuildLockPath = $globalLockPath
    })
    if (-not $preserveCandidate -and $null -ne $candidatePath -and
        (Test-PathDescendsFrom $candidatePath $deploymentPath) -and
        (Test-Path -LiteralPath $candidatePath)) {
        try { Remove-Item -LiteralPath $candidatePath -Recurse -Force }
        catch { Add-BuildLogLine $logPath "WARNING: failed candidate cleanup: $($_.Exception.Message)" }
    }
    Write-Error $message -ErrorAction Continue
    exit $exitCode
}
finally {
    if ($null -ne $deployLock) {
        $deployLock.Dispose()
        Remove-Item -LiteralPath $deployLockPath -Force -ErrorAction SilentlyContinue
    }
}
