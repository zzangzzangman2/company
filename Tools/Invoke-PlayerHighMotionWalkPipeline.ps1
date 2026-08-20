[CmdletBinding()]
param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $projectRoot 'Artifacts\PlayerHighMotionWalkQa'
$playerRoot = Join-Path $artifactRoot 'Player'
$playerExe = Join-Path $playerRoot 'FamilyCompany.exe'
$captureRoot = Join-Path $artifactRoot 'Capture'
$buildLog = Join-Path $artifactRoot 'high-motion-player-build.log'
$playerLog = Join-Path $captureRoot 'high-motion-player-qa.log'
$frameRoot = Join-Path $projectRoot 'Assets\Resources\FamilyCompany\Player2DWalkV2\Frames'
$directions = 'south','southwest','west','northwest','north','northeast','east','southeast'
$missingFrames = @(
    foreach ($pose in 0..5) {
        foreach ($direction in $directions) {
            $framePath = Join-Path $frameRoot "player_${direction}_walk_${pose}_v2.png"
            if (-not (Test-Path -LiteralPath $framePath -PathType Leaf)) {
                $framePath
            }
        }
    }
)

if ($missingFrames.Count -gt 0) {
    throw "PLAYER_2D_WALK_V2_NOT_READY: missing $($missingFrames.Count)/48 approved runtime PNGs. " +
        'This checkpoint is source/motion-only; finish and approve the east trace workflow first. ' +
        'No Unity build was started.'
}

if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity 6000.3.21f1 was not found: $UnityEditor"
}
$version = (Get-Item -LiteralPath $UnityEditor).VersionInfo.ProductVersion
if ($version -notlike '6000.3.21f1*') {
    throw "Expected Unity 6000.3.21f1, found $version"
}
$activeUnity = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($activeUnity.Count -gt 0) {
    throw 'Close the Unity Editor before running the 2D player walk pipeline.'
}

New-Item -ItemType Directory -Force -Path $artifactRoot,$playerRoot,$captureRoot | Out-Null

$build = Start-Process `
    -FilePath $UnityEditor `
    -ArgumentList @(
        '-force-free',
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $projectRoot,
        '-buildTarget', 'Win64',
        '-executeMethod', 'FamilyCompany.Editor.WindowsPlayerBuild.BuildWindowsX64',
        '-familyCompanyBuildOutput', $playerExe,
        '-logFile', $buildLog) `
    -WindowStyle Hidden `
    -PassThru
$build.WaitForExit()
$build.Refresh()
if ($build.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $playerExe -PathType Leaf)) {
    throw "2D player QA build failed with exit code $($build.ExitCode). See $buildLog"
}

$player = Start-Process `
    -FilePath $playerExe `
    -ArgumentList @(
        '-batchmode',
        '-force-d3d11',
        '-screen-width', '1280',
        '-screen-height', '720',
        '-screen-fullscreen', '0',
        '-familyCompanyPlayer2DWalkV2',
        '-familyCompanyCharacterLocomotionV1Qa',
        '-familyCompanyPlayer2DWalkV2VisualQa',
        '-familyCompanyCharacterLocomotionV1QaArtifacts', $captureRoot,
        '-logFile', $playerLog) `
    -WindowStyle Hidden `
    -PassThru
$player.WaitForExit()
$player.Refresh()
if ($player.ExitCode -ne 0) {
    throw "2D player D3D11 QA failed with exit code $($player.ExitCode). See $playerLog"
}

$resultPath = Join-Path $captureRoot 'character-locomotion-player-result.txt'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw "2D player QA result is missing: $resultPath"
}
$result = Get-Content -Raw -LiteralPath $resultPath
if ($result -notmatch 'FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1: PASS' -or
    $result -notmatch 'playerWalkMode=Player2DV2' -or
    $result -notmatch 'visualCandidateOnly=true') {
    throw "2D player QA did not report the Player2DV2 visual candidate: $resultPath"
}
$closeups = @(Get-ChildItem -LiteralPath $captureRoot -Filter '*-close.png' -File)
$overviews = @(Get-ChildItem -LiteralPath $captureRoot -Filter '*-overview.png' -File)
if ($closeups.Count -ne 48 -or $overviews.Count -ne 8) {
    throw "Expected 48 closeups and eight overviews, found $($closeups.Count) and $($overviews.Count)."
}

Write-Output 'PLAYER_2D_WALK_V2_VISUAL_PIPELINE: PASS_NON_SHIPPING'
Write-Output "Result: $resultPath"
Write-Output "Screenshots: $captureRoot"
