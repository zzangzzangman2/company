# Shared by the launcher, publisher and isolated regression tests. Windows PowerShell 5.1+.
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
# The GUI worker must not depend on inherited PowerShell module auto-loading preferences.
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$script:PatchRepository = 'zzangzzangman2/company'
$script:PatchProduct = 'family-company-windows-v1'
$script:PatchProgressSink = $null
$script:PatchCancellationCheck = $null

function Test-PatchCancellation {
    if ($script:PatchCancellationCheck -and (& $script:PatchCancellationCheck)) {
        throw [OperationCanceledException]::new('Update cancelled. No incomplete installation will be started.')
    }
}

function Send-PatchProgress([string]$Phase, [string]$Detail = '', [long]$Done = 0, [long]$Total = 0) {
    Test-PatchCancellation
    if ($Done -lt 0 -or $Total -lt 0 -or ($Total -gt 0 -and $Done -gt $Total)) { throw 'Invalid progress measurement.' }
    if ($script:PatchProgressSink) {
        # Percent is phase-specific. Unknown work is indeterminate, never a timer-generated percentage.
        & $script:PatchProgressSink ([ordered]@{schemaVersion=1; phase=$Phase; detail=$Detail;
            done=$Done; total=$Total; percent=$(if ($Total -gt 0) { [Math]::Floor(1000.0*$Done/$Total)/10.0 } else { -1 })}) | Out-Null
    }
}

function Copy-PatchStream($InputStream, $OutputStream, [long]$ExpectedSize, [scriptblock]$OnBytes) {
    $buffer = New-Object byte[] 131072
    [long]$received = 0
    $clock = [Diagnostics.Stopwatch]::StartNew()
    if ($OnBytes) { & $OnBytes $received | Out-Null }
    while (($count = $InputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
        Test-PatchCancellation
        $received += $count
        if ($received -gt $ExpectedSize) { throw 'Download exceeds declared size.' }
        $OutputStream.Write($buffer, 0, $count)
        if ($OnBytes -and ($clock.ElapsedMilliseconds -ge 100 -or $received -eq $ExpectedSize)) {
            & $OnBytes $received | Out-Null
            $clock.Restart()
        }
    }
    if ($received -ne $ExpectedSize) { throw 'Truncated download.' }
    $OutputStream.Flush()
}

function Get-PatchHash([string]$Path) {
    # Hashing must also work in the 32-bit Windows PowerShell selected by an AnyCPU GUI.
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hasher.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $hasher.Dispose(); $stream.Dispose() }
}

function Assert-PatchNoReparse([string]$Path) {
    $node = [IO.Path]::GetFullPath($Path)
    while ($node) {
        if (Test-Path -LiteralPath $node) {
            if ((Get-Item -LiteralPath $node -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Updater refuses junction/symlink: $node"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($node)
        if ($parent -eq $node) { break }
        $node = $parent
    }
}

function Resolve-PatchChild([string]$Root, [string]$Relative) {
    if (!$Relative -or $Relative.Length -gt 220 -or $Relative -match '[\\:\x00-\x1f<>"|?*]' -or
        $Relative.StartsWith('/') -or $Relative.EndsWith('/')) { throw 'Unsafe patch path.' }
    foreach ($part in $Relative.Split('/')) {
        if (!$part -or $part -eq '.' -or $part -eq '..' -or $part -match '[. ]$' -or
            $part -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') { throw 'Unsafe Windows path component.' }
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $result = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$result.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Patch path escaped its store.'
    }
    Assert-PatchNoReparse $result
    return $result
}

function Write-PatchJsonAtomic([string]$Path, $Value) {
    $temporary = $Path + '.writing-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($temporary, $Path, [NullString]::Value) }
    else { [IO.File]::Move($temporary, $Path) }
}

function Read-PatchManifest([string]$Path) {
    if ((Get-Item -LiteralPath $Path).Length -gt 4MB) { throw 'Oversized manifest.' }
    $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne $script:PatchProduct -or
        $manifest.repository -ne $script:PatchRepository -or $manifest.entryPoint -cne 'FamilyCompany.exe' -or
        $manifest.version -notmatch '^fc-win-[0-9]{8}\.[0-9]+$' -or
        $manifest.commit -notmatch '^[0-9a-f]{40}$' -or [long]$manifest.sequence -le 0 -or
        $manifest.eligibility -ne 'verified-release') { throw 'Unsupported or unverified patch manifest.' }
    $files = @($manifest.files)
    if ($files.Count -lt 3 -or $files.Count -gt 10000) { throw 'Invalid manifest file count.' }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    foreach ($file in $files) {
        [void](Resolve-PatchChild ([IO.Path]::GetDirectoryName($Path)) ([string]$file.path))
        if (!$names.Add([string]$file.path) -or $file.sha256 -notmatch '^[0-9a-f]{64}$' -or
            [long]$file.size -lt 0 -or [long]$file.size -gt 2GB -or [long]$file.packedSize -lt 1 -or
            [long]$file.packedSize -gt 2GB -or $file.packedSha256 -notmatch '^[0-9a-f]{64}$' -or
            $file.assetTag -notmatch '^fc-win-[0-9]{8}\.[0-9]+$' -or
            $file.assetName -cne ($file.sha256 + '.gz')) { throw 'Invalid, duplicate or unbounded file entry.' }
        $total += [long]$file.size
    }
    if ($total -gt 8GB -or !$names.Contains('FamilyCompany.exe') -or !$names.Contains('UnityPlayer.dll') -or
        !($files | Where-Object { $_.path.StartsWith('FamilyCompany_Data/', [StringComparison]::Ordinal) })) {
        throw 'Incomplete or oversized game payload.'
    }
    # File/parent collisions cannot be safely materialized on Windows.
    foreach ($name in $names) {
        $parts = $name.Split('/')
        for ($i = 1; $i -lt $parts.Length; $i++) {
            if ($names.Contains(($parts[0..($i-1)] -join '/'))) { throw 'File/directory collision.' }
        }
    }
    return $manifest
}

function Receive-PatchFile([string]$Url, [string]$Output, [long]$ExpectedSize, [string]$ExpectedHash, [scriptblock]$OnBytes) {
    if ($Url -notmatch '^https://github\.com/zzangzzangman2/company/releases/download/fc-win-[0-9]{8}\.[0-9]+/(?:[0-9a-f]{64}\.gz|family-company-manifest\.json)$') {
        throw 'Download is not a pinned company GitHub release asset.'
    }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Test-PatchCancellation
    $request = [Net.HttpWebRequest]::Create($Url)
    $request.UserAgent = 'FamilyCompany-Updater/2'
    $request.Timeout = 30000
    $request.ReadWriteTimeout = 30000
    $response = $null; $inputStream = $null; $outputStream = $null
    try {
        $response = $request.GetResponse()
        if ($response.ContentLength -ge 0 -and $response.ContentLength -ne $ExpectedSize) { throw 'Download content length mismatch.' }
        $inputStream = $response.GetResponseStream()
        $outputStream = [IO.File]::Open($Output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        Copy-PatchStream $inputStream $outputStream $ExpectedSize $OnBytes
    } finally {
        if ($outputStream) { $outputStream.Dispose() }
        if ($inputStream) { $inputStream.Dispose() }
        if ($response) { $response.Dispose() }
    }
    if ((Get-Item -LiteralPath $Output).Length -ne $ExpectedSize -or (Get-PatchHash $Output) -cne $ExpectedHash) {
        throw 'Downloaded release asset failed size/SHA-256 verification.'
    }
}

function Expand-PatchFile([string]$Packed, [string]$Output, [long]$ExpectedSize, [string]$ExpectedHash) {
    $patchInput = [IO.File]::OpenRead($Packed)
    $gzip = [IO.Compression.GZipStream]::new($patchInput, [IO.Compression.CompressionMode]::Decompress)
    $destination = [IO.File]::Open($Output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $buffer = New-Object byte[] 131072
        [long]$written = 0
        while (($count = $gzip.Read($buffer, 0, $buffer.Length)) -gt 0) {
            Test-PatchCancellation
            $written += $count
            if ($written -gt $ExpectedSize) { throw 'Expanded file exceeds its declared size.' }
            $destination.Write($buffer, 0, $count)
        }
        if ($written -ne $ExpectedSize) { throw 'Truncated expanded file.' }
        $destination.Flush($true)
    } finally { $destination.Dispose(); $gzip.Dispose(); $patchInput.Dispose() }
    if ((Get-PatchHash $Output) -cne $ExpectedHash) { throw 'Expanded file SHA-256 mismatch.' }
}

function Assert-PatchInstalled([string]$Directory, $Manifest) {
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$allowed.Add('family-company-manifest.json')
    [long]$verified = 0
    [long]$totalSize = ($Manifest.files | Measure-Object -Property size -Sum).Sum
    Send-PatchProgress 'verify' '' 0 $totalSize
    foreach ($file in $Manifest.files) {
        Test-PatchCancellation
        [void]$allowed.Add($file.path)
        $path = Resolve-PatchChild $Directory $file.path
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $file.size -or
            (Get-PatchHash $path) -cne $file.sha256) { throw "Installed file missing/corrupted: $($file.path)" }
        $verified += [long]$file.size
        Send-PatchProgress 'verify' $file.path $verified $totalSize
    }
    foreach ($item in Get-ChildItem -LiteralPath $Directory -Recurse -Force) {
        Assert-PatchNoReparse $item.FullName
        if (!$item.PSIsContainer) {
            $relative = $item.FullName.Substring($Directory.TrimEnd('\','/').Length + 1).Replace('\','/')
            if (!$allowed.Contains($relative)) { throw "Unexpected installed file: $relative" }
        }
    }
}

function Assert-PatchGameClosed([string]$Root) {
    foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
        try { $exe = $process.Path } catch { continue }
        if ($exe -and $exe.StartsWith($Root + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Close the running game before updating or launching another copy.'
        }
    }
}

function Initialize-PatchStore([string]$InstallRoot) {
    $root = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\','/')
    if ([IO.Path]::GetPathRoot($root).TrimEnd('\','/') -eq $root -or
        $root -eq [Environment]::GetFolderPath('UserProfile') -or $root -eq [Environment]::GetFolderPath('LocalApplicationData')) {
        throw 'A broad directory cannot be an install store.'
    }
    Assert-PatchNoReparse $root
    $marker = Join-Path $root '.family-company-updater.json'
    if (Test-Path -LiteralPath $root) {
        if (!(Test-Path -LiteralPath $marker) -and @(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) {
            throw 'Refusing to adopt a nonempty unrelated directory.'
        }
    } else { [void][IO.Directory]::CreateDirectory($root) }
    if (Test-Path -LiteralPath $marker) {
        if ((Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json).product -ne $script:PatchProduct) { throw 'Wrong store owner.' }
    } else { Write-PatchJsonAtomic $marker @{product=$script:PatchProduct; createdUtc=[DateTime]::UtcNow.ToString('o')} }
    return $root
}

function Get-PatchCurrent([string]$Root) {
    $pointer = Join-Path $Root 'current.json'
    if (!(Test-Path -LiteralPath $pointer)) { return $null }
    $state = Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json
    if ($state.directory -notmatch '^versions/[0-9]+-[0-9a-f]{12}$' -or $state.manifestSha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'Invalid current pointer.'
    }
    $directory = Resolve-PatchChild $Root $state.directory
    $manifestPath = Resolve-PatchChild $directory 'family-company-manifest.json'
    if ((Get-PatchHash $manifestPath) -cne $state.manifestSha256) { throw 'Current manifest was modified.' }
    $manifest = Read-PatchManifest $manifestPath
    return [pscustomobject]@{ Directory=$directory; Manifest=$manifest; Hash=$state.manifestSha256 }
}

function Install-CompanyPatch {
    param([string]$InstallRoot, [string]$ManifestPath, [string]$ExpectedManifestHash, [string]$LocalFeed = '',
        [switch]$PrepareOnly, [string]$SeedDirectory = '')
    $root = Initialize-PatchStore $InstallRoot
    $lock = [IO.File]::Open((Join-Path $root 'update.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $stage = $null
    try {
        if ((Get-PatchHash $ManifestPath) -cne $ExpectedManifestHash) { throw 'Manifest integrity mismatch.' }
        $manifest = Read-PatchManifest $ManifestPath
        $current = Get-PatchCurrent $root
        if ($current -and [long]$manifest.sequence -lt [long]$current.Manifest.sequence) { throw 'Downgrade rejected.' }
        if ($current -and [long]$manifest.sequence -eq [long]$current.Manifest.sequence -and $ExpectedManifestHash -cne $current.Hash) {
            throw 'Published version was mutated; refusing same-version replacement.'
        }
        # No file belonging to a running game is changed and no process is forcibly terminated.
        if (!$PrepareOnly) { Assert-PatchGameClosed $root }
        if ($SeedDirectory) { Assert-PatchNoReparse $SeedDirectory }
        if ($current -and $current.Hash -ceq $ExpectedManifestHash) {
            Assert-PatchInstalled $current.Directory $manifest
            return [pscustomobject]@{Status='current'; Directory=$current.Directory; DownloadedFiles=0; ReusedFiles=@($manifest.files).Count; DownloadedBytes=0}
        }
        $versionRelative = 'versions/' + $manifest.sequence + '-' + $ExpectedManifestHash.Substring(0,12)
        $version = Resolve-PatchChild $root $versionRelative
        if (Test-Path -LiteralPath $version) {
            # A crash after the directory rename but before pointer activation is recoverable.
            if ((Get-PatchHash (Resolve-PatchChild $version 'family-company-manifest.json')) -cne $ExpectedManifestHash) {
                throw 'Interrupted version manifest does not match.'
            }
            Assert-PatchInstalled $version $manifest
            if ($PrepareOnly) {
                return [pscustomobject]@{Status='prepared'; Directory=$version; DownloadedFiles=0; ReusedFiles=@($manifest.files).Count; DownloadedBytes=0}
            }
            Write-PatchJsonAtomic (Join-Path $root 'current.json') @{directory=$versionRelative; manifestSha256=$ExpectedManifestHash; activatedUtc=[DateTime]::UtcNow.ToString('o')}
            return [pscustomobject]@{Status='recovered'; Directory=$version; DownloadedFiles=0; ReusedFiles=@($manifest.files).Count; DownloadedBytes=0}
        }
        $stageRelative = 'staging/' + [Guid]::NewGuid().ToString('N')
        $stage = Resolve-PatchChild $root $stageRelative
        [void][IO.Directory]::CreateDirectory($stage)
        [int]$downloaded = 0; [int]$reused = 0; [long]$bytes = 0
        [long]$downloadTotal = 0
        $reuse = @{}
        foreach ($file in $manifest.files) {
            Send-PatchProgress 'check-files' $file.path
            $old = if ($current) { Resolve-PatchChild $current.Directory $file.path } elseif ($SeedDirectory) { Resolve-PatchChild $SeedDirectory $file.path } else { $null }
            $canReuse = $old -and (Test-Path -LiteralPath $old -PathType Leaf) -and (Get-Item -LiteralPath $old).Length -eq $file.size -and
                (Get-PatchHash $old) -ceq $file.sha256
            if ($canReuse) { $reuse[$file.path] = $old }
            else { $downloadTotal += [long]$file.packedSize }
        }
        Send-PatchProgress 'download' '' 0 $downloadTotal
        foreach ($file in $manifest.files) {
            $output = Resolve-PatchChild $stage $file.path
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
            if ($reuse.ContainsKey($file.path)) {
                Send-PatchProgress 'reuse' $file.path
                [IO.File]::Copy($reuse[$file.path], $output, $false); $reused++; continue
            }
            $packed = Join-Path $stage ('.download-' + [Guid]::NewGuid().ToString('N'))
            $onBytes = { param([long]$received) Send-PatchProgress 'download' $file.path ($bytes + $received) $downloadTotal }
            if ($LocalFeed) {
                $source = Resolve-PatchChild $LocalFeed ($file.assetTag + '/' + $file.assetName)
                $localInput = [IO.File]::OpenRead($source)
                $localOutput = [IO.File]::Open($packed, [IO.FileMode]::CreateNew)
                try { Copy-PatchStream $localInput $localOutput $file.packedSize $onBytes }
                finally { $localInput.Dispose(); $localOutput.Dispose() }
                if ((Get-Item -LiteralPath $packed).Length -ne $file.packedSize -or (Get-PatchHash $packed) -cne $file.packedSha256) { throw 'Local asset hash mismatch.' }
            } else {
                Receive-PatchFile "https://github.com/$script:PatchRepository/releases/download/$($file.assetTag)/$($file.assetName)" $packed $file.packedSize $file.packedSha256 $onBytes
            }
            Send-PatchProgress 'expand' $file.path
            Expand-PatchFile $packed $output $file.size $file.sha256
            Remove-Item -LiteralPath $packed
            $downloaded++; $bytes += [long]$file.packedSize
            Write-Host "[PATCH] $downloaded downloaded, $reused unchanged: $($file.path)"
        }
        Assert-PatchInstalled $stage $manifest
        [IO.File]::Copy($ManifestPath, (Join-Path $stage 'family-company-manifest.json'), $false)
        Send-PatchProgress 'activate'
        [void][IO.Directory]::CreateDirectory((Join-Path $root 'versions'))
        if (Test-Path -LiteralPath $version) { throw 'Version directory already exists; inspect interrupted activation.' }
        [IO.Directory]::Move($stage, $version)
        $stage = $null
        if ($PrepareOnly) {
            # A running Unity player is never replaced. The restart helper activates only after exit.
            return [pscustomobject]@{Status='prepared'; Directory=$version; DownloadedFiles=$downloaded; ReusedFiles=$reused; DownloadedBytes=$bytes}
        }
        Write-PatchJsonAtomic (Join-Path $root 'current.json') @{directory=$versionRelative; manifestSha256=$ExpectedManifestHash; activatedUtc=[DateTime]::UtcNow.ToString('o')}
        return [pscustomobject]@{Status='updated'; Directory=$version; DownloadedFiles=$downloaded; ReusedFiles=$reused; DownloadedBytes=$bytes}
    } catch {
        $failure = $_
        # Preserve non-executable failure evidence before removing this invocation's partial payload.
        $evidence = Resolve-PatchChild $root ('evidence/' + [Guid]::NewGuid().ToString('N') + '.json')
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($evidence))
        $hashes = @()
        if ($stage -and (Test-Path -LiteralPath $stage)) {
            $hashes = @(Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
                @{path=$_.FullName.Substring($stage.Length+1); size=$_.Length; sha256=(Get-PatchHash $_.FullName)}
            })
        }
        Write-PatchJsonAtomic $evidence @{timeUtc=[DateTime]::UtcNow.ToString('o'); root=$root; staging=$stage;
            expectedManifestHash=$ExpectedManifestHash; error=$failure.Exception.Message; files=$hashes; currentUnchanged=$true}
        throw $failure
    } finally {
      try {
        if ($stage -and (Test-Path -LiteralPath $stage)) {
            # Only this invocation's validated, GUID-named staging child may be removed.
            $expected = Resolve-PatchChild $root $stageRelative
            if ($stage -cne $expected -or [IO.Path]::GetFileName($stage) -notmatch '^[0-9a-f]{32}$') { throw 'Staging cleanup fence failed.' }
            Remove-Item -LiteralPath $stage -Recurse -Force
            if (Test-Path -LiteralPath $stage) { throw 'Partial payload removal failed.' }
        }
      } finally { $lock.Dispose() }
    }
}
