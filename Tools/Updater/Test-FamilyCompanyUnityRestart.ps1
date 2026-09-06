[CmdletBinding()]
param([string]$Player='', [switch]$ShowWindow, [switch]$Background)
$ErrorActionPreference='Stop'
if (!$ShowWindow -and !$Background) {throw 'Choose -Background (no UI) or explicitly announce -ShowWindow.'}
if ($ShowWindow -and $Background) {throw 'Choose exactly one visibility mode.'}
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Player) {$Player=Join-Path $repo 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe'}
$Player=(Resolve-Path -LiteralPath $Player).Path
$root=Join-Path $repo ('Artifacts/UnityPatchRestartTests/'+[Guid]::NewGuid().ToString('N'))
$base=Join-Path $root 'base'; $source=Join-Path $root 'source'
[void][IO.Directory]::CreateDirectory($base)
Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($Player)) | Copy-Item -Destination $base -Recurse
Rename-Item -LiteralPath (Join-Path $base 'FamilyCompany_FastQa.exe') -NewName 'FamilyCompany.exe'
Rename-Item -LiteralPath (Join-Path $base 'FamilyCompany_FastQa_Data') -NewName 'FamilyCompany_Data'
$workers=Join-Path $base 'FamilyCompanyPatch'; [void][IO.Directory]::CreateDirectory($workers)
foreach($file in @('FamilyCompany.Update.ps1','FamilyCompany.Restart.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $workers
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $root
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Tests/UnityRestart.Worker.ps1') -Destination (Join-Path $workers 'FamilyCompany.InGame.ps1')
Copy-Item -LiteralPath (Join-Path $workers 'FamilyCompany.InGame.ps1') -Destination $root
Write-PatchJsonAtomic (Join-Path $workers 'unity-test-root.json') @{root=$root}
Write-PatchJsonAtomic (Join-Path $root 'unity-test-root.json') @{root=$root}
# A real Unity payload is preserved as the seed. Exactly one changed data file must transfer.
[IO.File]::WriteAllBytes((Join-Path $workers 'patch-check.bytes'), (New-Object byte[] (4MB)))
Copy-Item -LiteralPath $base -Destination $source -Recurse
$bytes=New-Object byte[] (4MB); [Random]::new(260906).NextBytes($bytes)
[IO.File]::WriteAllBytes((Join-Path $source 'FamilyCompanyPatch/patch-check.bytes'),$bytes)
$commit=(& git -C $repo rev-parse HEAD).Trim()
$package=New-CompanyPatchPackage $source (Join-Path $root 'feed') 'fc-win-20260906.9001' 9001 $commit
Write-PatchJsonAtomic (Join-Path $root 'identity.json') @{scope='LOCAL actual Unity file install and restart, not GitHub release';
    root=$root;manifest=$package.ManifestPath;manifestHash=$package.ManifestHash;commit=$commit;
    basePlayer=$Player;basePlayerHash=(Get-PatchHash $Player); changedPath='FamilyCompanyPatch/patch-check.bytes';
    expectedChangedHash=(Get-PatchHash (Join-Path $source 'FamilyCompanyPatch/patch-check.bytes'))}
$info=[Diagnostics.ProcessStartInfo]::new($Player)
$info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.WorkingDirectory=$repo
$info.Arguments='-force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -familyCompanyInGamePatchQa "'+$root+'" -familyCompanyInGamePatchRestartQa -logFile "'+(Join-Path $root 'parent.log')+'"'
if ($Background) {$info.Arguments='-batchmode '+$info.Arguments; $info.WindowStyle='Hidden'}
$isolation=$null
if ($Background) {
    if (!('CompanyQaDesktop' -as [type])) { Add-Type -Path (Join-Path $repo 'Tools/Background/CompanyQaDesktop.cs') }
    $isolation=[CompanyQaDesktop]::Start($Player,$info.Arguments,$repo)
    $parent=$isolation.Process
} else { $parent=[Diagnostics.Process]::Start($info) }
Write-Host "ACTUAL UNITY PATCH TEST parent=$($parent.Id) root=$root"
try {
$deadline=[DateTime]::UtcNow.AddSeconds(180)
while(!$parent.WaitForExit(200) -and [DateTime]::UtcNow -lt $deadline) {}
if(!$parent.HasExited) {throw 'Parent did not exit normally; do not force-kill or claim PASS.'}
$current=$null; $child=$null
while([DateTime]::UtcNow -lt $deadline) {
    $current=Get-PatchCurrent (Join-Path $root 'install')
    if($current) {
        $child=Get-Process FamilyCompany -ErrorAction SilentlyContinue | Where-Object Path -EQ (Join-Path $current.Directory 'FamilyCompany.exe')
        if($child) {break}
    }
    Start-Sleep -Milliseconds 200
}
if(!$child) {throw 'Exact patched Unity child not observed.'}
$observedChildPath=$child.Path
Assert-PatchInstalled $current.Directory $current.Manifest
if ($Background) {
    if (!$child.WaitForExit(30000)) {throw 'Patched Unity did not finish its background boot check.'}
    if (!(Select-String -LiteralPath (Join-Path $root 'install/patched-player.log') -SimpleMatch 'IN_GAME_PATCH_READY_CURRENT' -Quiet)) {throw 'Patched Unity did not unlock the game.'}
}
if((Get-PatchHash $Player) -cne (Get-Content -LiteralPath (Join-Path $root 'identity.json') -Raw | ConvertFrom-Json).basePlayerHash) {throw 'Main entry changed.'}
Write-PatchJsonAtomic (Join-Path $root 'restart-observed.json') @{parentExit=$parent.ExitCode; childId=$child.Id;
    childPath=$observedChildPath; currentDirectory=$current.Directory;mainEntryUnchanged=$true;
    privateDesktop=$(if($isolation){$isolation.DesktopName}else{$null});desktopSwitchAllowed=$false;
    requiredNext='Inspect patched game UI and parent measured progress; not an automatic visual PASS.'}
Write-Host "UNITY RESTART OBSERVED: $root"
} finally {
    if ($isolation) { $isolation.Dispose() } else { $parent.Dispose() }
}
