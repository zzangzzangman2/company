$ErrorActionPreference = 'Stop'
# Pure managed checks only: never call Win32 desktop APIs or launch a Player.
if (!('CompanyQaDesktop' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'CompanyQaDesktop.cs') }
$taskCases = @(
    @('', $false, 'GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION'),
    @('-batchmode -force-d3d11', $false, 'GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION'),
    @('-nographics', $false, 'GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION'),
    @('-logFile "C:/example/-batchmode -nographics/log.txt"', $false, 'GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION'),
    @('-batchmode -nographics-extra', $false, 'GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION'),
    @('-batchmode -nographics -quit', $false, ''),
    @('-BATCHMODE -NOGRAPHICS', $false, ''),
    @('-force-d3d11', $true, ''),
    @('-familyCompanyCaptureStock', $true, 'NATIVE_INPUT_QA_BLOCKED'),
    @('-batchmode -nographics -familyCompanyOfficeBuildNativePointerQa', $false, 'NATIVE_INPUT_QA_BLOCKED'),
    @('-familyCompanyOfficeBuildPreviewAlignmentQa', $true, 'NATIVE_INPUT_QA_BLOCKED')
)
$taskAssertions = 0
foreach ($taskCase in $taskCases) {
    $taskFailure = ''
    try { [CompanyQaDesktop]::AssertLaunchAllowed($taskCase[0], $taskCase[1]) }
    catch { $taskFailure = $_.Exception.ToString() }
    if (!$taskCase[2] -and $taskFailure) { throw $taskFailure }
    if ($taskCase[2] -and !$taskFailure.Contains($taskCase[2])) { throw "Wrong launch policy result: $($taskCase[0])" }
    $taskAssertions++
}
# Invalid paths prove the default public entry rejects before resolving paths or touching Win32.
$taskFailure = ''
try { [CompanyQaDesktop]::Start('', '-force-d3d11', '') | Out-Null }
catch { $taskFailure = $_.Exception.ToString() }
if (!$taskFailure.Contains('GRAPHICS_QA_REQUIRES_EXPLICIT_AUTHORIZATION')) { throw 'Launch gate did not run first.' }
$taskAssertions++
$taskScripts = @(
    @('Invoke-UnifiedOfficeHudQa.ps1', @{}),
    @('Invoke-StarterProductQa.ps1', @{}),
    @('Invoke-ReleaseVisualQa.ps1', @{Player='unused'; EvidenceDirectory='unused'; Scenario='walk'}),
    @('../Invoke-FamilyCompanyNormalAutonomyQa.ps1', @{}),
    @('../Invoke-FamilyCompanyOpeningWalkAudit.ps1', @{}),
    @('../Updater/Test-FamilyCompanyInGamePatch.ps1', @{PrivateDesktop=$true}),
    @('../Updater/Test-FamilyCompanyUnityRestart.ps1', @{Background=$true})
)
foreach ($taskScript in $taskScripts) {
    $taskFailure = ''
    $taskParameters = $taskScript[1]
    try { & (Join-Path $PSScriptRoot $taskScript[0]) @taskParameters | Out-Null }
    catch { $taskFailure = $_.Exception.ToString() }
    if (!$taskFailure.Contains('AllowGraphicsQa')) { throw "Script did not stop at its authorization gate: $($taskScript[0])" }
    $taskAssertions++
}
Write-Output "LAUNCH POLICY PASS: $taskAssertions assertions; no Player, desktop, mouse, keyboard, or graphics operation executed."
