[CmdletBinding()]
param(
    [string]$UnityEditorPath = '',
    [string]$UnityEditorRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    if (-not [string]::IsNullOrWhiteSpace($UnityEditorRoot)) {
        $UnityEditorPath = Join-Path $UnityEditorRoot 'Unity.exe'
    }
    else {
        $UnityEditorPath = Find-FamilyCompanyUnityEditor
    }
}
$UnityEditorPath = Assert-ExactUnityEditor $UnityEditorPath $projectRoot
$UnityEditorRoot = Split-Path -Parent $UnityEditorPath
$dataRoot = Join-Path $UnityEditorRoot 'Data'
$dotnet = Join-Path $dataRoot 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $dataRoot 'DotNetSdkRoslyn\csc.dll'
$mono = Join-Path $dataRoot 'MonoBleedingEdge\bin\mono.exe'
$framework = Join-Path $dataRoot 'MonoBleedingEdge\lib\mono\4.7.1-api'
$netstandard = Join-Path $dataRoot 'NetStandard\ref\2.1.0\netstandard.dll'
$unityModules = Join-Path $dataRoot 'Managed\UnityEngine'
$templateAssemblies = Join-Path $dataRoot 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.3d-cross-platform-17.0.14\ScriptAssemblies'
$outputRoot = Join-Path $projectRoot 'work\management-ui-v2-validation'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

foreach ($required in @($dotnet, $csc, $mono, $netstandard)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Unity 6000.3.21f1 compiler dependency is missing: $required"
    }
}

$layoutExe = Join-Path $outputRoot 'ManagementUiV2LayoutHarness.exe'
& $dotnet $csc -nologo -langversion:latest -target:exe -nostdlib+ `
    -out:$layoutExe `
    -r:(Join-Path $framework 'mscorlib.dll') `
    -r:(Join-Path $framework 'System.dll') `
    -r:(Join-Path $framework 'System.Core.dll') `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Simulation\ManagementUi\ManagementUiLayoutMetrics.cs') `
    (Join-Path $projectRoot 'Tools\ManagementUiV2LayoutHarness.cs')
if ($LASTEXITCODE -ne 0) { throw "Management UI layout harness compilation failed with exit code $LASTEXITCODE." }
& $mono $layoutExe
if ($LASTEXITCODE -ne 0) { throw "Management UI layout harness failed with exit code $LASTEXITCODE." }

$runtimeOutput = Join-Path $outputRoot 'FamilyCompany.Runtime.External.dll'
$referencePaths = @(
    $netstandard,
    (Join-Path $unityModules 'UnityEngine.CoreModule.dll'),
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
    $csc,
    '-nologo',
    '-langversion:latest',
    '-target:library',
    '-nostdlib+',
    '-warn:4',
    "-out:$runtimeOutput"
) + $referenceArguments + $sourcePaths
& $dotnet @compileArguments
if ($LASTEXITCODE -ne 0) { throw "Management UI runtime external compilation failed with exit code $LASTEXITCODE." }

$editorOutput = Join-Path $outputRoot 'FamilyCompany.ManagementUi.Editor.External.dll'
$editorCore = Join-Path $dataRoot 'Managed\UnityEngine\UnityEditor.CoreModule.dll'
$engineCore = Join-Path $unityModules 'UnityEngine.CoreModule.dll'
$textRendering = Join-Path $unityModules 'UnityEngine.TextRenderingModule.dll'
& $dotnet $csc -nologo -langversion:latest -target:library -nostdlib+ -warn:4 `
    -out:$editorOutput `
    -r:$netstandard `
    -r:$engineCore `
    -r:$textRendering `
    -r:$editorCore `
    -r:$runtimeOutput `
    (Join-Path $projectRoot 'Assets\FamilyCompany\Editor\ManagementUiV2Validation.cs')
if ($LASTEXITCODE -ne 0) { throw "Management UI editor validator external compilation failed with exit code $LASTEXITCODE." }

$bootstrapSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\PrototypeBootstrap.cs') -Encoding UTF8
$activeBootstrapSource = [Text.RegularExpressions.Regex]::Replace($bootstrapSource, '#if false[\s\S]*?#endif', '')
if ($activeBootstrapSource -match 'OfficeManagementDashboard_v1|BusinessExpansionDashboard_v1|DashboardRect\(') {
    throw 'Active bootstrap code still references a baked dashboard or absolute dashboard coordinates.'
}
if ($activeBootstrapSource.IndexOf('ApplyOfficeObservationCamera(true)', [StringComparison]::Ordinal) -lt 0) {
    throw 'New/load game paths do not force the OfficeVisual observation camera.'
}
$cameraSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\IsometricCameraFollow.cs') -Encoding UTF8
if ($cameraSource.IndexOf('SetOfficeObservationForced', [StringComparison]::Ordinal) -lt 0 -or
    $cameraSource.IndexOf('snapImmediately', [StringComparison]::Ordinal) -lt 0) {
    throw 'The immediate office observation camera hook is missing.'
}
$presenterSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\ManagementUI\ManagementUiV2Presenter.cs') -Encoding UTF8
foreach ($requiredToken in @('CanvasScaler.ScaleMode.ScaleWithScreenSize', 'HorizontalLayoutGroup', 'VerticalLayoutGroup', 'Screen.safeArea', 'TextMeshProUGUI')) {
    if ($presenterSource.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
        throw "Management UI presenter is missing required structure token: $requiredToken"
    }
}
if ($presenterSource -match 'CreateDynamicFontFromOSFont') {
    throw 'Management UI must not use a system-installed font.'
}
$statusSource = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets\FamilyCompany\Presentation.Unity\ManagementUI\IOfficeObservationStatusSource.cs') -Encoding UTF8
foreach ($requiredStatus in @('Moving', 'Seated', 'Typing', 'Mouse', 'Drinking')) {
    if ($statusSource.IndexOf($requiredStatus, [StringComparison]::Ordinal) -lt 0) {
        throw "Office observation status contract is missing: $requiredStatus"
    }
}

Write-Output 'MANAGEMENT_UI_V2_EXTERNAL_COMPILE: PASS'
Write-Output 'MANAGEMENT_UI_V2_EDITOR_VALIDATOR_COMPILE: PASS'
Write-Output 'MANAGEMENT_UI_V2_STATIC_STRUCTURE: PASS'
