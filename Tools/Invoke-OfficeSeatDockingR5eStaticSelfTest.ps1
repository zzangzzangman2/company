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
$productionFixtureRunnerSource = Join-Path $repository `
    'Tools\OfficeSeatDockingR5eProductionFixtureRunner.cs'
$productionFixtureRunnerDll = Invoke-RoslynAssemblyCompile `
    -AssemblyName 'OfficeSeatDockingR5eProductionFixtureRunner' `
    -Sources @($productionFixtureRunnerSource) `
    -References ($netcoreReferences + @($simulationDll, $presentationDll) + $unityEngine) `
    -OutputKind ([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
Copy-Item -LiteralPath (
    Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json') `
    -Destination (Join-Path $output 'OfficeSeatDockingR5eProductionFixtureRunner.runtimeconfig.json') -Force
foreach ($dependency in ($unityEngine + @($ui, $tmp))) {
    Copy-Item -LiteralPath $dependency -Destination $output -Force
}
$productionFixtureTrace = Join-Path $output 'production-fixture-trace'
& $runner $productionFixtureRunnerDll `
    (Join-Path $repository `
        'Assets\FamilyCompany\Presentation.Unity\Resources\OfficeSeatDockingR5eScenarioCatalog.json') `
    $productionFixtureTrace
if ($LASTEXITCODE -ne 0) {
    throw "R5e production fixture runner failed with exit code $LASTEXITCODE."
}
$negativeCatalog = Join-Path $output 'scenario-catalog-negative.json'
$catalogText = [IO.File]::ReadAllText((Join-Path $repository `
    'Assets\FamilyCompany\Presentation.Unity\Resources\OfficeSeatDockingR5eScenarioCatalog.json'))
[IO.File]::WriteAllText(
    $negativeCatalog,
    $catalogText.Replace('"seed": 58193017', '"seed": 58193018'),
    (New-Object Text.UTF8Encoding($false)))
$oldPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$negativeCatalogOutput = @(& $runner $productionFixtureRunnerDll $negativeCatalog `
    (Join-Path $output 'scenario-negative-trace') 2>&1)
$negativeCatalogExit = $LASTEXITCODE
$ErrorActionPreference = $oldPreference
if ($negativeCatalogExit -eq 0 -or
    -not (($negativeCatalogOutput -join "`n").Contains('catalog SHA-256 mismatch')) -or
    (Test-Path -LiteralPath (Join-Path $output 'scenario-negative-trace'))) {
    throw 'R5e production scenario catalog mutation did not fail before execution/output.'
}
Write-Output 'OFFICE_SEAT_DOCKING_R5E_SCENARIO_NEGATIVE: PASS oracle=catalog-sha output=absent'
$ffmpegCommand = Get-Command ffmpeg -ErrorAction SilentlyContinue
$ffprobeCommand = Get-Command ffprobe -ErrorAction SilentlyContinue
if ($null -eq $ffmpegCommand -or $null -eq $ffprobeCommand) {
    $ffmpegRoot = Get-ChildItem `
        'C:\Users\godho\AppData\Local\Microsoft\WinGet\Packages' `
        -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty DirectoryName
    if ([string]::IsNullOrWhiteSpace($ffmpegRoot)) {
        throw 'Static postprocessor fixture requires ffmpeg/ffprobe.'
    }
    $ffmpegPath = Join-Path $ffmpegRoot 'ffmpeg.exe'
    $ffprobePath = Join-Path $ffmpegRoot 'ffprobe.exe'
}
else {
    $ffmpegPath = $ffmpegCommand.Source
    $ffprobePath = $ffprobeCommand.Source
}
$cleanVideo = Join-Path $productionFixtureTrace 'fixture-clean.mp4'
$annotatedVideo = Join-Path $productionFixtureTrace 'fixture-annotated.mp4'
& $ffmpegPath -v error -f lavfi -i 'color=c=black:s=64x64:r=10:d=0.4' `
    -pix_fmt yuv420p -y $cleanVideo
if ($LASTEXITCODE -ne 0) { throw 'Static clean fixture video generation failed.' }
& $ffmpegPath -v error -f lavfi -i 'color=c=blue:s=64x64:r=10:d=0.4' `
    -pix_fmt yuv420p -y $annotatedVideo
if ($LASTEXITCODE -ne 0) { throw 'Static annotated fixture video generation failed.' }
$cleanHash = (Get-FileHash -LiteralPath $cleanVideo -Algorithm SHA256).Hash
$annotatedHash = (Get-FileHash -LiteralPath $annotatedVideo -Algorithm SHA256).Hash
function Write-PgmRectangle {
    param(
        [string]$Path,
        [int]$X0,
        [int]$Y0,
        [int]$X1,
        [int]$Y1
    )
    $header = [Text.Encoding]::ASCII.GetBytes("P5`n64 64`n255`n")
    $pixels = New-Object byte[] (64 * 64)
    for ($y = $Y0; $y -le $Y1; $y++) {
        for ($x = $X0; $x -le $X1; $x++) {
            $pixels[$y * 64 + $x] = 255
        }
    }
    $bytes = New-Object byte[] ($header.Length + $pixels.Length)
    [Buffer]::BlockCopy($header, 0, $bytes, 0, $header.Length)
    [Buffer]::BlockCopy($pixels, 0, $bytes, $header.Length, $pixels.Length)
    [IO.File]::WriteAllBytes($Path, $bytes)
}
$maskFiles = @{
    source='source-frame.pgm'; actor='actor.pgm'; expected='expected.pgm';
    chair='chair-seat.pgm'; desk='desk.pgm'; furniture='furniture.pgm';
    head='head.pgm'; pelvis='pelvis.pgm'; left='left-foot.pgm'; right='right-foot.pgm'
}
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.source) 20 8 43 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.actor) 20 8 43 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.expected) 20 8 43 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.chair) 48 45 58 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.desk) 48 20 60 44
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.furniture) 48 20 60 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.head) 25 10 38 21
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.pelvis) 28 31 35 35
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.left) 23 47 28 52
Write-PgmRectangle (Join-Path $productionFixtureTrace $maskFiles.right) 35 47 40 52
$sourceHash = (Get-FileHash -LiteralPath (
    Join-Path $productionFixtureTrace $maskFiles.source) -Algorithm SHA256).Hash
$maskHeader = @(
    'runId','scenarioId','videoId','actorId','memberId','arrivalDirection','chairRotation',
    'frameIndex','renderFrame','sourceFrameSha256','sourceFramePath','actorMaskPath','expectedPoseMaskPath',
    'chairSeatMaskPath','deskMaskPath','furnitureMaskPath','headMaskPath','pelvisMaskPath',
    'leftFootMaskPath','rightFootMaskPath','state','rootDisplacementWorld',
    'locomotionSpeedWorld','renderedFacing','quantizedVelocityFacing','forwardDot',
    'cameraMatrixHash','actorTransformHash','chairTransformHash','deskTransformHash',
    'width','height','gameplayScale')
$maskRows = New-Object 'System.Collections.Generic.List[string]'
$maskRows.Add([string]::Join(',', $maskHeader))
$states = @('Docked','AtomicEntry','Seated','TurnInPlace')
for ($frame = 0; $frame -lt 4; $frame++) {
    $maskRows.Add([string]::Join(',', @(
        '1','1','static-fixture','player','player','east','0',$frame,'100',$sourceHash,
        $maskFiles.source,$maskFiles.actor,$maskFiles.expected,$maskFiles.chair,$maskFiles.desk,
        $maskFiles.furniture,$maskFiles.head,$maskFiles.pelvis,$maskFiles.left,$maskFiles.right,
        $states[$frame],'0','0','6','6','1',$cleanHash,$cleanHash,$cleanHash,$cleanHash,
        '64','64','1')))
}
[IO.File]::WriteAllLines(
    (Join-Path $productionFixtureTrace 'chair-r5e-mask-frame-input.csv'),
    $maskRows,
    (New-Object Text.UTF8Encoding($false)))
$maskAnalyzerSource = Join-Path $repository 'Tools\OfficeSeatDockingR5eMaskAnalyzer.cs'
$maskAnalyzerDll = Invoke-RoslynAssemblyCompile `
    -AssemblyName 'OfficeSeatDockingR5eMaskAnalyzer' `
    -Sources @($maskAnalyzerSource) `
    -References $netcoreReferences `
    -OutputKind ([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
Copy-Item -LiteralPath (
    Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json') `
    -Destination (Join-Path $output 'OfficeSeatDockingR5eMaskAnalyzer.runtimeconfig.json') -Force
& $runner $maskAnalyzerDll $productionFixtureTrace
if ($LASTEXITCODE -ne 0) {
    throw "R5e mask analyzer fixture failed with exit code $LASTEXITCODE."
}
$negativeMask = Join-Path $output 'mask-negative-source-hash'
New-Item -ItemType Directory -Force -Path $negativeMask | Out-Null
Copy-Item -LiteralPath $cleanVideo -Destination $negativeMask
Copy-Item -LiteralPath $annotatedVideo -Destination $negativeMask
foreach ($name in $maskFiles.Values) {
    Copy-Item -LiteralPath (Join-Path $productionFixtureTrace $name) -Destination $negativeMask
}
$negativeMaskInput = Join-Path $negativeMask 'chair-r5e-mask-frame-input.csv'
Copy-Item -LiteralPath (
    Join-Path $productionFixtureTrace 'chair-r5e-mask-frame-input.csv') `
    -Destination $negativeMaskInput
$negativeMaskText = [IO.File]::ReadAllText($negativeMaskInput).Replace($sourceHash, ('0' * 64))
[IO.File]::WriteAllText(
    $negativeMaskInput,
    $negativeMaskText,
    (New-Object Text.UTF8Encoding($false)))
$oldPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$negativeMaskOutput = @(& $runner $maskAnalyzerDll $negativeMask 2>&1)
$negativeMaskExit = $LASTEXITCODE
$ErrorActionPreference = $oldPreference
if ($negativeMaskExit -eq 0 -or
    -not (($negativeMaskOutput -join "`n").Contains('source-frame identity mismatch')) -or
    (Test-Path -LiteralPath (
        Join-Path $negativeMask 'chair-r5e-mask-analyzer-complete.marker'))) {
    throw 'R5e mask analyzer source-hash negative fixture did not fail closed.'
}
Write-Output 'OFFICE_SEAT_DOCKING_R5E_MASK_NEGATIVE: PASS oracle=source-frame-hash completion=absent'
$humanHeader = @(
    'runId','reviewerId','reviewedAtUtc','cleanVideoSha256','annotatedVideoSha256',
    'normalScale','entryReadable','exitReadable','noStandWhileMoving','noFootOnChair',
    'noDescendRise','noBodyPop','noPenetration','noGhostOrDouble','noHeadTeleport',
    'noStrafeOrBackward','pass','notes')
$humanRow = @(
    '1','static-pipeline-fixture','2026-08-15T00:00:00Z',$cleanHash,$annotatedHash,
    'true','true','true','true','true','true','true','true','true','true','true','true',
    'pipeline-only-not-runtime-evidence')
[IO.File]::WriteAllLines(
    (Join-Path $productionFixtureTrace 'chair-r5e-human-review-input.tsv'),
    @([string]::Join("`t", $humanHeader), [string]::Join("`t", $humanRow)),
    (New-Object Text.UTF8Encoding($false)))
$postProcessorSource = Join-Path $repository 'Tools\OfficeSeatDockingR5ePostProcessor.cs'
$postProcessorDll = Invoke-RoslynAssemblyCompile `
    -AssemblyName 'OfficeSeatDockingR5ePostProcessor' `
    -Sources @($postProcessorSource) `
    -References ($netcoreReferences + @($presentationDll)) `
    -OutputKind ([Microsoft.CodeAnalysis.OutputKind]::ConsoleApplication)
Copy-Item -LiteralPath (
    Join-Path $repository 'Tools\OfficeSeatDockingR5eOfflineHarness.runtimeconfig.json') `
    -Destination (Join-Path $output 'OfficeSeatDockingR5ePostProcessor.runtimeconfig.json') -Force
& $runner $postProcessorDll $productionFixtureTrace $ffprobePath
if ($LASTEXITCODE -ne 0) {
    throw "R5e postprocessor fixture failed with exit code $LASTEXITCODE."
}
$negativePostprocess = Join-Path $output 'postprocess-negative-hash'
New-Item -ItemType Directory -Force -Path $negativePostprocess | Out-Null
foreach ($name in @(
    'chair-r5e-runtime-result.txt',
    'fixture-clean.mp4',
    'fixture-annotated.mp4',
    'chair-r5e-decoded-measurements-input.csv',
    'chair-r5e-mask-frame-input.csv',
    'chair-r5e-mask-analyzer-complete.marker',
    'chair-r5e-human-review-input.tsv')) {
    Copy-Item -LiteralPath (Join-Path $productionFixtureTrace $name) `
        -Destination $negativePostprocess
}
$negativeHuman = Join-Path $negativePostprocess 'chair-r5e-human-review-input.tsv'
$negativeText = [IO.File]::ReadAllText($negativeHuman)
$negativeText = $negativeText.Replace($cleanHash, ('0' * 64))
[IO.File]::WriteAllText(
    $negativeHuman,
    $negativeText,
    (New-Object Text.UTF8Encoding($false)))
$oldPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$negativeOutput = @(& $runner $postProcessorDll $negativePostprocess $ffprobePath 2>&1)
$negativeExit = $LASTEXITCODE
$ErrorActionPreference = $oldPreference
if ($negativeExit -eq 0 -or
    -not (($negativeOutput -join "`n").Contains('human review video identity mismatch')) -or
    (Test-Path -LiteralPath (
        Join-Path $negativePostprocess 'chair-r5e-static-fixture-complete.marker'))) {
    throw 'R5e postprocessor actual-input hash negative fixture did not fail closed.'
}
Write-Output 'OFFICE_SEAT_DOCKING_R5E_POSTPROCESS_NEGATIVE: PASS oracle=human-video-hash completion=absent'
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
    -CompilerOutputDirectory (Join-Path $output 'offline-parser') `
    -TraceDirectory $productionFixtureTrace
if ($LASTEXITCODE -ne 0) {
    throw "R5e offline static/parser harness failed with exit code $LASTEXITCODE."
}

Write-Output (
    'OFFICE_SEAT_DOCKING_R5E_STATIC_SELF_TEST: PASS ' +
    "unityProcessStarted=0 playerProcessStarted=0 buildStarted=0 output=$output")
