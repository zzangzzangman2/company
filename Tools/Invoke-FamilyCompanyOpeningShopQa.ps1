[CmdletBinding()]
param(
    [string]$Player,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$openingProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Player)) {
    $Player = Join-Path $openingProject 'Artifacts\FastQa\cache\WindowsPlayer\FamilyCompany_FastQa.exe'
}
$Player = (Resolve-Path -LiteralPath $Player).Path
$openingArtifacts = Join-Path $openingProject ('Artifacts\FastQa\OpeningShop-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $openingArtifacts | Out-Null
$openingLog = Join-Path $openingArtifacts 'player.log'
$openingReceipt = Join-Path $openingArtifacts 'opening-shop-final.txt'
$openingStart = [Diagnostics.ProcessStartInfo]::new()
$openingStart.FileName = $Player
$openingStart.WorkingDirectory = $openingProject
$openingStart.UseShellExecute = $false
$openingStart.CreateNoWindow = $true
$openingStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$openingStart.Arguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 ' +
    '-familyCompanyOpeningShopQa -familyCompanyOpeningShopArtifacts "' + $openingArtifacts + '" -logFile "' + $openingLog + '"'
$openingProcess = [Diagnostics.Process]::Start($openingStart)
$openingWatch = [Diagnostics.Stopwatch]::StartNew()
$openingWindowSamples = 0
Write-Host "[OPENING QA] pid=$($openingProcess.Id) hidden D3D11; artifacts=$openingArtifacts"
try {
    while (-not $openingProcess.WaitForExit(200)) {
        $openingProcess.Refresh()
        $openingWindowSamples++
        if ($openingProcess.MainWindowHandle -ne [IntPtr]::Zero) {
            throw 'Hidden-window gate failed; aborting only this owned QA player.'
        }
        if ($openingWatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Opening QA exceeded $TimeoutSeconds seconds. See $openingLog"
        }
    }
    $openingProcess.WaitForExit()
    $openingExit = $openingProcess.ExitCode
    if (Test-Path -LiteralPath $openingReceipt) { Get-Content -LiteralPath $openingReceipt }
    if ($openingExit -ne 0 -or -not (Test-Path -LiteralPath $openingReceipt) -or
        -not (Select-String -LiteralPath $openingReceipt -Pattern '^FAMILY_3D_OPENING_SHOP_QA: PASS$' -Quiet)) {
        throw "Opening QA failed (exit=$openingExit). See $openingLog"
    }
    if (Select-String -LiteralPath $openingLog -Pattern 'Exception:|error CS|: FAIL|FAIL_CLOSED' -Quiet) {
        throw "Opening QA emitted a runtime error despite its receipt. See $openingLog"
    }
    Write-Host "[OPENING QA] PASS; MainWindowHandle=0 samples=$openingWindowSamples; native pointer NOT tested."
} finally {
    if (-not $openingProcess.HasExited) {
        $openingProcess.Kill()
        [void]$openingProcess.WaitForExit(10000)
    }
    $openingProcess.Dispose()
}
