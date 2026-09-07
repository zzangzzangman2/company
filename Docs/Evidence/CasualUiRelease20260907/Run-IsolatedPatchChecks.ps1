$ErrorActionPreference='Stop'
$taskRepo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $taskRepo
. Tools/Updater/FamilyCompany.Update.ps1
$taskRoot=Join-Path $taskRepo 'Artifacts/CasualUiRelease20260907'
$taskPlayer=Join-Path $taskRepo 'Artifacts/PatchCandidates/cb86605d-50e1e763ca534f269e904f68698e5383/payload/FamilyCompany.exe'
$taskCopy=Join-Path $taskRoot 'patch-ui-parent'
if(Test-Path -LiteralPath $taskCopy){throw 'Do not overwrite isolated fixture.'}
$taskSelf=[Diagnostics.Process]::GetCurrentProcess();$taskSelf.PriorityClass='BelowNormal';$taskSelf.ProcessorAffinity=[IntPtr]3
Copy-Item -LiteralPath (Split-Path $taskPlayer -Parent) -Destination $taskCopy -Recurse
Rename-Item -LiteralPath (Join-Path $taskCopy 'FamilyCompany.exe') -NewName 'FamilyCompany_FastQa.exe'
Rename-Item -LiteralPath (Join-Path $taskCopy 'FamilyCompany_Data') -NewName 'FamilyCompany_FastQa_Data'
$taskQaPlayer=Join-Path $taskCopy 'FamilyCompany_FastQa.exe'
if((Get-PatchHash $taskQaPlayer) -cne (Get-PatchHash $taskPlayer)){throw 'Copy identity differs.'}
$taskBefore=@(Get-ChildItem 'Artifacts/InGamePatchTests' -Directory|Select-Object -ExpandProperty Name)
& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File Tools/Updater/Test-FamilyCompanyInGamePatch.ps1 -Player $taskQaPlayer -PrivateDesktop -AllowGraphicsQa *> (Join-Path $taskRoot 'in-game-patch-isolated-run.log')
if($LASTEXITCODE -ne 0){throw 'Isolated Release progress test failed.'}
$taskNew=@(Get-ChildItem 'Artifacts/InGamePatchTests' -Directory|Where-Object Name -NotIn $taskBefore)
if($taskNew.Count -ne 1){throw 'Ambiguous fixture output.'}
if((Get-Content (Join-Path $taskNew[0].FullName 'player.log') -Raw) -notmatch 'IN_GAME_PATCH_START actualUnityUi=true qa=True'){throw 'QA isolation missing.'}
Write-PatchJsonAtomic (Join-Path $taskRoot 'in-game-patch-test.json') @{passed=$true;commit=((& git rev-parse HEAD).Trim());root=$taskNew[0].FullName;playerSha256=(Get-PatchHash $taskPlayer);scope='Exact Release bytes, isolated renamed QA parent to activate existing fixture flags. Local multi-folder transport and real rendered UI, not public download.'}
Write-Output ('ISOLATED RELEASE PROGRESS PASS: '+$taskNew[0].FullName)
& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $taskRoot 'Run-ReleaseRestart.ps1') -Player $taskPlayer -AllowGraphicsQa *> (Join-Path $taskRoot 'restart-run.log')
if($LASTEXITCODE -ne 0){throw 'Isolated Release restart failed.'}
Write-Output 'ISOLATED RELEASE UNITY RESTART PASS'
& 'C:\Users\godho\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Tools/analyse_opening_walk_audit.py Artifacts/ReleaseGameplay/cb86605d-walk *> (Join-Path $taskRoot 'walk-analysis.log')
if($LASTEXITCODE -ne 0){throw 'Walk analysis failed.'}
Write-Output 'WALK ANALYSIS GENERATED; direct visual review remains.'
