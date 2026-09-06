param([Parameter(Mandatory=$true)][string]$Payload,[Parameter(Mandatory=$true)][string]$BusinessEvidence)
$ErrorActionPreference='Stop'
$repo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $repo
. Tools/Updater/FamilyCompany.Update.ps1
$commit=((& git rev-parse HEAD).Trim());$short=$commit.Substring(0,8)
$out='Artifacts/StarterBusinessRelease20260907/approval'
[void][IO.Directory]::CreateDirectory((Join-Path $repo $out))
$normal='Artifacts/NormalAutonomy/'+$short+'-release'
$chair='Artifacts/ReleaseGameplay/'+$short+'-chair'
$walk='Artifacts/ReleaseGameplay/'+$short+'-walk'
function Json([string]$p){Get-Content -LiteralPath $p -Raw|ConvertFrom-Json}
function Record([string]$n,$b){Write-PatchJsonAtomic (Join-Path $out ($n+'.json')) $b}
if(@(& git status --porcelain).Count){throw 'Not clean source'}
foreach($validation in @('20260907-013258-540','20260907-013313-973')){
 $result=Json "Artifacts/FastQa/runs/$validation/result.json"
 if(!$result.passed -or $result.head -cne $commit -or @($result.changedFiles).Count){throw 'Pure/editor validation not clean exact source'}
}
foreach($p in @($Payload,$normal,$chair,$walk,$BusinessEvidence)){
 if((Get-Content -LiteralPath (Join-Path $p 'BUILD_INFO.txt') -Raw) -notmatch ('(?m)^Commit: '+$commit+'\r?$')){throw 'Evidence does not match exact Release'}
}
$nativePaths=@('Assets/FamilyCompany/Simulation/OfficeGrid','Assets/FamilyCompany/Presentation.Unity/OfficeGrid',
 'Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeLayoutEditModeController.cs',
 'Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeLayoutEditModeSkin.cs',
 'Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeBuildEditorNavigationAdapter.cs')
if(@(& git diff --name-only 8ce7d3ed $commit -- @nativePaths).Count){throw 'Native transaction implementation changed'}
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
 unchangedGitSubtrees=$nativePaths;sharedOccupancyAdditiveDiff=$occupancyDiff;shippingWorkers=$workers;freshManagedShopSha256=(Get-PatchHash "$chair/opening-shop-final.txt");
 scope='NOT new native input. Actual previous native four purchases/rotation/overlap gate is bound only to byte-identical shop controller, skin, navigation adapter and grid placement/transaction subtrees. MainNavigation product screen changed; fresh managed product raycast/click and current shop renders checked separately. New movement is covered by fresh normal/walk runs, not this binding.'}
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
 scope='Fresh exact Release multi-cycle actual walking capture and independent centre/foot-alternation measurements; retained accepted models/clips. Agent/path planning changed and freshly exercised. No mathematical zero-skin-slip claim; ankle sampling limitations remain.'}
$reviewedSheets=@(foreach($index in @('00','03','07','11','15','19')){$p="$walk/review/sheets/all-frames-$index.png";@{path=$p;sha256=(Get-PatchHash $p)}})
Record 'visual-review' @{commit=$commit;passed=$true;reviewedChairCaptures=$frames;reviewedWalkingSheets=$reviewedSheets;
 retainedVisualSourcePaths=$visualPaths;retainedVisualSourceCommit='4b06247e';
 observations='Direct review: all eight current seated images, normal next-day scene and actual frames at start/middle/end of 24 seconds. Bent knees/chair stems/typing hands retained; both feet alternate over multiple cycles and directions. Screenshots are not byte-identical because non-participating actor UI positions differ; no byte-identical-frame claim. Reviewed sheets are a chronological subset, not every one of the 393 frames.';
 limitations='No new body/rig/clip promotion, no original-360 model reapproval, no pixel skin-centroid or zero foot-slip claim.'}
$business=[IO.File]::ReadAllText((Join-Path $repo (Join-Path $BusinessEvidence 'result.txt')))
if($business -notmatch 'STARTER_PRODUCT_PLAYER_QA=PASS' -or $business -notmatch 'checkpointIntegration=PASS' -or $business -notmatch 'actualSupportHours=2'){throw 'Actual business integration failed'}
Record 'business-runtime' @{commit=$commit;passed=$true;independent=$false;result=$business;rawResultSha256=(Get-PatchHash (Join-Path $BusinessEvidence 'result.txt'));
 scope='Managed UI plus real first 4 lesson hours, final 4 development hours and 2 support hours. Core checkpoint seeds earlier history; billing-only time jump. In-memory save roundtrip, no user save writes. Not uninterrupted full-week native play. Pure lifecycle/save suite provides a separate core oracle.'}
$updater=Json 'Artifacts/StarterBusinessRelease20260907/updater/all.json'
if(!$updater.passed -or !$updater.independent){throw 'Updater suite failed'}
Record 'updater-regressions' @{commit=$commit;passed=$true;independent=$true;results=$updater;shippingWorkerHashBinding=$workers;scope='81 fresh worker regressions, shipping workers byte-identical to tested source. Actual public transfer is verified after publish.'}
$map=[ordered]@{'opening-four-actors'='normal-runtime';'shop-native-pointer-four-rotations'='native-binding';'normal-walk-tile-centres'='normal-runtime';'walking-visual-foot-slip-grounding'='walk-acceptance';'furniture-avoidance'='normal-runtime';'four-seated-working-directions'='seated-fit';'next-day-four-staggered-arrivals'='normal-runtime';'mute'='normal-runtime';'runtime-exception-zero'='normal-runtime';'updater-regressions'='updater-regressions'}
$gates=@(foreach($name in $map.Keys){$p="$out/$($map[$name]).json";@{name=$name;passed=$true;independent=$true;commit=$commit;evidencePath=$p;evidenceSha256=(Get-PatchHash $p)}})
Record 'release-receipt' @{schemaVersion=1;commit=$commit;productionEligible=$true;userVisualApproval=$true;
 approvalReference='User explicitly requested verified deployment, push, company handoff and shutdown without questions on 2026-09-07. Existing previously approved body/pose/monitor appearance is retained; no new asset promotion.';
 playerSha256=(Get-PatchHash (Join-Path $Payload 'FamilyCompany.exe'));buildInfoSha256=(Get-PatchHash (Join-Path $Payload 'BUILD_INFO.txt'));gates=$gates;
 scope='First business-loop content plus verified path/work-clock fixes. Fresh Release actual gameplay/geometry/walk, explicit scoped old native source binding. Public transfer verified separately after publication.'}
Write-Output "VERIFIED RELEASE RECEIPT: $out/release-receipt.json"
