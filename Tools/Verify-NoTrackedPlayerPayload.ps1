#Requires -Version 7.2

[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:OrdinalIgnoreCase = [StringComparer]::OrdinalIgnoreCase
$script:RegexOptions = [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [Text.RegularExpressions.RegexOptions]::CultureInvariant
$script:KnownDeploymentRoot = [regex]::new(
    '(^|/)Builds/Windows(/|$)',
    $script:RegexOptions)
$script:KnownPlayerDirectory = [regex]::new(
    '^(FamilyCompany_Playtest.*|Company_Playtest.*)$',
    $script:RegexOptions)
$script:KnownPlayerArchive = [regex]::new(
    '^(?:FamilyCompany|Company)(?:$|[ _.\-](?:Playtest|Windows|Player|Build|Old|Interim|Rejected)(?:[ _.\-].*)?)$',
    $script:RegexOptions)
$script:ArchiveExtensions = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
foreach ($extension in @('.zip', '.7z', '.rar')) {
    [void]$script:ArchiveExtensions.Add($extension)
}

function Test-OrdinalEqual {
    param([AllowEmptyString()][string]$Left, [AllowEmptyString()][string]$Right)
    return $script:OrdinalIgnoreCase.Equals($Left, $Right)
}

function ConvertTo-GuardPath {
    param([Parameter(Mandatory)][string]$Path)

    $normalized = $Path.Replace('\', '/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }
    return $normalized
}

function Get-TrackedPaths {
    param([Parameter(Mandatory)][string]$Root)

    $previousEncoding = $OutputEncoding
    try {
        $OutputEncoding = [Text.UTF8Encoding]::new($false)
        $nativeOutput = @(& git -C $Root -c core.quotepath=false ls-files -z --)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $OutputEncoding = $previousEncoding
    }

    if ($exitCode -ne 0) {
        throw "git ls-files -z failed with exit code $exitCode."
    }

    # PowerShell may split native output at embedded newlines. Joining with LF
    # reconstructs those boundaries while NUL remains the authoritative delimiter.
    $raw = [string]::Join("`n", $nativeOutput)
    $paths = @($raw.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { ConvertTo-GuardPath $_ })
    return $paths
}

function Add-GuardViolation {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.List[object]]$Violations,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$Keys,
        [Parameter(Mandatory)][string]$Rule,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Evidence
    )

    $key = $Rule + [char]0 + $Path
    if ($Keys.Add($key)) {
        $Violations.Add([pscustomobject]@{
            Rule = $Rule
            Path = $Path
            Evidence = $Evidence
        })
    }
}

function Test-KnownPlayerArchiveName {
    param([Parameter(Mandatory)][string]$Path)

    $name = ($Path -split '/')[-1]
    $extension = [IO.Path]::GetExtension($name)
    if (-not $script:ArchiveExtensions.Contains($extension)) {
        return $false
    }

    $stem = $name.Substring(0, $name.Length - $extension.Length)
    return $script:KnownPlayerArchive.IsMatch($stem)
}

function Get-PathViolations {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Paths,
        [string]$Scope = ''
    )

    $violations = [Collections.Generic.List[object]]::new()
    $keys = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    $executablesByDirectory = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new(
        $script:OrdinalIgnoreCase)
    $unityPlayerDirectories = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    $dataDirectoriesByParent = [Collections.Generic.Dictionary[string, Collections.Generic.HashSet[string]]]::new(
        $script:OrdinalIgnoreCase)

    foreach ($rawPath in $Paths) {
        $path = ConvertTo-GuardPath $rawPath
        if ([string]::IsNullOrEmpty($path)) { continue }
        $displayPath = if ($Scope) { "$Scope!/$path" } else { $path }
        $segments = @($path.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
        if ($segments.Count -eq 0) { continue }

        if ($script:KnownDeploymentRoot.IsMatch($path)) {
            Add-GuardViolation $violations $keys 'known-deployment-root' $displayPath 'Tracked content is under a Builds/Windows deployment root.'
        }

        if ($segments.Count -gt 1) {
            foreach ($segment in $segments[0..($segments.Count - 2)]) {
                if ($script:KnownPlayerDirectory.IsMatch($segment)) {
                    Add-GuardViolation $violations $keys 'known-player-directory' $displayPath "Tracked content is under the known Player directory '$segment'."
                    break
                }
            }
        }

        $name = $segments[-1]
        if ((Test-OrdinalEqual $name 'FamilyCompany.exe') -or
            (Test-OrdinalEqual $name 'Company.exe')) {
            Add-GuardViolation $violations $keys 'known-player-executable' $displayPath "Tracked executable uses the known Player name '$name'."
        }

        if ($segments.Count -gt 1) {
            foreach ($segment in $segments[0..($segments.Count - 2)]) {
                if ((Test-OrdinalEqual $segment 'FamilyCompany_Data') -or
                    (Test-OrdinalEqual $segment 'Company_Data')) {
                    Add-GuardViolation $violations $keys 'known-player-data-directory' $displayPath "Tracked content is under the known Player data directory '$segment'."
                    break
                }
            }
        }

        if (Test-KnownPlayerArchiveName $path) {
            Add-GuardViolation $violations $keys 'known-player-archive' $displayPath "Tracked archive uses a known Player bundle name '$name'."
        }

        $lastSlash = $path.LastIndexOf('/')
        $directory = if ($lastSlash -ge 0) { $path.Substring(0, $lastSlash) } else { '' }
        $extension = [IO.Path]::GetExtension($name)
        if (Test-OrdinalEqual $extension '.exe') {
            if (-not $executablesByDirectory.ContainsKey($directory)) {
                $executablesByDirectory[$directory] = [Collections.Generic.List[string]]::new()
            }
            $executablesByDirectory[$directory].Add($name)
        }
        if (Test-OrdinalEqual $name 'UnityPlayer.dll') {
            [void]$unityPlayerDirectories.Add($directory)
        }

        for ($index = 0; $index -lt ($segments.Count - 1); $index++) {
            $candidate = $segments[$index]
            if (-not $candidate.EndsWith('_Data', [StringComparison]::OrdinalIgnoreCase)) { continue }
            $parent = if ($index -eq 0) { '' } else { [string]::Join('/', $segments[0..($index - 1)]) }
            if (-not $dataDirectoriesByParent.ContainsKey($parent)) {
                $dataDirectoriesByParent[$parent] =
                    [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
            }
            [void]$dataDirectoriesByParent[$parent].Add($candidate)
        }
    }

    foreach ($directory in $executablesByDirectory.Keys) {
        if (-not $unityPlayerDirectories.Contains($directory)) { continue }
        if (-not $dataDirectoriesByParent.ContainsKey($directory)) { continue }

        foreach ($executable in $executablesByDirectory[$directory]) {
            $stem = [IO.Path]::GetFileNameWithoutExtension($executable)
            $expectedDataDirectory = $stem + '_Data'
            if (-not $dataDirectoriesByParent[$directory].Contains($expectedDataDirectory)) { continue }
            $bundlePath = if ($directory) { $directory + '/' } else { './' }
            $displayPath = if ($Scope) { "$Scope!/$bundlePath" } else { $bundlePath }
            Add-GuardViolation $violations $keys 'renamed-unity-player-bundle' $displayPath "Directory contains $executable, UnityPlayer.dll, and $expectedDataDirectory as one Player bundle."
        }
    }

    return @($violations)
}

function Invoke-RawProcess {
    param(
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $memory = [IO.MemoryStream]::new()
    try {
        if (-not $process.Start()) { throw "Could not start $FileName." }
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $process.WaitForExit()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $memory.ToArray()
            Stderr = $stderr
        }
    }
    finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

function Get-GitBlobBytes {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    $sizeResult = Invoke-RawProcess 'git' @('-C', $Root, 'cat-file', '-s', ":$Path") $Root
    if ($sizeResult.ExitCode -ne 0) {
        throw "Cannot read tracked archive size for '$Path': $($sizeResult.Stderr.Trim())"
    }
    $sizeText = [Text.Encoding]::UTF8.GetString($sizeResult.Stdout).Trim()
    $size = 0L
    if (-not [long]::TryParse($sizeText, [ref]$size)) {
        throw "Invalid tracked archive size '$sizeText' for '$Path'."
    }
    if ($size -gt 536870912L) {
        throw "Tracked archive '$Path' is $size bytes; the 512 MiB inspection limit is fail-closed."
    }

    $blobResult = Invoke-RawProcess 'git' @('-C', $Root, 'cat-file', 'blob', ":$Path") $Root
    if ($blobResult.ExitCode -ne 0) {
        throw "Cannot read tracked archive '$Path': $($blobResult.Stderr.Trim())"
    }
    if ($blobResult.Stdout.LongLength -ne $size) {
        throw "Tracked archive '$Path' changed or was truncated during inspection."
    }
    return $blobResult.Stdout
}

function Get-ZipEntryPaths {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $memory = [IO.MemoryStream]::new($Bytes, $false)
    $archive = $null
    try {
        $archive = [IO.Compression.ZipArchive]::new(
            $memory,
            [IO.Compression.ZipArchiveMode]::Read,
            $false)
        return @($archive.Entries | ForEach-Object { ConvertTo-GuardPath $_.FullName })
    }
    finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $memory.Dispose()
    }
}

function Find-ArchiveLister {
    foreach ($name in @('7z', '7zz', '7za')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return [pscustomobject]@{ Kind = '7zip'; Path = $command.Source }
        }
    }

    $windows7Zip = if ($env:ProgramFiles) { Join-Path $env:ProgramFiles '7-Zip\7z.exe' } else { '' }
    if ($windows7Zip -and (Test-Path -LiteralPath $windows7Zip -PathType Leaf)) {
        return [pscustomobject]@{ Kind = '7zip'; Path = $windows7Zip }
    }

    foreach ($name in @('bsdtar', 'tar')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return [pscustomobject]@{ Kind = 'tar'; Path = $command.Source }
        }
    }
    return $null
}

function ConvertFrom-ArchiveListingBytes {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    if ($IsWindows) {
        [Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance)
        $oemCodePage = [Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage
        return [Text.Encoding]::GetEncoding($oemCodePage).GetString($Bytes)
    }
    return [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
}

function Get-ExternalArchiveEntryPaths {
    param(
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][string]$Extension
    )

    $lister = Find-ArchiveLister
    if ($null -eq $lister) {
        throw "No 7z/bsdtar-compatible archive inspector is available for '$Extension'."
    }

    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $tempName = 'fc-player-payload-guard-' + [guid]::NewGuid().ToString('N') + $Extension
    $tempPath = [IO.Path]::GetFullPath((Join-Path $tempRoot $tempName))
    if (-not $tempPath.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($tempPath)).StartsWith('fc-player-payload-guard-', [StringComparison]::Ordinal)) {
        throw "Unsafe archive inspection temp path '$tempPath'."
    }

    try {
        [IO.File]::WriteAllBytes($tempPath, $Bytes)
        if ($lister.Kind -eq '7zip') {
            $result = Invoke-RawProcess $lister.Path @('l', '-slt', '-ba', '--', $tempPath) $tempRoot
            if ($result.ExitCode -ne 0) {
                throw "7-Zip could not inspect the archive: $($result.Stderr.Trim())"
            }
            $text = ConvertFrom-ArchiveListingBytes $result.Stdout
            return @([regex]::Matches($text, '(?m)^Path = (.*)\r?$') |
                ForEach-Object { ConvertTo-GuardPath $_.Groups[1].Value })
        }

        $result = Invoke-RawProcess $lister.Path @('-tf', $tempPath) $tempRoot
        if ($result.ExitCode -ne 0) {
            throw "bsdtar could not inspect the archive: $($result.Stderr.Trim())"
        }
        $text = ConvertFrom-ArchiveListingBytes $result.Stdout
        return @($text -split '\r?\n' | Where-Object { $_ -ne '' } |
            ForEach-Object { ConvertTo-GuardPath $_ })
    }
    finally {
        if (Test-Path -LiteralPath $tempPath -PathType Leaf) {
            [IO.File]::Delete($tempPath)
        }
    }
}

function Get-ArchiveEntryPaths {
    param(
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][string]$Extension
    )

    if (Test-OrdinalEqual $Extension '.zip') {
        return @(Get-ZipEntryPaths $Bytes | Where-Object { -not [string]::IsNullOrEmpty($_) })
    }
    return @(Get-ExternalArchiveEntryPaths $Bytes $Extension |
        Where-Object { -not [string]::IsNullOrEmpty($_) })
}

function Invoke-GuardScan {
    param([Parameter(Mandatory)][string]$Root)

    $resolvedRoot = [IO.Path]::GetFullPath($Root)
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        throw "Repository root does not exist: $resolvedRoot"
    }

    $topLevelOutput = @(& git -C $resolvedRoot rev-parse --show-toplevel)
    if ($LASTEXITCODE -ne 0 -or $topLevelOutput.Count -ne 1) {
        throw "Not a Git worktree: $resolvedRoot"
    }
    $topLevel = [IO.Path]::GetFullPath($topLevelOutput[0])
    if (-not (Test-OrdinalEqual $resolvedRoot $topLevel)) {
        throw "RepositoryRoot must be the exact worktree root '$topLevel', not '$resolvedRoot'."
    }

    $trackedPaths = @(Get-TrackedPaths $resolvedRoot)
    $violations = [Collections.Generic.List[object]]::new()
    foreach ($violation in @(Get-PathViolations $trackedPaths)) {
        $violations.Add($violation)
    }

    $archives = @($trackedPaths | Where-Object {
        $script:ArchiveExtensions.Contains([IO.Path]::GetExtension($_))
    })
    foreach ($archivePath in $archives) {
        # A known product archive is already rejected by path. Content cannot make it safer.
        if (Test-KnownPlayerArchiveName $archivePath) { continue }
        try {
            $bytes = Get-GitBlobBytes $resolvedRoot $archivePath
            $entries = @(Get-ArchiveEntryPaths $bytes ([IO.Path]::GetExtension($archivePath)))
            foreach ($entry in $entries) {
                $segments = @(($entry -replace '\\', '/') -split '/')
                if ($entry.StartsWith('/', [StringComparison]::Ordinal) -or
                    @($segments | Where-Object { $_ -eq '..' }).Count -gt 0) {
                    $violations.Add([pscustomobject]@{
                        Rule = 'unsafe-archive-entry'
                        Path = "$archivePath!/$entry"
                        Evidence = 'Archive entry is absolute or traverses a parent directory.'
                    })
                }
            }
            foreach ($violation in @(Get-PathViolations $entries $archivePath)) {
                $violations.Add($violation)
            }
        }
        catch {
            $violations.Add([pscustomobject]@{
                Rule = 'archive-inspection-failed'
                Path = $archivePath
                Evidence = $_.Exception.Message
            })
        }
    }

    return [pscustomobject]@{
        Root = $resolvedRoot
        TrackedCount = $trackedPaths.Count
        ArchiveCount = $archives.Count
        Violations = @($violations)
    }
}

function Write-FixtureFile {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [string]$Content = 'fixture'
    )

    $platformPath = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $fullPath = [IO.Path]::GetFullPath((Join-Path $Root $platformPath))
    $rootPrefix = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture escaped its root: $fullPath"
    }
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    [void][IO.Directory]::CreateDirectory($directory)
    [IO.File]::WriteAllText($fullPath, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-FixtureZip {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string[]]$Entries
    )

    $platformPath = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $fullPath = [IO.Path]::GetFullPath((Join-Path $Root $platformPath))
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    [void][IO.Directory]::CreateDirectory($directory)
    $archive = [IO.Compression.ZipFile]::Open($fullPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entryPath in $Entries) {
            $entry = $archive.CreateEntry($entryPath, [IO.Compression.CompressionLevel]::NoCompression)
            $stream = $entry.Open()
            try {
                $bytes = [Text.Encoding]::UTF8.GetBytes('fixture')
                $stream.Write($bytes, 0, $bytes.Length)
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Add-FixturePaths {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Paths
    )

    & git -C $Root --literal-pathspecs add -f -- @Paths 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "git add -f failed for fixture '$Root'."
    }
}

function Test-GuardFixture {
    param(
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][AllowEmptyCollection()][hashtable[]]$Files,
        [AllowEmptyCollection()][hashtable[]]$Zips = @(),
        [string]$ExpectedRule = ''
    )

    $root = Join-Path $Parent $Name
    [void][IO.Directory]::CreateDirectory($root)
    & git -C $root init --quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git init failed for fixture '$Name'." }

    $paths = [Collections.Generic.List[string]]::new()
    foreach ($file in $Files) {
        Write-FixtureFile $root $file.Path $file.Content
        $paths.Add($file.Path)
    }
    foreach ($zip in $Zips) {
        Write-FixtureZip $root $zip.Path $zip.Entries
        $paths.Add($zip.Path)
    }
    Add-FixturePaths $root @($paths)

    $result = Invoke-GuardScan $root
    if ([string]::IsNullOrEmpty($ExpectedRule)) {
        if ($result.Violations.Count -ne 0) {
            $summary = @($result.Violations | ForEach-Object { "$($_.Rule):$($_.Path):$($_.Evidence)" }) -join ', '
            throw "Fixture '$Name' should pass but failed: $summary"
        }
    }
    else {
        if ($result.Violations.Count -ne 1 -or
            -not (Test-OrdinalEqual $result.Violations[0].Rule $ExpectedRule)) {
            $summary = @($result.Violations | ForEach-Object { "$($_.Rule):$($_.Path):$($_.Evidence)" }) -join ', '
            throw "Fixture '$Name' expected sole rule '$ExpectedRule' but got: $summary"
        }
    }
    return [pscustomobject]@{ Name = $Name; Rule = if ($ExpectedRule) { $ExpectedRule } else { 'allow' } }
}

function Invoke-GuardSelfTest {
    $systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $leaf = 'fc-player-payload-guard-selftest-' + [guid]::NewGuid().ToString('N')
    $testRoot = [IO.Path]::GetFullPath((Join-Path $systemTemp $leaf))
    $tempPrefix = $systemTemp.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $testRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($testRoot)).StartsWith('fc-player-payload-guard-selftest-', [StringComparison]::Ordinal)) {
        throw "Unsafe self-test root '$testRoot'."
    }

    $results = [Collections.Generic.List[object]]::new()
    try {
        [void][IO.Directory]::CreateDirectory($testRoot)
        $results.Add((Test-GuardFixture $testRoot 'allow-legitimate-content' @(
            @{ Path = 'Assets/Plugins/LegitPlugin.dll'; Content = 'plugin' },
            @{ Path = 'Assets/Plugins/UnityPlayer.dll'; Content = 'lone exact DLL is not a bundle' },
            @{ Path = 'Assets/Generated/Moon_Data/content.bytes'; Content = 'lone arbitrary data directory' },
            @{ Path = 'Tools/Moon.exe'; Content = 'lone arbitrary executable' },
            @{ Path = 'Assets/DGGL/Plugins/DGGL.Runtime.dll'; Content = 'DGGL exclusion fixture' },
            @{ Path = 'Assets/Art/FamilyCompany_DataSheet.png'; Content = 'art' },
            @{ Path = 'Assets/한글 경로/공백 파일.txt'; Content = 'NUL-delimited path fixture' }
        ) @(
            @{ Path = 'Docs/CompanyResearch.zip'; Entries = @(
                'Sources/Company.cs',
                'Plugins/Legit.dll',
                'Sample_Data/readme.txt') },
            @{ Path = 'Docs/FamilyCompany_Source.zip'; Entries = @(
                'Assets/Plugins/LegitPlugin.dll',
                'Sources/FamilyCompany.cs') }
        )))
        $results.Add((Test-GuardFixture $testRoot 'deny-build-root' @(
            @{ Path = 'nested/bUiLdS/wInDoWs/공백 경로/marker.txt'; Content = 'x' }
        ) @() 'known-deployment-root'))
        $results.Add((Test-GuardFixture $testRoot 'deny-player-directory' @(
            @{ Path = '보존 테스트/FAMILYcompany_PLAYTEST_old/메모.txt'; Content = 'x' }
        ) @() 'known-player-directory'))
        $results.Add((Test-GuardFixture $testRoot 'deny-familycompany-exe' @(
            @{ Path = 'Assets/테스트 공백/FAMILYCOMPANY.EXE'; Content = 'x' }
        ) @() 'known-player-executable'))
        $results.Add((Test-GuardFixture $testRoot 'deny-company-exe' @(
            @{ Path = 'legacy/COMPANY.exe'; Content = 'x' }
        ) @() 'known-player-executable'))
        $results.Add((Test-GuardFixture $testRoot 'deny-known-data' @(
            @{ Path = 'stash/FamilyCompany_DATA/globalgamemanagers'; Content = 'x' }
        ) @() 'known-player-data-directory'))
        $results.Add((Test-GuardFixture $testRoot 'deny-known-zip-name' @(
            @{ Path = 'drops/FamilyCompany_INTERIM.zip'; Content = 'opaque fixture' }
        ) @() 'known-player-archive'))
        $results.Add((Test-GuardFixture $testRoot 'deny-known-7z-name' @(
            @{ Path = 'drops/Company_Playtest_old.7Z'; Content = 'opaque fixture' }
        ) @() 'known-player-archive'))
        $results.Add((Test-GuardFixture $testRoot 'deny-known-rar-name' @(
            @{ Path = 'drops/FamilyCompany_REJECTED.RAR'; Content = 'opaque fixture' }
        ) @() 'known-player-archive'))
        $results.Add((Test-GuardFixture $testRoot 'deny-renamed-bundle' @(
            @{ Path = 'payload 한글/Moon.EXE'; Content = 'x' },
            @{ Path = 'payload 한글/uNiTyPlAyEr.DlL'; Content = 'x' },
            @{ Path = 'payload 한글/mOoN_dAtA/globalgamemanagers'; Content = 'x' }
        ) @() 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture $testRoot 'deny-renamed-zip-bundle' @() @(
            @{ Path = 'docs/자료 묶음.zip'; Entries = @(
                '한글 묶음/Moon.exe',
                '한글 묶음/UnityPlayer.dll',
                '한글 묶음/Moon_Data/globalgamemanagers') }
        ) 'renamed-unity-player-bundle'))

        Write-Output "PLAYER_PAYLOAD_GUARD SELFTEST PASS fixtures=$($results.Count) forcedAddFixtures=$($results.Count)"
    }
    finally {
        if (Test-Path -LiteralPath $testRoot -PathType Container) {
            $resolvedForDelete = [IO.Path]::GetFullPath($testRoot)
            if (-not $resolvedForDelete.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                -not ([IO.Path]::GetFileName($resolvedForDelete)).StartsWith('fc-player-payload-guard-selftest-', [StringComparison]::Ordinal)) {
                throw "Refusing unsafe self-test cleanup '$resolvedForDelete'."
            }
            foreach ($fixtureFile in [IO.Directory]::EnumerateFiles(
                $resolvedForDelete,
                '*',
                [IO.SearchOption]::AllDirectories)) {
                [IO.File]::SetAttributes($fixtureFile, [IO.FileAttributes]::Normal)
            }
            [IO.Directory]::Delete($resolvedForDelete, $true)
        }
    }
}

try {
    if ($SelfTest) {
        Invoke-GuardSelfTest
        exit 0
    }

    $result = Invoke-GuardScan $RepositoryRoot
    if ($result.Violations.Count -gt 0) {
        Write-Output "PLAYER_PAYLOAD_GUARD FAIL tracked=$($result.TrackedCount) archives=$($result.ArchiveCount) violations=$($result.Violations.Count)"
        foreach ($violation in @($result.Violations | Sort-Object Rule, Path)) {
            Write-Output "[$($violation.Rule)] $($violation.Path) :: $($violation.Evidence)"
        }
        exit 1
    }

    Write-Output "PLAYER_PAYLOAD_GUARD PASS tracked=$($result.TrackedCount) archives=$($result.ArchiveCount) violations=0"
    exit 0
}
catch {
    [Console]::Error.WriteLine("PLAYER_PAYLOAD_GUARD ERROR: $($_.Exception.Message)")
    exit 2
}
