[CmdletBinding()]
param(
    [string]$CanonicalProjectPath = '',
    [string]$UnityEditorPath = '',
    [string]$DeploymentRoot = '',
    [string]$FinalOutputPath = '',
    [string]$StagingRoot = '',
    [string]$AutomationRoot = '',
    [int]$UnityWaitTimeoutMinutes = 120,
    [int]$UnityRetrySeconds = 15,
    [int]$MaximumLogFiles = 30,
    [switch]$DryRun
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$defaults = Get-FamilyCompanyBuildDefaults
if ([string]::IsNullOrWhiteSpace($CanonicalProjectPath)) { $CanonicalProjectPath = $defaults.CanonicalProjectPath }
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) { $UnityEditorPath = $defaults.UnityEditorPath }
if (-not [string]::IsNullOrWhiteSpace($DeploymentRoot) -and
    -not [string]::IsNullOrWhiteSpace($FinalOutputPath)) {
    throw 'Use either DeploymentRoot or FinalOutputPath, not both.'
}
$projectPath = Assert-CanonicalProjectPath $CanonicalProjectPath
$unityEditor = Assert-ExactUnityEditor $UnityEditorPath $projectPath
$deploymentRootPath = ''
if (-not [string]::IsNullOrWhiteSpace($DeploymentRoot)) {
    $deploymentRootPath = Assert-SafeFamilyCompanyBuildPath $projectPath $DeploymentRoot 'Deployment root'
    $FinalOutputPath = Join-Path $deploymentRootPath 'FamilyCompany_Playtest'
    if ([string]::IsNullOrWhiteSpace($StagingRoot)) {
        $StagingRoot = Join-Path $deploymentRootPath 'FamilyCompany_BuildAutomation\staging'
    }
    if ([string]::IsNullOrWhiteSpace($AutomationRoot)) {
        $AutomationRoot = Join-Path $deploymentRootPath 'FamilyCompany_BuildAutomation'
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($FinalOutputPath)) { $FinalOutputPath = $defaults.FinalOutputPath }
    if ([string]::IsNullOrWhiteSpace($StagingRoot)) { $StagingRoot = $defaults.BuildRoot }
    if ([string]::IsNullOrWhiteSpace($AutomationRoot)) { $AutomationRoot = $defaults.AutomationRoot }
}
$finalPath = Assert-SafeFamilyCompanyBuildPath $projectPath $FinalOutputPath 'Final output path'
Assert-FamilyCompanyReplaceableBuildDirectory $finalPath $defaults.ExecutableName
$stagingRootPath = Assert-SafeFamilyCompanyBuildPath $projectPath $StagingRoot 'Staging root'
$automationPath = Get-NormalizedFullPath $AutomationRoot
$automationPath = Assert-SafeFamilyCompanyBuildPath $projectPath $automationPath 'Automation root'
if ([string]::Equals($finalPath, $stagingRootPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Final output path and staging root must be different: $finalPath"
}
if ($UnityWaitTimeoutMinutes -lt 1) { throw 'UnityWaitTimeoutMinutes must be positive.' }
if ($UnityRetrySeconds -lt 2) { throw 'UnityRetrySeconds must be at least 2.' }
if ($MaximumLogFiles -lt 2) { throw 'MaximumLogFiles must be at least 2.' }

if ($DryRun) {
    Write-Output 'Family Company Windows build path validation: PASS'
    Write-Output "Project: $projectPath"
    Write-Output "Unity: $unityEditor"
    if (-not [string]::IsNullOrWhiteSpace($deploymentRootPath)) {
        Write-Output "DeploymentRoot: $deploymentRootPath"
    }
    Write-Output "StagingRoot: $stagingRootPath"
    Write-Output "Output: $finalPath"
    Write-Output "Automation: $automationPath"
    Write-Output 'Target: StandaloneWindows64 / D3D11'
    Write-Output 'ExecuteMethod: FamilyCompany.Editor.WindowsPlayerBuild.BuildWindowsX64'
    exit 0
}

$logDirectory = Join-Path $automationPath 'logs'
$statusPath = Join-Path $automationPath 'build-status.json'
$lockPath = Join-Path $automationPath 'build.lock'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$automationLogPath = Join-Path $logDirectory "build-$timestamp-automation.log"
$unityLogPath = Join-Path $logDirectory "build-$timestamp-unity.log"
$buildLock = Open-ExclusiveLock $lockPath
if ($null -eq $buildLock) {
    Add-BuildLogLine $automationLogPath 'SKIPPED: another Family Company build owns the exclusive build lock.'
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'SkippedLocked'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        automationLog = $automationLogPath; unityLog = $unityLogPath
    })
    exit 23
}

$stagingPath = $null
$backupPath = $null
$snapshot = $null
$startedUtc = [DateTime]::UtcNow
try {
    $snapshot = Get-CanonicalBuildSnapshot $projectPath
    Add-BuildLogLine $automationLogPath (
        "START head=$($snapshot.Head) branch=$($snapshot.Branch) dirty=$($snapshot.IsDirty) " +
        "fingerprint=$($snapshot.Fingerprint)")
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'WaitingForUnity'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        head = $snapshot.Head; branch = $snapshot.Branch; fingerprint = $snapshot.Fingerprint
        automationLog = $automationLogPath; unityLog = $unityLogPath
    })

    $waitDeadline = [DateTime]::UtcNow.AddMinutes($UnityWaitTimeoutMinutes)
    while (Test-AnyUnityEditorRunning) {
        if ([DateTime]::UtcNow -ge $waitDeadline) {
            throw "Timed out after $UnityWaitTimeoutMinutes minute(s) waiting for all Unity.exe processes to exit."
        }
        Add-BuildLogLine $automationLogPath "Unity.exe is already running; retrying in $UnityRetrySeconds second(s)."
        Start-Sleep -Seconds $UnityRetrySeconds
    }

    $stagingName = "FamilyCompany_Playtest.staging.$timestamp.$PID"
    New-Item -ItemType Directory -Path $stagingRootPath -Force | Out-Null
    $stagingPath = Join-Path $stagingRootPath $stagingName
    if (Test-Path -LiteralPath $stagingPath) {
        throw "Fresh staging path unexpectedly already exists: $stagingPath"
    }
    New-Item -ItemType Directory -Path $stagingPath | Out-Null
    $executablePath = Join-Path $stagingPath $defaults.ExecutableName

    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'Building'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        head = $snapshot.Head; branch = $snapshot.Branch; fingerprint = $snapshot.Fingerprint
        stagingPath = $stagingPath; automationLog = $automationLogPath; unityLog = $unityLogPath
    })
    Add-BuildLogLine $automationLogPath "Launching exact Unity editor: $unityEditor"
    $unityArguments = @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', ('"' + $projectPath + '"'),
        '-buildTarget', 'Win64',
        '-executeMethod', 'FamilyCompany.Editor.WindowsPlayerBuild.BuildWindowsX64',
        '-familyCompanyBuildOutput', ('"' + $executablePath + '"'),
        '-logFile', ('"' + $unityLogPath + '"'))
    $unityProcess = Start-Process `
        -FilePath $unityEditor `
        -ArgumentList $unityArguments `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    $unityExitCode = $unityProcess.ExitCode
    Add-BuildLogLine $automationLogPath "Unity exited with code $unityExitCode."
    if ($unityExitCode -ne 0) {
        throw "Unity Windows player build failed with exit code $unityExitCode. See $unityLogPath"
    }

    $requiredPaths = @(
        $executablePath,
        (Join-Path $stagingPath 'FamilyCompany_Data'),
        (Join-Path $stagingPath 'UnityPlayer.dll'))
    foreach ($requiredPath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Unity reported success but a required player output is missing: $requiredPath"
        }
    }

    $completedSnapshot = Get-CanonicalBuildSnapshot $projectPath
    if (-not [string]::Equals(
            $snapshot.Fingerprint,
            $completedSnapshot.Fingerprint,
            [StringComparison]::Ordinal)) {
        throw (
            'Canonical build inputs changed while Unity was building. ' +
            'The staged player will be discarded and the watcher will retry after debounce.')
    }

    $completedUtc = [DateTime]::UtcNow
    $buildInfo = @(
        'Family Company Windows x64 Playtest',
        "Commit: $($snapshot.Head)",
        "Branch: $($snapshot.Branch)",
        "WorkingTreeDirty: $($snapshot.IsDirty)",
        "BuildInputFingerprint: $($snapshot.Fingerprint)",
        "BuildStartedUtc: $($startedUtc.ToString('o'))",
        "BuildCompletedUtc: $($completedUtc.ToString('o'))",
        "UnityVersion: $($defaults.UnityVersion)",
        "UnityEditor: $unityEditor",
        'BuildTarget: StandaloneWindows64',
        'BuildType: Release (non-Development)',
        "FirstScene: $($defaults.FirstScene)",
        "CanonicalProject: $projectPath",
        "UnityLog: $unityLogPath") -join [Environment]::NewLine
    [IO.File]::WriteAllText(
        (Join-Path $stagingPath 'BUILD_INFO.txt'),
        $buildInfo + [Environment]::NewLine,
        (New-Object Text.UTF8Encoding($false)))

    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'Promoting'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        head = $snapshot.Head; branch = $snapshot.Branch; fingerprint = $snapshot.Fingerprint
        stagingPath = $stagingPath; finalPath = $finalPath
        automationLog = $automationLogPath; unityLog = $unityLogPath
    })

    $finalParent = Split-Path -Parent $finalPath
    New-Item -ItemType Directory -Path $finalParent -Force | Out-Null
    $backupPath = "$finalPath.previous.$timestamp.$PID"
    if (Test-Path -LiteralPath $backupPath) {
        throw "Promotion backup path unexpectedly exists: $backupPath"
    }
    if (Test-Path -LiteralPath $finalPath) {
        Move-Item -LiteralPath $finalPath -Destination $backupPath
        Add-BuildLogLine $automationLogPath "Moved the previous successful build to $backupPath"
    }
    try {
        Move-Item -LiteralPath $stagingPath -Destination $finalPath
        $stagingPath = $null
    }
    catch {
        if ((Test-Path -LiteralPath $backupPath) -and -not (Test-Path -LiteralPath $finalPath)) {
            Move-Item -LiteralPath $backupPath -Destination $finalPath
            $backupPath = $null
            Add-BuildLogLine $automationLogPath 'Promotion failed; restored the previous successful build.'
        }
        throw
    }

    if (-not (Test-Path -LiteralPath (Join-Path $finalPath $defaults.ExecutableName) -PathType Leaf)) {
        Remove-Item -LiteralPath $finalPath -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $backupPath) {
            Move-Item -LiteralPath $backupPath -Destination $finalPath
            $backupPath = $null
        }
        throw "Promotion verification failed; the failed output was removed and any previous build was restored: $finalPath"
    }
    if (Test-Path -LiteralPath $backupPath) {
        try {
            Remove-Item -LiteralPath $backupPath -Recurse -Force
            $backupPath = $null
        }
        catch {
            Add-BuildLogLine $automationLogPath "WARNING: could not remove promotion backup '$backupPath': $($_.Exception.Message)"
        }
    }

    Add-BuildLogLine $automationLogPath "PASS promoted $finalPath"
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'Succeeded'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        head = $snapshot.Head; branch = $snapshot.Branch; fingerprint = $snapshot.Fingerprint
        lastSuccessfulFingerprint = $snapshot.Fingerprint; finalPath = $finalPath
        automationLog = $automationLogPath; unityLog = $unityLogPath
    })
    try {
        Rotate-FamilyCompanyLogs $logDirectory $MaximumLogFiles
    }
    catch {
        Add-BuildLogLine $automationLogPath "WARNING: log rotation failed: $($_.Exception.Message)"
    }
    exit 0
}
catch {
    $message = $_.Exception.Message
    Add-BuildLogLine $automationLogPath "FAIL $message"
    Write-JsonAtomically $statusPath ([pscustomobject]@{
        state = 'Failed'; updatedUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
        head = if ($null -eq $snapshot) { $null } else { $snapshot.Head }
        branch = if ($null -eq $snapshot) { $null } else { $snapshot.Branch }
        fingerprint = if ($null -eq $snapshot) { $null } else { $snapshot.Fingerprint }
        error = $message; finalPath = $finalPath
        automationLog = $automationLogPath; unityLog = $unityLogPath
    })
    if ($null -ne $stagingPath -and (Test-Path -LiteralPath $stagingPath)) {
        try {
            Remove-Item -LiteralPath $stagingPath -Recurse -Force
        }
        catch {
            Add-BuildLogLine $automationLogPath "WARNING: could not remove failed staging output '$stagingPath': $($_.Exception.Message)"
        }
    }
    if ($null -ne $backupPath -and (Test-Path -LiteralPath $backupPath) -and -not (Test-Path -LiteralPath $finalPath)) {
        try {
            Move-Item -LiteralPath $backupPath -Destination $finalPath
            $backupPath = $null
            Add-BuildLogLine $automationLogPath 'Restored the previous successful build while handling the failure.'
        }
        catch {
            Add-BuildLogLine $automationLogPath "ERROR: automatic restore failed; the previous build remains at '$backupPath': $($_.Exception.Message)"
        }
    }
    try {
        Rotate-FamilyCompanyLogs $logDirectory $MaximumLogFiles
    }
    catch {
        Add-BuildLogLine $automationLogPath "WARNING: log rotation failed while handling the build failure: $($_.Exception.Message)"
    }
    Write-Error $message
    exit 1
}
finally {
    if ($null -ne $buildLock) {
        $buildLock.Dispose()
        Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    }
}
