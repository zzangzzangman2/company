Set-StrictMode -Version 2.0

function Get-FamilyCompanyBuildDefaults {
    [CmdletBinding()]
    param()

    [pscustomobject]@{
        CanonicalProjectPath = 'C:\Users\godho\Documents\Codex\family_company_unity'
        UnityEditorPath      = 'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe'
        UnityVersion         = '6000.3.21f1'
        DownloadsPath        = 'C:\Users\godho\Downloads\Family'
        FinalOutputPath      = 'C:\Users\godho\Downloads\Family\FamilyCompany_Playtest'
        AutomationRoot       = 'C:\Users\godho\Downloads\Family\FamilyCompany_BuildAutomation'
        ExecutableName       = 'FamilyCompany.exe'
        FirstScene           = 'Assets/FamilyCompany/Scenes/Prototype01.unity'
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

    $defaults = Get-FamilyCompanyBuildDefaults
    $expected = Get-NormalizedFullPath $defaults.CanonicalProjectPath
    $actual = Get-NormalizedFullPath $ProjectPath
    if (-not [string]::Equals($expected, $actual, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to build a non-canonical worktree. Expected '$expected', got '$actual'."
    }
    if (-not (Test-Path -LiteralPath $actual -PathType Container)) {
        throw "Canonical project path does not exist: $actual"
    }
    $actual
}

function Assert-ExactUnityEditor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$UnityEditorPath,
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    $defaults = Get-FamilyCompanyBuildDefaults
    $expectedEditor = Get-NormalizedFullPath $defaults.UnityEditorPath
    $actualEditor = Get-NormalizedFullPath $UnityEditorPath
    if (-not [string]::Equals($expectedEditor, $actualEditor, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected Unity editor. Expected '$expectedEditor', got '$actualEditor'."
    }
    if (-not (Test-Path -LiteralPath $actualEditor -PathType Leaf)) {
        throw "Unity editor does not exist: $actualEditor"
    }

    $versionPath = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "ProjectVersion.txt is missing: $versionPath"
    }
    $versionLine = Get-Content -LiteralPath $versionPath -Encoding UTF8 |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "Could not read the Unity version from $versionPath"
    }
    $projectVersion = $Matches[1].Trim()
    if (-not [string]::Equals($projectVersion, $defaults.UnityVersion, [StringComparison]::Ordinal)) {
        throw "Unity version mismatch. Expected '$($defaults.UnityVersion)', project requires '$projectVersion'."
    }
    $actualEditor
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
    $head = Invoke-CanonicalGitText $project @('rev-parse', 'HEAD')
    $branch = Invoke-CanonicalGitText $project @('branch', '--show-current')
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
        IsDirty     = (-not [string]::IsNullOrEmpty($diff)) -or $untracked.Count -gt 0
        Fingerprint = Get-Sha256ForText $builder.ToString()
        CapturedUtc = [DateTime]::UtcNow.ToString('o')
    }
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
        # If process inspection is unavailable, fail safe instead of starting a second editor.
        return $true
    }
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
