[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'FamilyCompany.InGame.ps1') -Raw
$start=[regex]::Match($source,'(?m)^        \$(matches|manifestAssets) = @\(\)')
if(!$start.Success){throw 'Actual worker manifest lookup block missing'}
$end=$source.IndexOf('        Receive-PatchFile',$start.Index)
if($end -lt $start.Index){throw 'Actual worker lookup boundary missing'}
$block=$source.Substring($start.Index,$end-$start.Index)
$collectionName=$start.Groups[1].Value
$script:PatchRepository='owner/repo'
$script:scenario='valid'
$script:pageCalls=0
function Test-PatchCancellation {}
function Invoke-RestMethod {
 param($Uri,$Headers,$TimeoutSec)
 $script:pageCalls++
 $asset=[pscustomobject]@{name='family-company-manifest.json';size=321;digest=('sha256:'+('a'*64))}
 if($script:scenario -eq 'page2' -and $script:pageCalls -eq 1){return @(1..100|ForEach-Object{[pscustomobject]@{name=('inert-'+$_);size=1;digest=('sha256:'+('b'*64))}})}
 switch($script:scenario){
  'bad-digest' {$asset.digest='invalid'}
  'missing-digest' {return [pscustomobject]@{name='family-company-manifest.json';size=321}}
  'oversize' {$asset.size=4194305}
  'duplicate' {return @($asset,$asset)}
  'missing' {return @()}
 }
 return @($asset)
}
$checks=@()
foreach($case in @('valid','page2','bad-digest','missing-digest','oversize','duplicate','missing')){
 $script:scenario=$case;$script:pageCalls=0
 $release=[pscustomobject]@{id=42};$headers=@{};$hash=$null;$caught=$null
 try{. ([scriptblock]::Create($block))}catch{$caught=$_.Exception.Message}
 $expected=$case -in @('valid','page2')
 if($expected){
   $retained=Get-Variable -Name $collectionName -ValueOnly -ErrorAction SilentlyContinue
   $ok=(!$caught -and $hash -ceq ('a'*64) -and $retained.Count -eq 1 -and $retained[0].size -eq 321)
   if($case -eq 'page2'){$ok=$ok -and $script:pageCalls -eq 2}
 }else{$ok=!!$caught}
 $checks+=@{name=$case;passed=[bool]$ok;error=$caught;apiPages=$script:pageCalls}
}
$passed=@($checks|Where-Object passed -NE $true).Count -eq 0
$out=Join-Path $repo ('Artifacts/ManifestAssetTests/'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($out)
$result=@{passed=$passed;scope='Exact production worker pagination/digest/hash block; realistic API object responses; no network or executable run';workerSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'FamilyCompany.InGame.ps1')).Hash;checks=$checks}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 8
Write-Host "MANIFEST ASSET TEST: $passed $out"
if(!$passed){exit 1}
