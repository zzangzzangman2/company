[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ParentId, [Parameter(Mandatory=$true)][long]$ParentStartTicks,
    [Parameter(Mandatory=$true)][string]$GameDirectory, [Parameter(Mandatory=$true)][string]$InstallRoot,
    [Parameter(Mandatory=$true)][string]$PendingDirectory, [Parameter(Mandatory=$true)][string]$ExpectedManifestHash,
    [Parameter(Mandatory=$true)][string]$ReadyPath, [switch]$DiagnosticBatchMode)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
$root = Initialize-PatchStore $InstallRoot
$launchLock = [IO.File]::Open((Join-Path $root 'launch.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $manifestPath = Join-Path $PendingDirectory 'family-company-manifest.json'
    if ((Get-PatchHash $manifestPath) -cne $ExpectedManifestHash) { throw 'Restart manifest mismatch.' }
    $manifest = Read-PatchManifest $manifestPath
    $relative = 'versions/' + $manifest.sequence + '-' + $ExpectedManifestHash.Substring(0,12)
    if ((Resolve-PatchChild $root $relative) -cne $PendingDirectory) { throw 'Restart target outside owned store.' }
    $parent = Get-Process -Id $ParentId -ErrorAction Stop
    if ($parent.StartTime.ToUniversalTime().Ticks -ne $ParentStartTicks -or
        [IO.Path]::GetDirectoryName($parent.Path) -ine $GameDirectory) { throw 'Restart parent identity changed.' }
    Assert-PatchInstalled $PendingDirectory $manifest
    Assert-PatchNoReparse $ReadyPath
    Write-PatchJsonAtomic $ReadyPath @{ready=$true; parentId=$ParentId; version=$manifest.version}
    if (!$parent.WaitForExit(60000)) { throw 'Game did not exit; no activation or forced kill.' }
    $updateLock = [IO.File]::Open((Join-Path $root 'update.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    try {
        Assert-PatchGameClosed $root
        $current = Get-PatchCurrent $root
        if ($current -and ($current.Manifest.sequence -gt $manifest.sequence -or
            ($current.Manifest.sequence -eq $manifest.sequence -and $current.Hash -cne $ExpectedManifestHash))) { throw 'Newer/different patch already active.' }
        Assert-PatchInstalled $PendingDirectory $manifest
        Write-PatchJsonAtomic (Join-Path $root 'current.json') @{directory=$relative; manifestSha256=$ExpectedManifestHash; activatedUtc=[DateTime]::UtcNow.ToString('o')}
        if ($DiagnosticBatchMode) {
            Start-Process -FilePath (Resolve-PatchChild $PendingDirectory 'FamilyCompany.exe') -WorkingDirectory $PendingDirectory -WindowStyle Hidden -ArgumentList ('-batchmode -force-d3d11 -familyCompanyPatchBackgroundExit -logFile "'+(Join-Path $root 'patched-player.log')+'"') | Out-Null
        } else {
            Start-Process -FilePath (Resolve-PatchChild $PendingDirectory 'FamilyCompany.exe') -WorkingDirectory $PendingDirectory | Out-Null
        }
    } finally { $updateLock.Dispose() }
} catch {
    Write-PatchJsonAtomic ($ReadyPath + '.error.json') @{error=$_.Exception.Message}
    exit 1
} finally { $launchLock.Dispose() }
