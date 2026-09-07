$ErrorActionPreference='Stop'
$taskRepo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $taskRepo
. Tools/Updater/FamilyCompany.Update.ps1
$taskSelf=[Diagnostics.Process]::GetCurrentProcess();$taskSelf.PriorityClass='BelowNormal';$taskSelf.ProcessorAffinity=[IntPtr]3
$taskPlayer=Join-Path $taskRepo 'Artifacts/PatchCandidates/cb86605d-50e1e763ca534f269e904f68698e5383/payload/FamilyCompany.exe'
$taskRoot=Join-Path $taskRepo 'Artifacts/CasualUiRelease20260907/fixture-entry-guards-v2'
if(Test-Path -LiteralPath $taskRoot){throw 'Preserve prior policy evidence.'}
[void][IO.Directory]::CreateDirectory($taskRoot)
if(@(Get-CimInstance Win32_Process|Where-Object Name -Like 'FamilyCompany*.exe').Count){throw 'Do not test while user game is running.'}
$taskBefore=@(Get-ChildItem Artifacts/InGamePatchTests,Artifacts/UnityPatchRestartTests -Directory|Sort-Object FullName|Select-Object -ExpandProperty FullName)
$taskChecks=@()
foreach($taskRuntime in @('powershell.exe',(Get-Process -Id $PID).Path)){
 foreach($taskKind in @('InGamePatch','UnityRestart')){
  $taskScript='Tools/Updater/Test-FamilyCompany'+$taskKind+'.ps1'
  $taskFlag=if($taskKind -eq 'InGamePatch'){'-PrivateDesktop'}else{'-Background'}
  $taskLog=Join-Path $taskRoot (([IO.Path]::GetFileNameWithoutExtension($taskRuntime))+'-'+$taskKind+'.log')
  & $taskRuntime -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $taskScript -Player $taskPlayer -AllowGraphicsQa $taskFlag *> $taskLog
  $taskExit=$LASTEXITCODE
  # PS7 wraps formatted errors with a leading pipe. Normalize display wrapping, not the refusal condition.
  $taskMessage=((Get-Content -LiteralPath $taskLog -Raw) -replace '(?m)^\s*\|\s*',' ') -replace '\s+',' '
  if($taskExit -eq 0 -or $taskMessage -notmatch 'normal Release invocation is refused before launch'){throw 'Expected early fixture refusal missing.'}
  $taskChecks+=@{script=$taskScript;runtime=$taskRuntime;expectedExit=$taskExit;passed=$true;logSha256=(Get-PatchHash $taskLog);scriptSha256=(Get-PatchHash $taskScript)}
 }
}
$taskAfter=@(Get-ChildItem Artifacts/InGamePatchTests,Artifacts/UnityPatchRestartTests -Directory|Sort-Object FullName|Select-Object -ExpandProperty FullName)
if(($taskBefore -join "`n") -cne ($taskAfter -join "`n") -or @(Get-CimInstance Win32_Process|Where-Object Name -Like 'FamilyCompany*.exe').Count){throw 'Unexpected fixture/process creation.'}
Write-PatchJsonAtomic (Join-Path $taskRoot 'result.json') @{passed=$true;checks=$taskChecks;gameProcessesCreated=0;fixtureDirectoriesCreated=0;scope='Negative guard tests of normal-name candidate in PS5.1 and PS7. No graphical game was launched. These are post-release test-tool changes, not a rebuilt shipping game.'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'result.json') -Destination 'Docs/Evidence/CasualUiRelease20260907/fixture-entry-guards.json'
Write-Output 'FIXTURE ENTRY GUARDS PASS: 4 expected refusals, no game or fixture created.'
