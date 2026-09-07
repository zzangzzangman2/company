# Adaptation of Tools/Updater/Test-FamilyCompanyUnityRestart.ps1 for a Release already named FamilyCompany.exe.
param([Parameter(Mandatory=$true)][string]$Player, [switch]$AllowGraphicsQa)
$ErrorActionPreference='Stop'
if(!$AllowGraphicsQa){throw 'Fresh graphics authorization required'}
$repo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $repo
$taskWorkers=Join-Path $repo 'Tools/Updater'
. (Join-Path $taskWorkers 'FamilyCompany.Package.ps1')
$Player=(Resolve-Path -LiteralPath $Player).Path
if([IO.Path]::GetFileName($Player) -cne 'FamilyCompany.exe'){throw 'Actual normal Release required'}
$root=Join-Path $repo ('Artifacts/UnityPatchRestartTests/'+[Guid]::NewGuid().ToString('N'))
$base=Join-Path $root 'base';$source=Join-Path $root 'source'
[void][IO.Directory]::CreateDirectory($base)
Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($Player)) | Copy-Item -Destination $base -Recurse
$workers=Join-Path $base 'FamilyCompanyPatch'
foreach($file in @('FamilyCompany.Update.ps1','FamilyCompany.Restart.ps1')) {
 Copy-Item -LiteralPath (Join-Path $taskWorkers $file) -Destination $workers
 Copy-Item -LiteralPath (Join-Path $taskWorkers $file) -Destination $root
}
Copy-Item -LiteralPath (Join-Path $taskWorkers 'Tests/UnityRestart.Worker.ps1') -Destination (Join-Path $workers 'FamilyCompany.InGame.ps1')
Copy-Item -LiteralPath (Join-Path $workers 'FamilyCompany.InGame.ps1') -Destination $root
Write-PatchJsonAtomic (Join-Path $workers 'unity-test-root.json') @{root=$root}
Write-PatchJsonAtomic (Join-Path $root 'unity-test-root.json') @{root=$root}
[IO.File]::WriteAllBytes((Join-Path $workers 'patch-check.bytes'), (New-Object byte[] (4MB)))
Copy-Item -LiteralPath $base -Destination $source -Recurse
$bytes=New-Object byte[] (4MB);[Random]::new(260906).NextBytes($bytes)
[IO.File]::WriteAllBytes((Join-Path $source 'FamilyCompanyPatch/patch-check.bytes'),$bytes)
$commit=(& git rev-parse HEAD).Trim()
$package=New-CompanyPatchPackage $source (Join-Path $root 'feed') 'fc-win-20260906.9001' 9001 $commit
Write-PatchJsonAtomic (Join-Path $root 'identity.json') @{scope='LOCAL exact Release parent/child install and restart; not public transfer';root=$root;manifest=$package.ManifestPath;manifestHash=$package.ManifestHash;commit=$commit;
 basePlayer=$Player;basePlayerHash=(Get-PatchHash $Player);buildInfoSha256=(Get-PatchHash (Join-Path (Split-Path $Player -Parent) 'BUILD_INFO.txt'));changedPath='FamilyCompanyPatch/patch-check.bytes';expectedChangedHash=(Get-PatchHash (Join-Path $source 'FamilyCompanyPatch/patch-check.bytes'))}
$qaParent=Join-Path $root 'qa-parent'
Copy-Item -LiteralPath $base -Destination $qaParent -Recurse
# Non-Development Release ignores fixture flags under its production data-folder name.
# Rename only this isolated copy to the existing explicit QA identity; executable and DLL bytes stay identical.
Rename-Item -LiteralPath (Join-Path $qaParent 'FamilyCompany.exe') -NewName 'FamilyCompany_FastQa.exe'
Rename-Item -LiteralPath (Join-Path $qaParent 'FamilyCompany_Data') -NewName 'FamilyCompany_FastQa_Data'
$qaPlayer=Join-Path $qaParent 'FamilyCompany_FastQa.exe'
if((Get-PatchHash $qaPlayer) -cne (Get-PatchHash $Player)){throw 'QA copy bytes differ'}
Add-Type -Path (Join-Path $repo 'Tools/Background/CompanyQaDesktop.cs')
$taskArgs='-force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -familyCompanyInGamePatchQa "'+$root+'" -familyCompanyInGamePatchRestartQa -familyCompanyRestartChildBackgroundQa -logFile "'+(Join-Path $root 'parent.log')+'"'
$isolation=[CompanyQaDesktop]::Start($qaPlayer,$taskArgs,$repo,$AllowGraphicsQa.IsPresent)
$parent=$isolation.Process
Write-Output "RELEASE UNITY PATCH TEST parent=$($parent.Id) root=$root"
try {
 $deadline=[DateTime]::UtcNow.AddSeconds(180)
 while(!$parent.WaitForExit(200) -and [DateTime]::UtcNow -lt $deadline){}
 if(!$parent.HasExited -or $parent.ExitCode -ne 0){throw 'Release parent did not exit normally'}
 $parentText=Get-Content -LiteralPath (Join-Path $root 'parent.log') -Raw
 if($parentText -notmatch 'IN_GAME_PATCH_START actualUnityUi=true qa=True'){throw 'Fixture isolation was not activated'}
 if($parentText -notmatch 'IN_GAME_PATCH_RESTART_NOTICE_COMPLETE visibleSeconds=([0-9.]+)' -or
    [double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) -lt 4 -or
    !(Test-Path -LiteralPath (Join-Path $root 'restart-notice.png'))){throw 'Actual four-second notice missing'}
 if($parentText -notmatch 'IN_GAME_PATCH_OVERALL_COMPLETE percent=100 validatedResult=true'){throw 'Validated overall completion missing'}
 [double]$previous=0
 foreach($event in [regex]::Matches($parentText,'IN_GAME_PATCH_PROGRESS phase=\S+[^\r\n]* percent=([0-9.]+) ')){
  $value=[double]::Parse($event.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
  if($value -lt $previous -or $value -gt 99){throw 'Overall progress reset or premature 100'}
  $previous=$value
 }
 $current=$null;$child=$null
 while([DateTime]::UtcNow -lt $deadline){
  $current=Get-PatchCurrent (Join-Path $root 'install')
  if($current){$child=Get-Process FamilyCompany -ErrorAction SilentlyContinue|Where-Object Path -EQ (Join-Path $current.Directory 'FamilyCompany.exe');if($child){break}}
  Start-Sleep -Milliseconds 200
 }
 if(!$child){throw 'Exact patched child not observed'}
 $childPath=$child.Path
 Assert-PatchInstalled $current.Directory $current.Manifest
 if(!$child.WaitForExit(30000) -or !(Select-String -LiteralPath (Join-Path $root 'install/patched-player.log') -SimpleMatch 'IN_GAME_PATCH_READY_CURRENT' -Quiet)){throw 'Verified child did not unlock game'}
 if((Get-PatchHash $Player) -cne (Get-Content -LiteralPath (Join-Path $root 'identity.json') -Raw|ConvertFrom-Json).basePlayerHash){throw 'Parent Release changed'}
 Write-PatchJsonAtomic (Join-Path $root 'restart-observed.json') @{passed=$true;commit=$commit;parentExit=$parent.ExitCode;childId=$child.Id;childPath=$childPath;currentDirectory=$current.Directory;mainEntryUnchanged=$true;noticeAtLeastSeconds=4;overallMonotonic=$true;privateDesktop=$isolation.DesktopName;desktopSwitchAllowed=$false;nativeInput=$false;scope='Exact non-Development Release executable/DLL bytes in renamed QA parent folder activate existing fixture switch; real normal-named Unity successor, local transport fixture workers only. Not a public network/UI test.'}
 Write-Output "RELEASE UNITY RESTART PASS: $root"
 Write-PatchJsonAtomic (Join-Path $repo 'Artifacts/CasualUiRelease20260907/restart-test.json') @{root=$root;passed=$true;commit=$commit;receiptHash=(Get-PatchHash (Join-Path $root 'restart-observed.json'))}
} finally {$isolation.Dispose()}
