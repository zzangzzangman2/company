[CmdletBinding()]
param([string]$OutputPath = '', [string]$ApprovedReleaseInventory = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Read-Git([string[]]$Arguments) {
    $result = & git -C $repoRoot @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git failed: $($Arguments -join ' ')" }
    return $result
}
function Read-Gh([string[]]$Arguments) {
    $result = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub inventory failed: $($Arguments -join ' ')" }
    return ($result -join "`n" | ConvertFrom-Json)
}
$remote = Read-Git @('remote','get-url','origin')
if ($remote -notmatch '^https://github.com/zzangzzangman2/company(?:\.git)?$') { throw 'Unexpected remote.' }
[void](Read-Git @('fetch','origin','--prune','--tags'))
$refs = @(Read-Git @('for-each-ref','--format=%(refname)','refs/remotes/origin','refs/tags'))
$observations = @(); $unknown = @(); $prohibited = @()
$treesSeen = @{}
$approved=@()
if (!$ApprovedReleaseInventory) { $ApprovedReleaseInventory=Join-Path $repoRoot 'Docs/Evidence/VerifiedReleaseInventory.json' }
if (Test-Path -LiteralPath $ApprovedReleaseInventory) {
    $allow=Get-Content -LiteralPath $ApprovedReleaseInventory -Raw | ConvertFrom-Json
    if ($allow.schemaVersion -ne 1) { throw 'Unsupported reviewed release inventory.' }
    $approved=@($allow.releases)
    foreach ($known in $approved) {
        if (!$known.approvalReference -or (Get-PatchHash $known.independentReceiptPath) -cne $known.independentReceiptSha256) {
            throw 'Reviewed release inventory lacks approval/independent evidence.'
        }
    }
}
foreach ($ref in $refs) {
    $object = Read-Git @('rev-parse',$ref)
    $tree = Read-Git @('rev-parse',($ref+'^{tree}'))
    $observations += @{ref=$ref; object=$object; tree=$tree}
    if ($treesSeen.ContainsKey($tree)) { continue }
    $treesSeen[$tree]=$true
    foreach ($line in @(Read-Git @('ls-tree','-r','-l',$tree))) {
        if ($line -notmatch '^\d+\s+blob\s+([0-9a-f]+)\s+(\d+)\t(.+)$') { continue }
        $blob=$Matches[1]; $size=[long]$Matches[2]; $path=$Matches[3]
        if ($path -match '(?i)(^|/)(Builds?|Artifacts|[^/]+_Data)/|(^|/)(FamilyCompany[^/]*\.exe|UnityPlayer\.dll)$') {
            $prohibited+=@{tree=$tree; path=$path; blob=$blob; size=$size; classification='tracked playable output'}
        } elseif ($path -match '(?i)\.(exe|dll|zip|7z|rar|tar|tgz|gz|msi|cab|unitypackage)$') {
            # Fail closed. A real source dependency needs a reviewed exact-path allowlist, not a blanket exception.
            $unknown+=@{tree=$tree; path=$path; blob=$blob; size=$size; classification='unclassified executable/archive'}
        } elseif ($size -le 1024) {
            $body = Read-Git @('cat-file','blob',$blob)
            if (($body -join "`n") -match 'version https://git-lfs.github.com/spec/v1') {
                $unknown+=@{tree=$tree; path=$path; blob=$blob; size=$size; classification='LFS requires content audit'}
            }
        }
    }
}
$pages=Read-Gh @('api','--paginate','--slurp','repos/zzangzzangman2/company/releases?per_page=100')
$releaseRecords=@()
foreach ($page in $pages) { foreach ($release in $page) {
    $assetPages=Read-Gh @('api','--paginate','--slurp',("repos/zzangzzangman2/company/releases/$($release.id)/assets?per_page=100"))
    $assets=@()
    foreach ($assetPage in $assetPages) { foreach ($asset in $assetPage) {
        $assets+=@{id=$asset.id; name=$asset.name; size=$asset.size; digest=$asset.digest}
        $allowRelease=@($approved | Where-Object { $_.id -eq $release.id -and $_.tag -ceq $release.tag_name })
        $allowAsset=@()
        if ($allowRelease.Count -eq 1 -and !$release.draft -and !$release.prerelease) {
            $allowAsset=@($allowRelease[0].assets | Where-Object { $_.id -eq $asset.id -and $_.name -ceq $asset.name -and
                $_.size -eq $asset.size -and $_.digest -cmatch '^sha256:[0-9a-f]{64}$' -and $_.digest -ceq $asset.digest })
        }
        if ($allowAsset.Count -ne 1) {
            $unknown+=@{releaseId=$release.id; tag=$release.tag_name; assetId=$asset.id; name=$asset.name;
                classification='release requires independently reviewed identity allowlist'; digest=$asset.digest}
        }
    } }
    $releaseRecords+=@{id=$release.id; tag=$release.tag_name; draft=$release.draft; prerelease=$release.prerelease; assets=$assets}
} }
if (!$OutputPath) { $OutputPath=Join-Path $repoRoot ('Artifacts/UpdaterRemoteInventory/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'/remote-zero-inventory.json') }
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath)))
$pass=$unknown.Count -eq 0 -and $prohibited.Count -eq 0
Write-PatchJsonAtomic $OutputPath @{observationUtc=[DateTime]::UtcNow.ToString('o'); remote=$remote;
    gitVersion=(Read-Git @('--version')); tool='Test-FamilyCompanyRemoteInventory-v1'; paginationComplete=$true;
    main=(Read-Git @('rev-parse','origin/main')); refs=$observations; releases=$releaseRecords;
    prohibited=$prohibited; unknown=$unknown; prohibitedCount=$prohibited.Count; unknownCount=$unknown.Count; passed=$pass}
Write-Host "REMOTE INVENTORY: passed=$pass prohibited=$($prohibited.Count) unknown=$($unknown.Count); $OutputPath"
if (!$pass) { throw 'Remote inventory is not zero. No push or release is permitted.' }
