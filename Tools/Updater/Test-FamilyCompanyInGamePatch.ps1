[CmdletBinding()]
param([string]$Player = '', [switch]$ShowWindow, [switch]$PrivateDesktop)
$ErrorActionPreference = 'Stop'
if ($ShowWindow -eq $PrivateDesktop) { throw 'Choose isolated -PrivateDesktop or explicitly authorized -ShowWindow. A black PNG is not a pass.' }
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Player) { $Player=Join-Path $repo 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe' }
$root=Join-Path $repo ('Artifacts/InGamePatchTests/'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $root 'source'
[void][IO.Directory]::CreateDirectory((Join-Path $source 'FamilyCompany_Data'))
[IO.File]::WriteAllText((Join-Path $source 'FamilyCompany.exe'),'INERT NOT EXECUTABLE')
[IO.File]::WriteAllText((Join-Path $source 'UnityPlayer.dll'),'INERT NOT DLL')
$bytes=New-Object byte[] (4MB); [Random]::new(917).NextBytes($bytes)
[IO.File]::WriteAllBytes((Join-Path $source 'FamilyCompany_Data/progress-fixture.bytes'),$bytes)
$package=New-CompanyPatchPackage $source (Join-Path $root 'feed') 'fc-win-20260906.1' 1 ('1'*40)
Write-PatchJsonAtomic (Join-Path $root 'test-identity.json') @{type='actual Unity UI with inert paced local transport; not game Release';
    manifest=$package.ManifestPath; manifestSha256=$package.ManifestHash; installRoot=(Join-Path $root 'install');
    player=(Resolve-Path -LiteralPath $Player).Path; playerSha256=(Get-PatchHash $Player)}
[IO.File]::Copy((Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1'),(Join-Path $root 'FamilyCompany.Update.ps1'))
[IO.File]::Copy((Join-Path $PSScriptRoot 'Tests/ProgressWindow.Worker.ps1'),(Join-Path $root 'FamilyCompany.InGame.ps1'))
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=(Resolve-Path -LiteralPath $Player).Path
$start.WorkingDirectory=$repo; $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WindowStyle='Normal'
$start.Arguments='-force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -familyCompanyInGamePatchQa "'+$root+'" -logFile "'+(Join-Path $root 'player.log')+'"'
$isolation=$null
if ($PrivateDesktop) {
    if (!('CompanyQaDesktop' -as [type])) { Add-Type -Path (Join-Path $repo 'Tools/Background/CompanyQaDesktop.cs') }
    $isolation=[CompanyQaDesktop]::Start($start.FileName,$start.Arguments,$repo)
    $process=$isolation.Process
} else { $process=[Diagnostics.Process]::Start($start) }
$timer=[Diagnostics.Stopwatch]::StartNew()
Write-Host "IN-GAME PATCH QA pid=$($process.Id) root=$root"
try {
    while (!$process.WaitForExit(200)) {
        $process.Refresh()
        if ($timer.Elapsed.TotalSeconds -gt 150) { throw 'In-game patch QA timed out.' }
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity patch QA failed: $($process.ExitCode)" }
    if (!(Test-Path -LiteralPath (Join-Path $root 'in-game-patch.png'))) { throw 'Missing actual in-game screenshot.' }
    Add-Type -AssemblyName System.Drawing
    $bitmap=[Drawing.Bitmap]::new((Join-Path $root 'in-game-patch.png'))
    try {
        [long]$luma=0; $samples=0
        for($y=0;$y -lt $bitmap.Height;$y+=32){ for($x=0;$x -lt $bitmap.Width;$x+=32){$pixel=$bitmap.GetPixel($x,$y); $luma+=$pixel.R+$pixel.G+$pixel.B; $samples++} }
        if($samples -eq 0 -or $luma -lt $samples*20){throw 'Actual screenshot is black; no visual PASS.'}
    } finally {$bitmap.Dispose()}
    $result=Get-Content -LiteralPath (Join-Path $root 'unity-patch-result.json') -Raw | ConvertFrom-Json
    if ($result.status -ne 'prepared' -or (Test-Path -LiteralPath (Join-Path $root 'install/current.json'))) { throw 'Prepared-only state was incorrectly activated.' }
    if (Select-String -LiteralPath (Join-Path $root 'player.log') -Pattern 'Exception:|error CS|IN_GAME_PATCH_UNAVAILABLE' -Quiet) { throw 'Runtime patch error.' }
    Write-Host "IN-GAME PATCH QA PASS: $root"
} finally {
    try {
        if (!$process.HasExited) { $process.Kill(); [void]$process.WaitForExit(10000) }
        Write-PatchJsonAtomic (Join-Path $root 'process.json') @{
            pid=$process.Id;exitCode=$process.ExitCode;privateDesktop=$(if($isolation){$isolation.DesktopName}else{$null});
            interactiveDesktopAtStart=$(if($isolation){$isolation.InteractiveDesktopAtStart}else{$null});
            desktopSwitchAllowed=$false;scope='Own Unity UI on an isolated desktop; no desktop switch or native input'}
    } finally {
        if ($isolation) { $isolation.Dispose() } else { $process.Dispose() }
    }
}
