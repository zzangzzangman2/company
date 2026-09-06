param([ValidateSet('prepare','verify')][string]$Mode)
$ErrorActionPreference='Stop'
$repo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $repo
. 'Tools/Updater/FamilyCompany.Update.ps1'
$qa=Join-Path $repo 'Artifacts/MonitorAlignment20260906/public-delta'
$root=Join-Path $qa 'install'
$main='C:\Users\godho\Downloads\FamilyCompany_Playtest'
$userStore='C:\Users\godho\AppData\Local\FamilyCompany\PatchedGame'
$saveDir='C:\Users\godho\AppData\LocalLow\FamilyCompany\FamilyCompanyPrototype'
$oldManifest=Join-Path $repo 'Artifacts/Patches/fc-win-20260906.2/family-company-manifest.json'
$player=Join-Path $repo 'Artifacts/PatchCandidates/4b06247e-fad8442a745048b786236332580394fc/payload'
function Json([string]$p){Get-Content -LiteralPath $p -Raw|ConvertFrom-Json}
function UserState {
 $m=Read-PatchManifest $oldManifest
 Assert-PatchInstalled $main $m
 $state=@{main=$main;mainManifestSha256=(Get-PatchHash $oldManifest);mainFilesVerified=$m.files.Count;
   userPointerSha256=(Get-PatchHash (Join-Path $userStore 'current.json'));
   userPointer=(Json (Join-Path $userStore 'current.json'));
   saves=@(Get-ChildItem -LiteralPath $saveDir -File|Where-Object Name -Like 'family-company-save*'|ForEach-Object {@{name=$_.Name;sha256=(Get-PatchHash $_.FullName);size=$_.Length}})}
 $current=Get-PatchCurrent $userStore
 Assert-PatchInstalled $current.Directory $current.Manifest
 $state.userManifestSha256=$current.Hash
 $state.userSequence=$current.Manifest.sequence
 return $state
}
[void][IO.Directory]::CreateDirectory($qa)
if($Mode -eq 'prepare'){
 if(Test-Path -LiteralPath (Join-Path $qa 'before.json')){throw 'Preserve original before-state; do not rerun prepare'}
 Write-PatchJsonAtomic (Join-Path $qa 'before.json') (UserState)
 $seed=Install-CompanyPatch $root $oldManifest (Get-PatchHash $oldManifest) -SeedDirectory $main
 if($seed.DownloadedFiles -ne 0 -or $seed.ReusedFiles -ne 169){throw 'Isolated v2 seed did not reuse exact installed bytes'}
 Write-PatchJsonAtomic (Join-Path $qa 'seed.json') $seed
 Write-Output 'PREPARED isolated verified v2; user main/cache/save read only.'
 return
}
$log=Join-Path $qa 'worker.txt'
if(Test-Path -LiteralPath $log){throw 'Preserve original public-worker run; inspect before retry'}
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $player 'FamilyCompanyPatch/FamilyCompany.InGame.ps1') -GameDirectory $main -InstallRoot $root -ResultPath (Join-Path $qa 'worker-result.json') *> $log
$exitCode=$LASTEXITCODE
if($exitCode -ne 0){throw "Production public worker failed: exit $exitCode; original log retained"}
$result=Json (Join-Path $qa 'worker-result.json')
$latest=Read-PatchManifest (Join-Path $result.directory 'family-company-manifest.json')
if($latest.version -cne 'fc-win-20260906.3' -or $latest.commit -cne '4b06247ea2c4652fc320fa13c141f3501e3b5cae' -or $result.status -cne 'prepared'){throw 'Unexpected prepared release identity'}
Assert-PatchInstalled $result.directory $latest
$old=Read-PatchManifest $oldManifest
$changed=@($latest.files|Where-Object { $f=$_; @($old.files|Where-Object {$_.path -ceq $f.path -and $_.sha256 -ceq $f.sha256}).Count -eq 0})
[long]$expectedBytes=($changed|Measure-Object packedSize -Sum).Sum
$events=@(Get-Content -LiteralPath $log|Where-Object {$_ -like 'FC_PROGRESS *'}|ForEach-Object {$_.Substring(12)|ConvertFrom-Json})
$downloads=@($events|Where-Object phase -CEQ download)
if(!$downloads.Count -or @($downloads|Where-Object {[long]$_.total -ne $expectedBytes -or [long]$_.done -gt $expectedBytes}).Count -or [long]$downloads[-1].done -ne $expectedBytes -or $downloads[-1].percent -ne 100){throw 'Actual byte progress mismatch'}
if(@($events|Where-Object phase -CEQ reuse).Count -ne (169-$changed.Count)){throw 'Unexpected reused files'}
for($i=1;$i -lt $downloads.Count;$i++){if($downloads[$i].done -lt $downloads[$i-1].done){throw 'Regressed byte progress'}}
if((Get-PatchCurrent $root).Manifest.sequence -ne 2){throw 'Prepare-only worker activated before restart'}
$before=Json (Join-Path $qa 'before.json');$after=UserState
if($before.userPointerSha256 -cne $after.userPointerSha256 -or $before.userManifestSha256 -cne $after.userManifestSha256){throw 'User cache changed during test'}
if($before.saves.Count -ne $after.saves.Count){throw 'Save list changed'}
foreach($s in $before.saves){if(@($after.saves|Where-Object {$_.name -ceq $s.name -and $_.sha256 -ceq $s.sha256}).Count -ne 1){throw 'Save changed'}}
Write-PatchJsonAtomic (Join-Path $qa 'after.json') $after
Write-PatchJsonAtomic (Join-Path $qa 'public-delta.json') @{passed=$true;independent=$true;commit=$latest.commit;version=$latest.version;
 publicWorkerExitCode=$exitCode;publicWorkerSha256=(Get-PatchHash (Join-Path $player 'FamilyCompanyPatch/FamilyCompany.InGame.ps1'));
 manifestSha256=$result.manifestHash;changedFiles=$changed;reusedFiles=(169-$changed.Count);verifiedFiles=$latest.files.Count;
 downloadedCompressedBytes=$expectedBytes;downloadProgressEvents=$downloads.Count;lastDownloadEvent=$downloads[-1];
 userMainUnchanged=$true;userCacheUnchanged=$true;saveFilesUnchanged=$after.saves.Count;
 userStillOnSequence=$after.userSequence;qaCurrentStillOnSequence=2;
 beforeStateSha256=(Get-PatchHash (Join-Path $qa 'before.json'));afterStateSha256=(Get-PatchHash (Join-Path $qa 'after.json'));
 workerLogSha256=(Get-PatchHash $log);unityRestartExercised=$false;nativeInputUsed=$false;
 scope='Actual unchanged shipping worker with public GitHub latest/API/download, isolated v2 to v3 store; SHA validation of all 169 files and real compressed-byte progress through 100%. PrepareOnly does not activate or launch Unity. Existing v2 Unity patch UI/restart implementation unchanged and previously tested. User will personally observe next real main launch; user cache/main/saves were read only and unchanged.'}
Write-Output "PUBLIC DELTA PASS: $($changed.Count) files / $expectedBytes bytes / $($downloads.Count) progress events / user remains v$($after.userSequence)"
