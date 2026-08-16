[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter(Mandatory = $false)]
    [string]$TraceDirectory = '',

    [Parameter(Mandatory = $false)]
    [string]$UnityEditorPath =
        'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe',

    [Parameter(Mandatory = $false)]
    [string]$CompilerOutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$harnessSource = Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.cs'
$runtimeConfigSource = Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json'
if (-not (Test-Path -LiteralPath $harnessSource -PathType Leaf)) {
    throw "R5e offline harness source is missing: $harnessSource"
}
if (-not (Test-Path -LiteralPath $runtimeConfigSource -PathType Leaf)) {
    throw "R5e offline harness runtimeconfig is missing: $runtimeConfigSource"
}

$unityProcesses = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -match '^(Unity|FamilyCompany)$'
})
if ($unityProcesses.Count -ne 0) {
    throw "R5e offline parser requires Unity/FamilyCompany process0; observed $($unityProcesses.Count)."
}

$editor = [IO.Path]::GetFullPath($UnityEditorPath)
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) {
    throw "Exact Unity Editor executable is missing: $editor"
}
$editorData = Join-Path (Split-Path -Parent $editor) 'Data'
$roslyn = Join-Path $editorData 'DotNetSdkRoslyn'
$netcore = Join-Path $editorData 'netcorerun'
$runner = Join-Path $netcore 'netcorerun.exe'
foreach ($required in @(
    (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll'),
    (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll'),
    $runner)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Offline compiler dependency is missing: $required"
    }
}

if ([string]::IsNullOrWhiteSpace($CompilerOutputDirectory)) {
    $CompilerOutputDirectory = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath (
        'family-company-chair-r5e-parser-' + [Guid]::NewGuid().ToString('N'))
}
$compilerOutput = [IO.Path]::GetFullPath($CompilerOutputDirectory)
New-Item -ItemType Directory -Force -Path $compilerOutput | Out-Null

Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')
$metadata = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
foreach ($path in (Get-ChildItem -LiteralPath $netcore -Filter '*.dll' | ForEach-Object FullName)) {
    try {
        [void][Reflection.AssemblyName]::GetAssemblyName($path)
        $metadata.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($path))
    }
    catch [System.BadImageFormatException] {
        # Native runtime DLL; intentionally not a compiler metadata reference.
    }
}
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)
$trees = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]'
$trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
    [IO.File]::ReadAllText($harnessSource),
    $parseOptions,
    $harnessSource))
$compilationOptions = (New-Object Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
    [Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)).WithOptimizationLevel(
    [Microsoft.CodeAnalysis.OptimizationLevel]::Release)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
    'OfficeSeatDockingR5eOfflineHarness',
    $trees,
    $metadata,
    $compilationOptions)
$harnessDll = Join-Path $compilerOutput 'OfficeSeatDockingR5eOfflineHarness.dll'
$stream = [IO.File]::Open(
    $harnessDll,
    [IO.FileMode]::Create,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None)
try {
    $emit = $compilation.Emit([IO.Stream]$stream)
}
finally {
    $stream.Dispose()
}
$compileErrors = @($emit.Diagnostics | Where-Object Severity -eq Error)
if (-not $emit.Success) {
    $compileErrors | ForEach-Object { Write-Error $_.ToString() }
    throw "R5e offline parser Roslyn compile failed with $($compileErrors.Count) errors."
}
Copy-Item -LiteralPath $runtimeConfigSource -Destination (
    Join-Path $compilerOutput 'OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json') -Force

$arguments = @($harnessDll, $repository)
if (-not [string]::IsNullOrWhiteSpace($TraceDirectory)) {
    $arguments += [IO.Path]::GetFullPath($TraceDirectory)
}
& $runner $arguments
if ($LASTEXITCODE -ne 0) {
    throw "R5e offline parser failed with exit code $LASTEXITCODE."
}
Write-Output (
    'OFFICE_SEAT_DOCKING_R5E_OFFLINE_ENTRYPOINT: PASS ' +
    "repository=$repository trace=$TraceDirectory compilerOutput=$compilerOutput")
