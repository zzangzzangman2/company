[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$testRoot = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) ('Artifacts/UpdaterTests/' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$script:checks = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) {
    $script:checks.Add(@{name=$Name; passed=$Passed})
    if (!$Passed) { throw "FAIL: $Name" }
    Write-Host "PASS: $Name"
}
function Reject([string]$Name, [scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Check $Name $rejected
}
function Test-Bytes([string]$Root, [string]$Relative, [string]$Value) {
    $path = Resolve-PatchChild $Root $Relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    # Inert non-PE fixture text; none of these files is ever executed.
    [IO.File]::WriteAllText($path, ('INERT UPDATER TEST - NOT AN EXECUTABLE: ' + $Value), [Text.UTF8Encoding]::new($false))
}
$success = $false
try {
    $source = Join-Path $testRoot 'source'
    $feed = Join-Path $testRoot 'feed'
    $install = Join-Path $testRoot 'install'
    foreach ($name in @('FamilyCompany.exe','UnityPlayer.dll','FamilyCompany_Data/level0','removed.txt')) { Test-Bytes $source $name $name }
    $save = Join-Path $testRoot 'save-sentinel.txt'
    [IO.File]::WriteAllText($save, 'user save remains untouched')
    $saveHash = Get-PatchHash $save
    $commit = '1234567890123456789012345678901234567890'
    $v1 = New-CompanyPatchPackage $source $feed 'fc-win-20260905.1' 1 $commit
    $script:progressEvents = [Collections.Generic.List[object]]::new()
    $script:PatchProgressSink = { param($event) $script:progressEvents.Add($event) }
    $first = Install-CompanyPatch $install $v1.ManifestPath $v1.ManifestHash -LocalFeed $feed
    Check 'first install downloads every file' ($first.DownloadedFiles -eq 4 -and $first.ReusedFiles -eq 0)
    $downloadEvents = @($script:progressEvents | Where-Object phase -EQ 'download')
    $expectedPackedBytes = ((Read-PatchManifest $v1.ManifestPath).files | Measure-Object packedSize -Sum).Sum
    Check 'download denominator equals changed compressed byte sum' (@($downloadEvents | Where-Object total -NE $expectedPackedBytes).Count -eq 0)
    Check 'download completion uses actual bytes not file count' ($downloadEvents[-1].done -eq $first.DownloadedBytes -and $downloadEvents[-1].percent -eq 100)
    $verifyEvents = @($script:progressEvents | Where-Object phase -EQ 'verify')
    Check 'verification is a separate measured phase' ($verifyEvents.Count -gt 1 -and $verifyEvents[-1].percent -eq 100)
    $preparedRoot = Join-Path $testRoot 'prepared-in-game'
    $prepared = Install-CompanyPatch $preparedRoot $v1.ManifestPath $v1.ManifestHash -LocalFeed $feed -PrepareOnly -SeedDirectory $source
    Check 'in-game preparation reuses authenticated base files' ($prepared.DownloadedFiles -eq 0 -and $prepared.ReusedFiles -eq 4)
    Check 'preparation does not activate while game runs' ($prepared.Status -eq 'prepared' -and !(Test-Path -LiteralPath (Join-Path $preparedRoot 'current.json')))
    $activated = Install-CompanyPatch $preparedRoot $v1.ManifestPath $v1.ManifestHash -LocalFeed $feed
    Check 'prepared snapshot activates after separate closed-game gate' ($activated.Status -eq 'recovered' -and $activated.DownloadedFiles -eq 0)
    $script:progressEvents.Clear()
    $again = Install-CompanyPatch $install $v1.ManifestPath $v1.ManifestHash -LocalFeed $feed
    Check 'same version downloads nothing' ($again.DownloadedFiles -eq 0 -and $again.Status -eq 'current')
    Check 'unchanged version never pretends to download' (@($script:progressEvents | Where-Object phase -EQ 'download').Count -eq 0)
    # Explicit owned single-file fixture deletion: tests removal by snapshot replacement.
    Remove-Item -LiteralPath (Resolve-PatchChild $source 'removed.txt')
    Test-Bytes $source 'FamilyCompany_Data/level0' 'changed level'
    Test-Bytes $source 'FamilyCompany_Data/new.txt' 'added file'
    $v2 = New-CompanyPatchPackage $source $feed 'fc-win-20260905.2' 2 $commit -PreviousManifest $v1.ManifestPath
    Check 'publisher reuses previous release assets' ($v2.NewAssets -eq 2)
    $script:progressEvents.Clear()
    $second = Install-CompanyPatch $install $v2.ManifestPath $v2.ManifestHash -LocalFeed $feed
    Check 'changed/addition only download, two files reused' ($second.DownloadedFiles -eq 2 -and $second.ReusedFiles -eq 2)
    $downloadEvents = @($script:progressEvents | Where-Object phase -EQ 'download')
    $expectedPackedBytes = ((Read-PatchManifest $v2.ManifestPath).files | Where-Object assetTag -EQ 'fc-win-20260905.2' | Measure-Object packedSize -Sum).Sum
    Check 'delta progress excludes reused file bytes' ($downloadEvents[-1].total -eq $expectedPackedBytes -and $downloadEvents[-1].done -eq $expectedPackedBytes)
    $lastDone = 0L; $monotonic = $true
    foreach ($event in $downloadEvents) { if ($event.done -lt $lastDone -or $event.percent -ne ([Math]::Floor(1000.0*$event.done/$event.total)/10)) { $monotonic=$false }; $lastDone=$event.done }
    Check 'download percentage is monotonic and rounded down' $monotonic
    Check 'removed files absent from active snapshot' (!(Test-Path -LiteralPath (Join-Path $second.Directory 'removed.txt')))
    $pointerHash = Get-PatchHash (Join-Path $install 'current.json')
    Reject 'downgrade denied' { Install-CompanyPatch $install $v1.ManifestPath $v1.ManifestHash -LocalFeed $feed }
    $mutated = Get-Content -LiteralPath $v2.ManifestPath -Raw | ConvertFrom-Json
    $mutated.commit = 'abcdef1234567890123456789012345678901234'
    $bad = Join-Path $testRoot 'mutation.json'
    Write-PatchJsonAtomic $bad $mutated
    Reject 'same sequence mutation denied' { Install-CompanyPatch $install $bad (Get-PatchHash $bad) -LocalFeed $feed }
    Reject 'manifest hash mismatch denied' { Install-CompanyPatch $install $bad ('0' * 64) -LocalFeed $feed }
    foreach ($path in @('../escape','C:/escape','/absolute','dir\\name','file:stream','NUL.txt','dir/../x','dir//x','trailing.','trail ','COM1')) {
        Reject "unsafe path $path" { Resolve-PatchChild $testRoot $path }
    }
    $invalid = Get-Content -LiteralPath $v2.ManifestPath -Raw | ConvertFrom-Json
    $invalid.files[1].path = $invalid.files[0].path.ToUpperInvariant()
    Write-PatchJsonAtomic $bad $invalid
    Reject 'case insensitive path collision denied' { Read-PatchManifest $bad }
    $invalid = Get-Content -LiteralPath $v2.ManifestPath -Raw | ConvertFrom-Json
    $invalid.files[1].path = 'FamilyCompany_Data'
    Write-PatchJsonAtomic $bad $invalid
    Reject 'file versus directory collision denied' { Read-PatchManifest $bad }
    $held = [IO.File]::Open((Join-Path $install 'update.lock'), 'Open', 'ReadWrite', 'None')
    try { Reject 'simultaneous updater denied' { Install-CompanyPatch $install $v2.ManifestPath $v2.ManifestHash -LocalFeed $feed } }
    finally { $held.Dispose() }
    Test-Bytes $source 'FamilyCompany_Data/level0' 'third level'
    $v3 = New-CompanyPatchPackage $source $feed 'fc-win-20260905.3' 3 $commit -PreviousManifest $v2.ManifestPath
    $newManifest = Read-PatchManifest $v3.ManifestPath
    $newFile = @($newManifest.files | Where-Object assetTag -EQ 'fc-win-20260905.3')[0]
    $asset = Resolve-PatchChild $feed ($newFile.assetTag + '/' + $newFile.assetName)
    $originalBytes = [IO.File]::ReadAllBytes($asset)
    $script:cancelNow = $false
    $script:PatchCancellationCheck = { $script:cancelNow }
    $script:PatchProgressSink = { param($event) if ($event.phase -eq 'download' -and $event.done -gt 0) { $script:cancelNow = $true } }
    Reject 'cancel after received bytes aborts before activation' { Install-CompanyPatch $install $v3.ManifestPath $v3.ManifestHash -LocalFeed $feed }
    $script:PatchCancellationCheck = $null; $script:PatchProgressSink = $null
    Check 'cancellation preserves previous pointer' ((Get-PatchHash (Join-Path $install 'current.json')) -eq $pointerHash)
    Check 'cancelled staging is completely removed' (@(Get-ChildItem -LiteralPath (Join-Path $install 'staging') -Force).Count -eq 0)
    [IO.File]::WriteAllText($asset, 'corrupted download')
    Reject 'corrupt asset rejected' { Install-CompanyPatch $install $v3.ManifestPath $v3.ManifestHash -LocalFeed $feed }
    Check 'failed patch leaves active pointer untouched' ((Get-PatchHash (Join-Path $install 'current.json')) -eq $pointerHash)
    Check 'failed partial executable payload removed' (@(Get-ChildItem -LiteralPath (Join-Path $install 'staging') -Force).Count -eq 0)
    Check 'failure evidence preserved outside partial payload' (@(Get-ChildItem -LiteralPath (Join-Path $install 'evidence') -Filter '*.json').Count -gt 0)
    [IO.File]::WriteAllBytes($asset, $originalBytes)
    $unavailableFeed = Join-Path $testRoot 'unavailable-feed'
    Reject 'missing download rejected' { Install-CompanyPatch $install $v3.ManifestPath $v3.ManifestHash -LocalFeed $unavailableFeed }
    $third = Install-CompanyPatch $install $v3.ManifestPath $v3.ManifestHash -LocalFeed $feed
    Check 'retry after failed transfer succeeds' ($third.DownloadedFiles -eq 1)
    $state = Get-PatchCurrent $install
    Assert-PatchInstalled $state.Directory $state.Manifest
    Check 'verified offline installation valid' $true
    # Simulate crash after completed directory rename, before pointer write: restore the old pointer only.
    Write-PatchJsonAtomic (Join-Path $install 'current.json') @{directory=('versions/2-'+$v2.ManifestHash.Substring(0,12)); manifestSha256=$v2.ManifestHash}
    $recovered = Install-CompanyPatch $install $v3.ManifestPath $v3.ManifestHash -LocalFeed $feed
    Check 'interrupted activation resumes with zero downloads' ($recovered.Status -eq 'recovered' -and $recovered.DownloadedFiles -eq 0)
    Test-Bytes $third.Directory 'unexpected.dll' 'not in manifest'
    Reject 'unexpected installed binary blocks offline launch' { Assert-PatchInstalled $third.Directory $newManifest }
    Remove-Item -LiteralPath (Resolve-PatchChild $third.Directory 'unexpected.dll')
    Test-Bytes $third.Directory 'FamilyCompany.exe' 'tampered'
    Reject 'tampered installed executable blocks offline launch' { Assert-PatchInstalled $third.Directory $newManifest }
    $unrelated = Join-Path $testRoot 'unrelated'
    Test-Bytes $unrelated 'keep.txt' 'unrelated'
    Reject 'nonempty unrelated directory never adopted' { Initialize-PatchStore $unrelated }
    Reject 'drive root never adopted' { Initialize-PatchStore ([IO.Path]::GetPathRoot($testRoot)) }
    $junction = Join-Path $testRoot 'junction'
    New-Item -ItemType Junction -Path $junction -Target $unrelated | Out-Null
    Reject 'junction directory rejected' { Resolve-PatchChild $junction 'escape.txt' }
    Check 'save sentinel unchanged' ((Get-PatchHash $save) -eq $saveHash)
    $memory = [IO.MemoryStream]::new([byte[]](1,2,3,4)); $destination = [IO.MemoryStream]::new()
    try { Reject 'stream cannot exceed manifest size' { Copy-PatchStream $memory $destination 3 $null } }
    finally { $memory.Dispose(); $destination.Dispose() }
    $memory = [IO.MemoryStream]::new([byte[]](1,2)); $destination = [IO.MemoryStream]::new()
    try { Reject 'truncated stream never reaches completion' { Copy-PatchStream $memory $destination 3 $null } }
    finally { $memory.Dispose(); $destination.Dispose() }
    Reject 'invalid progress exceeds total rejected' { Send-PatchProgress 'download' 'bad' 4 3 }
    $success = $true
} finally {
    Write-PatchJsonAtomic (Join-Path $testRoot 'result.json') @{passed=$success; checks=@($script:checks.ToArray());
        fixture='inert text, no game launched, no network'; timeUtc=[DateTime]::UtcNow.ToString('o')}
    Write-Host "UPDATER TESTS: $success; $testRoot"
}
if (!$success) { exit 1 }
