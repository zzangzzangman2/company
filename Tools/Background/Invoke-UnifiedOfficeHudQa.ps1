param([string]$Player = '', [int]$TimeoutSeconds = 180, [switch]$FullNavigation, [switch]$AllowGraphicsQa)
$ErrorActionPreference = 'Stop'
if (!$AllowGraphicsQa) { throw 'Graphics QA is paused by default. Obtain user authorization before -AllowGraphicsQa; private desktop is not headless.' }
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Player) { $Player = Join-Path $taskRepo 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe' }
$taskPlayer = (Resolve-Path -LiteralPath $Player).Path
$taskOutput = Join-Path $taskRepo ('Artifacts/FastQa/UnifiedOfficeHud/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskOutput | Out-Null
Add-Type -Path (Join-Path $PSScriptRoot 'CompanyQaDesktop.cs')
$taskSaves = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/FamilyCompany/FamilyCompanyPrototype'
$taskMain = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads/FamilyCompany_Playtest/FamilyCompany.exe'
function Snapshot-UserFiles {
    $taskPaths = @()
    if (Test-Path -LiteralPath $taskMain) { $taskPaths += $taskMain }
    if (Test-Path -LiteralPath $taskSaves) {
        $taskPaths += @(Get-ChildItem -LiteralPath $taskSaves -File | Where-Object Name -Like '*.json*' | Select-Object -ExpandProperty FullName)
    }
    return @($taskPaths | Sort-Object | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash + ' ' + $_ })
}
$taskBefore = Snapshot-UserFiles
$taskArgs = '-force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -unifiedOfficeHudQa -unifiedOfficeHudOutput "' + $taskOutput + '" -familyCompanyManualGameplayObservation "' + $taskOutput + '" -familyCompanyBackgroundChairObservation "' + (Join-Path $taskOutput 'observer') + '" -familyCompanyTraceOnlyQa -logFile "' + (Join-Path $taskOutput 'player.log') + '"'
if ($FullNavigation) {
    $taskArgs = $taskArgs.Replace('-unifiedOfficeHudQa -unifiedOfficeHudOutput', '-familyCompanyMainNavigationHudQa -familyCompanyMainNavigationHudQaOutput')
}
$taskOwner = [CompanyQaDesktop]::Start($taskPlayer, $taskArgs, (Split-Path $taskPlayer -Parent), $AllowGraphicsQa.IsPresent)
try {
    Write-Output ('PRIVATE-DESKTOP HUD QA: ' + $taskOutput)
    $taskTimer = [Diagnostics.Stopwatch]::StartNew()
    while (!$taskOwner.Process.WaitForExit(250)) {
        if ($taskTimer.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw 'HUD QA timeout.' }
    }
    $taskAfter = Snapshot-UserFiles
    if (($taskBefore -join "`n") -cne ($taskAfter -join "`n")) { throw 'User main/save files changed.' }
    if ([CompanyQaDesktop]::ReadInteractiveDesktopName() -ne $taskOwner.InteractiveDesktopAtStart) { throw 'Interactive desktop changed.' }
    Write-Output 'USER_MAIN_AND_SAVES_UNCHANGED; INTERACTIVE_DESKTOP_UNCHANGED'
    $taskResult = Join-Path $taskOutput 'result.txt'
    $taskPass = 'UNIFIED_OFFICE_HUD_QA: PASS'
    if ($FullNavigation) { $taskResult = Join-Path $taskOutput 'main-navigation-hud-player-qa.txt'; $taskPass = 'PLAYER_QA_PASS' }
    if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult }
    if ($taskOwner.Process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskResult) -or
        !(Select-String -LiteralPath $taskResult -SimpleMatch $taskPass -Quiet)) {
        throw 'HUD QA failed. Inspect the isolated result and player.log.'
    }
} finally { $taskOwner.Dispose() }
