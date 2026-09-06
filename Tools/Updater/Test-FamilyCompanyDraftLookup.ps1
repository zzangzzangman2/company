$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Publish-FamilyCompanyPatch.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Publisher parse failure'}
$function=$ast.Find({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-DraftReleaseForVerification'},$true)
if(!$function){throw 'Missing production draft lookup'}
. ([scriptblock]::Create($function.Extent.Text))
$script:mockMode='valid';$script:calls=@()
function Invoke-ReleaseGh([string[]]$Arguments){
  $script:calls+=@($Arguments -join ' ')
  if($Arguments[0] -eq 'release'){
    if($script:mockMode -eq 'missing-id'){return '{}'}
    return '{"databaseId":383541607}'
  }
  if($Arguments[1] -cne 'repos/owner/repo/releases/383541607'){throw 'Used published-tag endpoint for a draft'}
  $value=@{id=383541607;draft=$true;prerelease=$false;tag_name='fc-win-20260906.1';target_commitish=('4'*40)}
  switch($script:mockMode){
    'wrong-id' {$value.id=1}
    'published' {$value.draft=$false}
    'prerelease' {$value.prerelease=$true}
    'wrong-tag' {$value.tag_name='fc-win-20260906.2'}
    'wrong-commit' {$value.target_commitish=('5'*40)}
  }
  return ($value|ConvertTo-Json -Compress)
}
$checks=@()
foreach($mode in @('valid','missing-id','wrong-id','published','prerelease','wrong-tag','wrong-commit')){
  $script:mockMode=$mode;$rejected=$false
  try{$found=Read-DraftReleaseForVerification 'owner/repo' 'fc-win-20260906.1' ('4'*40)}catch{$rejected=$true}
  $passed=if($mode -eq 'valid'){!$rejected -and $found.id -eq 383541607}else{$rejected}
  $checks+=@{name=$mode;passed=$passed}
  if(!$passed){throw "Draft lookup failed: $mode"}
}
[pscustomobject]@{passed=$true;checks=$checks;scope='Production function with inert GitHub responses; no release created/edited/uploaded';calls=$script:calls}|ConvertTo-Json -Depth 8
