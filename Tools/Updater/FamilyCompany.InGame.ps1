[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$GameDirectory,
    [Parameter(Mandatory=$true)][string]$ResultPath,
    [string]$InstallRoot = '', [string]$CancelPath = '')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$script:PatchProgressSink = { param($event)
    [Console]::Out.WriteLine('FC_PROGRESS ' + ($event | ConvertTo-Json -Compress)); [Console]::Out.Flush()
}
if ($CancelPath) { $script:PatchCancellationCheck = { Test-Path -LiteralPath $CancelPath } }
try {
    if (!$InstallRoot) { $InstallRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FamilyCompany/PatchedGame' }
    $root = Initialize-PatchStore $InstallRoot
    Assert-PatchNoReparse $ResultPath
    $temporary = Join-Path $root ('manifest-' + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        Send-PatchProgress 'check'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $headers = @{'User-Agent'='FamilyCompany-InGame/1'; 'Accept'='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'}
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:PatchRepository/releases/latest" -Headers $headers -TimeoutSec 15
        if ($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^fc-win-[0-9]{8}\.[0-9]+$') { throw 'No verified game release is available.' }
        $matches = @()
        for ($page=1; $page -le 101; $page++) {
            Test-PatchCancellation
            $assetPage = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:PatchRepository/releases/$([long]$release.id)/assets?per_page=100&page=$page" -Headers $headers -TimeoutSec 15
            $assetPage = @($assetPage)
            $matches += @($assetPage | Where-Object name -CEQ 'family-company-manifest.json')
            if ($assetPage.Count -lt 100) { break }
            if ($page -eq 101) { throw 'Unbounded release assets.' }
        }
        if ($matches.Count -ne 1 -or $matches[0].size -gt 4MB -or $matches[0].digest -notmatch '^sha256:[0-9a-f]{64}$') { throw 'Missing authenticated manifest digest.' }
        $hash = $matches[0].digest.Substring(7)
        Receive-PatchFile "https://github.com/$script:PatchRepository/releases/download/$($release.tag_name)/family-company-manifest.json" $temporary $matches[0].size $hash
        $manifest = Read-PatchManifest $temporary
        if ($manifest.version -cne $release.tag_name) { throw 'Release identity mismatch.' }
        # Even the first install enters the authenticated immutable snapshot store.
        $installed = Install-CompanyPatch $root $temporary $hash -PrepareOnly -SeedDirectory $GameDirectory
        $result = @{status=$installed.Status; directory=$installed.Directory; manifestHash=$hash}
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
    Write-PatchJsonAtomic $ResultPath $result
    Send-PatchProgress 'ready' $result.status 1 1
} catch {
    $script:PatchCancellationCheck = $null
    Send-PatchProgress 'error' $_.Exception.Message
    exit 1
}
