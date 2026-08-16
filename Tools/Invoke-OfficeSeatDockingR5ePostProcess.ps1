[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string]$PresentationAssemblyPath,

    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [Parameter(Mandatory = $false)]
    [string]$UnityEditorPath =
        'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe',

    [Parameter(Mandatory = $false)]
    [string]$FfprobePath = '',

    [Parameter(Mandatory = $false)]
    [string]$CompilerOutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$artifacts = [IO.Path]::GetFullPath($ArtifactDirectory)
$presentation = [IO.Path]::GetFullPath($PresentationAssemblyPath)
$editor = [IO.Path]::GetFullPath($UnityEditorPath)
foreach ($required in @($artifacts, $presentation, $editor)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Chair R5e postprocess dependency missing: $required"
    }
}
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -match '^(Unity|FamilyCompany)$'
})
if ($running.Count -ne 0) {
    throw "Chair R5e postprocess requires Unity/FamilyCompany process0; observed $($running.Count)."
}
if ([string]::IsNullOrWhiteSpace($FfprobePath)) {
    $ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
    if ($null -ne $ffprobe) { $FfprobePath = $ffprobe.Source }
    else {
        $FfprobePath = Get-ChildItem `
            'C:\Users\godho\AppData\Local\Microsoft\WinGet\Packages' `
            -Recurse -Filter ffprobe.exe -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
}
if ([string]::IsNullOrWhiteSpace($FfprobePath) -or
    -not (Test-Path -LiteralPath $FfprobePath -PathType Leaf)) {
    throw 'Chair R5e postprocess ffprobe dependency missing.'
}
if ([string]::IsNullOrWhiteSpace($CompilerOutputDirectory)) {
    $CompilerOutputDirectory = Join-Path ([IO.Path]::GetTempPath()) (
        'chair-r5e-postprocess-' + [Guid]::NewGuid().ToString('N'))
}
$compilerOutput = [IO.Path]::GetFullPath($CompilerOutputDirectory)
New-Item -ItemType Directory -Force -Path $compilerOutput | Out-Null

$editorData = Join-Path (Split-Path -Parent $editor) 'Data'
$roslyn = Join-Path $editorData 'DotNetSdkRoslyn'
$netcore = Join-Path $editorData 'netcorerun'
$runner = Join-Path $netcore 'netcorerun.exe'
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')
$references = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
foreach ($path in (Get-ChildItem -LiteralPath $netcore -Filter '*.dll' | ForEach-Object FullName)) {
    try {
        [void][Reflection.AssemblyName]::GetAssemblyName($path)
        $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($path))
    }
    catch [System.BadImageFormatException] { }
}
$parse = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)

function Compile-PostProcessTool {
    param([string]$Name, [string]$Source, [bool]$NeedsPresentation)
    $trees = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]'
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($Source), $parse, $Source))
    $metadata = New-Object 'System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]'
    foreach ($reference in $references) { $metadata.Add($reference) }
    if ($NeedsPresentation) {
        $metadata.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($presentation))
    }
    $options = (New-Object Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
        [Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)).WithOptimizationLevel(
        [Microsoft.CodeAnalysis.OptimizationLevel]::Release)
    $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create(
        $Name, $trees, $metadata, $options)
    $target = Join-Path $compilerOutput ($Name + '.dll')
    $stream = [IO.File]::Open($target, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try { $emit = $compilation.Emit([IO.Stream]$stream) }
    finally { $stream.Dispose() }
    if (-not $emit.Success) {
        $emit.Diagnostics | Where-Object Severity -eq Error | ForEach-Object { Write-Error $_.ToString() }
        throw "$Name compile failed."
    }
    Copy-Item -LiteralPath (
        Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json') `
        -Destination (Join-Path $compilerOutput ($Name + '.runtimeconfig.json')) -Force
    return $target
}

$analyzer = Compile-PostProcessTool `
    -Name 'OfficeSeatDockingR5eMaskAnalyzer' `
    -Source (Join-Path $repository 'Tools\OfficeSeatDockingR5eMaskAnalyzer.cs') `
    -NeedsPresentation $false
& $runner $analyzer $artifacts
if ($LASTEXITCODE -ne 0) { throw "Chair R5e mask analyzer failed: $LASTEXITCODE" }

$postprocessor = Compile-PostProcessTool `
    -Name 'OfficeSeatDockingR5ePostProcessor' `
    -Source (Join-Path $repository 'Tools\OfficeSeatDockingR5ePostProcessor.cs') `
    -NeedsPresentation $true
foreach ($dependency in (Get-ChildItem -LiteralPath (
             Split-Path -Parent $presentation) -Filter '*.dll')) {
    if ([IO.Path]::GetFullPath($dependency.DirectoryName) -eq $compilerOutput) { continue }
    Copy-Item -LiteralPath $dependency.FullName -Destination $compilerOutput -Force
}
& $runner $postprocessor $artifacts ([IO.Path]::GetFullPath($FfprobePath))
if ($LASTEXITCODE -ne 0) { throw "Chair R5e postprocessor failed: $LASTEXITCODE" }

Write-Output (
    'OFFICE_SEAT_DOCKING_R5E_POSTPROCESS_ENTRYPOINT: PASS ' +
    "artifacts=$artifacts compilerOutput=$compilerOutput")
