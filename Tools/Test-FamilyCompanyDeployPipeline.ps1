[CmdletBinding()]
param(
    [string]$SandboxRoot = '',
    [string]$UnityEditorPath = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')

$defaults = Get-FamilyCompanyDeployDefaults
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) { $UnityEditorPath = $defaults.UnityEditorPath }
$koreanToken = ([string][char]0xD55C) + ([string][char]0xAE00)
$koreanArgument = "space $koreanToken value"
if ([string]::IsNullOrWhiteSpace($SandboxRoot)) {
    $SandboxRoot = Join-Path $script:FamilyCompanyProjectRoot (
        'Builds\Windows\Automation\DeployDryRunTest\run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + "-$PID")
}
$sandbox = Get-NormalizedFullPath $SandboxRoot
$project = Join-Path $sandbox "space project $koreanToken\Family Company integration"
$target = Join-Path $sandbox "fake Downloads $koreanToken\FamilyCompany_Playtest"
$deploymentRoot = Join-Path $sandbox "fake Downloads $koreanToken\.FamilyCompany_Playtest.deploy-staging"
$automationRoot = Join-Path $sandbox "automation logs $koreanToken"
$globalLockPath = Join-Path $sandbox "global lock $koreanToken\unity-build.lock"
$resultPath = Join-Path $sandbox 'DRY_RUN_TEST_RESULTS.json'
$deployScript = Join-Path $PSScriptRoot 'Deploy-FamilyCompanyWindows.ps1'
$watchScript = Join-Path $PSScriptRoot 'Watch-FamilyCompanyDeploy.ps1'
$runnerTemplate = Join-Path $PSScriptRoot 'DeployTemplates\RUN_WINDOWS.cmd'
$results = New-Object Collections.Generic.List[object]

function Add-TestResult {
    param([string]$Name, [string]$Evidence)
    $results.Add([pscustomobject]@{ name = $Name; status = 'PASS'; evidence = $Evidence })
}

function Invoke-FixtureGit {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git -c "safe.directory=$project" -C $project @Arguments 2>&1 | Out-Null
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousErrorActionPreference }
    if ($code -ne 0) { throw "Fixture git failed ($code): git $($Arguments -join ' ')" }
}

function New-FakePlayerBundle {
    param([string]$Path, [string]$Commit, [string]$Branch)
    New-Item -ItemType Directory -Path (Join-Path $Path 'FamilyCompany_Data') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $Path 'FamilyCompany.exe'), "fake-exe-$Commit")
    [IO.File]::WriteAllText((Join-Path $Path 'UnityPlayer.dll'), "fake-dll-$Commit")
    [IO.File]::WriteAllText((Join-Path $Path 'BUILD_INFO.txt'), "Commit: $Commit`r`nUnityVersion: 6000.3.21f1`r`n")
    $state = [pscustomobject]@{ Head = $Commit; Branch = $Branch }
    [void](Write-FamilyCompanyDeployManifest -BuildDirectory $Path -RepositoryState $state -BuildStartedUtc ([DateTime]::UtcNow.AddSeconds(-2)) -BuildCompletedUtc ([DateTime]::UtcNow -as [DateTime]) -UnityVersion '6000.3.21f1' -TestStatus 'DRY-RUN FIXTURE PASS')
    Install-FamilyCompanyDeployRunner $Path $runnerTemplate
}

New-Item -ItemType Directory -Path $project -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $project 'Assets\FamilyCompany') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $project 'ProjectSettings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $project 'Tools') -Force | Out-Null
[IO.File]::WriteAllText(
    (Join-Path $project 'ProjectSettings\ProjectVersion.txt'),
    "m_EditorVersion: 6000.3.21f1`r`nm_EditorVersionWithRevision: 6000.3.21f1 (c02631ffc030)`r`n")
[IO.File]::WriteAllText((Join-Path $project 'Tools\Build-FamilyCompanyWindows.ps1'), '# fixture marker')
[IO.File]::WriteAllText((Join-Path $project 'README fixture.txt'), 'base')

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & git -C $project init 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git init failed.' }
}
finally { $ErrorActionPreference = $previousErrorActionPreference }
Invoke-FixtureGit config user.email 'family-company-deploy-test@example.invalid'
Invoke-FixtureGit config user.name 'Family Company Deploy Test'
Invoke-FixtureGit checkout -b 'codex/integration-p0-qa'
Invoke-FixtureGit add --all
Invoke-FixtureGit commit -m 'fixture base'

$exactEditor = Assert-ExactUnityEditor $UnityEditorPath $project
Add-TestResult 'exact-unity-version' "ProductVersion 6000.3.21f1_c02631ffc030: $exactEditor"

$cleanState = Assert-FamilyCompanyDeployableHead $project 'codex/integration-p0-qa'
Add-TestResult 'clean-committed-head' "$($cleanState.Branch) $($cleanState.Head)"

$dirtyFilePath = Join-Path $project "dirty $koreanToken.tmp"
[IO.File]::WriteAllText($dirtyFilePath, 'uncommitted')
$powershell = Get-FamilyCompanyPowerShellHost
$dirtyArguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $deployScript,
    '-CanonicalProjectPath', $project, '-UnityEditorPath', $exactEditor,
    '-TargetPath', $target, '-DeploymentRoot', $deploymentRoot,
    '-AutomationRoot', $automationRoot, '-GlobalBuildLockPath', $globalLockPath,
    '-RequiredBranch', 'codex/integration-p0-qa', '-DryRun')
$ErrorActionPreference = 'Continue'
try {
    & $powershell @dirtyArguments 2>&1 | Out-Null
    $dirtyExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
if ($dirtyExitCode -ne 31) { throw "Dirty worktree returned $dirtyExitCode instead of 31." }
Add-TestResult 'dirty-worktree-exit-code' 'Deploy held with exit code 31.'
Remove-Item -LiteralPath $dirtyFilePath -Force

$dryRunArguments = $dirtyArguments
$ErrorActionPreference = 'Continue'
try {
    & $powershell @dryRunArguments 2>&1 | Out-Null
    $dryRunExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
if ($dryRunExitCode -ne 0) { throw "Clean dry-run exited with $dryRunExitCode." }
if (Test-Path -LiteralPath $target) { throw 'Dry-run created or changed the isolated deployment target.' }
if (Test-Path -LiteralPath $deploymentRoot) { throw 'Dry-run created the isolated candidate staging root.' }
$dryStatus = Read-JsonIfPresent (Join-Path $automationRoot 'deploy-status.json')
if ($null -eq $dryStatus -or $dryStatus.state -ne 'DryRunPassed' -or $dryStatus.downloadsModified -ne $false) {
    throw 'Dry-run status did not prove that the target remained untouched.'
}
Add-TestResult 'isolated-dry-run' 'Exit 0; target and staging absent; downloadsModified=false.'

$unchangedTarget = Join-Path $sandbox "fake Downloads $koreanToken\already current build"
New-FakePlayerBundle $unchangedTarget $cleanState.Head $cleanState.Branch
$unchangedAutomation = Join-Path $sandbox "already current automation $koreanToken"
$unchangedArguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $deployScript,
    '-CanonicalProjectPath', $project, '-UnityEditorPath', $exactEditor,
    '-TargetPath', $unchangedTarget, '-DeploymentRoot', (Join-Path $sandbox "already current staging $koreanToken"),
    '-AutomationRoot', $unchangedAutomation, '-GlobalBuildLockPath', $globalLockPath,
    '-RequiredBranch', 'codex/integration-p0-qa', '-DryRun')
$ErrorActionPreference = 'Continue'
try {
    & $powershell @unchangedArguments 2>&1 | Out-Null
    $unchangedExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
$unchangedStatus = Read-JsonIfPresent (Join-Path $unchangedAutomation 'deploy-status.json')
if ($unchangedExitCode -ne 0 -or $null -eq $unchangedStatus -or $unchangedStatus.state -ne 'SkippedUnchanged') {
    throw 'Already deployed committed HEAD was not skipped.'
}
Add-TestResult 'unchanged-head-skip' 'Matching deployed commit skipped without candidate creation.'

$debounceAutomation = Join-Path $sandbox "debounce watcher automation $koreanToken"
$debounceArguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $watchScript,
    '-CanonicalProjectPath', $project, '-UnityEditorPath', $exactEditor,
    '-TargetPath', (Join-Path $sandbox "debounce target $koreanToken"),
    '-DeploymentRoot', (Join-Path $sandbox 'debounce staging'),
    '-AutomationRoot', $debounceAutomation, '-GlobalBuildLockPath', $globalLockPath,
    '-RequiredBranch', 'codex/integration-p0-qa', '-PollSeconds', 1,
    '-DebounceSeconds', 60, '-MaximumIterations', 1)
$ErrorActionPreference = 'Continue'
try {
    & $powershell @debounceArguments 2>&1 | Out-Null
    $debounceExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
$debounceLogText = @(Get-Content -LiteralPath (Get-ChildItem -LiteralPath (Join-Path $debounceAutomation 'logs') -Filter 'watch-*.log' | Select-Object -First 1).FullName) -join "`n"
if ($debounceExitCode -ne 0 -or $debounceLogText -notmatch 'STATE DebouncingCommittedHead') {
    throw 'Watcher did not hold a new clean HEAD for debounce.'
}
if (Test-Path -LiteralPath (Join-Path $sandbox 'debounce staging')) { throw 'Debounce unexpectedly created a build candidate.' }
Add-TestResult 'committed-head-debounce' 'New clean HEAD held for debounce; no candidate/build invocation.'

Invoke-FixtureGit checkout -b 'conflict-source'
[IO.File]::WriteAllText((Join-Path $project 'README fixture.txt'), 'source')
Invoke-FixtureGit add --all
Invoke-FixtureGit commit -m 'fixture conflict source'
Invoke-FixtureGit checkout 'codex/integration-p0-qa'
[IO.File]::WriteAllText((Join-Path $project 'README fixture.txt'), 'integration')
Invoke-FixtureGit add --all
Invoke-FixtureGit commit -m 'fixture conflict integration'
$ErrorActionPreference = 'Continue'
try {
    & git -c "safe.directory=$project" -C $project merge 'conflict-source' 2>&1 | Out-Null
    $mergeExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
if ($mergeExitCode -eq 0) { throw 'Fixture merge unexpectedly did not conflict.' }
$conflictState = Get-FamilyCompanyRepositoryState $project
if (-not $conflictState.HasConflicts) { throw 'Unmerged fixture was not classified as a conflict.' }
try { [void](Assert-FamilyCompanyDeployableHead $project 'codex/integration-p0-qa'); throw 'Conflict was not rejected.' }
catch {
    if (-not $_.Exception.Message.StartsWith('MERGE_CONFLICT:', [StringComparison]::Ordinal)) { throw }
}
Add-TestResult 'merge-conflict-hold' 'Unmerged path classified before ordinary dirty state.'
$conflictAutomation = Join-Path $sandbox "conflict automation $koreanToken"
$conflictArguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $deployScript,
    '-CanonicalProjectPath', $project, '-UnityEditorPath', $exactEditor,
    '-TargetPath', (Join-Path $sandbox "conflict target $koreanToken"), '-DeploymentRoot', (Join-Path $sandbox "conflict staging $koreanToken"),
    '-AutomationRoot', $conflictAutomation, '-GlobalBuildLockPath', $globalLockPath,
    '-RequiredBranch', 'codex/integration-p0-qa', '-DryRun')
$ErrorActionPreference = 'Continue'
try {
    & $powershell @conflictArguments 2>&1 | Out-Null
    $conflictExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
if ($conflictExitCode -ne 32) { throw "Merge conflict returned $conflictExitCode instead of 32." }
Add-TestResult 'merge-conflict-exit-code' 'Deploy held with exit code 32.'
Invoke-FixtureGit merge --abort

$duplicateAutomation = Join-Path $sandbox "duplicate watcher automation $koreanToken"
$duplicateLockPath = Join-Path $duplicateAutomation 'watcher.lock'
$heldWatcherLock = Open-ExclusiveLock $duplicateLockPath
if ($null -eq $heldWatcherLock) { throw 'Could not establish duplicate watcher fixture lock.' }
try {
    $duplicateArguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $watchScript,
        '-AutomationRoot', $duplicateAutomation, '-MaximumIterations', 1)
    $ErrorActionPreference = 'Continue'
    try {
        & $powershell @duplicateArguments 2>&1 | Out-Null
        $duplicateExitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = 'Stop' }
    if ($duplicateExitCode -ne 24) { throw "Duplicate watcher returned $duplicateExitCode instead of 24." }
}
finally {
    $heldWatcherLock.Dispose()
    Remove-Item -LiteralPath $duplicateLockPath -Force -ErrorAction SilentlyContinue
}
Add-TestResult 'duplicate-watcher-lock' 'Second watcher rejected with exit code 24.'

$cmdDirectory = Join-Path $sandbox "CMD space $koreanToken path"
New-Item -ItemType Directory -Path $cmdDirectory -Force | Out-Null
$cmdProbe = Join-Path $cmdDirectory "argument probe $koreanToken.cmd"
$cmdProbeText = "@echo off`r`n" + 'if not "%~1"=="' + $koreanArgument + '" exit /b 9' + "`r`nexit /b 7`r`n"
[IO.File]::WriteAllText($cmdProbe, $cmdProbeText, (New-Object Text.UTF8Encoding($false)))
$ErrorActionPreference = 'Continue'
try {
    & $env:ComSpec /d /c "call `"$cmdProbe`" `"$koreanArgument`"" 2>&1 | Out-Null
    $cmdExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = 'Stop' }
if ($cmdExitCode -ne 7) { throw "CMD quoting/error propagation returned $cmdExitCode instead of 7." }
Add-TestResult 'cmd-space-korean-error-code' 'Quoted Korean path/argument preserved; exit code 7 propagated.'

$oldCommit = '1111111111111111111111111111111111111111'
$newCommit = '2222222222222222222222222222222222222222'
$branch = 'codex/integration-p0-qa'
New-FakePlayerBundle $target $oldCommit $branch
$partialCandidate = Join-Path $deploymentRoot 'candidate-partial-copy'
New-Item -ItemType Directory -Path (Join-Path $partialCandidate 'FamilyCompany_Data') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $partialCandidate 'FamilyCompany.exe'), 'partial')
try {
    [void](Publish-FamilyCompanyDeployCandidate -CandidatePath $partialCandidate -TargetPath $target -ExpectedCommit $newCommit -ExpectedBranch $branch -ExpectedUnityVersion '6000.3.21f1')
    throw 'Incomplete candidate was unexpectedly published.'
}
catch {
    if (-not $_.Exception.Message.Contains('required output is missing')) { throw }
}
if ((Get-FamilyCompanyDeployedCommit $target) -ne $oldCommit) { throw 'Incomplete candidate validation changed the active build.' }
Add-TestResult 'partial-copy-rejected' 'Missing DLL/manifests rejected before active target mutation.'
$candidate = Join-Path $deploymentRoot 'candidate-new'
New-FakePlayerBundle $candidate $newCommit $branch
try {
    [void](Publish-FamilyCompanyDeployCandidate -CandidatePath $candidate -TargetPath $target -ExpectedCommit $newCommit -ExpectedBranch $branch -ExpectedUnityVersion '6000.3.21f1' -TestFailureAfterBackup)
    throw 'Injected promotion failure did not throw.'
}
catch {
    if (-not $_.Exception.Message.Contains('TEST_INJECTED_PROMOTION_FAILURE')) { throw }
}
if ((Get-FamilyCompanyDeployedCommit $target) -ne $oldCommit) { throw 'Promotion failure did not restore the active old build.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { throw 'Promotion failure destroyed the validated candidate.' }
Add-TestResult 'partial-promotion-rollback' 'Injected failure restored active old commit and retained candidate.'

try {
    [void](Publish-FamilyCompanyDeployCandidate -CandidatePath $candidate -TargetPath $target -ExpectedCommit $newCommit -ExpectedBranch $branch -ExpectedUnityVersion '6000.3.21f1' -TestFailureAfterCandidateMove)
    throw 'Injected post-move validation failure did not throw.'
}
catch {
    if (-not $_.Exception.Message.Contains('TEST_INJECTED_POST_MOVE_FAILURE')) { throw }
}
if ((Get-FamilyCompanyDeployedCommit $target) -ne $oldCommit) { throw 'Post-move failure did not restore the active old build.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { throw 'Post-move failure did not restore the reusable candidate.' }
Add-TestResult 'post-move-validation-rollback' 'Failure after target rename restored both old active build and reusable candidate.'

$promotion = Publish-FamilyCompanyDeployCandidate -CandidatePath $candidate -TargetPath $target -ExpectedCommit $newCommit -ExpectedBranch $branch -ExpectedUnityVersion '6000.3.21f1'
if ((Get-FamilyCompanyDeployedCommit $target) -ne $newCommit) { throw 'Successful promotion did not activate the new commit.' }
$lkgFolders = @(Get-ChildItem -LiteralPath (Split-Path -Parent $target) -Directory -Filter 'FamilyCompany_Playtest.last-known-good.*')
if ($lkgFolders.Count -ne 1) { throw "Expected exactly one LKG folder, found $($lkgFolders.Count)." }
if ((Get-FamilyCompanyDeployedCommit $lkgFolders[0].FullName) -ne $oldCommit) { throw 'LKG folder does not contain the prior commit.' }
Add-TestResult 'atomic-promotion-lkg' "Active=$newCommit; LKG=$oldCommit; count=1."

$runningRoot = Join-Path $sandbox "running process detection $koreanToken"
New-Item -ItemType Directory -Path $runningRoot -Force | Out-Null
$fakeRunningExe = Join-Path $runningRoot 'FamilyCompany.exe'
Copy-Item -LiteralPath $env:ComSpec -Destination $fakeRunningExe
$ownedProcess = Start-Process -FilePath $fakeRunningExe -ArgumentList '/d', '/c', 'ping -n 30 127.0.0.1 > nul' -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Milliseconds 300
    if (-not (Test-FamilyCompanyPlayerRunning $fakeRunningExe)) {
        throw 'The exact running FamilyCompany.exe path was not detected.'
    }
    $blocking = @(Get-FamilyCompanyBlockingBuildProcesses)
    if (-not ($blocking | Where-Object { $_.ProcessId -eq $ownedProcess.Id })) {
        throw 'Running QA player was not classified as a build-blocking process.'
    }
    $ignored = @(Get-FamilyCompanyBlockingBuildProcesses $fakeRunningExe)
    if ($ignored | Where-Object { $_.ProcessId -eq $ownedProcess.Id }) {
        throw 'The explicitly allowed active deployment target was not excluded from the build-idle gate.'
    }
}
finally {
    if (-not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -Force }
}
Add-TestResult 'running-player-deployment-hold' 'Exact target detected; other QA blocks builds while explicitly allowed active target waits at promotion.'

$summary = [pscustomobject]@{
    status = 'PASS'
    completedUtc = [DateTime]::UtcNow.ToString('o')
    sandboxRoot = $sandbox
    downloadsTouched = $false
    unityLaunched = $false
    cases = $results
}
Write-JsonAtomically $resultPath $summary
Write-Output "FAMILY_COMPANY_DEPLOY_PIPELINE_DRY_RUN: PASS cases=$($results.Count) downloadsTouched=false unityLaunched=false"
foreach ($result in $results) { Write-Output "PASS $($result.name): $($result.evidence)" }
Write-Output "Results: $resultPath"
