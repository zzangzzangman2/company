#Requires -Version 7.2
# Packaging requires modern .NET ZIP entry separators. The shipping game worker still supports Windows PowerShell 5.1.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$VerifiedPlayerDirectory,
    [Parameter(Mandatory=$true)][string]$ReleaseReceipt,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][long]$Sequence,
    [string]$PreviousManifest = '',
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or (& git -C $repoRoot branch --show-current).Trim() -ne 'main') { throw 'Use canonical main.' }
$dirty = @(& git -C $repoRoot status --porcelain)
if ($LASTEXITCODE -ne 0 -or $dirty.Count -gt 0) { throw 'Release packaging requires clean committed source (including untracked inputs).' }
$player = (Resolve-Path -LiteralPath $VerifiedPlayerDirectory).Path
Assert-PatchNoReparse $player
$buildInfo = Get-Content -LiteralPath (Join-Path $player 'BUILD_INFO.txt') -Raw
if ($buildInfo -notmatch ('(?m)^Commit: '+$commit+'\r?$') -or $buildInfo -notmatch '(?m)^WorkingTreeDirty: False\r?$' -or
    $buildInfo -notmatch '(?m)^BuildType: Release \(non-Development\)\r?$' -or
    $buildInfo -notmatch '(?m)^UnityVersion: 6000\.3\.21f1\r?$' -or $player -match '(?i)FastQa') {
    throw 'FastQA, stale or dirty player cannot become a release.'
}
$receipt = Get-Content -LiteralPath $ReleaseReceipt -Raw | ConvertFrom-Json
if ($receipt.schemaVersion -ne 1 -or $receipt.commit -cne $commit -or $receipt.productionEligible -ne $true -or
    $receipt.userVisualApproval -ne $true -or !$receipt.approvalReference -or
    $receipt.playerSha256 -cne (Get-PatchHash (Join-Path $player 'FamilyCompany.exe')) -or
    $receipt.buildInfoSha256 -cne (Get-PatchHash (Join-Path $player 'BUILD_INFO.txt'))) {
    throw 'Independent release receipt and explicit current visual approval are required.'
}
$required = @('opening-four-actors','shop-native-pointer-four-rotations','normal-walk-tile-centres',
    'walking-visual-foot-slip-grounding','furniture-avoidance','four-seated-working-directions',
    'next-day-four-staggered-arrivals','mute','runtime-exception-zero','updater-regressions')
foreach ($name in $required) {
    $gates=@($receipt.gates | Where-Object name -CEQ $name)
    if ($gates.Count -ne 1 -or $gates[0].passed -ne $true -or $gates[0].independent -ne $true -or
        $gates[0].commit -cne $commit -or !$gates[0].evidencePath -or
        (Get-PatchHash $gates[0].evidencePath) -cne $gates[0].evidenceSha256) { throw "Missing/stale independent gate: $name" }
}
& (Join-Path $PSScriptRoot 'Test-FamilyCompanyRemoteInventory.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Remote inventory failed.' }
$remoteCommit=(& git -C $repoRoot rev-parse origin/main).Trim()
if ($remoteCommit -cne $commit) { throw 'Commit and verify source on origin/main before packaging.' }
$packageDirectory=Resolve-PatchChild (Join-Path $repoRoot 'Artifacts/Patches') $Version
if (Test-Path -LiteralPath $packageDirectory) {
    $packageManifestPath=Join-Path $packageDirectory 'family-company-manifest.json'
    $packageManifest=Read-PatchManifest $packageManifestPath
    if ($packageManifest.commit -cne $commit -or $packageManifest.sequence -ne $Sequence -or $packageManifest.version -cne $Version) { throw 'Prepared package identity differs.' }
    Assert-PatchInstalled $player $packageManifest
    foreach ($file in $packageManifest.files | Where-Object assetTag -CEQ $Version) {
        $packed=Resolve-PatchChild $packageDirectory $file.assetName
        if ((Get-Item -LiteralPath $packed).Length -ne $file.packedSize -or (Get-PatchHash $packed) -cne $file.packedSha256) { throw 'Prepared asset changed.' }
    }
    $result=[pscustomobject]@{Directory=$packageDirectory; NewAssets=@(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.gz').Count; Files=@($packageManifest.files).Count}
} else {
    $result=New-CompanyPatchPackage -Source $player -OutputRoot (Join-Path $repoRoot 'Artifacts/Patches') -Version $Version -Sequence $Sequence -Commit $commit -PreviousManifest $PreviousManifest
}
# Independent receipt is evidence-only. First install contains the actual Unity game, not a GUI launcher.
$receiptCopy=Join-Path $result.Directory 'release-receipt.json'
if (Test-Path -LiteralPath $receiptCopy) {
    if ((Get-PatchHash $receiptCopy) -cne (Get-PatchHash $ReleaseReceipt)) { throw 'Prepared release receipt changed.' }
} else { [IO.File]::Copy((Resolve-Path -LiteralPath $ReleaseReceipt).Path, $receiptCopy) }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bootstrapZip=Join-Path $result.Directory 'FamilyCompany-Windows.zip'
if (!(Test-Path -LiteralPath $bootstrapZip)) { [IO.Compression.ZipFile]::CreateFromDirectory($player,$bootstrapZip,[IO.Compression.CompressionLevel]::Optimal,$false) }
if ((Get-Item -LiteralPath $bootstrapZip).Length -gt 2GB) { throw 'First-install ZIP exceeds the single GitHub asset limit; do not publish.' }
$bootstrap=@((Read-PatchManifest (Join-Path $result.Directory 'family-company-manifest.json')).files)
$archive=[IO.Compression.ZipFile]::OpenRead($bootstrapZip)
try {
    if (@($archive.Entries | Where-Object Name -NE '').Count -ne $bootstrap.Count) { throw 'Unexpected game archive entries.' }
    foreach ($sourceFile in $bootstrap) {
        $entry=$archive.GetEntry($sourceFile.path)
        if (!$entry) { throw 'Missing bootstrap file.' }
        $stream=$entry.Open(); $hasher=[Security.Cryptography.SHA256]::Create()
        try { $entryHash=([BitConverter]::ToString($hasher.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
        finally { $stream.Dispose(); $hasher.Dispose() }
        if ($entryHash -cne $sourceFile.sha256) { throw 'Prepared game archive differs from reviewed source.' }
    }
} finally { $archive.Dispose() }
$expectedAssets=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in @('family-company-manifest.json','release-receipt.json','FamilyCompany-Windows.zip')) { [void]$expectedAssets.Add($name) }
foreach ($file in (Read-PatchManifest (Join-Path $result.Directory 'family-company-manifest.json')).files | Where-Object assetTag -CEQ $Version) {
    [void]$expectedAssets.Add($file.assetName)
}
foreach ($asset in Get-ChildItem -LiteralPath $result.Directory -Force) {
    if ($asset.PSIsContainer -or !$expectedAssets.Contains($asset.Name)) { throw 'Unexpected file in upload directory; no external write.' }
}
Write-Host "PACKAGE READY: $($result.Directory); new compressed assets=$($result.NewAssets) files=$($result.Files)"
if (!$Publish) { Write-Host 'No GitHub write. Review package, then publish through the documented verified-release procedure.'; return }
if (@(& git -C $repoRoot status --porcelain).Count -gt 0 -or (& git -C $repoRoot rev-parse HEAD).Trim() -cne $commit) {
    throw 'Source changed during packaging; no external write.'
}
function Invoke-ReleaseGh([string[]]$Arguments) {
    $output=& gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub release operation failed; leave draft unpublished for inspection: $($Arguments[0..1]-join ' ')" }
    return $output
}
function Read-DraftReleaseForVerification([string]$Repository,[string]$Tag,[string]$ExpectedCommit) {
    # The REST tag endpoint returns published releases, not an unpublished pending tag.
    # gh resolves the draft; validate its numeric ID before requesting/publishing anything.
    $identity=((Invoke-ReleaseGh @('release','view',$Tag,'--repo',$Repository,'--json','databaseId')) -join "`n") | ConvertFrom-Json
    if (!$identity.databaseId -or [long]$identity.databaseId -le 0) { throw 'Draft release ID missing.' }
    $draft=((Invoke-ReleaseGh @('api',("repos/"+$Repository+"/releases/"+[long]$identity.databaseId))) -join "`n") | ConvertFrom-Json
    if ($draft.id -ne $identity.databaseId -or !$draft.draft -or $draft.prerelease -or
        $draft.tag_name -cne $Tag -or $draft.target_commitish -cne $ExpectedCommit) { throw 'Draft release identity differs.' }
    return $draft
}
[void](Invoke-ReleaseGh @('release','create',$Version,'--repo',$script:PatchRepository,'--target',$commit,'--draft',
    '--title',("Family Company "+$Version),'--notes',("Verified Windows patch for commit "+$commit+". Extract FamilyCompany-Windows.zip and open the Unity FamilyCompany.exe. Patch progress appears inside the game; saves remain local.")))
foreach ($asset in Get-ChildItem -LiteralPath $result.Directory -File) {
    [void](Invoke-ReleaseGh @('release','upload',$Version,$asset.FullName,'--repo',$script:PatchRepository))
}
$release=Read-DraftReleaseForVerification $script:PatchRepository $Version $commit
$pages=(Invoke-ReleaseGh @('api','--paginate','--slurp',("repos/"+$script:PatchRepository+"/releases/"+$release.id+"/assets?per_page=100"))) -join "`n" | ConvertFrom-Json
$uploaded=@(); foreach ($page in $pages) { $uploaded+=@($page) }
if ($uploaded.Count -ne @(Get-ChildItem -LiteralPath $result.Directory -File).Count) { throw 'Unexpected uploaded asset count; draft stays unpublished.' }
foreach ($asset in Get-ChildItem -LiteralPath $result.Directory -File) {
    $matches=@($uploaded | Where-Object name -CEQ $asset.Name)
    if ($matches.Count -ne 1 -or $matches[0].size -ne $asset.Length -or $matches[0].digest -cne ('sha256:'+(Get-PatchHash $asset.FullName))) {
        throw 'Uploaded digest mismatch; draft stays unpublished.'
    }
}
[void](Invoke-ReleaseGh @('release','edit',$Version,'--repo',$script:PatchRepository,'--draft=false','--latest'))
Write-PatchJsonAtomic (Join-Path $repoRoot ('Artifacts/UpdaterRemoteInventory/published-'+$Version+'.json')) @{
    schemaVersion=1; releases=@(@{id=$release.id; tag=$Version; commit=$commit; approvalReference=$receipt.approvalReference;
        independentReceiptPath=(Resolve-Path -LiteralPath $ReleaseReceipt).Path; independentReceiptSha256=(Get-PatchHash $ReleaseReceipt);
        assets=@($uploaded | ForEach-Object { @{id=$_.id; name=$_.name; size=$_.size; digest=$_.digest} })})}
Write-Host "PUBLISHED: https://github.com/$script:PatchRepository/releases/tag/$Version"
