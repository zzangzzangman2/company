param([switch]$Apply)
$ErrorActionPreference='Stop'
$taskRepo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $taskRepo
. Tools/Updater/FamilyCompany.Update.ps1
$taskVersion='fc-win-20260907.3'
$taskCommit='cb86605dad19a823551498c2d4bb6bd073031864'
$taskMain='C:\Users\godho\Downloads\FamilyCompany_Playtest'
$taskStore='C:\Users\godho\AppData\Local\FamilyCompany\PatchedGame'
$taskSaves='C:\Users\godho\AppData\LocalLow\FamilyCompany\FamilyCompanyPrototype'
$taskOut=Join-Path $taskRepo 'Artifacts/CasualUiRelease20260907/main-refresh'
$taskPackage=Join-Path $taskRepo ('Artifacts/Patches/'+$taskVersion)
$taskOldManifest=Read-PatchManifest 'Artifacts/Patches/fc-win-20260906.2/family-company-manifest.json'
$taskManifestPath=Join-Path $taskPackage 'family-company-manifest.json'
$taskManifest=Read-PatchManifest $taskManifestPath
if(!$Apply){throw 'Explicit -Apply requires the recorded user approval: 메인도 백업 후 갱신.'}
if(Test-Path -LiteralPath $taskOut){throw 'Preserve previous installer evidence; inspect before retry.'}
if($taskManifest.version -cne $taskVersion -or $taskManifest.sequence -ne 6 -or $taskManifest.commit -cne $taskCommit){throw 'Wrong new package.'}
$taskProof=Get-Content 'Artifacts/CasualUiRelease20260907/public-delta/public-delta.json' -Raw|ConvertFrom-Json
if(!$taskProof.passed -or $taskProof.commit -cne $taskCommit -or $taskProof.version -cne $taskVersion){throw 'Public transfer must be verified first.'}
$taskRelease=((& gh api repos/zzangzzangman2/company/releases/latest) -join "`n")|ConvertFrom-Json
if($LASTEXITCODE -ne 0 -or $taskRelease.draft -or $taskRelease.prerelease -or $taskRelease.tag_name -cne $taskVersion -or $taskRelease.target_commitish -cne $taskCommit){throw 'Latest public release differs.'}
$taskZip=Join-Path $taskPackage 'FamilyCompany-Windows.zip'
foreach($taskName in @('FamilyCompany-Windows.zip','family-company-manifest.json')){
 $taskAsset=@($taskRelease.assets|Where-Object name -CEQ $taskName)
 $taskLocal=Join-Path $taskPackage $taskName
 if($taskAsset.Count -ne 1 -or $taskAsset[0].size -ne (Get-Item -LiteralPath $taskLocal).Length -or $taskAsset[0].digest -cne ('sha256:'+(Get-PatchHash $taskLocal))){throw 'Public package digest mismatch.'}
}
function Snapshot-Files([string]$Directory){
 Assert-PatchNoReparse $Directory
 return @(Get-ChildItem -LiteralPath $Directory -Recurse -Force -File|Sort-Object FullName|ForEach-Object {
  Assert-PatchNoReparse $_.FullName
  @{path=$_.FullName.Substring($Directory.Length+1).Replace('\','/');sha256=(Get-PatchHash $_.FullName);size=$_.Length}
 })
}
function Assert-SameFiles($Before,$After){
 if(@($Before).Count -ne @($After).Count){throw 'Preserved file count changed.'}
 foreach($taskFile in $Before){if(@($After|Where-Object {$_.path -ceq $taskFile.path -and $_.sha256 -ceq $taskFile.sha256 -and $_.size -eq $taskFile.size}).Count -ne 1){throw ('Preserved file changed: '+$taskFile.path)}}
}
function Check-GameStopped {
 $taskProcesses=@(Get-CimInstance Win32_Process|Where-Object {$_.Name -like 'FamilyCompany*.exe'})
 if($taskProcesses.Count){throw 'A game is running; do not stop it or replace installation.'}
}
function Assert-ExactChild([string]$Path,[string]$Parent){
 $taskResolved=[IO.Path]::GetFullPath($Path)
 if(!$taskResolved.StartsWith([IO.Path]::GetFullPath($Parent).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Installer target escaped its explicit root.'}
 Assert-PatchNoReparse $taskResolved
 foreach($taskItem in @(Get-ChildItem -LiteralPath $taskResolved -Recurse -Force -ErrorAction SilentlyContinue)){Assert-PatchNoReparse $taskItem.FullName}
}
Check-GameStopped
Assert-PatchInstalled $taskMain $taskOldManifest
$taskBeforeMain=Snapshot-Files $taskMain
$taskBeforeSaves=Snapshot-Files $taskSaves
$taskBeforeCache=Snapshot-Files $taskStore
$taskUnique=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N')
$taskBackupParent='C:\Users\godho\AppData\Local\FamilyCompany\InstallationBackups'
$taskStagingParent='C:\Users\godho\AppData\Local\FamilyCompany\InstallerStaging'
$taskBackup=Join-Path $taskBackupParent ($taskUnique+'-before-'+$taskVersion+'\FamilyCompany_Playtest')
$taskStage=Join-Path $taskStagingParent ($taskVersion+'-'+$taskUnique)
$taskFailed=Join-Path $taskStagingParent ('failed-'+$taskVersion+'-'+$taskUnique)
foreach($taskTarget in @($taskBackup,$taskStage,$taskFailed)){if(Test-Path -LiteralPath $taskTarget){throw 'Unique target already exists.'}}
Assert-ExactChild $taskMain 'C:\Users\godho\Downloads'
Assert-ExactChild $taskBackup $taskBackupParent
Assert-ExactChild $taskStage $taskStagingParent
Assert-ExactChild $taskFailed $taskStagingParent
foreach($taskTarget in @($taskBackup,$taskStage,$taskFailed)){if([IO.Path]::GetPathRoot($taskTarget) -cne [IO.Path]::GetPathRoot($taskMain)){throw 'Same-volume rename required.'}}
[void][IO.Directory]::CreateDirectory($taskOut)
Write-PatchJsonAtomic (Join-Path $taskOut 'before.json') @{main=$taskMain;files=$taskBeforeMain;saves=$taskBeforeSaves;cache=$taskBeforeCache;backup=$taskBackup;stage=$taskStage;approval='User explicitly approved 메인도 백업 후 갱신; retain main path, whole old installation, all saves and patch cache.'}
[void][IO.Directory]::CreateDirectory($taskStage)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskZip)
try{
 if(@($taskArchive.Entries|Where-Object Name -NE '').Count -ne $taskManifest.files.Count){throw 'Unexpected archive entries.'}
 foreach($taskEntry in $taskArchive.Entries){
  if(!$taskEntry.Name){continue}
  $taskExpected=@($taskManifest.files|Where-Object path -CEQ $taskEntry.FullName)
  if($taskExpected.Count -ne 1 -or $taskEntry.Length -ne $taskExpected[0].size){throw 'Unexpected ZIP path or size.'}
  $taskDestination=Resolve-PatchChild $taskStage $taskEntry.FullName
  [void][IO.Directory]::CreateDirectory((Split-Path $taskDestination -Parent))
  [IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry,$taskDestination,$false)
 }
}finally{$taskArchive.Dispose()}
Assert-PatchInstalled $taskStage $taskManifest
Assert-SameFiles $taskBeforeMain (Snapshot-Files $taskMain)
Assert-SameFiles $taskBeforeSaves (Snapshot-Files $taskSaves)
Assert-SameFiles $taskBeforeCache (Snapshot-Files $taskStore)
Check-GameStopped
[void][IO.Directory]::CreateDirectory((Split-Path $taskBackup -Parent))
Assert-ExactChild $taskMain 'C:\Users\godho\Downloads'
Assert-ExactChild $taskBackup $taskBackupParent
Assert-ExactChild $taskStage $taskStagingParent
# Preserve the entire old installation first by same-volume rename; nothing is deleted.
$taskOldMoved=$false;$taskNewMoved=$false
try{
 Move-Item -LiteralPath $taskMain -Destination $taskBackup
 $taskOldMoved=$true
 Assert-SameFiles $taskBeforeMain (Snapshot-Files $taskBackup)
 Move-Item -LiteralPath $taskStage -Destination $taskMain
 $taskNewMoved=$true
 Assert-PatchInstalled $taskMain $taskManifest
 Assert-SameFiles $taskBeforeSaves (Snapshot-Files $taskSaves)
 Assert-SameFiles $taskBeforeCache (Snapshot-Files $taskStore)
 Assert-SameFiles $taskBeforeMain (Snapshot-Files $taskBackup)
 Write-PatchJsonAtomic (Join-Path $taskOut 'result.json') @{passed=$true;version=$taskVersion;commit=$taskCommit;main=$taskMain;backup=$taskBackup;oldFiles=$taskBeforeMain.Count;newFiles=$taskManifest.files.Count;savesPreserved=@($taskBeforeSaves|Where-Object path -Like 'family-company-save*').Count;saveDirectoryFilesPreserved=$taskBeforeSaves.Count;cacheFilesPreserved=$taskBeforeCache.Count;publicZipSha256=(Get-PatchHash $taskZip);publicManifestSha256=(Get-PatchHash $taskManifestPath);sameEntryPath=$true;gameLaunched=$false;deletedFiles=0;scope='Whole previous v2 installation retained in backup; public-verified v6 bootstrap installed at same main path; no saves/cache mutation. Existing user v5 cache remains and takes precedence over seed files in current worker, so next user launch downloads the verified v5-to-v6 delta and activates v6. Thereafter unchanged snapshots require no repeated download.'}
}catch{
 $taskFailure=$_.Exception.Message
 if($taskNewMoved){
  Assert-ExactChild $taskMain 'C:\Users\godho\Downloads'
  Assert-ExactChild $taskFailed $taskStagingParent
  Move-Item -LiteralPath $taskMain -Destination $taskFailed
 }
 if($taskOldMoved -and !(Test-Path -LiteralPath $taskMain)){
  Assert-ExactChild $taskBackup $taskBackupParent
  Move-Item -LiteralPath $taskBackup -Destination $taskMain
  Assert-SameFiles $taskBeforeMain (Snapshot-Files $taskMain)
 }
 Write-PatchJsonAtomic (Join-Path $taskOut 'failure.json') @{failure=$taskFailure;oldMoved=$taskOldMoved;newMoved=$taskNewMoved;failedStage=$taskFailed;scope='No recursive deletion; exact old installation restored when possible.'}
 throw
}
Write-Output ('MAIN REFRESH PASS: '+$taskMain+'; backup='+$taskBackup)
