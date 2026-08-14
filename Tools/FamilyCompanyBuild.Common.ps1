Set-StrictMode -Version 2.0

$script:FamilyCompanyToolsRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd([char]'\', [char]'/')
$script:FamilyCompanyProjectRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent $PSScriptRoot)).TrimEnd([char]'\', [char]'/')
$script:FamilyCompanyUnityVersion = '6000.3.21f1'

function Find-FamilyCompanyUnityEditor {
    [CmdletBinding()]
    param()

    $candidates = New-Object Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:FAMILY_COMPANY_UNITY_EDITOR)) {
        $candidates.Add($env:FAMILY_COMPANY_UNITY_EDITOR)
    }
    $searchRoot = $script:FamilyCompanyProjectRoot
    for ($depth = 0; $depth -lt 5 -and -not [string]::IsNullOrWhiteSpace($searchRoot); $depth++) {
        $candidates.Add((Join-Path $searchRoot "UnityEditors\$script:FamilyCompanyUnityVersion\Editor\Unity.exe"))
        $parent = Split-Path -Parent $searchRoot
        if ([string]::Equals($parent, $searchRoot, [StringComparison]::OrdinalIgnoreCase)) { break }
        $searchRoot = $parent
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$script:FamilyCompanyUnityVersion\Editor\Unity.exe"))
    }
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$script:FamilyCompanyUnityVersion\Editor\Unity.exe"))
    }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return Get-NormalizedFullPath $candidate
        }
    }
    return ''
}

function Get-FamilyCompanyBuildDefaults {
    [CmdletBinding()]
    param()

    $buildRoot = Join-Path $script:FamilyCompanyProjectRoot 'Builds\Windows'
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrWhiteSpace($localAppData)) {
        $localAppData = Join-Path $buildRoot 'Automation'
    }
    [pscustomobject]@{
        CanonicalProjectPath = $script:FamilyCompanyProjectRoot
        UnityEditorPath      = Find-FamilyCompanyUnityEditor
        UnityVersion         = $script:FamilyCompanyUnityVersion
        BuildRoot            = $buildRoot
        FinalOutputPath      = Join-Path $buildRoot 'FamilyCompany_Playtest'
        AutomationRoot       = Join-Path $buildRoot 'Automation'
        ExecutableName       = 'FamilyCompany.exe'
        FirstScene           = 'Assets/FamilyCompany/Scenes/Prototype01.unity'
        GlobalBuildLockPath  = Join-Path $localAppData 'FamilyCompany\BuildAutomation\unity-build.lock'
    }
}

function Get-FamilyCompanyDeployDefaults {
    [CmdletBinding()]
    param()

    $buildDefaults = Get-FamilyCompanyBuildDefaults
    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if ([string]::IsNullOrWhiteSpace($userProfile)) { throw 'Could not resolve the Windows user profile.' }
    $downloads = Join-Path $userProfile 'Downloads'
    [pscustomobject]@{
        CanonicalProjectPath = $buildDefaults.CanonicalProjectPath
        UnityEditorPath      = $buildDefaults.UnityEditorPath
        UnityVersion         = $buildDefaults.UnityVersion
        TargetPath           = Join-Path $downloads 'FamilyCompany_Playtest'
        DeploymentRoot       = Join-Path $downloads '.FamilyCompany_Playtest.deploy-staging'
        AutomationRoot       = Join-Path $buildDefaults.AutomationRoot 'Deploy'
        GlobalBuildLockPath  = $buildDefaults.GlobalBuildLockPath
        ExecutableName       = $buildDefaults.ExecutableName
        RequiredBranch       = 'codex/integration-p0-qa'
    }
}

function Get-NormalizedFullPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    [IO.Path]::GetFullPath($Path).TrimEnd([char]'\', [char]'/')
}

function Assert-CanonicalProjectPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $actual = Get-NormalizedFullPath $ProjectPath
    if (-not (Test-Path -LiteralPath $actual -PathType Container)) {
        throw "Unity project path does not exist: $actual"
    }
    $requiredPaths = @(
        (Join-Path $actual 'Assets\FamilyCompany'),
        (Join-Path $actual 'ProjectSettings\ProjectVersion.txt'),
        (Join-Path $actual 'Tools\Build-FamilyCompanyWindows.ps1'))
    foreach ($requiredPath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "The selected folder is not a Family Company repository root: $actual"
        }
    }
    $actual
}

function Assert-ExactUnityEditor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$UnityEditorPath,
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
        throw (
            "Unity $script:FamilyCompanyUnityVersion was not found. Install it with Unity Hub, " +
            'or set FAMILY_COMPANY_UNITY_EDITOR to the full Unity.exe path.')
    }
    $actualEditor = Get-NormalizedFullPath $UnityEditorPath
    if (-not (Test-Path -LiteralPath $actualEditor -PathType Leaf)) {
        throw "Unity editor does not exist: $actualEditor"
    }

    $versionPath = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "ProjectVersion.txt is missing: $versionPath"
    }
    $versionLines = @(Get-Content -LiteralPath $versionPath -Encoding UTF8)
    $versionLine = $versionLines |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "Could not read the Unity version from $versionPath"
    }
    $projectVersion = $Matches[1].Trim()
    if (-not [string]::Equals($projectVersion, $script:FamilyCompanyUnityVersion, [StringComparison]::Ordinal)) {
        throw "Unity version mismatch. Expected '$script:FamilyCompanyUnityVersion', project requires '$projectVersion'."
    }

    $revisionLine = $versionLines |
        Where-Object { $_ -match '^m_EditorVersionWithRevision:\s*.+\s+\(([^)]+)\)$' } |
        Select-Object -First 1
    if (-not $revisionLine -or $revisionLine -notmatch '^m_EditorVersionWithRevision:\s*.+\s+\(([^)]+)\)$') {
        throw "Could not read the Unity revision from $versionPath"
    }
    $projectRevision = $Matches[1].Trim()
    $productVersion = [string](Get-Item -LiteralPath $actualEditor).VersionInfo.ProductVersion
    $expectedProductVersion = "${projectVersion}_${projectRevision}"
    if (-not [string]::Equals($productVersion, $expectedProductVersion, [StringComparison]::OrdinalIgnoreCase)) {
        throw (
            "Unity executable version mismatch. Expected '$expectedProductVersion', " +
            "found '$productVersion' at '$actualEditor'.")
    }
    $actualEditor
}

function Get-FamilyCompanyPowerShellHost {
    [CmdletBinding()]
    param()

    $candidates = @(
        (Join-Path $PSHOME 'powershell.exe'),
        (Join-Path $PSHOME 'pwsh.exe'))
    if (-not [string]::IsNullOrWhiteSpace($env:SystemRoot)) {
        $candidates += Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return Get-NormalizedFullPath $candidate
        }
    }
    throw 'Could not locate powershell.exe or pwsh.exe for the child automation process.'
}

function Invoke-CanonicalGitText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $standardErrorPath = [IO.Path]::GetTempFileName()
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            # The managed Codex workspace can be owned by its sandbox service account while
            # this build runs as the signed-in Windows user. Trust only the already-validated
            # canonical project path instead of mutating the user's global Git configuration.
            $output = & git -c "safe.directory=$ProjectPath" -C $ProjectPath @Arguments 2> $standardErrorPath
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        $standardError = @(Get-Content -LiteralPath $standardErrorPath -ErrorAction SilentlyContinue) -join [Environment]::NewLine
        $standardError = $standardError.Trim()
        if ($exitCode -ne 0) {
            throw "git $($Arguments -join ' ') failed with exit code ${exitCode}: $standardError"
        }
    }
    finally {
        Remove-Item -LiteralPath $standardErrorPath -Force -ErrorAction SilentlyContinue
    }
    $standardOutput = @($output) -join "`n"
    $standardOutput.TrimEnd()
}

function Get-Sha256ForText {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
        ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Get-CanonicalBuildSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $project = Assert-CanonicalProjectPath $ProjectPath
    $repositoryState = Get-FamilyCompanyRepositoryState $project
    $head = $repositoryState.Head
    $branch = $repositoryState.Branch
    $diff = Invoke-CanonicalGitText $project @(
        'diff', '--no-ext-diff', '--binary', '--ignore-space-at-eol', 'HEAD', '--',
        'Assets', 'Packages', 'ProjectSettings')
    $untrackedText = Invoke-CanonicalGitText $project @(
        'ls-files', '--others', '--exclude-standard', '--', 'Assets', 'Packages', 'ProjectSettings')
    $untracked = @()
    if (-not [string]::IsNullOrWhiteSpace($untrackedText)) {
        $untracked = @($untrackedText -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    }

    $builder = New-Object Text.StringBuilder
    [void]$builder.AppendLine("head=$head")
    [void]$builder.AppendLine("diff-sha256=$(Get-Sha256ForText $diff)")
    foreach ($relativePath in $untracked) {
        $nativeRelative = $relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $absolutePath = Join-Path $project $nativeRelative
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
            [void]$builder.AppendLine("untracked-missing=$relativePath")
            continue
        }
        $fileHash = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
        [void]$builder.AppendLine("untracked=$relativePath|$fileHash")
    }

    [pscustomobject]@{
        Head        = $head
        Branch      = $branch
        IsDirty     = $repositoryState.IsDirty
        HasConflicts = $repositoryState.HasConflicts
        Fingerprint = Get-Sha256ForText $builder.ToString()
        CapturedUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Get-FamilyCompanyRepositoryState {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $project = Assert-CanonicalProjectPath $ProjectPath
    $head = Invoke-CanonicalGitText $project @('rev-parse', 'HEAD')
    $branch = Invoke-CanonicalGitText $project @('branch', '--show-current')
    $status = Invoke-CanonicalGitText $project @('status', '--porcelain=v2', '--untracked-files=all')
    $conflicts = Invoke-CanonicalGitText $project @('diff', '--name-only', '--diff-filter=U')
    [pscustomobject]@{
        Head         = $head
        Branch       = $branch
        IsDirty      = -not [string]::IsNullOrWhiteSpace($status)
        HasConflicts = -not [string]::IsNullOrWhiteSpace($conflicts)
        StatusText   = $status
        Conflicts    = $conflicts
        CapturedUtc  = [DateTime]::UtcNow.ToString('o')
    }
}

function Assert-FamilyCompanyDeployableHead {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$RequiredBranch,
        [string]$ExpectedHead = ''
    )

    $state = Get-FamilyCompanyRepositoryState $ProjectPath
    if ($state.HasConflicts) {
        throw "MERGE_CONFLICT: deployment is held because unresolved paths exist: $($state.Conflicts)"
    }
    if ($state.IsDirty) {
        throw 'DIRTY_WORKTREE: deployment is held until every tracked and untracked change is committed or removed.'
    }
    if (-not [string]::Equals($state.Branch, $RequiredBranch, [StringComparison]::Ordinal)) {
        throw "WRONG_BRANCH: expected '$RequiredBranch', found '$($state.Branch)'."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedHead) -and
        -not [string]::Equals($state.Head, $ExpectedHead, [StringComparison]::Ordinal)) {
        throw "HEAD_CHANGED: expected '$ExpectedHead', found '$($state.Head)'."
    }
    $state
}

function Test-PathDescendsFrom {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ParentPath
    )

    $child = (Get-NormalizedFullPath $Path) + [IO.Path]::DirectorySeparatorChar
    $parent = (Get-NormalizedFullPath $ParentPath) + [IO.Path]::DirectorySeparatorChar
    $child.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase)
}

function Write-FamilyCompanyDeployManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$BuildDirectory,
        [Parameter(Mandatory = $true)]$RepositoryState,
        [Parameter(Mandatory = $true)][DateTime]$BuildStartedUtc,
        [Parameter(Mandatory = $true)][DateTime]$BuildCompletedUtc,
        [Parameter(Mandatory = $true)][string]$UnityVersion,
        [Parameter(Mandatory = $true)][string]$TestStatus
    )

    $directory = Get-NormalizedFullPath $BuildDirectory
    $durationSeconds = [Math]::Round(($BuildCompletedUtc - $BuildStartedUtc).TotalSeconds, 3)
    $manifest = [ordered]@{
        schemaVersion       = 1
        product             = 'Family Company Windows x64 Playtest'
        sourceBranch        = [string]$RepositoryState.Branch
        commitSha           = [string]$RepositoryState.Head
        buildStartedUtc     = $BuildStartedUtc.ToString('o')
        buildCompletedUtc   = $BuildCompletedUtc.ToString('o')
        buildDurationSeconds = $durationSeconds
        unityVersion        = $UnityVersion
        buildType           = 'Release (non-Development)'
        testStatus          = $TestStatus
    }
    Write-JsonAtomically (Join-Path $directory 'DEPLOY_MANIFEST.json') ([pscustomobject]$manifest)
    $text = @(
        $manifest.product,
        "Source branch: $($manifest.sourceBranch)",
        "Commit SHA: $($manifest.commitSha)",
        "Build started UTC: $($manifest.buildStartedUtc)",
        "Build completed UTC: $($manifest.buildCompletedUtc)",
        "Build duration seconds: $($manifest.buildDurationSeconds)",
        "Unity version: $($manifest.unityVersion)",
        "Build type: $($manifest.buildType)",
        "Test status: $($manifest.testStatus)") -join [Environment]::NewLine
    [IO.File]::WriteAllText(
        (Join-Path $directory 'DEPLOY_MANIFEST.txt'),
        $text + [Environment]::NewLine,
        (New-Object Text.UTF8Encoding($false)))
    [pscustomobject]$manifest
}

function Install-FamilyCompanyDeployRunner {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$BuildDirectory,
        [Parameter(Mandatory = $true)][string]$TemplatePath
    )

    if (-not (Test-Path -LiteralPath $TemplatePath -PathType Leaf)) {
        throw "Deployment RUN_WINDOWS.cmd template is missing: $TemplatePath"
    }
    Copy-Item -LiteralPath $TemplatePath -Destination (Join-Path $BuildDirectory 'RUN_WINDOWS.cmd') -Force
}

function Assert-FamilyCompanyDeployCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$BuildDirectory,
        [Parameter(Mandatory = $true)][string]$ExpectedCommit,
        [Parameter(Mandatory = $true)][string]$ExpectedBranch,
        [Parameter(Mandatory = $true)][string]$ExpectedUnityVersion
    )

    $directory = Get-NormalizedFullPath $BuildDirectory
    $required = @(
        (Join-Path $directory 'FamilyCompany.exe'),
        (Join-Path $directory 'FamilyCompany_Data'),
        (Join-Path $directory 'UnityPlayer.dll'),
        (Join-Path $directory 'BUILD_INFO.txt'),
        (Join-Path $directory 'DEPLOY_MANIFEST.json'),
        (Join-Path $directory 'DEPLOY_MANIFEST.txt'),
        (Join-Path $directory 'RUN_WINDOWS.cmd'))
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Deployment candidate is incomplete; required output is missing: $path"
        }
    }
    $manifest = Read-JsonIfPresent (Join-Path $directory 'DEPLOY_MANIFEST.json')
    if ($null -eq $manifest) { throw 'DEPLOY_MANIFEST.json is not valid JSON.' }
    if (-not [string]::Equals([string]$manifest.commitSha, $ExpectedCommit, [StringComparison]::Ordinal)) {
        throw "Deployment manifest commit mismatch. Expected '$ExpectedCommit', found '$($manifest.commitSha)'."
    }
    if (-not [string]::Equals([string]$manifest.sourceBranch, $ExpectedBranch, [StringComparison]::Ordinal)) {
        throw "Deployment manifest branch mismatch. Expected '$ExpectedBranch', found '$($manifest.sourceBranch)'."
    }
    if (-not [string]::Equals([string]$manifest.unityVersion, $ExpectedUnityVersion, [StringComparison]::Ordinal)) {
        throw "Deployment manifest Unity mismatch. Expected '$ExpectedUnityVersion', found '$($manifest.unityVersion)'."
    }
    $manifest
}

function Get-FamilyCompanyDeployedCommit {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$TargetPath)

    $manifest = Read-JsonIfPresent (Join-Path $TargetPath 'DEPLOY_MANIFEST.json')
    if ($null -eq $manifest -or -not ($manifest.PSObject.Properties.Name -contains 'commitSha')) { return '' }
    [string]$manifest.commitSha
}

function Test-FamilyCompanyPlayerRunning {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$TargetExecutablePath)

    $expected = Get-NormalizedFullPath $TargetExecutablePath
    try {
        $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'FamilyCompany.exe'" -ErrorAction Stop)
        foreach ($process in $processes) {
            $path = [string]$process.ExecutablePath
            if ([string]::IsNullOrWhiteSpace($path)) { return $true }
            if ([string]::Equals((Get-NormalizedFullPath $path), $expected, [StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
        return $false
    }
    catch {
        foreach ($process in @(Get-Process -Name FamilyCompany -ErrorAction SilentlyContinue)) {
            try {
                if ([string]::Equals((Get-NormalizedFullPath $process.Path), $expected, [StringComparison]::OrdinalIgnoreCase)) {
                    return $true
                }
            }
            catch { return $true }
        }
        return $false
    }
}

function Publish-FamilyCompanyDeployCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CandidatePath,
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [Parameter(Mandatory = $true)][string]$ExpectedCommit,
        [Parameter(Mandatory = $true)][string]$ExpectedBranch,
        [Parameter(Mandatory = $true)][string]$ExpectedUnityVersion,
        [switch]$TestFailureAfterBackup,
        [switch]$TestFailureAfterCandidateMove
    )

    $candidate = Get-NormalizedFullPath $CandidatePath
    $target = Get-NormalizedFullPath $TargetPath
    [void](Assert-FamilyCompanyDeployCandidate $candidate $ExpectedCommit $ExpectedBranch $ExpectedUnityVersion)
    $targetParent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetParent -PathType Container)) {
        New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
    }
    if (-not [string]::Equals([IO.Path]::GetPathRoot($candidate), [IO.Path]::GetPathRoot($target), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Candidate and target must be on the same volume for atomic directory promotion.'
    }

    $targetLeaf = Split-Path -Leaf $target
    $existingLkg = @(Get-ChildItem -LiteralPath $targetParent -Directory -Filter "$targetLeaf.last-known-good.*" -ErrorAction SilentlyContinue)
    foreach ($old in $existingLkg) {
        Remove-Item -LiteralPath $old.FullName -Recurse -Force
    }

    $backupPath = $null
    if (Test-Path -LiteralPath $target) {
        $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
        $previousCommit = Get-FamilyCompanyDeployedCommit $target
        if ([string]::IsNullOrWhiteSpace($previousCommit)) { $previousCommit = 'unknown-commit' }
        $shortCommit = if ($previousCommit.Length -gt 12) { $previousCommit.Substring(0, 12) } else { $previousCommit }
        $backupPath = Join-Path $targetParent "$targetLeaf.last-known-good.$timestamp.$shortCommit"
        Move-Item -LiteralPath $target -Destination $backupPath
    }
    try {
        if ($TestFailureAfterBackup) { throw 'TEST_INJECTED_PROMOTION_FAILURE' }
        Move-Item -LiteralPath $candidate -Destination $target
        if ($TestFailureAfterCandidateMove) { throw 'TEST_INJECTED_POST_MOVE_FAILURE' }
        [void](Assert-FamilyCompanyDeployCandidate $target $ExpectedCommit $ExpectedBranch $ExpectedUnityVersion)
    }
    catch {
        if ($null -ne $backupPath -and (Test-Path -LiteralPath $backupPath)) {
            if ((Test-Path -LiteralPath $target) -and -not (Test-Path -LiteralPath $candidate)) {
                Move-Item -LiteralPath $target -Destination $candidate
            }
            if (-not (Test-Path -LiteralPath $target)) {
                Move-Item -LiteralPath $backupPath -Destination $target
                $backupPath = $null
            }
        }
        throw
    }
    [pscustomobject]@{ TargetPath = $target; LastKnownGoodPath = $backupPath }
}

function Write-JsonAtomically {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $temporaryPath = "$Path.tmp.$PID"
    $json = $Value | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($temporaryPath, $json, (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Read-JsonIfPresent {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try {
        Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Add-BuildLogLine {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $line = "[$([DateTime]::UtcNow.ToString('o'))] $Message"
    [IO.File]::AppendAllText($Path, $line + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
}

function Open-ExclusiveLock {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    try {
        New-Object IO.FileStream(
            $Path,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        return $null
    }
}

function Test-AnyUnityEditorRunning {
    [CmdletBinding()]
    param()

    try {
        $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop)
        return $processes.Count -gt 0
    }
    catch {
        # Managed Windows accounts can be denied Win32_Process/CIM access even though the
        # ordinary process table is readable. Falling back to Get-Process avoids an endless
        # "Unity is already running" wait while still matching only Unity.exe.
        $processes = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
        return $processes.Count -gt 0
    }
}

function Get-FamilyCompanyBlockingBuildProcesses {
    [CmdletBinding()]
    param([string]$IgnoredPlayerExecutablePath = '')

    $ignoredPath = ''
    if (-not [string]::IsNullOrWhiteSpace($IgnoredPlayerExecutablePath)) {
        $ignoredPath = Get-NormalizedFullPath $IgnoredPlayerExecutablePath
    }
    $result = New-Object Collections.Generic.List[object]
    try {
        $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop |
            Where-Object { $_.Name -in @('Unity.exe', 'FamilyCompany.exe') })
        foreach ($process in $processes) {
            $executablePath = [string]$process.ExecutablePath
            if ($process.Name -eq 'FamilyCompany.exe' -and
                -not [string]::IsNullOrWhiteSpace($ignoredPath) -and
                -not [string]::IsNullOrWhiteSpace($executablePath) -and
                [string]::Equals((Get-NormalizedFullPath $executablePath), $ignoredPath, [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
            $result.Add([pscustomobject]@{
                ProcessId = [int]$process.ProcessId
                Name = [string]$process.Name
                ExecutablePath = $executablePath
                CommandLine = [string]$process.CommandLine
            })
        }
    }
    catch {
        foreach ($process in @(Get-Process -Name Unity, FamilyCompany -ErrorAction SilentlyContinue)) {
            $executablePath = ''
            try { $executablePath = [string]$process.Path } catch { }
            if ($process.ProcessName -eq 'FamilyCompany' -and
                -not [string]::IsNullOrWhiteSpace($ignoredPath) -and
                -not [string]::IsNullOrWhiteSpace($executablePath) -and
                [string]::Equals((Get-NormalizedFullPath $executablePath), $ignoredPath, [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
            $result.Add([pscustomobject]@{
                ProcessId = [int]$process.Id
                Name = [string]$process.ProcessName
                ExecutablePath = $executablePath
                CommandLine = ''
            })
        }
    }
    $result | ForEach-Object { $_ }
}

function Rotate-FamilyCompanyLogs {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$LogDirectory,
        [int]$MaximumLogFiles = 30
    )

    if ($MaximumLogFiles -lt 2) { throw 'MaximumLogFiles must be at least 2.' }
    if (-not (Test-Path -LiteralPath $LogDirectory -PathType Container)) { return }
    $files = @(Get-ChildItem -LiteralPath $LogDirectory -File -Filter 'build-*.log' |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($files.Count -le $MaximumLogFiles) { return }
    foreach ($file in $files[$MaximumLogFiles..($files.Count - 1)]) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}

function Test-WatcherProcessIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][string]$WorkerScriptPath
    )

    try {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop
        if ($null -eq $process) { return $false }
        $expected = Get-NormalizedFullPath $WorkerScriptPath
        $commandLine = [string]$process.CommandLine
        return $commandLine.IndexOf($expected, [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
    catch {
        return $false
    }
}
