$ErrorActionPreference='Stop'
$taskRepo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $taskRepo
. Tools/Updater/FamilyCompany.Update.ps1
$taskQa=Join-Path $taskRepo 'Artifacts/CasualUiRelease20260907/public-delta'
$taskRoot=Join-Path $taskQa 'install'
$taskMain='C:\Users\godho\Downloads\FamilyCompany_Playtest'
$taskStore='C:\Users\godho\AppData\Local\FamilyCompany\PatchedGame'
$taskSaveDir='C:\Users\godho\AppData\LocalLow\FamilyCompany\FamilyCompanyPrototype'
$taskOldManifestPath=Join-Path $taskRepo 'Artifacts/Patches/fc-win-20260907.2/family-company-manifest.json'
$taskOld=Read-PatchManifest $taskOldManifestPath
$taskOldPayload=Join-Path $taskRepo 'Artifacts/PatchCandidates/c5199855-4238547758784d2bab8edfc141ed1fa9/payload'
$taskPlayer=Join-Path $taskRepo 'Artifacts/PatchCandidates/cb86605d-50e1e763ca534f269e904f68698e5383/payload'
function Json([string]$Path){Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json}
function UserState {
 $taskMainManifest='Artifacts/Patches/fc-win-20260906.2/family-company-manifest.json'
 $taskM=Read-PatchManifest $taskMainManifest
 Assert-PatchInstalled $taskMain $taskM
 $taskCurrent=Get-PatchCurrent $taskStore
 Assert-PatchInstalled $taskCurrent.Directory $taskCurrent.Manifest
 return @{main=$taskMain;mainManifestSha256=(Get-PatchHash $taskMainManifest);mainFilesVerified=$taskM.files.Count;
  userPointerSha256=(Get-PatchHash (Join-Path $taskStore 'current.json'));userManifestSha256=$taskCurrent.Hash;userSequence=$taskCurrent.Manifest.sequence;
  saves=@(Get-ChildItem -LiteralPath $taskSaveDir -File -Force|Where-Object Name -Like 'family-company-save*'|ForEach-Object {@{name=$_.Name;sha256=(Get-PatchHash $_.FullName);size=$_.Length}})}
}
if(Test-Path -LiteralPath $taskQa){throw 'Preserve previous public test; do not overwrite.'}
[void][IO.Directory]::CreateDirectory($taskQa)
Write-PatchJsonAtomic (Join-Path $taskQa 'before.json') (UserState)
Assert-PatchInstalled $taskOldPayload $taskOld
$taskSeed=Install-CompanyPatch $taskRoot $taskOldManifestPath (Get-PatchHash $taskOldManifestPath) -SeedDirectory $taskOldPayload
if($taskSeed.DownloadedFiles -ne 0 -or $taskSeed.ReusedFiles -ne $taskOld.files.Count){throw 'Isolated v5 seed mismatch.'}
Write-PatchJsonAtomic (Join-Path $taskQa 'seed.json') $taskSeed
$taskLog=Join-Path $taskQa 'worker.txt'
& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $taskPlayer 'FamilyCompanyPatch/FamilyCompany.InGame.ps1') -GameDirectory $taskOldPayload -InstallRoot $taskRoot -ResultPath (Join-Path $taskQa 'worker-result.json') *> $taskLog
$taskExit=$LASTEXITCODE
if($taskExit -ne 0){throw "Actual public worker failed: $taskExit"}
$taskResult=Json (Join-Path $taskQa 'worker-result.json')
$taskLatest=Read-PatchManifest (Join-Path $taskResult.directory 'family-company-manifest.json')
if($taskLatest.version -cne 'fc-win-20260907.3' -or $taskLatest.sequence -ne 6 -or $taskLatest.commit -cne 'cb86605dad19a823551498c2d4bb6bd073031864' -or $taskResult.status -cne 'prepared'){throw 'Unexpected public prepared identity.'}
Assert-PatchInstalled $taskResult.directory $taskLatest
$taskChanged=@($taskLatest.files|Where-Object {$taskFile=$_;@($taskOld.files|Where-Object {$_.path -ceq $taskFile.path -and $_.sha256 -ceq $taskFile.sha256}).Count -eq 0})
[long]$taskBytes=($taskChanged|Measure-Object packedSize -Sum).Sum
$taskEvents=@(Get-Content -LiteralPath $taskLog|Where-Object {$_ -like 'FC_PROGRESS *'}|ForEach-Object {$_.Substring(12)|ConvertFrom-Json})
$taskDownloads=@($taskEvents|Where-Object phase -CEQ download)
if(!$taskDownloads.Count -or @($taskDownloads|Where-Object {[long]$_.total -ne $taskBytes -or [long]$_.done -gt $taskBytes}).Count -or [long]$taskDownloads[-1].done -ne $taskBytes -or $taskDownloads[-1].percent -ne 100){throw 'Actual aggregate byte progress mismatch.'}
$taskReused=$taskLatest.files.Count-$taskChanged.Count
if(@($taskEvents|Where-Object phase -CEQ reuse).Count -ne $taskReused){throw 'Reuse count mismatch.'}
for($taskI=1;$taskI -lt $taskDownloads.Count;$taskI++){if($taskDownloads[$taskI].done -lt $taskDownloads[$taskI-1].done){throw 'Regressed aggregate download bytes.'}}
if((Get-PatchCurrent $taskRoot).Manifest.sequence -ne 5){throw 'Prepare-only worker activated prematurely.'}
$taskBefore=Json (Join-Path $taskQa 'before.json');$taskAfter=UserState
if($taskBefore.userPointerSha256 -cne $taskAfter.userPointerSha256 -or $taskBefore.userManifestSha256 -cne $taskAfter.userManifestSha256 -or $taskBefore.saves.Count -ne $taskAfter.saves.Count){throw 'User state changed during test.'}
foreach($taskSave in $taskBefore.saves){if(@($taskAfter.saves|Where-Object {$_.name -ceq $taskSave.name -and $_.sha256 -ceq $taskSave.sha256}).Count -ne 1){throw 'Save changed.'}}
Write-PatchJsonAtomic (Join-Path $taskQa 'after.json') $taskAfter
Write-PatchJsonAtomic (Join-Path $taskQa 'public-delta.json') @{passed=$true;independent=$true;commit=$taskLatest.commit;version=$taskLatest.version;publicWorkerExitCode=$taskExit;publicWorkerSha256=(Get-PatchHash (Join-Path $taskPlayer 'FamilyCompanyPatch/FamilyCompany.InGame.ps1'));manifestSha256=$taskResult.manifestHash;changedFiles=$taskChanged;reusedFiles=$taskReused;verifiedFiles=$taskLatest.files.Count;downloadedCompressedBytes=$taskBytes;downloadProgressEvents=$taskDownloads.Count;lastDownloadEvent=$taskDownloads[-1];userMainUnchanged=$true;userCacheUnchanged=$true;saveFilesUnchanged=$taskAfter.saves.Count;userStillOnSequence=$taskAfter.userSequence;qaCurrentStillOnSequence=5;beforeStateSha256=(Get-PatchHash (Join-Path $taskQa 'before.json'));afterStateSha256=(Get-PatchHash (Join-Path $taskQa 'after.json'));workerLogSha256=(Get-PatchHash $taskLog);unityRestartExercised=$false;nativeInputUsed=$false;scope='Fresh shipping worker and actual public GitHub latest/API/download, isolated verified v5 to v6; real aggregate compressed-byte progress, all hashes checked. Does not activate or launch Unity. Actual new Unity overall UI/four-second notice/real child restart covered by separate local-fixture tests. User main/cache/saves read-only; approved main refresh occurs afterwards.'}
Write-Output "PUBLIC DELTA PASS: $($taskChanged.Count) files / $taskBytes bytes / $($taskDownloads.Count) events; user state unchanged."
