[CmdletBinding()]
param([string]$InstallRoot = '', [switch]$UpdateOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
if (!$InstallRoot) { $InstallRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FamilyCompany\PatchedGame' }
$root = Initialize-PatchStore $InstallRoot
$launcherLock = [IO.File]::Open((Join-Path $root 'launch.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
Assert-PatchGameClosed $root
$temporary = Join-Path $root ('manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    Write-Host '[PATCH] Checking verified Family Company releases...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:PatchRepository/releases/latest" -TimeoutSec 15 -Headers @{
        'User-Agent'='FamilyCompany-Updater/1'; 'Accept'='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'
    }
    if ($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^fc-win-[0-9]{8}\.[0-9]+$') { throw 'No verified game release is available.' }
    $assets = @($release.assets | Where-Object name -CEQ 'family-company-manifest.json')
    if ($assets.Count -ne 1 -or $assets[0].size -gt 4MB -or $assets[0].digest -notmatch '^sha256:[0-9a-f]{64}$') { throw 'Missing authenticated manifest digest.' }
    $asset = $assets[0]
    $hash = $asset.digest.Substring(7)
    $url = "https://github.com/$script:PatchRepository/releases/download/$($release.tag_name)/family-company-manifest.json"
    Receive-PatchFile $url $temporary $asset.size $hash
    $manifest = Read-PatchManifest $temporary
    if ($manifest.version -cne $release.tag_name) { throw 'Release/manifest version mismatch.' }
    $result = Install-CompanyPatch -InstallRoot $root -ManifestPath $temporary -ExpectedManifestHash $hash
    Write-Host "[PATCH] $($result.Status): downloaded $($result.DownloadedFiles), reused $($result.ReusedFiles)."
} catch {
    Write-Warning ('Update unavailable: ' + $_.Exception.Message)
    # No incomplete payload is run. Offline fallback is the previously validated installation only.
    $current = Get-PatchCurrent $root
    if (!$current) { throw 'No verified installation yet. Ask the developer for the first game release.' }
    Assert-PatchInstalled $current.Directory $current.Manifest
    $result = [pscustomobject]@{Directory=$current.Directory; Status='verified-offline'}
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
if (!$UpdateOnly) {
    Assert-PatchGameClosed $root
    $game = Resolve-PatchChild $result.Directory 'FamilyCompany.exe'
    # User explicitly launches the game through this entry point; no automatic startup task.
    Start-Process -FilePath $game -WorkingDirectory $result.Directory | Out-Null
}
} finally { $launcherLock.Dispose() }
