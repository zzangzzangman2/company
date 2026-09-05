# Packaging primitives. This file never uploads or executes a payload.
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
function New-CompanyPatchPackage {
    param([string]$Source, [string]$OutputRoot, [string]$Version, [long]$Sequence,
        [string]$Commit, [string]$PreviousManifest = '')
    if ($Version -notmatch '^fc-win-[0-9]{8}\.[0-9]+$' -or $Commit -notmatch '^[0-9a-f]{40}$' -or $Sequence -le 0) {
        throw 'Invalid package identity.'
    }
    $sourceRoot = (Resolve-Path -LiteralPath $Source).Path.TrimEnd('\','/')
    Assert-PatchNoReparse $sourceRoot
    $target = Resolve-PatchChild $OutputRoot $Version
    if ($target.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $sourceRoot.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase) -or $target -eq $sourceRoot) {
        throw 'Source and package output must not overlap.'
    }
    if (Test-Path -LiteralPath $target) { throw 'Package version already exists; never overwrite it.' }
    $previous = if ($PreviousManifest) { Read-PatchManifest $PreviousManifest } else { $null }
    if ($previous -and [long]$previous.sequence -ge $Sequence) { throw 'New sequence must increase.' }
    $byHash = @{}
    if ($previous) { foreach ($file in $previous.files) { $byHash[$file.sha256] = $file } }
    [void][IO.Directory]::CreateDirectory($target)
    $entries = @()
    foreach ($item in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force) {
        Assert-PatchNoReparse $item.FullName
        if ($item.PSIsContainer) { continue }
        $relative = $item.FullName.Substring($sourceRoot.Length + 1).Replace('\','/')
        [void](Resolve-PatchChild $target $relative)
        if ($relative -eq 'family-company-manifest.json') { throw 'Do not package a previously patched installation.' }
        $hash = Get-PatchHash $item.FullName
        if ($byHash.ContainsKey($hash)) {
            $old = $byHash[$hash]
            $entries += [ordered]@{path=$relative; size=$item.Length; sha256=$hash;
                assetTag=$old.assetTag; assetName=$old.assetName; packedSize=$old.packedSize; packedSha256=$old.packedSha256}
            continue
        }
        $packed = Join-Path $target ($hash + '.gz')
        if (!(Test-Path -LiteralPath $packed)) {
            $readStream = [IO.File]::OpenRead($item.FullName)
            $writeStream = [IO.File]::Open($packed, [IO.FileMode]::CreateNew)
            $compressor = [IO.Compression.GZipStream]::new($writeStream, [IO.Compression.CompressionLevel]::Optimal)
            try { $readStream.CopyTo($compressor) }
            finally { $compressor.Dispose(); $writeStream.Dispose(); $readStream.Dispose() }
        }
        $entries += [ordered]@{path=$relative; size=$item.Length; sha256=$hash; assetTag=$Version;
            assetName=($hash+'.gz'); packedSize=(Get-Item -LiteralPath $packed).Length; packedSha256=(Get-PatchHash $packed)}
    }
    $manifestPath = Join-Path $target 'family-company-manifest.json'
    Write-PatchJsonAtomic $manifestPath ([ordered]@{schemaVersion=1; product=$script:PatchProduct;
        repository=$script:PatchRepository; version=$Version; sequence=$Sequence; commit=$Commit;
        eligibility='verified-release'; entryPoint='FamilyCompany.exe'; files=$entries})
    [void](Read-PatchManifest $manifestPath)
    return [pscustomobject]@{Directory=$target; ManifestPath=$manifestPath; ManifestHash=(Get-PatchHash $manifestPath);
        NewAssets=@(Get-ChildItem -LiteralPath $target -Filter '*.gz').Count; Files=$entries.Count}
}
