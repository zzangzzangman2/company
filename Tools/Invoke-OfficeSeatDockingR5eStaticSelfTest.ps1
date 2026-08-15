[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter(Mandatory = $false)]
    [string]$UnityEditorPath =
        'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe',

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$editor = [IO.Path]::GetFullPath($UnityEditorPath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) {
    throw "Exact Unity Editor executable is missing: $editor"
}
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -match '^(Unity|FamilyCompany)$'
})
if ($running.Count -ne 0) {
    throw "Static self-test requires Unity/FamilyCompany process0; observed $($running.Count)."
}
New-Item -ItemType Directory -Force -Path $output | Out-Null

$editorData = Join-Path (Split-Path -Parent $editor) 'Data'
$roslyn = Join-Path $editorData 'DotNetSdkRoslyn'
$netcore = Join-Path $editorData 'netcorerun'
$runner = Join-Path $netcore 'netcorerun.exe'
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)

function Invoke-RoslynAssemblyCompile {
    param(
        [string]$AssemblyName,
        [string[]]$Sources,
        [string[]]$References,
        [Microsoft.CodeAnalysis.OutputKind]$OutputKind =
            [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary
    )
    $trees = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]'
    foreach ($source in $Sources) {
        $full = [IO.Path]::GetFullPath($source)
        $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
            [IO.File]::ReadAllText($full),
            $parseOptions,
            $full))
    }
    $options = (New-Object Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
        $OutputKind)).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
    $metadata = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
    foreach ($referencePath in ($References | Sort-Object -Unique)) {
        $metadata.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($referencePath))
    }
    $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        $AssemblyName,
        $trees,
        $metadata,
        $options)
    $target = Join-Path $output ($AssemblyName + '.dll')
    $stream = [IO.File]::Open(
        $target,
        [IO.FileMode]::Create,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    try {
        $emit = $compilation.Emit([IO.Stream]$stream)
    }
    finally {
        $stream.Dispose()
    }
    $errors = @($emit.Diagnostics | Where-Object Severity -eq Error)
    if (-not $emit.Success) {
        $errors | ForEach-Object { Write-Error $_.ToString() }
        throw "$AssemblyName Roslyn compile failed with $($errors.Count) errors."
    }
    Write-Host (
        "ROSLYN_ASSEMBLY: PASS name=$AssemblyName sources=$($Sources.Count) " +
        "references=$($References.Count) output=$target")
    return $target
}

$netstandard = @(
    Get-ChildItem (Join-Path $editorData 'NetStandard\compat\2.1.0\shims\netfx') -Filter '*.dll' |
        ForEach-Object FullName
) + @(
    Get-ChildItem (Join-Path $editorData 'NetStandard\compat\2.1.0\shims\netstandard') -Filter '*.dll' |
        ForEach-Object FullName
) + @(
    Join-Path $editorData 'NetStandard\ref\2.1.0\netstandard.dll'
)
$unityEngine = @(
    Get-ChildItem (Join-Path $editorData 'Managed\UnityEngine') -Filter 'UnityEngine*.dll' |
        ForEach-Object FullName
)
$unityEditor = @(
    Get-ChildItem (Join-Path $editorData 'Managed\UnityEngine') -Filter 'UnityEditor*.dll' |
        ForEach-Object FullName
) + @(
    Join-Path $editorData 'Managed\UnityEditor.dll'
)
$templateRoot = Join-Path $editorData 'Resources\PackageManager\ProjectTemplates\libcache'
$ui = Get-ChildItem $templateRoot -Recurse -Filter 'UnityEngine.UI.dll' |
    Select-Object -First 1 -ExpandProperty FullName
$tmp = Get-ChildItem $templateRoot -Recurse -Filter 'Unity.TextMeshPro.dll' |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($ui) -or [string]::IsNullOrWhiteSpace($tmp)) {
    throw 'Unity UI/TextMeshPro reference assemblies are missing.'
}

$simulationSources = @(
    Get-ChildItem (Join-Path $repository 'Assets\FamilyCompany\Simulation') -Recurse -Filter '*.cs' |
        ForEach-Object FullName
)
$simulationDll = Invoke-RoslynAssemblyCompile -AssemblyName 'FamilyCompany.Simulation' `
    -Sources $simulationSources -References $netstandard
$saveSources = @(
    Get-ChildItem (Join-Path $repository 'Assets\FamilyCompany\Save') -Recurse -Filter '*.cs' |
        ForEach-Object FullName
)
$saveDll = Invoke-RoslynAssemblyCompile -AssemblyName 'FamilyCompany.Save' `
    -Sources $saveSources -References ($netstandard + @($simulationDll))
$infrastructureSources = @(
    Get-ChildItem (Join-Path $repository 'Assets\FamilyCompany\Infrastructure.Unity') -Recurse -Filter '*.cs' |
        ForEach-Object FullName
)
$infrastructureDll = Invoke-RoslynAssemblyCompile -AssemblyName 'FamilyCompany.Infrastructure.Unity' `
    -Sources $infrastructureSources `
    -References ($netstandard + @($simulationDll, $saveDll) + $unityEngine)
$presentationSources = @(
    Get-ChildItem (Join-Path $repository 'Assets\FamilyCompany\Presentation.Unity') -Recurse -Filter '*.cs' |
        ForEach-Object FullName
)
$presentationDll = Invoke-RoslynAssemblyCompile -AssemblyName 'FamilyCompany.Presentation.Unity' `
    -Sources $presentationSources `
    -References ($netstandard + @($simulationDll, $saveDll, $infrastructureDll) + $unityEngine + @($ui, $tmp))

$aggregator = Join-Path $repository 'Assets\FamilyCompany\Editor\OfficeSeatDockingR5eStaticValidation.cs'
[void](Invoke-RoslynAssemblyCompile -AssemblyName 'FamilyCompany.Editor.ChairR5eStatic' `
    -Sources @($aggregator) `
    -References ($netstandard + @($simulationDll, $presentationDll) + $unityEngine + $unityEditor))

$netcoreReferences = @()
foreach ($path in (Get-ChildItem $netcore -Filter '*.dll' | ForEach-Object FullName)) {
    try {
        [void][Reflection.AssemblyName]::GetAssemblyName($path)
        $netcoreReferences += $path
    }
    catch [System.BadImageFormatException] {
        # Native runtime DLL.
    }
}
$simulationHarness = Join-Path $repository 'Tools\OfficeSeatDockingR5eSimulationHarness.cs'
$simulationHarnessDll = Invoke-RoslynAssemblyCompile `
    -AssemblyName 'OfficeSeatDockingR5eSimulationHarness' `
    -Sources @($simulationHarness) `
    -References ($netcoreReferences + @($simulationDll)) `
    -OutputKind ([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
Copy-Item -LiteralPath (
    Join-Path $repository 'Tools\OfficeSeatDockingR5eSimulationHarness.runtimeconfig.json') `
    -Destination (Join-Path $output 'OfficeSeatDockingR5eSimulationHarness.runtimeconfig.json') -Force
& $runner $simulationHarnessDll
if ($LASTEXITCODE -ne 0) {
    throw "R5e simulation harness failed with exit code $LASTEXITCODE."
}

& (Join-Path $repository 'Tools\Invoke-OfficeSeatDockingR5eOfflineValidation.ps1') `
    -RepositoryRoot $repository `
    -UnityEditorPath $editor `
    -CompilerOutputDirectory (Join-Path $output 'offline-parser')
if ($LASTEXITCODE -ne 0) {
    throw "R5e offline static/parser harness failed with exit code $LASTEXITCODE."
}

Write-Output (
    'OFFICE_SEAT_DOCKING_R5E_STATIC_SELF_TEST: PASS ' +
    "unityProcessStarted=0 playerProcessStarted=0 buildStarted=0 output=$output")
