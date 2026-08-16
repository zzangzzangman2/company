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

function Get-TrackedRecords {
    param([Parameter(Mandatory)][string]$Root)

    $result = Invoke-RawProcess 'git' @('-C', $Root, '-c', 'core.quotepath=false', 'ls-files', '-s', '-z', '--') $Root
    if ($result.ExitCode -ne 0) { throw "git ls-files -s -z failed: $($result.Stderr.Trim())" }
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString($result.Stdout)
    $records = [Collections.Generic.List[object]]::new()
    foreach ($entry in $raw.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        $tab = $entry.IndexOf([char]9)
        if ($tab -le 0) { throw "Malformed git ls-files record." }
        $metadata = @($entry.Substring(0, $tab).Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
        if ($metadata.Count -ne 3) { throw "Malformed git index metadata '$($entry.Substring(0, $tab))'." }
        if ($metadata[2] -ne '0') { throw "Unmerged index stage '$($metadata[2])' is fail-closed." }
        $records.Add([pscustomobject]@{
            Mode = $metadata[0]
            ObjectId = $metadata[1]
            Path = ConvertTo-GuardPath $entry.Substring($tab + 1)
        })
    }
    return @($records)
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

function Read-StreamLineAscii {
    param([Parameter(Mandatory)][IO.Stream]$Stream)

    $bytes = [Collections.Generic.List[byte]]::new()
    while ($true) {
        $value = $Stream.ReadByte()
        if ($value -lt 0) { throw 'Unexpected EOF while reading git cat-file header.' }
        if ($value -eq 10) { break }
        if ($value -ne 13) { $bytes.Add([byte]$value) }
    }
    return [Text.Encoding]::ASCII.GetString($bytes.ToArray())
}

function Read-ExactBytes {
    param(
        [Parameter(Mandatory)][IO.Stream]$Stream,
        [Parameter(Mandatory)][long]$Length
    )

    if ($Length -lt 0 -or $Length -gt [int]::MaxValue) {
        throw "Git blob length $Length cannot be inspected safely."
    }
    $bytes = [byte[]]::new([int]$Length)
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $read = $Stream.Read($bytes, $offset, $bytes.Length - $offset)
        if ($read -le 0) { throw "Unexpected EOF after $offset of $Length blob bytes." }
        $offset += $read
    }
    return $bytes
}

function Get-UInt16LittleEndian {
    param([byte[]]$Bytes, [int]$Offset)
    return [uint16]([uint32]$Bytes[$Offset] -bor ([uint32]$Bytes[$Offset + 1] -shl 8))
}

function Get-UInt32LittleEndian {
    param([byte[]]$Bytes, [int]$Offset)
    return [uint32]([uint32]$Bytes[$Offset] -bor
        ([uint32]$Bytes[$Offset + 1] -shl 8) -bor
        ([uint32]$Bytes[$Offset + 2] -shl 16) -bor
        ([uint32]$Bytes[$Offset + 3] -shl 24))
}

function Get-UInt32BigEndian {
    param([byte[]]$Bytes, [int]$Offset)
    return [uint32](([uint32]$Bytes[$Offset] -shl 24) -bor
        ([uint32]$Bytes[$Offset + 1] -shl 16) -bor
        ([uint32]$Bytes[$Offset + 2] -shl 8) -bor
        [uint32]$Bytes[$Offset + 3])
}

function Get-UInt64BigEndian {
    param([byte[]]$Bytes, [int]$Offset)
    $value = [uint64]0
    for ($index = 0; $index -lt 8; $index++) {
        $value = ($value -shl 8) -bor [uint64]$Bytes[$Offset + $index]
    }
    return $value
}

function Get-PortableExecutableInfo {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    if ($Bytes.Length -lt 128 -or $Bytes[0] -ne 0x4d -or $Bytes[1] -ne 0x5a) {
        return [pscustomobject]@{ IsPe = $false; IsDll = $false }
    }
    $peOffset = [int](Get-UInt32LittleEndian $Bytes 0x3c)
    if ($peOffset -lt 0x40 -or $peOffset -gt ($Bytes.Length - 24)) {
        return [pscustomobject]@{ IsPe = $false; IsDll = $false }
    }
    if ($Bytes[$peOffset] -ne 0x50 -or $Bytes[$peOffset + 1] -ne 0x45 -or
        $Bytes[$peOffset + 2] -ne 0 -or $Bytes[$peOffset + 3] -ne 0) {
        return [pscustomobject]@{ IsPe = $false; IsDll = $false }
    }
    $characteristics = Get-UInt16LittleEndian $Bytes ($peOffset + 22)
    return [pscustomobject]@{
        IsPe = $true
        IsDll = (($characteristics -band 0x2000) -ne 0)
    }
}

function Test-BinaryMarker {
    param(
        [Parameter(Mandatory)][string]$Latin1Text,
        [Parameter(Mandatory)][string]$Marker,
        [switch]$Utf16
    )

    if ($Utf16) {
        $needle = [Text.Encoding]::Latin1.GetString([Text.Encoding]::Unicode.GetBytes($Marker))
        return $Latin1Text.Contains($needle, [StringComparison]::Ordinal)
    }
    return $Latin1Text.Contains($Marker, [StringComparison]::Ordinal)
}

function Get-ArchiveMagicKind {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    if ($Bytes.Length -ge 6 -and
        $Bytes[0] -eq 0x37 -and $Bytes[1] -eq 0x7a -and $Bytes[2] -eq 0xbc -and
        $Bytes[3] -eq 0xaf -and $Bytes[4] -eq 0x27 -and $Bytes[5] -eq 0x1c) {
        return '7z'
    }
    if ($Bytes.Length -ge 7 -and
        $Bytes[0] -eq 0x52 -and $Bytes[1] -eq 0x61 -and $Bytes[2] -eq 0x72 -and
        $Bytes[3] -eq 0x21 -and $Bytes[4] -eq 0x1a -and $Bytes[5] -eq 0x07 -and
        ($Bytes[6] -eq 0x00 -or ($Bytes[6] -eq 0x01 -and $Bytes.Length -ge 8 -and $Bytes[7] -eq 0x00))) {
        return 'rar'
    }
    if ($Bytes.Length -ge 4 -and $Bytes[0] -eq 0x50 -and $Bytes[1] -eq 0x4b -and
        $Bytes[2] -in @(0x03, 0x05, 0x07) -and $Bytes[3] -in @(0x04, 0x06, 0x08)) {
        return 'zip'
    }
    return ''
}

function Test-UnitySerializedFile {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    if ($Bytes.Length -lt 64) { return $false }
    $version = Get-UInt32BigEndian $Bytes 8
    if ($version -lt 9 -or $version -gt 100) { return $false }

    if ($version -ge 22) {
        $metadataSize = Get-UInt32BigEndian $Bytes 20
        $fileSize = Get-UInt64BigEndian $Bytes 24
        $dataOffset = Get-UInt64BigEndian $Bytes 32
        if ($metadataSize -le 0 -or $fileSize -ne [uint64]$Bytes.LongLength -or
            $dataOffset -le $metadataSize -or $dataOffset -ge $fileSize) {
            return $false
        }
    }
    else {
        $metadataSize = Get-UInt32BigEndian $Bytes 0
        $fileSize = Get-UInt32BigEndian $Bytes 4
        $dataOffset = Get-UInt32BigEndian $Bytes 12
        if ($metadataSize -le 0 -or $fileSize -ne [uint32]$Bytes.Length -or
            $dataOffset -le $metadataSize -or $dataOffset -ge $fileSize) {
            return $false
        }
    }

    $prefixLength = [Math]::Min(256, $Bytes.Length)
    $prefix = [Text.Encoding]::ASCII.GetString($Bytes, 0, $prefixLength)
    return [regex]::IsMatch($prefix, '(?<!\d)\d{1,4}\.\d+\.\d+[abfp]\d+(?!\d)', $script:RegexOptions)
}

function Get-BlobEvidence {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $pe = Get-PortableExecutableInfo $Bytes
    $unityBinaryKind = ''
    $isManagedAssembly = $false
    if ($pe.IsPe) {
        $latin1 = [Text.Encoding]::Latin1.GetString($Bytes)
        $hasCompany = (Test-BinaryMarker $latin1 'Unity Technologies') -or
            (Test-BinaryMarker $latin1 'Unity Technologies' -Utf16)
        $hasUnityMain = Test-BinaryMarker $latin1 'UnityMain'
        $hasUnityPlayer = Test-BinaryMarker $latin1 'UnityPlayer.dll'
        $hasPlayerAssembly = Test-BinaryMarker $latin1 'UnityTechnologies.Unity.UnityPlayer'
        $hasCrashIdentity = Test-BinaryMarker $latin1 'UnityCrashHandler'
        $hasCrashProduct = (Test-BinaryMarker $latin1 'Unity Crash Handler') -or
            (Test-BinaryMarker $latin1 'Unity Crash Handler' -Utf16) -or
            (Test-BinaryMarker $latin1 'UnityTechnologies.Unity.UnityCrashHandler')

        if ($pe.IsDll -and $hasUnityPlayer -and $hasUnityMain -and $hasCompany) {
            $unityBinaryKind = 'UnityPlayerDll'
        }
        elseif (-not $pe.IsDll -and $hasCrashIdentity -and $hasCrashProduct -and $hasCompany) {
            $unityBinaryKind = 'CrashHandler'
        }
        elseif (-not $pe.IsDll -and $hasUnityPlayer -and $hasUnityMain -and $hasPlayerAssembly) {
            $unityBinaryKind = 'PlayerExecutable'
        }
        $isManagedAssembly = $latin1.Contains('BSJB', [StringComparison]::Ordinal)
    }

    $isBootConfig = $false
    if ($Bytes.Length -le 1048576) {
        $text = [Text.Encoding]::Latin1.GetString($Bytes)
        $bootMarkers = 0
        foreach ($marker in @('gfx-enable-gfx-jobs=', 'gfx-threading-mode=', 'hdr-display-enabled=', 'gc-max-time-slice=')) {
            if ($text.Contains($marker, [StringComparison]::Ordinal)) { $bootMarkers++ }
        }
        $isBootConfig = $bootMarkers -ge 2
    }

    return [pscustomobject]@{
        Size = $Bytes.LongLength
        IsPe = $pe.IsPe
        IsDll = $pe.IsDll
        UnityBinaryKind = $unityBinaryKind
        IsManagedAssembly = $isManagedAssembly
        IsUnitySerialized = Test-UnitySerializedFile $Bytes
        IsUnityBootConfig = $isBootConfig
        ArchiveKind = Get-ArchiveMagicKind $Bytes
    }
}

function Get-GitBlobAnalyses {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records
    )

    $unique = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $archiveObjects = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($record in $Records) {
        if ($record.Mode -eq '160000') { continue }
        if (-not $unique.ContainsKey($record.ObjectId)) { $unique[$record.ObjectId] = $record }
        if ($script:ArchiveExtensions.Contains([IO.Path]::GetExtension($record.Path))) {
            [void]$archiveObjects.Add($record.ObjectId)
        }
    }

    $analyses = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    if ($unique.Count -eq 0) { return $analyses }

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.WorkingDirectory = $Root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-C', $Root, 'cat-file', '--batch')) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $writer = $null
    $started = $false
    try {
        if (-not $process.Start()) { throw 'Could not start git cat-file --batch.' }
        $started = $true
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $writer = [IO.StreamWriter]::new(
            $process.StandardInput.BaseStream,
            [Text.UTF8Encoding]::new($false),
            1024,
            $true)
        $writer.NewLine = "`n"
        $writer.AutoFlush = $true

        foreach ($objectId in $unique.Keys) {
            $writer.WriteLine($objectId)
            $header = Read-StreamLineAscii $process.StandardOutput.BaseStream
            if ($header -notmatch '^([0-9a-f]+) blob ([0-9]+)$') {
                throw "Unexpected git cat-file header '$header' for $objectId."
            }
            $length = [long]$Matches[2]
            $bytes = Read-ExactBytes $process.StandardOutput.BaseStream $length
            if ($process.StandardOutput.BaseStream.ReadByte() -ne 10) {
                throw "Missing git cat-file delimiter after $objectId."
            }
            $evidence = Get-BlobEvidence $bytes
            $retainBytes = (-not [string]::IsNullOrEmpty($evidence.ArchiveKind)) -or
                $archiveObjects.Contains($objectId)
            $analyses[$objectId] = [pscustomobject]@{
                Evidence = $evidence
                Bytes = if ($retainBytes) { $bytes } else { $null }
            }
        }

        $writer.Dispose()
        $writer = $null
        $process.StandardInput.Close()
        $process.WaitForExit()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "git cat-file --batch failed: $($stderr.Trim())" }
        return $analyses
    }
    finally {
        if ($null -ne $writer) { $writer.Dispose() }
        if ($started -and -not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}

function Get-PathBundleDirectories {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Paths)

    $executables = [Collections.Generic.Dictionary[string, Collections.Generic.List[string]]]::new(
        $script:OrdinalIgnoreCase)
    $unityPlayer = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    $dataDirectories = [Collections.Generic.Dictionary[string, Collections.Generic.HashSet[string]]]::new(
        $script:OrdinalIgnoreCase)
    foreach ($rawPath in $Paths) {
        $path = ConvertTo-GuardPath $rawPath
        $segments = @($path.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
        if ($segments.Count -eq 0) { continue }
        $lastSlash = $path.LastIndexOf('/')
        $directory = if ($lastSlash -ge 0) { $path.Substring(0, $lastSlash) } else { '' }
        $name = $segments[-1]
        if (Test-OrdinalEqual ([IO.Path]::GetExtension($name)) '.exe') {
            if (-not $executables.ContainsKey($directory)) {
                $executables[$directory] = [Collections.Generic.List[string]]::new()
            }
            $executables[$directory].Add($name)
        }
        if (Test-OrdinalEqual $name 'UnityPlayer.dll') { [void]$unityPlayer.Add($directory) }
        for ($index = 0; $index -lt ($segments.Count - 1); $index++) {
            if (-not $segments[$index].EndsWith('_Data', [StringComparison]::OrdinalIgnoreCase)) { continue }
            $parent = if ($index -eq 0) { '' } else { [string]::Join('/', $segments[0..($index - 1)]) }
            if (-not $dataDirectories.ContainsKey($parent)) {
                $dataDirectories[$parent] = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
            }
            [void]$dataDirectories[$parent].Add($segments[$index])
        }
    }

    $directories = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    foreach ($directory in $executables.Keys) {
        if (-not $unityPlayer.Contains($directory) -or -not $dataDirectories.ContainsKey($directory)) { continue }
        foreach ($executable in $executables[$directory]) {
            $expectedData = [IO.Path]::GetFileNameWithoutExtension($executable) + '_Data'
            if ($dataDirectories[$directory].Contains($expectedData)) { [void]$directories.Add($directory) }
        }
    }
    return @($directories)
}

function Test-PathInDirectory {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Path,
        [AllowEmptyString()][string]$Directory
    )

    if ([string]::IsNullOrEmpty($Directory)) { return $true }
    return (Test-OrdinalEqual $Path $Directory) -or
        $Path.StartsWith($Directory.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase)
}

function Get-ContentViolations {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][Collections.Generic.Dictionary[string, object]]$Analyses,
        [string]$Scope = ''
    )

    $candidates = [Collections.Generic.List[object]]::new()
    foreach ($record in $Records) {
        $analysis = if ($record.PSObject.Properties.Name -contains 'Analysis') {
            $record.Analysis
        }
        elseif ($Analyses.ContainsKey($record.ObjectId)) {
            $Analyses[$record.ObjectId].Evidence
        }
        else {
            continue
        }
        $path = ConvertTo-GuardPath $record.Path
        if (-not [string]::IsNullOrEmpty($analysis.UnityBinaryKind)) {
            $candidates.Add([pscustomobject]@{ Path = $path; Kind = $analysis.UnityBinaryKind; Individual = $true })
        }
        if ($analysis.IsUnitySerialized) {
            $candidates.Add([pscustomobject]@{ Path = $path; Kind = 'SerializedSupport'; Individual = $false })
        }

        $segments = @($path.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
        if ($segments.Count -lt 2) { continue }
        for ($index = 0; $index -lt ($segments.Count - 1); $index++) {
            if (-not $segments[$index].EndsWith('_Data', [StringComparison]::OrdinalIgnoreCase)) { continue }
            $name = $segments[-1]
            $relative = @($segments[($index + 1)..($segments.Count - 1)])
            $isDataEvidence =
                ($analysis.IsUnitySerialized -and
                    ((Test-OrdinalEqual $name 'globalgamemanagers') -or (Test-OrdinalEqual $name 'resources.assets'))) -or
                ($analysis.IsUnityBootConfig -and (Test-OrdinalEqual $name 'boot.config')) -or
                ($analysis.IsManagedAssembly -and $relative.Count -ge 2 -and (Test-OrdinalEqual $relative[0] 'Managed'))
            if ($isDataEvidence) {
                $candidates.Add([pscustomobject]@{ Path = $path; Kind = 'PlayerData'; Individual = $true })
                break
            }
        }
    }

    $paths = @($Records | ForEach-Object { ConvertTo-GuardPath $_.Path })
    $pathBundleDirectories = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    foreach ($directory in @(Get-PathBundleDirectories $paths)) { [void]$pathBundleDirectories.Add($directory) }
    $suppressedDirectories = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    foreach ($directory in $pathBundleDirectories) { [void]$suppressedDirectories.Add($directory) }

    $ancestorTypes = [Collections.Generic.Dictionary[string, Collections.Generic.HashSet[string]]]::new(
        $script:OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        $slash = $candidate.Path.LastIndexOf('/')
        $directory = if ($slash -ge 0) { $candidate.Path.Substring(0, $slash) } else { '' }
        while ($true) {
            if (-not $ancestorTypes.ContainsKey($directory)) {
                $ancestorTypes[$directory] = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
            }
            $groupKind = if ($candidate.Kind -in @('SerializedSupport', 'PlayerData')) { 'PlayerData' } else { $candidate.Kind }
            [void]$ancestorTypes[$directory].Add($groupKind)
            if ([string]::IsNullOrEmpty($directory)) { break }
            $parentSlash = $directory.LastIndexOf('/')
            $directory = if ($parentSlash -ge 0) { $directory.Substring(0, $parentSlash) } else { '' }
        }
    }

    $contentBundleDirectories = @($ancestorTypes.Keys | Where-Object {
        $types = $ancestorTypes[$_]
        $types.Contains('PlayerExecutable') -and $types.Contains('UnityPlayerDll') -and
        $types.Contains('CrashHandler') -and $types.Contains('PlayerData')
    } | Sort-Object Length -Descending)

    $violations = [Collections.Generic.List[object]]::new()
    $qualifyingContentDirectories = [Collections.Generic.List[string]]::new()
    foreach ($directory in $contentBundleDirectories) {
        $hasQualifiedDescendant = $false
        foreach ($descendant in $qualifyingContentDirectories) {
            if (Test-PathInDirectory $descendant $directory) { $hasQualifiedDescendant = $true; break }
        }
        if ($hasQualifiedDescendant) { continue }
        $qualifyingContentDirectories.Add($directory)
        $alreadyCovered = $false
        foreach ($covered in $suppressedDirectories) {
            if (Test-PathInDirectory $directory $covered) { $alreadyCovered = $true; break }
        }
        if ($alreadyCovered) { continue }
        [void]$suppressedDirectories.Add($directory)
        $bundlePath = if ($directory) { "$directory/" } else { './' }
        $display = if ($Scope) { "$Scope!/$bundlePath" } else { $bundlePath }
        $violations.Add([pscustomobject]@{
            Rule = 'unity-player-bundle-content'
            Path = $display
            Evidence = 'Blob identities include Unity Player EXE, UnityPlayer DLL, CrashHandler, and serialized Player data under one subtree.'
        })
    }

    $individualRules = @{
        PlayerExecutable = 'unity-player-executable-identity'
        UnityPlayerDll = 'unity-player-dll-identity'
        CrashHandler = 'unity-crash-handler-identity'
        PlayerData = 'unity-player-data-topology'
    }
    $keys = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        if (-not $candidate.Individual) { continue }
        $suppressed = $false
        foreach ($directory in $suppressedDirectories) {
            if (Test-PathInDirectory $candidate.Path $directory) { $suppressed = $true; break }
        }
        if ($suppressed) { continue }
        $rule = $individualRules[$candidate.Kind]
        $display = if ($Scope) { "$Scope!/$($candidate.Path)" } else { $candidate.Path }
        $key = $rule + [char]0 + $display
        if ($keys.Add($key)) {
            $violations.Add([pscustomobject]@{
                Rule = $rule
                Path = $display
                Evidence = "Git blob bytes match verified $($candidate.Kind) PE/data identity."
            })
        }
    }
    return @($violations)
}

function Invoke-CappedRawProcess {
    param(
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [long]$MaximumBytes = 536870912L
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$startInfo.ArgumentList.Add($argument) }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $memory = [IO.MemoryStream]::new()
    $started = $false
    try {
        if (-not $process.Start()) { throw "Could not start $FileName." }
        $started = $true
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $buffer = [byte[]]::new(65536)
        while (($read = $process.StandardOutput.BaseStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            if (($memory.Length + $read) -gt $MaximumBytes) {
                $process.Kill($true)
                throw "Archive member exceeds the $MaximumBytes-byte inspection limit."
            }
            $memory.Write($buffer, 0, $read)
        }
        $process.WaitForExit()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $memory.ToArray(); Stderr = $stderr }
    }
    finally {
        if ($started -and -not $process.HasExited) { $process.Kill($true) }
        $memory.Dispose()
        $process.Dispose()
    }
}

function Test-UnsafeArchiveEntry {
    param([Parameter(Mandatory)][string]$Path)

    $normalized = ConvertTo-GuardPath $Path
    if ($normalized.StartsWith('/', [StringComparison]::Ordinal) -or
        $normalized.StartsWith('//', [StringComparison]::Ordinal) -or
        [regex]::IsMatch($normalized, '^[A-Za-z]:/', $script:RegexOptions)) {
        return $true
    }
    return @($normalized.Split('/') | Where-Object { $_ -eq '..' }).Count -gt 0
}

function Get-ZipEntryRecords {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $memory = [IO.MemoryStream]::new($Bytes, $false)
    $archive = $null
    $records = [Collections.Generic.List[object]]::new()
    $totalSize = 0L
    try {
        $archive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Read, $false)
        if ($archive.Entries.Count -gt 100000) { throw 'ZIP has more than 100000 entries.' }
        foreach ($entry in $archive.Entries) {
            $path = ConvertTo-GuardPath $entry.FullName
            if ([string]::IsNullOrEmpty($path) -or $path.EndsWith('/', [StringComparison]::Ordinal)) { continue }
            $totalSize += $entry.Length
            if ($totalSize -gt 536870912L) { throw 'ZIP expanded content exceeds the 512 MiB inspection limit.' }
            $stream = $entry.Open()
            $entryMemory = [IO.MemoryStream]::new()
            try {
                $stream.CopyTo($entryMemory)
                if ($entryMemory.Length -ne $entry.Length) { throw "ZIP member '$path' was truncated." }
                $entryBytes = $entryMemory.ToArray()
            }
            finally {
                $entryMemory.Dispose()
                $stream.Dispose()
            }
            $records.Add([pscustomobject]@{ Path = $path; Analysis = Get-BlobEvidence $entryBytes })
        }
        return @($records)
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

function Get-ExternalArchiveEntryRecords {
    param(
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][ValidateSet('7z', 'rar')][string]$Kind
    )

    $lister = Find-ArchiveLister
    if ($null -eq $lister) {
        throw "No 7z/bsdtar-compatible archive inspector is available for '$Kind'."
    }

    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $tempName = 'fc-player-payload-guard-' + [guid]::NewGuid().ToString('N') + '.' + $Kind
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
            $entries = @([regex]::Matches($text, '(?m)^Path = (.*)\r?$') |
                ForEach-Object { ConvertTo-GuardPath $_.Groups[1].Value })
        }
        else {
            $result = Invoke-RawProcess $lister.Path @('-tf', $tempPath) $tempRoot
            if ($result.ExitCode -ne 0) {
                throw "bsdtar could not inspect the archive: $($result.Stderr.Trim())"
            }
            $text = ConvertFrom-ArchiveListingBytes $result.Stdout
            $entries = @($text -split '\r?\n' | Where-Object { $_ -ne '' } |
                ForEach-Object { ConvertTo-GuardPath $_ })
        }

        if ($entries.Count -gt 100000) { throw 'Archive has more than 100000 entries.' }
        $records = [Collections.Generic.List[object]]::new()
        $totalSize = 0L
        foreach ($entry in $entries) {
            if ([string]::IsNullOrEmpty($entry) -or $entry.EndsWith('/', [StringComparison]::Ordinal)) { continue }
            if (Test-UnsafeArchiveEntry $entry) { throw "Unsafe archive entry '$entry' cannot be extracted for inspection." }
            $arguments = if ($lister.Kind -eq '7zip') {
                @('x', '-so', '--', $tempPath, $entry)
            }
            else {
                @('-xOf', $tempPath, $entry)
            }
            $member = Invoke-CappedRawProcess $lister.Path $arguments $tempRoot (536870912L - $totalSize)
            if ($member.ExitCode -ne 0) {
                throw "Could not inspect archive member '$entry': $($member.Stderr.Trim())"
            }
            $totalSize += $member.Stdout.LongLength
            $records.Add([pscustomobject]@{ Path = $entry; Analysis = Get-BlobEvidence $member.Stdout })
        }
        return @($records)
    }
    finally {
        if (Test-Path -LiteralPath $tempPath -PathType Leaf) {
            [IO.File]::Delete($tempPath)
        }
    }
}

function Get-ArchiveEntryRecords {
    param(
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][ValidateSet('zip', '7z', 'rar')][string]$Kind
    )

    if ($Kind -eq 'zip') { return @(Get-ZipEntryRecords $Bytes) }
    return @(Get-ExternalArchiveEntryRecords $Bytes $Kind)
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

    $trackedRecords = @(Get-TrackedRecords $resolvedRoot)
    $trackedPaths = @($trackedRecords | ForEach-Object { $_.Path })
    $analyses = Get-GitBlobAnalyses $resolvedRoot $trackedRecords
    $violations = [Collections.Generic.List[object]]::new()
    $violationKeys = [Collections.Generic.HashSet[string]]::new($script:OrdinalIgnoreCase)
    foreach ($violation in @(Get-PathViolations $trackedPaths)) {
        Add-GuardViolation $violations $violationKeys $violation.Rule $violation.Path $violation.Evidence
    }
    foreach ($violation in @(Get-ContentViolations $trackedRecords $analyses)) {
        Add-GuardViolation $violations $violationKeys $violation.Rule $violation.Path $violation.Evidence
    }

    $archives = @($trackedRecords | Where-Object {
        $analysis = $analyses[$_.ObjectId].Evidence
        (-not [string]::IsNullOrEmpty($analysis.ArchiveKind)) -or
            $script:ArchiveExtensions.Contains([IO.Path]::GetExtension($_.Path))
    })
    foreach ($archive in $archives) {
        $archivePath = $archive.Path
        # A known product archive is already rejected by path. Content cannot make it safer.
        if (Test-KnownPlayerArchiveName $archivePath) { continue }
        try {
            $stored = $analyses[$archive.ObjectId]
            if ($null -eq $stored.Bytes) { throw "Archive blob bytes were not retained for '$archivePath'." }
            $kind = $stored.Evidence.ArchiveKind
            if ([string]::IsNullOrEmpty($kind)) {
                $kind = [IO.Path]::GetExtension($archivePath).TrimStart('.').ToLowerInvariant()
            }
            $entries = @(Get-ArchiveEntryRecords $stored.Bytes $kind)
            foreach ($entry in $entries) {
                if (Test-UnsafeArchiveEntry $entry.Path) {
                    Add-GuardViolation $violations $violationKeys 'unsafe-archive-entry' "$archivePath!/$($entry.Path)" 'Archive entry is absolute or traverses a parent directory.'
                }
            }
            foreach ($violation in @(Get-PathViolations @($entries | ForEach-Object { $_.Path }) $archivePath)) {
                Add-GuardViolation $violations $violationKeys $violation.Rule $violation.Path $violation.Evidence
            }
            $emptyAnalyses = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
            foreach ($violation in @(Get-ContentViolations $entries $emptyAnalyses $archivePath)) {
                Add-GuardViolation $violations $violationKeys $violation.Rule $violation.Path $violation.Evidence
            }
        }
        catch {
            Add-GuardViolation $violations $violationKeys 'archive-inspection-failed' $archivePath $_.Exception.Message
        }
    }

    return [pscustomobject]@{
        Root = $resolvedRoot
        TrackedCount = $trackedPaths.Count
        ArchiveCount = $archives.Count
        Violations = @($violations)
    }
}

function Get-FixtureFullPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$RelativePath)

    $platformPath = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $fullPath = [IO.Path]::GetFullPath((Join-Path $Root $platformPath))
    $rootPrefix = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture escaped its root: $fullPath"
    }
    return $fullPath
}

function Write-FixtureBytes {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][byte[]]$Bytes
    )

    $fullPath = Get-FixtureFullPath $Root $RelativePath
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullPath))
    [IO.File]::WriteAllBytes($fullPath, $Bytes)
}

function Get-FixtureEntryBytes {
    param([Parameter(Mandatory)][hashtable]$Entry)
    if ($Entry.ContainsKey('Bytes')) { return [byte[]]$Entry.Bytes }
    return [Text.Encoding]::UTF8.GetBytes([string]$Entry.Content)
}

function New-FaithfulPeFixture {
    param([Parameter(Mandatory)][ValidateSet('PlayerExecutable', 'UnityPlayerDll', 'CrashHandler', 'ManagedPlugin')][string]$Kind)

    $bytes = [byte[]]::new(4096)
    $bytes[0] = 0x4d; $bytes[1] = 0x5a
    $bytes[0x3c] = 0x80
    $bytes[0x80] = 0x50; $bytes[0x81] = 0x45
    $characteristics = if ($Kind -in @('UnityPlayerDll', 'ManagedPlugin')) { 0x2022 } else { 0x0022 }
    $bytes[0x96] = [byte]($characteristics -band 0xff)
    $bytes[0x97] = [byte](($characteristics -shr 8) -band 0xff)
    $markers = switch ($Kind) {
        'PlayerExecutable' { @('UnityPlayer.dll', 'UnityMain2', 'UnityTechnologies.Unity.UnityPlayer') }
        'UnityPlayerDll' { @('UnityPlayer.dll', 'UnityMain2', 'Unity Technologies') }
        'CrashHandler' { @('UnityCrashHandler', 'Unity Crash Handler', 'Unity Technologies') }
        'ManagedPlugin' { @('BSJB', 'Legitimate.Source.Plugin') }
    }
    $offset = 512
    foreach ($marker in $markers) {
        $markerBytes = [Text.Encoding]::ASCII.GetBytes($marker)
        [Array]::Copy($markerBytes, 0, $bytes, $offset, $markerBytes.Length)
        $offset += $markerBytes.Length + 32
    }
    return $bytes
}

function Set-FixtureUInt64BigEndian {
    param([byte[]]$Bytes, [int]$Offset, [uint64]$Value)
    for ($index = 7; $index -ge 0; $index--) {
        $Bytes[$Offset + $index] = [byte]($Value -band 0xff)
        $Value = $Value -shr 8
    }
}

function Set-FixtureUInt32BigEndian {
    param([byte[]]$Bytes, [int]$Offset, [uint32]$Value)
    for ($index = 3; $index -ge 0; $index--) {
        $Bytes[$Offset + $index] = [byte]($Value -band 0xff)
        $Value = $Value -shr 8
    }
}

function New-FaithfulSerializedFixture {
    $bytes = [byte[]]::new(4096)
    $bytes[8] = 0; $bytes[9] = 0; $bytes[10] = 0; $bytes[11] = 22
    Set-FixtureUInt32BigEndian $bytes 20 64
    Set-FixtureUInt64BigEndian $bytes 24 ([uint64]$bytes.Length)
    Set-FixtureUInt64BigEndian $bytes 32 512
    $version = [Text.Encoding]::ASCII.GetBytes("6000.3.21f1`0")
    [Array]::Copy($version, 0, $bytes, 52, $version.Length)
    return $bytes
}

function Write-FixtureZip {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][hashtable[]]$Entries
    )

    $fullPath = Get-FixtureFullPath $Root $RelativePath
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullPath))
    $archive = [IO.Compression.ZipFile]::Open($fullPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entrySpec in $Entries) {
            $entry = $archive.CreateEntry($entrySpec.Path, [IO.Compression.CompressionLevel]::NoCompression)
            $stream = $entry.Open()
            try {
                $entryBytes = Get-FixtureEntryBytes $entrySpec
                $stream.Write($entryBytes, 0, $entryBytes.Length)
            }
            finally { $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Write-Fixture7Zip {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][hashtable[]]$Entries
    )

    $lister = Find-ArchiveLister
    if ($null -eq $lister) { throw 'No 7z/bsdtar-compatible program is available for the 7z self-test.' }
    $sourceRoot = Get-FixtureFullPath $Root ('.archive-source-' + [guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($sourceRoot)
    $destination = Get-FixtureFullPath $Root $RelativePath
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    $creating = $destination + '.creating.7z'
    try {
        foreach ($entry in $Entries) { Write-FixtureBytes $sourceRoot $entry.Path (Get-FixtureEntryBytes $entry) }
        $result = if ($lister.Kind -eq '7zip') {
            Invoke-RawProcess $lister.Path @('a', '-t7z', '-mx=0', '--', $creating, '*') $sourceRoot
        }
        else {
            Invoke-RawProcess $lister.Path @('-a', '-cf', $creating, '-C', $sourceRoot, '.') $sourceRoot
        }
        if ($result.ExitCode -ne 0) { throw "Could not create 7z fixture: $($result.Stderr.Trim())" }
        if ((Get-ArchiveMagicKind ([IO.File]::ReadAllBytes($creating))) -ne '7z') {
            throw 'Self-test archive creator did not produce 7z magic.'
        }
        [IO.File]::Move($creating, $destination, $true)
    }
    finally {
        if (Test-Path -LiteralPath $creating -PathType Leaf) { [IO.File]::Delete($creating) }
        if (Test-Path -LiteralPath $sourceRoot -PathType Container) { [IO.Directory]::Delete($sourceRoot, $true) }
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
        [AllowEmptyCollection()][hashtable[]]$SevenZips = @(),
        [string]$ExpectedRule = ''
    )

    $root = Join-Path $Parent $Name
    [void][IO.Directory]::CreateDirectory($root)
    & git -C $root init --quiet 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git init failed for fixture '$Name'." }

    Write-FixtureBytes $root '.gitignore' ([Text.Encoding]::UTF8.GetBytes("/Builds/`n"))
    Add-FixturePaths $root @('.gitignore')
    $baseline = Invoke-GuardScan $root
    if ($baseline.Violations.Count -ne 0) { throw "Fixture '$Name' baseline should pass." }

    $paths = [Collections.Generic.List[string]]::new()
    foreach ($file in $Files) {
        $bytes = if ($file.ContainsKey('Bytes')) { [byte[]]$file.Bytes } else { [Text.Encoding]::UTF8.GetBytes([string]$file.Content) }
        Write-FixtureBytes $root $file.Path $bytes
        $paths.Add($file.Path)
    }
    foreach ($zip in $Zips) {
        Write-FixtureZip $root $zip.Path $zip.Entries
        $paths.Add($zip.Path)
    }
    foreach ($sevenZip in $SevenZips) {
        Write-Fixture7Zip $root $sevenZip.Path $sevenZip.Entries
        $paths.Add($sevenZip.Path)
    }
    Add-FixturePaths $root @($paths)

    $target = Invoke-GuardScan $root
    if ([string]::IsNullOrEmpty($ExpectedRule)) {
        if ($target.Violations.Count -ne 0) {
            $summary = @($target.Violations | ForEach-Object { "$($_.Rule):$($_.Path):$($_.Evidence)" }) -join ', '
            throw "Fixture '$Name' should pass but failed: $summary"
        }
    }
    else {
        if ($target.Violations.Count -ne 1 -or
            -not (Test-OrdinalEqual $target.Violations[0].Rule $ExpectedRule)) {
            $summary = @($target.Violations | ForEach-Object { "$($_.Rule):$($_.Path):$($_.Evidence)" }) -join ', '
            throw "Fixture '$Name' expected sole rule '$ExpectedRule' but got: $summary"
        }
    }

    & git -C $root --literal-pathspecs rm --cached --quiet -- @($paths) 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Fixture '$Name' could not restore its index." }
    $restore = Invoke-GuardScan $root
    if ($restore.Violations.Count -ne 0) { throw "Fixture '$Name' restore should pass." }
    return [pscustomobject]@{ Name = $Name; Rule = if ($ExpectedRule) { $ExpectedRule } else { 'allow' }; Baseline = 'PASS'; Target = 'PASS'; Restore = 'PASS' }
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
        $player = New-FaithfulPeFixture PlayerExecutable
        $unityPlayer = New-FaithfulPeFixture UnityPlayerDll
        $crash = New-FaithfulPeFixture CrashHandler
        $serialized = New-FaithfulSerializedFixture
        $bundleEntries = @(
            @{ Path = '한글 Bundle/Moon.exe'; Bytes = $player },
            @{ Path = '한글 Bundle/UnityPlayer.dll'; Bytes = $unityPlayer },
            @{ Path = '한글 Bundle/UnityCrashHandler64.exe'; Bytes = $crash },
            @{ Path = '한글 Bundle/Moon_Data/globalgamemanagers'; Bytes = $serialized }
        )

        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'legal-content-matrix' -Files @(
            @{ Path = 'Packages/manifest.json'; Content = '{"dependencies":{}}' },
            @{ Path = 'Assets/Scripts/FamilyCompany.cs'; Content = 'class FamilyCompany {}' },
            @{ Path = 'Assets/Plugins/AMDUnityPlugin.dll'; Bytes = (New-FaithfulPeFixture ManagedPlugin) },
            @{ Path = 'Assets/DGGL/Plugins/DGGL.Runtime.dll'; Bytes = (New-FaithfulPeFixture ManagedPlugin) },
            @{ Path = 'Assets/Art/FamilyCompany_DataSheet.png'; Bytes = [byte[]](0x89,0x50,0x4e,0x47) },
            @{ Path = 'Assets/Fonts/source.ttf'; Content = 'font source' },
            @{ Path = 'Assets/Audio/source.ogg'; Content = 'audio source' },
            @{ Path = '-@특수/한글 공백/UnityPlayer.dll'; Content = 'source filename only' },
            @{ Path = 'Assets/Generated/Sample_Data/readme.txt'; Content = 'ordinary data directory' }
        ) -Zips @(
            @{ Path = 'Docs/CompanyResearch.zip'; Entries = @(
                @{ Path = 'Sources/Company.cs'; Content = 'class Company {}' },
                @{ Path = 'Plugins/Legit.dll'; Bytes = (New-FaithfulPeFixture ManagedPlugin) }) }
        ) -SevenZips @(
            @{ Path = 'Docs/FamilyCompany_Source.7z'; Entries = @(
                @{ Path = 'Sources/FamilyCompany.cs'; Content = 'class FamilyCompany {}' }) }
        )))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'forced-add-ignored-known-build-root' -Files @(
            @{ Path = 'Builds/Windows/ignored/marker.txt'; Content = 'forced ignored target' }
        ) -ExpectedRule 'known-deployment-root'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-standalone-player-exe' -Files @(
            @{ Path = 'drop/renamed.payload'; Bytes = $player }
        ) -ExpectedRule 'unity-player-executable-identity'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-standalone-unityplayer-mixed-case' -Files @(
            @{ Path = 'drop/한글 Space/uNiTyPlAyEr.DlL'; Bytes = $unityPlayer }
        ) -ExpectedRule 'unity-player-dll-identity'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-standalone-crashhandler' -Files @(
            @{ Path = 'drop/helper.bin'; Bytes = $crash }
        ) -ExpectedRule 'unity-crash-handler-identity'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-standalone-player-data-directory' -Files @(
            @{ Path = 'drop/한글 Space/Moon_Data/globalgamemanagers'; Bytes = $serialized }
        ) -ExpectedRule 'unity-player-data-topology'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-renamed-unpacked-conventional-topology' -Files @(
            @{ Path = 'payload/Moon.exe'; Bytes = $player },
            @{ Path = 'payload/UnityPlayer.dll'; Bytes = $unityPlayer },
            @{ Path = 'payload/UnityCrashHandler64.exe'; Bytes = $crash },
            @{ Path = 'payload/Moon_Data/globalgamemanagers'; Bytes = $serialized }
        ) -ExpectedRule 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-surface-renamed-unpacked' -Files @(
            @{ Path = 'payload/Moon.bin'; Bytes = $player },
            @{ Path = 'payload/Engine.dat'; Bytes = $unityPlayer },
            @{ Path = 'payload/Helper.payload'; Bytes = $crash },
            @{ Path = 'payload/Cache/content.bin'; Bytes = $serialized }
        ) -ExpectedRule 'unity-player-bundle-content'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-renamed-zip-bundle' -Files @() -Zips @(
            @{ Path = 'archives/자료 묶음.zip'; Entries = $bundleEntries }
        ) -ExpectedRule 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-zip-renamed-bin' -Files @() -Zips @(
            @{ Path = 'archives/자료 묶음.bin'; Entries = $bundleEntries }
        ) -ExpectedRule 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-renamed-7z-bundle' -Files @() -SevenZips @(
            @{ Path = 'archives/자료 7z 묶음.7z'; Entries = $bundleEntries }
        ) -ExpectedRule 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'actual-7z-renamed-bin' -Files @() -SevenZips @(
            @{ Path = 'archives/자료 7z 묶음.bin'; Entries = $bundleEntries }
        ) -ExpectedRule 'renamed-unity-player-bundle'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'malformed-uninspectable-rar' -Files @(
            @{ Path = 'archives/uninspectable.rar'; Content = 'not a RAR container' }
        ) -ExpectedRule 'archive-inspection-failed'))
        $results.Add((Test-GuardFixture -Parent $testRoot -Name 'nul-delimited-space-korean-case-known-exe' -Files @(
            @{ Path = '-@특수/한글 공백/FAMILYCOMPANY.EXE'; Content = 'path transport fixture' }
        ) -ExpectedRule 'known-player-executable'))

        Write-Output "PLAYER_PAYLOAD_GUARD SELFTEST PASS fixtures=$($results.Count) baselinePass=$($results.Count) targetPass=$($results.Count) restorePass=$($results.Count) forcedAddFixtures=$($results.Count)"
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
