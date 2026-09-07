[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRspPath = Join-Path $taskRepo 'Library/Bee/artifacts/1900b0aE.dag/FamilyCompany.Presentation.Unity.rsp'
if (!(Test-Path -LiteralPath $taskRspPath)) { throw 'Warm Editor compiler response file is required. Do not launch Unity automatically.' }
$taskRsp = Get-Content -LiteralPath $taskRspPath
$taskSources = @($taskRsp | Where-Object { $_ -match '^"Assets/.+\.cs"$' } | ForEach-Object { $_.Trim('"').Replace('\','/') })
$taskActual = @(Get-ChildItem -LiteralPath (Join-Path $taskRepo 'Assets/FamilyCompany/Presentation.Unity') -Filter '*.cs' -Recurse -File |
    ForEach-Object { $_.FullName.Substring($taskRepo.Length + 1).Replace('\','/') })
if (@(Compare-Object ($taskSources | Sort-Object) ($taskActual | Sort-Object)).Count) {
    throw 'Cached source list differs from current assembly. This check cannot substitute for a new Unity import.'
}
$taskUnityReference = $taskRsp | Where-Object { $_ -match '^-r:".+/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"$' } | Select-Object -First 1
if (!$taskUnityReference) { throw 'Cannot resolve the exact installed compiler from the cached Unity reference.' }
$taskEditorData = $taskUnityReference.Substring(4).TrimEnd('"') -replace '/Managed/UnityEngine/UnityEngine.CoreModule.dll$', ''
$taskDotnet = Join-Path $taskEditorData 'NetCoreRuntime/dotnet.exe'
$taskCompiler = Join-Path $taskEditorData 'DotNetSdkRoslyn/csc.dll'
foreach ($taskDependency in @($taskDotnet, $taskCompiler)) {
    if (!(Test-Path -LiteralPath $taskDependency)) { throw "Missing dependency: $taskDependency" }
}
$taskOutput = Join-Path $taskRepo ('Artifacts/FastQa/OfflineUiCompile/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
[void][IO.Directory]::CreateDirectory($taskOutput)
# Mechanical response-file copy: no Library, built Player, Downloads, or save files are written.
$taskCompileRsp = @($taskRsp | Where-Object { $_ -notmatch '^[-/](out:|refout:|parallel|pdb:)' }) + @(
    '-out:"' + (Join-Path $taskOutput 'FamilyCompany.Presentation.Unity.dll') + '"',
    '-refout:"' + (Join-Path $taskOutput 'FamilyCompany.Presentation.Unity.ref.dll') + '"',
    '-parallel-'
)
$taskOutputRsp = Join-Path $taskOutput 'compile.rsp'
[IO.File]::WriteAllLines($taskOutputRsp, [string[]]$taskCompileRsp, [Text.UTF8Encoding]::new($false))
$taskInfo = [Diagnostics.ProcessStartInfo]::new()
$taskInfo.FileName = $taskDotnet
$taskInfo.Arguments = '"' + $taskCompiler + '" @"' + $taskOutputRsp + '"'
$taskInfo.WorkingDirectory = $taskRepo
$taskInfo.UseShellExecute = $false; $taskInfo.CreateNoWindow = $true; $taskInfo.WindowStyle = 'Hidden'
$taskInfo.RedirectStandardOutput = $true; $taskInfo.RedirectStandardError = $true
$taskProcess = [Diagnostics.Process]::Start($taskInfo)
try {
    $taskProcess.PriorityClass = 'BelowNormal'
    $taskAvailableMask = [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity.ToInt64()
    $taskLimitedMask = 0L; $taskCoreCount = 0
    for ($taskBit = 0; $taskBit -lt 63 -and $taskCoreCount -lt 2; $taskBit++) {
        $taskBitMask = 1L -shl $taskBit
        if (($taskAvailableMask -band $taskBitMask) -ne 0) { $taskLimitedMask = $taskLimitedMask -bor $taskBitMask; $taskCoreCount++ }
    }
    if ($taskLimitedMask -ne 0) { $taskProcess.ProcessorAffinity = [IntPtr]$taskLimitedMask }
    $taskStdout = $taskProcess.StandardOutput.ReadToEndAsync()
    $taskStderr = $taskProcess.StandardError.ReadToEndAsync()
    if (!$taskProcess.WaitForExit(45000)) { throw 'Offline source compile timed out.' }
    $taskLog = $taskStdout.GetAwaiter().GetResult() + $taskStderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $taskOutput 'compile.log'), $taskLog)
    if ($taskProcess.ExitCode -ne 0) { throw "Offline source compile failed. See $taskOutput/compile.log" }
    $taskResult = [ordered]@{ status='PASS'; scope='C# source compile against warm cached Unity references; NOT runtime, import, layout or release verification';
        sourceFiles=$taskSources.Count; unityStarted=$false; playerStarted=$false; priority='BelowNormal'; compilerParallel=$false; processorLimit=$taskCoreCount;
        sourceHashes=@($taskSources | ForEach-Object { @{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $taskRepo $_) -Algorithm SHA256).Hash} }) }
    [IO.File]::WriteAllText((Join-Path $taskOutput 'result.json'), ($taskResult | ConvertTo-Json -Depth 5))
    Write-Output "OFFLINE UI COMPILE PASS: $($taskSources.Count) source files; no Unity/Player; $taskOutput"
} finally {
    if (!$taskProcess.HasExited) { $taskProcess.Kill(); [void]$taskProcess.WaitForExit(5000) }
    $taskProcess.Dispose()
}
