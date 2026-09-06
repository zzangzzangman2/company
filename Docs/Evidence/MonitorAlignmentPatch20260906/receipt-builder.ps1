$ErrorActionPreference='Stop'
$repo='C:\Users\godho\Documents\Codex\fc_agents\integration_p0'
Set-Location -LiteralPath $repo
. 'Tools/Updater/FamilyCompany.Update.ps1'
$commit='4b06247ea2c4652fc320fa13c141f3501e3b5cae'
$out='Artifacts/MonitorAlignment20260906/approval'
[void][IO.Directory]::CreateDirectory((Join-Path $repo $out))
$player='Artifacts/PatchCandidates/4b06247e-fad8442a745048b786236332580394fc/payload'
$normal='Artifacts/NormalAutonomy/4b06247e-release'
$chair='Artifacts/ReleaseGameplay/4b06247e-chair'
$walk='Artifacts/ReleaseGameplay/4b06247e-walk'
$prior='Docs/Evidence/ReleaseCandidate8ce7d3ed20260906'
function Json([string]$p){Get-Content -LiteralPath $p -Raw | ConvertFrom-Json}
function Record([string]$name,$body){Write-PatchJsonAtomic (Join-Path $out ($name+'.json')) $body}
if ((& git rev-parse HEAD).Trim() -cne $commit -or @(& git status --porcelain).Count) {throw 'Not clean exact main'}
$allowed=@('Assets/FamilyCompany/Runtime/Character3D/Family3DWorkstation.cs',
 'Assets/FamilyCompany/Experimental/Family3DPrototype/Editor/WorkstationTileCentreRegression.cs')
$allowed+=@('ne','nw','se','sw'|ForEach-Object {"Assets/FamilyCompany/Content/Resources/OfficeBuildFurniture/desk_with_pc_$_.png"})
$changes=@(& git diff --name-only ee48a72c $commit -- Assets Packages ProjectSettings)
if($changes.Count -ne 6 -or @($changes|Where-Object {$_ -cnotin $allowed}).Count){throw 'Unexpected game input change'}
if(@(& git diff --name-only 8ce7d3ed ee48a72c -- Assets Packages ProjectSettings).Count){throw 'Original native/locomotion binding differs'}
$old=Json 'Artifacts/Patches/fc-win-20260906.2/family-company-manifest.json'
$files=@(foreach($f in $old.files){$p=Join-Path $player $f.path; $hash=Get-PatchHash $p;
 @{path=$f.path;sha256=$hash;size=(Get-Item -LiteralPath $p).Length;unchanged=($hash -ceq $f.sha256)}})
$changedFiles=@($files|Where-Object {!$_.unchanged})
$expectedFiles=@('BUILD_INFO.txt','FamilyCompany_Data/boot.config','FamilyCompany_Data/globalgamemanagers',
 'FamilyCompany_Data/resources.assets','FamilyCompany_Data/resources.assets.resS','FamilyCompany_Data/Managed/Assembly-CSharp.dll')
if($changedFiles.Count -ne 6 -or @($changedFiles|Where-Object {$_.path -cnotin $expectedFiles}).Count){throw 'Unexpected release file changes'}
foreach($f in $files|Where-Object {$_.path -like 'FamilyCompanyPatch/*'}){if(!$f.unchanged){throw 'Shipping worker changed'}}
$frames=@(foreach($f in Get-ChildItem -LiteralPath $chair -Filter 'chair-fit*.png'){
 $a=Join-Path 'Artifacts/MonitorAlignment20260906/fast-chair' $f.Name
 $hash=Get-PatchHash $f.FullName
 if($hash -cne (Get-PatchHash $a)){throw "Approved/candidate frame changed: $($f.Name)"}
 @{name=$f.Name;sha256=$hash;approvedFastFrameIdentical=$true}})
if($frames.Count -ne 8){throw 'Incomplete actual frame coverage'}
Record 'content-binding' @{commit=$commit;passed=$true;independent=$true;baseline='ee48a72c8e9979a605a64c59820af8d23fdbcf4c';
 sourceChangedInputs=$changes;files=$files;changedFiles=$changedFiles;unchangedFileCount=($files.Count-$changedFiles.Count);approvedFrames=$frames;
 scope='All game inputs outside the six exact visual/oracle/preview files are git-byte-identical. Runtime diff manually reviewed: only CRT/keyboard visual basis and CRT normals, no seat/socket/pose/navigation/transaction change. All three shipping workers and 163 payload files byte-identical to v2. Not full gameplay binary equivalence: Assembly-CSharp and resource bundles intentionally differ.'}
$approvalReference='User explicitly approved the displayed corrected four-direction seat sheet with "1" and "이 모습으로 배포" on 2026-09-06; latest message asks to continue.'
Record 'approval' @{commit=$commit;userVisualApproval=$true;approvalReference=$approvalReference;
 approvedSheetSha256=(Get-PatchHash 'Artifacts/MonitorAlignment20260906/fast-chair/seated-four-directions-review.png');
 releaseSheetSha256=(Get-PatchHash "$chair/seated-four-directions-review.png");
 scope='Current monitor/keyboard correction approval. Eight actual Release captures match approved FastQA captures byte-for-byte. Existing accepted character motion is unchanged, not newly approved or reauthored.'}
$nav=Json "$normal/independent-navigation.json"; $seat=Json "$normal/independent-seat-blend.json"; $proc=Json "$normal/process.json"
$attendance=Get-Content -LiteralPath "$normal/normal-autonomy-observed.txt" -Raw
if(!$nav.navigationPassed -or !$seat.passed -or $proc.exitCode -ne 0 -or $proc.forcedStop -or $proc.runnerFailure -or $attendance -notmatch 'nextDayAttendanceGatePassed=True'){throw 'Normal gate failed'}
$rows=@(Import-Csv -LiteralPath "$normal/observer/observations.csv"|Where-Object ready -EQ True)
if(@($rows.member|Sort-Object -Unique).Count -ne 4 -or @($rows|Where-Object {[int]$_.bodies -ne 4 -or [int]$_.staticViolations -ne 0 -or [int]$_.interactionViolations -ne 0 -or [int]$_.agentPenetrations -ne 0 -or [int]$_.errors -ne 0 -or [int]$_.legacyCharacters -ne 0 -or [int]$_.legacyFurniture -ne 0}).Count){throw 'Actual actor/collision/error gate failed'}
$muted=@($rows|Where-Object {[double]$_.listenerVolume -eq 0})
if(!$muted.Count -or @($muted|Where-Object {[double]$_.outputPeak -gt .00001}).Count){throw 'Muted output failed'}
Record 'normal-runtime' @{commit=$commit;passed=$true;independent=$true;navigation=$nav;seated=$seat;process=$proc;
 attendance=$attendance;mutedSamples=$muted.Count;mutedMaxOutput=($muted|Measure-Object outputPeak -Maximum).Maximum;
 rawObservationSha256=(Get-PatchHash "$normal/observer/observations.csv");runtimeErrors=0;
 scope='Fresh exact Release, normal clock/routes/seat transitions; programmatic shop setup and afternoon/night-only next-day clock setup. Not native pointer input. Private desktop, no input desktop switch.'}
$native=Json "$prior/native-pointer.json"
if(!$native.passed -or $native.successfulPurchases -ne 4 -or !$native.overlapRejectedWithoutCharge){throw 'Original native gate failed'}
Record 'native-binding' @{commit=$commit;passed=$true;independent=$true;originalNativeCommit='8ce7d3ed';
 originalNativeSha256=(Get-PatchHash "$prior/native-pointer.json");originalNativeEvidence=$native;
 currentContentBindingSha256=(Get-PatchHash "$out/content-binding.json");currentProgrammaticShopSha256=(Get-PatchHash "$chair/opening-shop-final.txt");
 scope='Not a new native click run. Original actual pointer transactions retained only for unchanged purchase/rotation/occupancy code. Fresh current programmatic shop plus current geometry and user-approved render cover changed visuals. Prior maximumAxisDegrees measured centrelines, NOT CRT edge or face-normal error; it did not cover the reported bug.'}
$fit=@(Import-Csv -LiteralPath "$chair/chair-fit.csv"); $proc=Json "$chair/process.json"
if($proc.exitCode -ne 0 -or $proc.forcedStop -or $fit.Count -ne 264 -or @($fit|Where-Object {[double]$_.maxHandError -gt .015 -or [int]$_.chairPenetrations -ne 0 -or [double]$_.leftKnee -lt 80 -or [double]$_.leftKnee -gt 140 -or [double]$_.rightKnee -lt 80 -or [double]$_.rightKnee -gt 140}).Count){throw 'Chair fit failed'}
Record 'seated-fit' @{commit=$commit;passed=$true;independent=$true;samples=$fit.Count;maxIndividualHand=($fit|Measure-Object maxHandError -Maximum).Maximum;
 penetrations=0;poseInjection=$true;normalLiveSamples=$seat.settledSamples;normalLiveFailures=$seat.settledFailureSamples;
 csvSha256=(Get-PatchHash "$chair/chair-fit.csv");process=$proc;scope='Two real body models x four controlled seated directions; separate normal live work observations. Current Release sheet visually inspected and all eight frames identical to approved images.'}
$analysis=Json "$walk/review/analysis.json"; $proc=Json "$walk/process.json"
if($proc.exitCode -ne 0 -or $proc.forcedStop -or $analysis.capture -notmatch 'runtimeErrors=0' -or $analysis.capture -notmatch 'agentPenetration=0'){throw 'Walk capture failed'}
foreach($a in $analysis.actors.PSObject.Properties){if(!$a.Value.footMidGate -or $a.Value.leadAlternations -lt 8){throw 'Foot gate failed'}}
Record 'walk-acceptance' @{commit=$commit;passed=$true;independent=$true;actualCurrentCapture=$analysis;process=$proc;
 priorApprovedWalkSha256=(Get-PatchHash "$prior/four-actors-closeup.mp4");currentBindingSha256=(Get-PatchHash "$out/content-binding.json");
 scope='Previously user-accepted motion/grounding retained only for unchanged actor/locomotion/camera/model/clip inputs, with fresh current multi-cycle capture and independent foot-centre/alternation measurements. No new motion edit or mathematical zero-skin-slip assertion. Analysis skin-sampling/ankle limitations remain explicit.'}
$green=Json 'Artifacts/MonitorAlignment20260906/green/geometry.json'; $red=Json 'Artifacts/MonitorAlignment20260906/red/geometry.json'
if(!$green.passed -or $red.passed -or $green.samples.Count -ne 8){throw 'Geometry red/green failed'}
Record 'geometry' @{commit=$commit;passed=$true;independent=$true;red=$red;green=$green;
 scope='Independent actual mesh-axis/CRT lighting-normal/chair centre geometry; original 0.1 degree and 0.0001 position gates unchanged.'}
$updater=Json 'Artifacts/MonitorAlignment20260906/updater/all.json'
if(!$updater.passed -or !$updater.independent -or $updater.commit -cne $commit){throw 'Updater result identity failed'}
Record 'updater-regressions' $updater
$map=[ordered]@{'opening-four-actors'='normal-runtime';'shop-native-pointer-four-rotations'='native-binding';
 'normal-walk-tile-centres'='normal-runtime';'walking-visual-foot-slip-grounding'='walk-acceptance';
 'furniture-avoidance'='normal-runtime';'four-seated-working-directions'='seated-fit';
 'next-day-four-staggered-arrivals'='normal-runtime';'mute'='normal-runtime';'runtime-exception-zero'='normal-runtime';
 'updater-regressions'='updater-regressions';'monitor-desk-actual-mesh-axes'='geometry'}
$gates=@(foreach($name in $map.Keys){$p="$out/$($map[$name]).json";@{name=$name;passed=$true;independent=$true;commit=$commit;evidencePath=$p;evidenceSha256=(Get-PatchHash $p)}})
Record 'release-receipt' @{schemaVersion=1;commit=$commit;productionEligible=$true;userVisualApproval=$true;approvalReference=$approvalReference;
 playerSha256=(Get-PatchHash "$player/FamilyCompany.exe");buildInfoSha256=(Get-PatchHash "$player/BUILD_INFO.txt");gates=$gates;
 scope='Approved monitor/keyboard-only visual change. Fresh exact Release normal/chair/walk regressions. Native input and original accepted locomotion reuse are explicitly scoped and source/hash-bound, not relabelled as fresh native input or full binary equivalence. Public patch-transfer check follows publication in isolated QA root, never user cache.'}
Write-Output "APPROVED exact Release: $out/release-receipt.json"
