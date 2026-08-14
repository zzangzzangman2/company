[CmdletBinding()]
param(
    [string]$UnityEditorRoot = 'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dataRoot = Join-Path $UnityEditorRoot 'Data'
$dotnet = Join-Path $dataRoot 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $dataRoot 'DotNetSdkRoslyn\csc.dll'
$mono = Join-Path $dataRoot 'MonoBleedingEdge\bin\mono.exe'
$framework = Join-Path $dataRoot 'MonoBleedingEdge\lib\mono\4.7.1-api'
$netstandard = Join-Path $dataRoot 'NetStandard\ref\2.1.0\netstandard.dll'
$unityModules = Join-Path $dataRoot 'Managed\UnityEngine'
$templateAssemblies = Join-Path $dataRoot 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.3d-cross-platform-17.0.14\ScriptAssemblies'
$outputRoot = Join-Path $projectRoot 'work\main-navigation-hud-validation'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

foreach ($required in @($dotnet, $csc, $mono, $netstandard)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Unity 6000.3.21f1 compiler dependency is missing: $required"
    }
}

$harnessExe = Join-Path $outputRoot 'MainNavigationHudHarness.exe'
& $dotnet $csc -nologo -langversion:latest -target:exe -nostdlib+ `
    -out:$harnessExe `
    -r:(Join-Path $framework 'mscorlib.dll') `
    -r:(Join-Path $framework 'System.dll') `
    -r:(Join-Path $framework 'System.Core.dll') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Simulation\ManagementUi\ManagementUiLayoutMetrics.cs') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\MainNavigation\MainNavigationCatalog.cs') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\MainNavigation\MainNavigationSession.cs') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\MainNavigation\MainNavigationLayoutMetrics.cs') `
    (Join-Path $projectRoot 'Tools\MainNavigationHudHarness.cs')
if ($LASTEXITCODE -ne 0) { throw "Main navigation harness compilation failed with exit code $LASTEXITCODE." }
& $mono $harnessExe
if ($LASTEXITCODE -ne 0) { throw "Main navigation harness failed with exit code $LASTEXITCODE." }

$runtimeOutput = Join-Path $outputRoot 'FamilyCompany.Runtime.External.dll'
$referencePaths = @(
    $netstandard,
    (Join-Path $unityModules 'UnityEngine.CoreModule.dll'),
    (Join-Path $unityModules 'UnityEngine.AnimationModule.dll'),
    (Join-Path $unityModules 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $unityModules 'UnityEngine.AudioModule.dll'),
    (Join-Path $unityModules 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $unityModules 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $unityModules 'UnityEngine.ImageConversionModule.dll'),
    (Join-Path $unityModules 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $unityModules 'UnityEngine.TextCoreFontEngineModule.dll'),
    (Join-Path $unityModules 'UnityEngine.TextCoreTextEngineModule.dll'),
    (Join-Path $unityModules 'UnityEngine.UIModule.dll'),
    (Join-Path $unityModules 'UnityEngine.ScreenCaptureModule.dll'),
    (Join-Path $unityModules 'UnityEngine.JSONSerializeModule.dll'),
    (Join-Path $unityModules 'UnityEngine.UnityWebRequestModule.dll'),
    (Join-Path $unityModules 'UnityEngine.UnityWebRequestAudioModule.dll'),
    (Join-Path $unityModules 'UnityEngine.GridModule.dll'),
    (Join-Path $unityModules 'UnityEngine.TilemapModule.dll'),
    (Join-Path $templateAssemblies 'UnityEngine.UI.dll'),
    (Join-Path $templateAssemblies 'Unity.TextMeshPro.dll')
)
$referenceArguments = @($referencePaths | ForEach-Object {
    if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) { throw "Compiler reference is missing: $_" }
    "-r:$_"
})
$sourcePaths = @(
    & rg --files (Join-Path $projectRoot 'Assets\FamilyCompany\Simulation') -g '*.cs'
    & rg --files (Join-Path $projectRoot 'Assets\FamilyCompany\Save') -g '*.cs'
    & rg --files (Join-Path $projectRoot 'Assets\FamilyCompany\Infrastructure.Unity') -g '*.cs'
    & rg --files (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity') -g '*.cs'
)
$compileArguments = @(
    '-nologo',
    '-langversion:latest',
    '-target:library',
    '-nostdlib+',
    '-warn:4',
    "-out:$runtimeOutput"
) + $referenceArguments + $sourcePaths
$runtimeResponse = Join-Path $outputRoot 'runtime.rsp'
[IO.File]::WriteAllLines($runtimeResponse, $compileArguments, [Text.UTF8Encoding]::new($false))
& $dotnet $csc "@$runtimeResponse"
if ($LASTEXITCODE -ne 0) { throw "Main navigation runtime compilation failed with exit code $LASTEXITCODE." }

$editorOutput = Join-Path $outputRoot 'FamilyCompany.MainNavigation.Editor.External.dll'
$editorCore = Join-Path $dataRoot 'Managed\UnityEngine\UnityEditor.CoreModule.dll'
& $dotnet $csc -nologo -langversion:latest -target:library -nostdlib+ -warn:4 `
    -out:$editorOutput `
    -r:$netstandard `
    -r:(Join-Path $unityModules 'UnityEngine.CoreModule.dll') `
    -r:(Join-Path $unityModules 'UnityEngine.TextRenderingModule.dll') `
    -r:$editorCore `
    -r:$runtimeOutput `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Editor\MainNavigation\MainNavigationV2AssetImporter.cs') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Editor\MainNavigation\MainNavigationHudValidation.cs')
if ($LASTEXITCODE -ne 0) { throw "Main navigation editor validator compilation failed with exit code $LASTEXITCODE." }

$presenterSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\MainNavigation\MainNavigationHudPresenter.cs') -Encoding UTF8
foreach ($requiredToken in @(
    'CanvasScaler.ScaleMode.ScaleWithScreenSize',
    'Screen.safeArea',
    'MainNavigationCatalog.All',
    'SetWorldTimeScaleNow(capturedSpeed)',
    'Image.Type.Sliced',
    'Selectable.Transition.SpriteSwap',
    'MAIN_NAVIGATION_V2_ASSET_MISSING',
    'ReturnToOfficeNow')) {
    if ($presenterSource.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Main navigation presenter is missing required structure token: $requiredToken"
    }
}
foreach ($forbiddenToken in @('●  LIVE', '저장 완료', 'Ctrl+S', '관리 화면   ESC')) {
    if ($presenterSource.IndexOf($forbiddenToken, [StringComparison]::Ordinal) -ge 0) {
        throw "Main navigation presenter exposes removed HUD clutter: $forbiddenToken"
    }
}
$legacySource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\ManagementUI\ManagementUiV2Presenter.cs') -Encoding UTF8
if ($legacySource.IndexOf('var officeVisible = false;', [StringComparison]::Ordinal) -lt 0) {
    throw 'Legacy family/LIVE/notice HUD is not explicitly hidden.'
}
$bootstrapSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\PrototypeBootstrap.cs') -Encoding UTF8
$escapeIndex = $bootstrapSource.IndexOf('TryHandleEscape()', [StringComparison]::Ordinal)
$legacySwitchIndex = $bootstrapSource.IndexOf('switch (_screen)', $escapeIndex, [StringComparison]::Ordinal)
if ($escapeIndex -lt 0 -or $legacySwitchIndex -le $escapeIndex) {
    throw 'Main navigation ESC priority does not precede the legacy management switch.'
}

Write-Output 'MAIN_NAVIGATION_RUNTIME_EXTERNAL_COMPILE: PASS'
Write-Output 'MAIN_NAVIGATION_EDITOR_VALIDATOR_COMPILE: PASS'
Write-Output 'MAIN_NAVIGATION_STATIC_STRUCTURE: PASS'
