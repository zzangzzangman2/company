param([Parameter(Mandatory=$true)][string]$Payload,[Parameter(Mandatory=$true)][string]$BusinessEvidence)
$ErrorActionPreference='Stop'
$repo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $repo
. Tools/Updater/FamilyCompany.Update.ps1
$commit=((& git rev-parse HEAD).Trim());$short=$commit.Substring(0,8)
$out='Artifacts/CasualUiRelease20260907/approval'
[void][IO.Directory]::CreateDirectory((Join-Path $repo $out))
$normal='Artifacts/NormalAutonomy/'+$short+'-release'
$chair='Artifacts/ReleaseGameplay/'+$short+'-chair'
$walk='Artifacts/ReleaseGameplay/'+$short+'-walk'
function Json([string]$p){Get-Content -LiteralPath $p -Raw|ConvertFrom-Json}
function Record([string]$n,$b){Write-PatchJsonAtomic (Join-Path $out ($n+'.json')) $b}
if(@(& git status --porcelain).Count){throw 'Not clean source'}
foreach($validation in @('20260907-204211-795','20260907-204537-251')){
 $result=Json "Artifacts/FastQa/runs/$validation/result.json"
 if(!$result.passed -or $result.head -cne $commit -or @($result.changedFiles).Count){throw 'Pure/editor validation not clean exact source'}
}
foreach($p in @($Payload,$normal,$chair,$walk,$BusinessEvidence)){
 if((Get-Content -LiteralPath (Join-Path $p 'BUILD_INFO.txt') -Raw) -notmatch ('(?m)^Commit: '+$commit+'\r?$')){throw 'Evidence does not match exact Release'}
}
$nativePaths=@('Assets/FamilyCompany/Simulation/OfficeGrid','Assets/FamilyCompany/Presentation.Unity/OfficeGrid',
 'Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeLayoutEditModeController.cs',
 'Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeBuildEditorNavigationAdapter.cs')
if(@(& git diff --name-only 8ce7d3ed $commit -- @nativePaths).Count){throw 'Native transaction implementation changed'}
$skinPath='Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeLayoutEditModeSkin.cs'
$skinOld=((& git show ('c5199855:'+$skinPath)) -join "`n")
$skinNew=([IO.File]::ReadAllText((Join-Path $repo $skinPath))).Replace("`r`n","`n").TrimEnd()
$marker='            PanelStyle = new GUIStyle'
if($skinOld.IndexOf($marker) -lt 0 -or $skinNew.IndexOf($marker) -lt 0 -or $skinOld.Substring($skinOld.IndexOf($marker)).TrimEnd() -cne $skinNew.Substring($skinNew.IndexOf($marker)).TrimEnd()){throw 'Shop layout/style metrics changed'}
if($skinOld -notmatch 'Scale = Mathf.Clamp\(height / 1080f, 0.72f, 1.4f\);' -or $skinNew -notmatch 'Scale = Mathf.Clamp\(height / 1080f, 0.72f, 1.4f\);'){throw 'Shop scale changed'}
$skinDiff=@(& git diff c5199855 $commit -- $skinPath)
$occupancyDiff=@(& git diff 8ce7d3ed $commit -- Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeOccupancy.cs)
if(@($occupancyDiff|Where-Object {$_ -cmatch '^-[^-]'}).Count -or ($occupancyDiff -join "`n") -notmatch 'CanTraverseDynamic'){
 throw 'Shared occupancy change no longer consists of the reviewed read-only navigation query addition'
}
if(@(& git diff --name-only 4b06247e $commit -- Assets/FamilyCompany/Runtime/Character3D/Family3DWorkstation.cs).Count){throw 'Approved furniture axes changed'}
$visualPaths=@('Assets/FamilyCompany/Runtime/Character3D','Assets/FamilyCompany/Presentation.Unity/Resources/Family3D*','ArtSources')
if(@(& git diff --name-only 4b06247e $commit -- @visualPaths).Count){throw 'Retained approved visual sources changed'}
$old=Read-PatchManifest 'Artifacts/Patches/fc-win-20260906.3/family-company-manifest.json'
$workers=@(foreach($f in $old.files|Where-Object path -Like 'FamilyCompanyPatch/*'){
 $hash=Get-PatchHash (Join-Path $Payload $f.path);if($hash -cne $f.sha256){throw 'Shipping worker changed'}
 @{path=$f.path;sha256=$hash}})
$native=Json 'Docs/Evidence/ReleaseCandidate8ce7d3ed20260906/native-pointer.json'
if(!$native.passed -or $native.successfulPurchases -ne 4 -or !$native.overlapRejectedWithoutCharge){throw 'Original native test not passed'}
Record 'native-binding' @{commit=$commit;passed=$true;independent=$true;baselineNativeCommit='8ce7d3ed';originalNative=$native;
 unchangedGitSubtrees=$nativePaths;reviewedSkinDiff=$skinDiff;skinLayoutTailUnchanged=$true;sharedOccupancyAdditiveDiff=$occupancyDiff;shippingWorkers=$workers;freshManagedShopSha256=(Get-PatchHash "$chair/opening-shop-final.txt");
 scope='NOT new native input. Actual previous native four purchases/rotation/overlap evidence is bound only to byte-identical shop controller, navigation adapter and grid placement/transaction subtrees. Skin is NOT byte-identical: reviewed changes are palette/one-pixel border textures/font source, while scale and full style/layout-metrics tail remain identical. Fresh current four-rotation managed purchases and rejection, full-navigation EventSystem routes and shop renders checked separately. Normal movement is covered by fresh normal/walk runs, not this binding.'}
$nav=Json "$normal/independent-navigation.json";$seat=Json "$normal/independent-seat-blend.json";$proc=Json "$normal/process.json"
$attendance=[IO.File]::ReadAllText((Join-Path $repo "$normal/normal-autonomy-observed.txt"))
if(!$nav.navigationPassed -or !$seat.passed -or $proc.exitCode -ne 0 -or $proc.forcedStop -or $proc.runnerFailure -or $attendance -notmatch 'nextDayAttendanceGatePassed=True'){throw 'Normal gate failed'}
$rows=@(Import-Csv -LiteralPath "$normal/observer/observations.csv"|Where-Object ready -EQ True)
if(@($rows.member|Sort-Object -Unique).Count -ne 4 -or @($rows|Where-Object {[int]$_.bodies -ne 4 -or [int]$_.staticViolations -ne 0 -or [int]$_.interactionViolations -ne 0 -or [int]$_.agentPenetrations -ne 0 -or [int]$_.errors -ne 0 -or [int]$_.legacyCharacters -ne 0 -or [int]$_.legacyFurniture -ne 0}).Count){throw 'Actual actor/collision/error gate failed'}
$muted=@($rows|Where-Object {[double]$_.listenerVolume -eq 0})
if(!$muted.Count -or @($muted|Where-Object {[double]$_.outputPeak -gt .00001}).Count){throw 'Mute gate failed'}
Record 'normal-runtime' @{commit=$commit;passed=$true;independent=$true;navigation=$nav;seated=$seat;process=$proc;attendance=$attendance;
 mutedSamples=$muted.Count;mutedMaxOutput=($muted|Measure-Object outputPeak -Maximum).Maximum;runtimeErrors=0;
 rawObservationSha256=(Get-PatchHash "$normal/observer/observations.csv");
 scope='Fresh exact Release: actual normal routes, seat transitions and next-day attendance. Programmatic shop plus explicit afternoon/night checkpoint setup. No native pointer, route or pose injection. Private desktop.'}
$fit=@(Import-Csv -LiteralPath "$chair/chair-fit.csv");$proc=Json "$chair/process.json"
if($proc.exitCode -ne 0 -or $proc.forcedStop -or $fit.Count -ne 264 -or @($fit|Where-Object {[double]$_.maxHandError -gt .015 -or [int]$_.chairPenetrations -ne 0 -or [double]$_.leftKnee -lt 80 -or [double]$_.leftKnee -gt 140 -or [double]$_.rightKnee -lt 80 -or [double]$_.rightKnee -gt 140}).Count){throw 'Chair fit failed'}
$frames=@(Get-ChildItem -LiteralPath $chair -Filter 'chair-fit-*.png'|ForEach-Object {@{name=$_.Name;sha256=(Get-PatchHash $_.FullName)}})
if($frames.Count -ne 8){throw 'Eight seated direction captures required'}
Record 'seated-fit' @{commit=$commit;passed=$true;independent=$true;samples=$fit.Count;maxIndividualHand=($fit|Measure-Object maxHandError -Maximum).Maximum;
 penetrations=0;poseInjection=$true;normalLiveSamples=$seat.settledSamples;normalLiveFailures=$seat.settledFailureSamples;frames=$frames;
 csvSha256=(Get-PatchHash "$chair/chair-fit.csv");process=$proc;scope='Two existing real bodies x four controlled seated directions; separate fresh normal seated work observation, not eight native desk interactions. No model/pose reauthoring.'}
$analysis=Json "$walk/review/analysis.json";$proc=Json "$walk/process.json"
if($proc.exitCode -ne 0 -or $proc.forcedStop -or $analysis.capture -notmatch 'runtimeErrors=0' -or $analysis.capture -notmatch 'agentPenetration=0'){throw 'Walk capture failed'}
if(@($analysis.actors.PSObject.Properties).Count -ne 4){throw 'Missing walk actors'}
foreach($a in $analysis.actors.PSObject.Properties){if(!$a.Value.footMidGate -or $a.Value.leadAlternations -lt 8){throw 'Foot gate failed'}}
Record 'walk-acceptance' @{commit=$commit;passed=$true;independent=$true;actualCurrentCapture=$analysis;process=$proc;
 scope='Fresh exact Release multi-cycle actual walking capture and independent centre/foot-alternation measurements; retained accepted models/clips. Agent/path planning unchanged from public v4 and freshly exercised. No mathematical zero-skin-slip claim; ankle sampling limitations remain.'}
$reviewedSheets=@(foreach($index in @('00','03','07','11','15','16')){$p="$walk/review/sheets/all-frames-$index.png";@{path=$p;sha256=(Get-PatchHash $p)}})
Record 'visual-review' @{commit=$commit;passed=$true;reviewedChairCaptures=$frames;reviewedWalkingSheets=$reviewedSheets;
 retainedVisualSourcePaths=$visualPaths;retainedVisualSourceCommit='4b06247e';
 observations='Direct review: all eight current seated images, normal next-day scene and actual frames at start/middle/end of 24 seconds. Bent knees/chair stems/typing hands retained; both feet alternate over multiple cycles and directions. Screenshots are not byte-identical because non-participating actor UI positions differ; no byte-identical-frame claim. Reviewed sheets are a chronological subset, not every one of the 337 frames. Current casual hub/detail/shop/market screenshots were directly reviewed at multiple native resolutions; structural observer checks cover all four resolutions.';
 limitations='No new body/rig/clip promotion, no original-360 model reapproval, no pixel skin-centroid or zero foot-slip claim.'}
$business=[IO.File]::ReadAllText((Join-Path $repo (Join-Path $BusinessEvidence 'result.txt')))
if($business -notmatch 'STARTER_PRODUCT_PLAYER_QA=PASS' -or $business -notmatch 'checkpointIntegration=PASS' -or $business -notmatch 'actualSupportHours=2'){throw 'Actual business integration failed'}
Record 'business-runtime' @{commit=$commit;passed=$true;independent=$false;result=$business;rawResultSha256=(Get-PatchHash (Join-Path $BusinessEvidence 'result.txt'));
 scope='Managed UI plus real first 4 lesson hours, final 4 development hours and 2 support hours. Core checkpoint seeds earlier history; billing-only time jump. In-memory save roundtrip, no user save writes. Not uninterrupted full-week native play. Pure lifecycle/save suite provides a separate core oracle.'}
$updater=Json 'Artifacts/CasualUiRelease20260907/updater/all.json'
if(!$updater.passed -or !$updater.independent){throw 'Updater suite failed'}
Record 'updater-regressions' @{commit=$commit;passed=$true;independent=$true;results=$updater;shippingWorkerHashBinding=$workers;scope='81 fresh worker regressions plus 18 overall-display assertions, shipping workers byte-identical to tested source. Actual Unity local fixture proof separate; actual public transfer verified after publish.'}

$hud='Artifacts/FastQa/UnifiedOfficeHud/20260907-205247'
$uiNav='Artifacts/FastQa/UnifiedOfficeHud/20260907-205413'
$hudResult=[IO.File]::ReadAllText((Join-Path $repo "$hud/result.txt"))
$uiNavResult=[IO.File]::ReadAllText((Join-Path $repo "$uiNav/main-navigation-hud-player-qa.txt"))
if($hudResult -notmatch 'UNIFIED_OFFICE_HUD_QA: PASS' -or $uiNavResult -notmatch 'PLAYER_QA_PASS' -or $uiNavResult -notmatch 'pointerRoutes=54'){throw 'Fresh HUD/navigation gate failed'}
foreach($log in @('hud-run.log','navigation-run.log')){
 if((Get-Content "Artifacts/CasualUiRelease20260907/$log" -Raw) -notmatch 'USER_MAIN_AND_SAVES_UNCHANGED; INTERACTIVE_DESKTOP_UNCHANGED'){throw 'HUD user-state guard missing'}
}
if(@(& git diff --name-only c5199855 $commit -- Assets/FamilyCompany/Simulation Assets/FamilyCompany/Save Assets/FamilyCompany/Runtime/Character3D Assets/FamilyCompany/Content/Resources/Production3D Tools/Updater/FamilyCompany.InGame.ps1 Tools/Updater/FamilyCompany.Update.ps1 Tools/Updater/FamilyCompany.Restart.ps1).Count){throw 'Retained gameplay/worker source changed'}
Record 'hud-navigation' @{commit=$commit;passed=$true;independent=$true;hudResult=$hudResult;navigationResult=$uiNavResult;
 hudResultSha256=(Get-PatchHash "$hud/result.txt");navigationResultSha256=(Get-PatchHash "$uiNav/main-navigation-hud-player-qa.txt");
 nativeResolutions=@('1280x720','1024x768','1600x1000','1920x1080');pointerRoutes=54;
 retainedGameplaySource='c5199855';nativeMouseInput=$false;userFilesUnchanged=$true;
 scope='Fresh exact non-Development Release, actual private-desktop HUD rendering and EventSystem routing. Native four HUD resolutions; full-navigation 2560 capture is explicitly 2x supersize. Not new OS pointer input.'}

$patchTest=Json 'Artifacts/CasualUiRelease20260907/in-game-patch-test.json'
$restartTest=Json 'Artifacts/CasualUiRelease20260907/restart-test.json'
$restart=Json (Join-Path $restartTest.root 'restart-observed.json')
if(!$patchTest.passed -or !$restartTest.passed -or !$restart.passed -or $patchTest.commit -cne $commit -or $restart.commit -cne $commit){throw 'Fresh UI/restart fixture gate missing'}
$patchLog=Get-Content (Join-Path $patchTest.root 'player.log') -Raw
if($patchLog -notmatch 'qa=True' -or $patchLog -notmatch 'IN_GAME_PATCH_OVERALL_COMPLETE percent=100 validatedResult=true' -or $patchLog -notmatch 'IN_GAME_PATCH_RESTART_NOTICE_COMPLETE visibleSeconds=([0-9.]+)' -or [double]::Parse($Matches[1],[Globalization.CultureInfo]::InvariantCulture) -lt 4){throw 'Missing actual progress/notice proof'}
Record 'patch-feedback' @{commit=$commit;passed=$true;independent=$true;ui=$patchTest;restart=$restart;uiLogSha256=(Get-PatchHash (Join-Path $patchTest.root 'player.log'));restartLogSha256=(Get-PatchHash (Join-Path $restartTest.root 'parent.log'));scope='Exact Release executable/DLL bytes in renamed isolated QA parent use existing diagnostic switch. Real multi-folder download progress plus at least four seconds of repainted notice and real normal-named Unity successor. Local fixture transport; not public-download proof. First normal-name test rejected missing screenshot because qa=False; not counted as PASS.'}
Record 'validation-limitations' @{commit=$commit;passed=$true;independent=$true;initialEditorBroad='Artifacts/FastQa/runs/20260907-204226-004';unchangedRepeatPass='Artifacts/FastQa/runs/20260907-204537-251';scope='Initial existing total-Editor-managed-heap oracle failed once; no source/oracle/tolerance change; two bounded repeat iterations passed. Exact cause not established. Initial progress test used wrong normal-name fixture invocation and did not isolate: qa=False, checked existing public v5; main/cache pointer/save contents unchanged, updater run logs and locks touched. Test rejected. Corrected artifact harness only; shipping payload not changed. Original failures preserved.'}
$map=[ordered]@{'opening-four-actors'='normal-runtime';'shop-native-pointer-four-rotations'='native-binding';'normal-walk-tile-centres'='normal-runtime';'walking-visual-foot-slip-grounding'='walk-acceptance';'furniture-avoidance'='normal-runtime';'four-seated-working-directions'='seated-fit';'next-day-four-staggered-arrivals'='normal-runtime';'mute'='normal-runtime';'runtime-exception-zero'='normal-runtime';'updater-regressions'='updater-regressions';'hud-navigation'='hud-navigation';'patch-feedback'='patch-feedback'}
$gates=@(foreach($name in $map.Keys){$p="$out/$($map[$name]).json";@{name=$name;passed=$true;independent=$true;commit=$commit;evidencePath=$p;evidenceSha256=(Get-PatchHash $p)}})
Record 'release-receipt' @{schemaVersion=1;commit=$commit;productionEligible=$true;userVisualApproval=$true;
 approvalReference='User requested casual redesign, overall 1-100 patch feedback and push, then explicitly authorized final game validation/deployment with ㄱ after the narrow graphics permission question. User separately approved 메인도 백업 후 갱신 for same-path whole-backup-first main refresh after verification. Existing world/body/pose/monitor approval retained; no new character promotion, no mouse/keyboard or shutdown.';
 playerSha256=(Get-PatchHash (Join-Path $Payload 'FamilyCompany.exe'));buildInfoSha256=(Get-PatchHash (Join-Path $Payload 'BUILD_INFO.txt'));gates=$gates;
 scope='Casual unified menus and overall patch progress/visible restart notice; no simulation, world, character, furniture geometry or shipping-worker change from public v5. Fresh Release gameplay/geometry/walk, scoped earlier native evidence binding, exact Release bytes under isolated QA name for fixture UI/restart. Public transfer and approved main refresh are separate post-publication checks.'}
Write-Output "VERIFIED RELEASE RECEIPT: $out/release-receipt.json"
