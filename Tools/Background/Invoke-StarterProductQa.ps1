param([int]$TimeoutSeconds = 480, [string]$Player = '', [switch]$Lifecycle)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskPlayer = Join-Path $taskRoot 'Artifacts\FastQa\cache\WindowsPlayer\FamilyCompany_FastQa.exe'
if ($Player) { $taskPlayer = (Resolve-Path -LiteralPath $Player).Path }
if (-not (Test-Path -LiteralPath $taskPlayer -PathType Leaf)) { throw 'Build the FastQA player first.' }
$taskOutput = Join-Path $taskRoot ('Artifacts\FastQa\StarterProduct\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if ($Player) { Copy-Item -LiteralPath (Join-Path (Split-Path $taskPlayer -Parent) 'BUILD_INFO.txt') -Destination $taskOutput }
Add-Type -Path (Join-Path $PSScriptRoot 'CompanyQaDesktop.cs')
$taskSaves = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\FamilyCompany\FamilyCompanyPrototype'
$taskMain = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FamilyCompany_Playtest\FamilyCompany.exe'
function Snapshot-UserFiles {
    $paths = @()
    if (Test-Path -LiteralPath $taskMain) { $paths += $taskMain }
    if (Test-Path -LiteralPath $taskSaves) {
        $paths += @(Get-ChildItem -LiteralPath $taskSaves -File | Where-Object Name -Like '*.json*' | Select-Object -ExpandProperty FullName)
    }
    return @($paths | Sort-Object | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash + ' ' + $_ })
}
$taskBefore = Snapshot-UserFiles
$taskArgs = '-force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -starterProductQa -starterProductArtifacts "' + $taskOutput + '" -logFile "' + (Join-Path $taskOutput 'player.log') + '"'
if ($Lifecycle) { $taskArgs += ' -starterProductLifecycleQa' }
if ($Player) { $taskArgs += ' -familyCompanyManualGameplayObservation "' + $taskOutput + '" -familyCompanyBackgroundChairObservation "' + (Join-Path $taskOutput 'observer') + '" -familyCompanyTraceOnlyQa' }
$taskOwner = [CompanyQaDesktop]::Start($taskPlayer, $taskArgs, (Split-Path $taskPlayer -Parent))
try {
    Write-Output ('Background starter-product QA running: ' + $taskOutput)
    $taskTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $taskOwner.Process.WaitForExit(500)) {
        if ($taskTimer.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw 'Starter-product QA timed out.' }
    }
    $taskExit = $taskOwner.Process.ExitCode
    $taskAfter = Snapshot-UserFiles
    if (Compare-Object $taskBefore $taskAfter) { throw 'User main/save file changed during QA.' }
    if ([CompanyQaDesktop]::ReadInteractiveDesktopName() -ne $taskOwner.InteractiveDesktopAtStart) {
        throw 'Interactive desktop changed during QA.'
    }
    Write-Output 'USER_MAIN_AND_SAVES_UNCHANGED; INTERACTIVE_DESKTOP_UNCHANGED'
    $taskResult = Join-Path $taskOutput 'result.txt'
    if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult }
    if ($taskExit -ne 0 -or -not (Test-Path -LiteralPath $taskResult)) { throw "QA player exit code $taskExit" }
} finally { $taskOwner.Dispose() }
