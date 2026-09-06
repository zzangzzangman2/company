[CmdletBinding()]
param([string]$InstallRoot = '', [switch]$UpdateOnly, [switch]$ProgressProtocol, [string]$CancelPath = '')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if ($ProgressProtocol) {
    $script:PatchProgressSink = { param($event)
        [Console]::Out.WriteLine('FC_PROGRESS ' + ($event | ConvertTo-Json -Compress))
        [Console]::Out.Flush()
    }
}
if ($CancelPath) { $script:PatchCancellationCheck = { Test-Path -LiteralPath $CancelPath } }
try {
if (!$InstallRoot) { $InstallRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FamilyCompany\PatchedGame' }
$root = Initialize-PatchStore $InstallRoot
$launcherLock = [IO.File]::Open((Join-Path $root 'launch.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
Assert-PatchGameClosed $root
$temporary = Join-Path $root ('manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    Send-PatchProgress 'check'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:PatchRepository/releases/latest" -TimeoutSec 15 -Headers @{
        'User-Agent'='FamilyCompany-Updater/1'; 'Accept'='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'
    }
    if ($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^fc-win-[0-9]{8}\.[0-9]+$') { throw 'No verified game release is available.' }
    # Large first releases can have hundreds of assets. Do not rely on an embedded/truncated list.
    $assets = @()
    for ($page = 1; $page -le 101; $page++) {
        Test-PatchCancellation
        $assetPage = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:PatchRepository/releases/$([long]$release.id)/assets?per_page=100&page=$page" -TimeoutSec 15 -Headers @{
            'User-Agent'='FamilyCompany-Updater/2'; 'Accept'='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'
        }
        $assetPage = @($assetPage)
        $assets += @($assetPage | Where-Object name -CEQ 'family-company-manifest.json')
        if ($assetPage.Count -lt 100) { break }
        if ($page -eq 101) { throw 'Release asset pagination exceeded its safety limit.' }
    }
    if ($assets.Count -ne 1 -or $assets[0].size -gt 4MB -or $assets[0].digest -notmatch '^sha256:[0-9a-f]{64}$') { throw 'Missing authenticated manifest digest.' }
    $asset = $assets[0]
    $hash = $asset.digest.Substring(7)
    $url = "https://github.com/$script:PatchRepository/releases/download/$($release.tag_name)/family-company-manifest.json"
    Receive-PatchFile $url $temporary $asset.size $hash
    $manifest = Read-PatchManifest $temporary
    if ($manifest.version -cne $release.tag_name) { throw 'Release/manifest version mismatch.' }
    $result = Install-CompanyPatch -InstallRoot $root -ManifestPath $temporary -ExpectedManifestHash $hash
    Write-Host "[PATCH] $($result.Status): downloaded $($result.DownloadedFiles), reused $($result.ReusedFiles)."
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
if (!$UpdateOnly) {
    Assert-PatchGameClosed $root
    $game = Resolve-PatchChild $result.Directory 'FamilyCompany.exe'
    Send-PatchProgress 'launch' $result.Status
    # User explicitly launches the game through this entry point; no automatic startup task.
    Start-Process -FilePath $game -WorkingDirectory $result.Directory | Out-Null
}
Send-PatchProgress 'complete' $result.Status 1 1
} finally { $launcherLock.Dispose() }
} catch {
    # Cancellation must never run an offline fallback or activate an incomplete snapshot.
    $script:PatchCancellationCheck = $null
    if ($_.Exception -is [OperationCanceledException]) { Send-PatchProgress 'cancelled'; exit 2 }
    Send-PatchProgress 'error' $_.Exception.Message
    Write-Error $_ -ErrorAction Continue
    exit 1
}
