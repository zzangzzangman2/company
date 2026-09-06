[CmdletBinding()]
param(
    [ValidateSet('auto','diagnose','simulation-pure','editor-validation','editor-broad','player-scripts','player-startup','asset-capture','full-fallback','d3d-capture','clean-build')]
    [string]$Profile = 'auto',
    [string]$BaseRef = 'HEAD',
    [string]$UnityEditor,
    [string]$PrebuiltPlayer,
    [int]$TimeoutSeconds = 900,
    [int]$Repeat = 1,
    [switch]$NoPlayerSmoke,
    [switch]$Diagnose
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$script:ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:ArtifactRoot = Join-Path $script:ProjectRoot 'Artifacts\FastQa'
$script:RunRoot = Join-Path $script:ArtifactRoot ('runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$script:CacheRoot = Join-Path $script:ArtifactRoot 'cache'
$script:PlayerRoot = Join-Path $script:CacheRoot 'WindowsPlayer'
$script:PlayerExe = Join-Path $script:PlayerRoot 'FamilyCompany_FastQa.exe'
$script:CapturePlayerExe = if ([string]::IsNullOrWhiteSpace($PrebuiltPlayer)) { $script:PlayerExe } else { [IO.Path]::GetFullPath($PrebuiltPlayer) }
$script:LockStream = $null
$script:OwnedProcesses = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:Timings = [ordered]@{ totalMs = 0; startupAndOtherMs = 0; licenseMs = 0; assetImportMs = 0; scriptCompilationMs = 0;
    domainReloadMs = 0; editorMethodMs = 0; compileMs = 0; buildMs = 0; playerMs = 0; captureMs = 0 }
$script:FallbackReasons = [Collections.Generic.List[string]]::new()
$script:Iterations = [Collections.Generic.List[object]]::new()
$script:StartTime = Get-Date
$script:CurrentIteration = 1
$script:CacheStatus = if ([string]::IsNullOrWhiteSpace($PrebuiltPlayer)) { 'not-used' } else { 'external-prebuilt' }

function Write-Section([string]$Text) { Write-Host "[FAST QA] $Text" }

function Get-FileSha256([string]$Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-','') }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

function Invoke-Git([string[]]$Arguments, [switch]$AllowFailure) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & git -c "safe.directory=$script:ProjectRoot" -C $script:ProjectRoot @Arguments 2>&1 }
    finally { $ErrorActionPreference = $previousPreference }
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return @($output)
}

function Resolve-UnityEditor {
    $projectVersion = (Get-Content -LiteralPath (Join-Path $script:ProjectRoot 'ProjectSettings\ProjectVersion.txt') |
        Select-String 'm_EditorVersion:\s*(.+)$').Matches.Groups[1].Value.Trim()
    $candidates = [Collections.Generic.List[string]]::new()
    foreach ($item in @($UnityEditor, $env:UNITY_EDITOR, $env:FAMILY_COMPANY_UNITY_EDITOR)) {
        if (-not [string]::IsNullOrWhiteSpace($item)) { $candidates.Add($item) }
    }
    $ancestor = [IO.DirectoryInfo]$script:ProjectRoot
    while ($null -ne $ancestor) {
        $candidates.Add((Join-Path $ancestor.FullName "UnityEditors\$projectVersion\Editor\Unity.exe"))
        $ancestor = $ancestor.Parent
    }
    foreach ($programRoot in @(${env:ProgramFiles}, ${env:ProgramFiles(x86)})) {
        if (-not [string]::IsNullOrWhiteSpace($programRoot)) {
            $candidates.Add((Join-Path $programRoot "Unity\Hub\Editor\$projectVersion\Editor\Unity.exe"))
        }
    }
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $resolved = if (Test-Path -LiteralPath $candidate -PathType Container) {
            Join-Path $candidate 'Unity.exe'
        } else { $candidate }
        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            return [pscustomobject]@{ Path = [IO.Path]::GetFullPath($resolved); Version = $projectVersion }
        }
    }
    throw "Unity $projectVersion was not found. Set UNITY_EDITOR to Unity.exe or its Editor directory."
}

function Acquire-ProjectLock {
    New-Item -ItemType Directory -Force -Path (Join-Path $script:ArtifactRoot 'locks') | Out-Null
    $unityLock = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $unityLock) {
        throw "This project path is already open in Unity: $unityLock"
    }
    $lockPath = Join-Path $script:ArtifactRoot 'locks\fast-qa.lock'
    try {
        $script:LockStream = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $payload = [Text.Encoding]::UTF8.GetBytes("pid=$PID`nstarted=$([DateTime]::UtcNow.ToString('o'))`nproject=$script:ProjectRoot`n")
        $script:LockStream.SetLength(0); $script:LockStream.Write($payload, 0, $payload.Length); $script:LockStream.Flush()
    } catch { throw "Another fast QA owns this project: $lockPath`n$($_.Exception.Message)" }
}

function ConvertTo-ProcessArgument([string]$Value) {
    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') { return $Value }
    $builder = [Text.StringBuilder]::new(); [void]$builder.Append('"'); $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $slashes += 1; continue }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * (2 * $slashes + 1))); [void]$builder.Append('"'); $slashes = 0; continue
        }
        if ($slashes -gt 0) { [void]$builder.Append(('\' * $slashes)); $slashes = 0 }
        [void]$builder.Append($character)
    }
    if ($slashes -gt 0) { [void]$builder.Append(('\' * (2 * $slashes))) }
    [void]$builder.Append('"'); return $builder.ToString()
}

function Stop-OwnedProcessTree([Diagnostics.Process]$Process) {
    try {
        if ($null -eq $Process -or $Process.HasExited) { return }
        $ownedPid = $Process.Id
        $killerInfo = [Diagnostics.ProcessStartInfo]::new()
        $killerInfo.FileName = Join-Path $env:SystemRoot 'System32\taskkill.exe'
        $killerInfo.Arguments = "/PID $ownedPid /T /F"
        $killerInfo.UseShellExecute = $false; $killerInfo.CreateNoWindow = $true
        $killerInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $killerInfo.RedirectStandardOutput = $true; $killerInfo.RedirectStandardError = $true
        $killer = [Diagnostics.Process]::Start($killerInfo)
        if (-not $killer.WaitForExit(10000)) { try { $killer.Kill() } catch { } }
        $killer.Dispose()
        if (-not $Process.WaitForExit(5000)) { try { $Process.Kill(); $Process.WaitForExit(5000) | Out-Null } catch { } }
    } catch { }
}

function Remove-OwnedUnityLock([string]$FilePath, [DateTime]$ProcessStart) {
    if (-not [string]::Equals([IO.Path]::GetFileName($FilePath), 'Unity.exe', [StringComparison]::OrdinalIgnoreCase)) { return }
    $unityLock = Join-Path $script:ProjectRoot 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $unityLock)) { return }
    $created = (Get-Item -LiteralPath $unityLock).LastWriteTime
    if ($created -ge $ProcessStart.AddSeconds(-5)) {
        Remove-Item -LiteralPath $unityLock -Force
        Write-Section "removed lock left by owned Unity"
    }
}

function Start-OwnedProcess([string]$FilePath, [string[]]$Arguments, [int]$Timeout, [string]$Label) {
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FilePath
    $psi.WorkingDirectory = $script:ProjectRoot
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $psi.Arguments = (($Arguments | ForEach-Object { ConvertTo-ProcessArgument ([string]$_) }) -join ' ')
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $psi
    $timer = [Diagnostics.Stopwatch]::StartNew()
    if (-not $process.Start()) { throw "Failed to start $Label." }
    $script:OwnedProcesses.Add($process)
    if (-not $process.WaitForExit($Timeout * 1000)) {
        Stop-OwnedProcessTree $process
        if (-not $process.HasExited) {
            throw "$Label timed out after $Timeout seconds and owned pid $($process.Id) could not be terminated; its Unity lock was retained."
        }
        Remove-OwnedUnityLock $FilePath $process.StartTime
        $timedOutPid = $process.Id
        [void]$script:OwnedProcesses.Remove($process)
        $process.Dispose()
        throw "$Label timed out after $Timeout seconds. Only owned pid $timedOutPid was terminated."
    }
    $timer.Stop()
    $exitCode = $process.ExitCode
    Remove-OwnedUnityLock $FilePath $process.StartTime
    [void]$script:OwnedProcesses.Remove($process)
    $process.Dispose()
    if ($exitCode -ne 0) { throw "$Label failed with exit code $exitCode." }
    return $timer.ElapsedMilliseconds
}

function Get-ChangedFiles {
    $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Invoke-Git @('diff','--name-only','--diff-filter=ACMR',$BaseRef,'--')) {
        if ($line -is [Management.Automation.ErrorRecord] -or ([string]$line).StartsWith('warning:', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$files.Add(($line -replace '\\','/')) }
    }
    foreach ($line in Invoke-Git @('ls-files','--others','--exclude-standard')) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { [void]$files.Add(($line -replace '\\','/')) }
    }
    return @($files | Sort-Object)
}

function Test-SerializationLayoutChange([string[]]$Files) {
    foreach ($file in $Files | Where-Object { $_ -like '*.cs' }) {
        $diff = (Invoke-Git @('diff','--unified=0',$BaseRef,'--',$file) -AllowFailure) -join "`n"
        if ($diff -match '(?m)^[+-].*(SerializeField|Serializable|ISerializationCallbackReceiver|class\s+\w+\s*:\s*MonoBehaviour|public\s+[\w<>,\[\]?\.]+\s+\w+\s*(=|;))') {
            return $true
        }
        if ([string]::IsNullOrWhiteSpace($diff)) {
            $path = Join-Path $script:ProjectRoot ($file -replace '/', '\')
            if ((Test-Path -LiteralPath $path) -and
                ((Get-Content -Raw -LiteralPath $path) -match 'SerializeField|Serializable|ISerializationCallbackReceiver|class\s+\w+\s*:\s*MonoBehaviour')) {
                return $true
            }
        }
    }
    return $false
}

function Select-FastQaProfile([string[]]$Files, [object]$Manifest) {
    if ($Diagnose -or $Profile -eq 'diagnose') { return [pscustomobject]@{ Name='diagnose'; Methods=@(); Reason='explicit diagnostics' } }
    if ($Profile -ne 'auto') {
        $methods = if ($Profile -eq 'editor-broad') { @($Manifest.broadMethods) }
            elseif ($Profile -eq 'editor-validation') { @('FamilyCompany.Editor.PrototypeValidation.Run') }
            else { @() }
        return [pscustomobject]@{ Name=$Profile; Methods=$methods; Reason='explicit profile' }
    }
    if ($Files.Count -eq 0) { return [pscustomobject]@{ Name='diagnose'; Methods=@(); Reason='no changed files' } }
    if ($Files | Where-Object { $_ -match '(^Packages/|^ProjectSettings/|\.asmdef$|\.asmref$)' }) {
        return [pscustomobject]@{ Name='full-fallback'; Methods=@($Manifest.broadMethods); Reason='assembly/package/project/build settings changed' }
    }
    $nonSimulation = @($Files | Where-Object { $_ -notmatch '^Assets/FamilyCompany/Simulation/.*\.cs$' })
    if ($nonSimulation.Count -eq 0) { return [pscustomobject]@{ Name='simulation-pure'; Methods=@(); Reason='pure Simulation C# only' } }
    $content = @($Files | Where-Object { $_ -match '\.(png|psd|jpg|jpeg|tga|wav|mp3|ogg|prefab|unity|asset|mat|controller|anim)$' })
    if ($content.Count -gt 0) { return [pscustomobject]@{ Name='asset-capture'; Methods=@($Manifest.broadMethods); Reason='asset/scene/prefab/UI content requires data build and capture' } }
    $runtime = @($Files | Where-Object { $_ -match '^Assets/FamilyCompany/(Presentation\.Unity|Infrastructure\.Unity|Save)/.*\.cs$' })
    if ($runtime.Count -gt 0) {
        if (Test-SerializationLayoutChange $runtime) {
            return [pscustomobject]@{ Name='full-fallback'; Methods=@($Manifest.broadMethods); Reason='possible serialized layout change; scripts-only is unsafe' }
        }
        return [pscustomobject]@{ Name='player-scripts'; Methods=@(); Reason='runtime C# with unchanged serialized-layout signature' }
    }
    $methods = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $editorOnly = $true
    foreach ($file in $Files) {
        $matched = $false
        foreach ($rule in $Manifest.rules) {
            if ($file -match $rule.pattern) {
                $matched = $true
                foreach ($method in $rule.methods) { [void]$methods.Add([string]$method) }
            }
        }
        if ($file -notmatch '^Assets/FamilyCompany/Editor/.*\.cs$' -and $file -notmatch '^(Tools/|FAST_QA_WINDOWS\.cmd|Docs/)') { $editorOnly = $false }
        if (-not $matched -and $file -match '^Assets/') { $editorOnly = $false }
    }
    if ($editorOnly -and $methods.Count -gt 0) { return [pscustomobject]@{ Name='editor-validation'; Methods=@($methods | Sort-Object); Reason='manifest-selected Editor validation' } }
    if ($Files | Where-Object { $_ -match '^(Tools/|FAST_QA_WINDOWS\.cmd|Docs/)' }) {
        return [pscustomobject]@{ Name='diagnose'; Methods=@(); Reason='pipeline/docs-only self-diagnostics' }
    }
    return [pscustomobject]@{ Name='editor-broad'; Methods=@($Manifest.broadMethods); Reason='unknown file fell back to broader suite' }
}

function Get-CompatibilityFingerprint([string]$UnityVersion) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $builder = [Text.StringBuilder]::new()
    [void]$builder.AppendLine("unity=$UnityVersion`ntarget=StandaloneWindows64")
    foreach ($treeLine in Invoke-Git @('ls-tree','-r','HEAD','--','Assets','Packages','ProjectSettings')) {
        $textLine = [string]$treeLine
        $tab = $textLine.IndexOf("`t")
        if ($tab -lt 0) { continue }
        $treePath = $textLine.Substring($tab + 1) -replace '\\','/'
        if ($treePath -match '^Assets/FamilyCompany/Editor/' -or $treePath -match '\.cs$') { continue }
        [void]$builder.AppendLine($textLine)
    }
    $fixed = @('ProjectSettings/ProjectVersion.txt','ProjectSettings/ProjectSettings.asset',
        'ProjectSettings/EditorBuildSettings.asset','Packages/manifest.json','Packages/packages-lock.json')
    $assemblyFiles = @(Get-ChildItem -LiteralPath (Join-Path $script:ProjectRoot 'Assets') -Recurse -File |
        Where-Object { $_.Extension -in @('.asmdef','.asmref') } | ForEach-Object { $_.FullName })
    $files = @($fixed) + $assemblyFiles
    foreach ($relative in $files | Sort-Object -Unique) {
        $path = if ([IO.Path]::IsPathRooted($relative)) { $relative } else { Join-Path $script:ProjectRoot $relative }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        [void]$builder.AppendLine(($path.Substring($script:ProjectRoot.Length).TrimStart('\') -replace '\\','/'))
        [void]$builder.AppendLine((Get-FileSha256 $path))
    }
    foreach ($path in @(Get-ChildItem -LiteralPath (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Presentation.Unity') -Recurse -Filter '*.cs' -File |
            Select-Object -ExpandProperty FullName | Sort-Object)) {
        $layout = Get-Content -LiteralPath $path | Where-Object { $_ -match 'SerializeField|Serializable|ISerializationCallbackReceiver|class\s+\w+\s*:\s*MonoBehaviour|public\s+[\w<>,\[\]?\.]+\s+\w+\s*(=|;)' }
        [void]$builder.AppendLine(($path.Substring($script:ProjectRoot.Length).TrimStart('\') -replace '\\','/'))
        foreach ($line in $layout) { [void]$builder.AppendLine($line.Trim()) }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($builder.ToString())
    return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()
}

function Invoke-PureSimulation([object]$Unity) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $data = Join-Path (Split-Path $Unity.Path -Parent) 'Data'
    $dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
    $csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
    $mono = Join-Path $data 'MonoBleedingEdge\bin\mono.exe'
    $framework = Join-Path $data 'MonoBleedingEdge\lib\mono\4.7.1-api'
    $netstandard = Join-Path $data 'NetStandard\ref\2.1.0\netstandard.dll'
    foreach ($required in @($dotnet,$csc,$mono,$netstandard)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Pure harness dependency missing: $required" }
    }
    $output = Join-Path $script:RunRoot 'pure'; New-Item -ItemType Directory -Force -Path $output | Out-Null
    $exe = Join-Path $output 'FamilyCompany.Simulation.FastQa.exe'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Simulation') -Recurse -Filter '*.cs' -File |
        Select-Object -ExpandProperty FullName | Sort-Object) + @(
        Get-ChildItem -LiteralPath (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Save') -Recurse -Filter '*.cs' -File |
            Select-Object -ExpandProperty FullName | Sort-Object) + @(
        (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Editor\StarterProductValidation.cs'),
        (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Editor\StaminaSimulationValidation.cs'),
        (Join-Path $script:ProjectRoot 'Tools\FastQa\SimulationSmokeHarness.cs'))
    $arguments = @($csc,'-nologo','-langversion:latest','-target:exe','-nostdlib+','-warn:4',
        '-main:FamilyCompany.Tools.FastQa.SimulationSmokeHarness',"-out:$exe","-r:$netstandard") + $sources
    $rsp = Join-Path $output 'compile.rsp'; [IO.File]::WriteAllLines($rsp, $arguments[1..($arguments.Count-1)], [Text.UTF8Encoding]::new($false))
    $compile = Start-OwnedProcess $dotnet @($csc,"@$rsp") $TimeoutSeconds 'Roslyn simulation compile'
    $script:Timings.compileMs += $compile
    [void](Start-OwnedProcess $mono @($exe) $TimeoutSeconds 'pure simulation harness')
    $timer.Stop(); return $timer.ElapsedMilliseconds
}

function Invoke-ExternalEditorEntryCompile([object]$Unity) {
    $data = Join-Path (Split-Path $Unity.Path -Parent) 'Data'
    $dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'; $csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
    $output = Join-Path $script:RunRoot 'external-editor'; New-Item -ItemType Directory -Force -Path $output | Out-Null
    $args = @($csc,'-nologo','-langversion:9.0','-target:library','-nostdlib+',
        "-out:$(Join-Path $output 'FastQaEditorEntry.dll')",
        "-r:$(Join-Path $data 'NetStandard\ref\2.1.0\netstandard.dll')",
        "-r:$(Join-Path $data 'Managed\UnityEngine.dll')","-r:$(Join-Path $data 'Managed\UnityEditor.dll')",
        (Join-Path $script:ProjectRoot 'Assets\FamilyCompany\Editor\FastQaEditorEntry.cs'))
    $elapsed = Start-OwnedProcess $dotnet $args $TimeoutSeconds 'external FastQa Editor entry compile'
    $script:Timings.compileMs += $elapsed
    return $elapsed
}

function Invoke-Unity([object]$Unity, [string]$Mode, [string[]]$Methods) {
    $log = Join-Path $script:RunRoot ("unity-$Mode-$script:CurrentIteration.log")
    $args = @('-batchmode','-nographics','-quit','-projectPath',$script:ProjectRoot,'-buildTarget','StandaloneWindows64',
        '-executeMethod','FamilyCompany.Editor.FastQaEditorEntry.Run','-fastQaMode',$Mode,'-logFile',$log)
    if ($Methods.Count -gt 0) { $args += @('-fastQaMethods',($Methods -join ';')) }
    if ($Mode -like 'build-*') { $args += @('-fastQaBuildOutput',$script:PlayerExe) }
    $elapsed = Start-OwnedProcess $Unity.Path $args $TimeoutSeconds "Unity $Mode"
    if ($Mode -like 'build-*') { $script:Timings.buildMs += $elapsed } else { $script:Timings.compileMs += $elapsed }
    if (-not (Test-Path -LiteralPath $log)) { throw "Unity did not create its log: $log" }
    $logText = Get-Content -Raw -LiteralPath $log
    $assetMs = 0.0
    foreach ($match in [regex]::Matches($logText, 'Asset Pipeline Refresh[^\r\n]*Total:\s*([0-9.]+) seconds')) {
        $assetMs += 1000.0 * [double]::Parse($match.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    $scriptMs = 0.0
    foreach ($match in [regex]::Matches($logText, 'AssetDatabase: script compilation time:\s*([0-9.]+)s')) {
        $scriptMs += 1000.0 * [double]::Parse($match.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    $reloadMs = 0.0
    foreach ($match in [regex]::Matches($logText, 'Domain Reload Profiling:\s*([0-9]+)ms')) { $reloadMs += [double]$match.Groups[1].Value }
    $methodMs = 0.0
    foreach ($match in [regex]::Matches($logText, 'FAST_QA_EDITOR:\s*PASS[^\r\n]*elapsedMs=([0-9]+)')) {
        $methodMs += [double]$match.Groups[1].Value
    }
    $licenseMs = 0.0
    foreach ($match in [regex]::Matches($logText, 'Licensing is initialized \(took\s*([0-9.]+)s\)')) {
        $licenseMs += 1000.0 * [double]::Parse($match.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($match in [regex]::Matches($logText, 'Timed-out after\s*([0-9.]+)s, waiting for channel')) {
        $licenseMs += 1000.0 * [double]::Parse($match.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    $script:Timings.licenseMs += [int64]$licenseMs
    $script:Timings.assetImportMs += [int64]$assetMs
    $script:Timings.scriptCompilationMs += [int64]$scriptMs
    $script:Timings.domainReloadMs += [int64]$reloadMs
    $script:Timings.editorMethodMs += [int64]$methodMs
    $known = [Math]::Min($elapsed, [int64]$licenseMs + [int64]$assetMs + [int64]$scriptMs + [int64]$reloadMs + [int64]$methodMs)
    $script:Timings.startupAndOtherMs += ($elapsed - $known)
    return $elapsed
}

function Invoke-PlayerCapture {
    if (-not (Test-Path -LiteralPath $script:CapturePlayerExe -PathType Leaf)) { throw "Fast QA player is missing: $script:CapturePlayerExe" }
    $capture = Join-Path $script:RunRoot ("d3d-capture-$script:CurrentIteration"); New-Item -ItemType Directory -Force -Path $capture | Out-Null
    $log = Join-Path $capture 'player.log'
    $args = @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-force-d3d11',
        '-familyCompanyRenderClarityQa','-familyCompanyQaWidth','1920','-familyCompanyQaHeight','1080','-logFile',$log)
    $elapsed = Start-OwnedProcess $script:CapturePlayerExe $args ([Math]::Min($TimeoutSeconds,180)) 'D3D11 fast QA capture'
    $script:Timings.playerMs += $elapsed; $script:Timings.captureMs += $elapsed
    $text = Get-Content -Raw -LiteralPath $log
    if ($text -notmatch 'RENDER_CLARITY_PLAYER_QA: PASS') { throw "D3D11 capture did not report PASS: $log" }
    return $elapsed
}

function Invoke-PlayerStartupProbe {
    if (-not (Test-Path -LiteralPath $script:CapturePlayerExe -PathType Leaf)) { throw "Fast QA player is missing: $script:CapturePlayerExe" }
    $folder = Join-Path $script:RunRoot ("player-startup-$script:CurrentIteration"); New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $log = Join-Path $folder 'player.log'
    $args = @('-screen-width','640','-screen-height','360','-screen-fullscreen','0','-force-d3d11',
        '-familyCompanyFastQaStartupProbe','-logFile',$log)
    $elapsed = Start-OwnedProcess $script:CapturePlayerExe $args ([Math]::Min($TimeoutSeconds,60)) 'player startup probe'
    $script:Timings.playerMs += $elapsed
    if ((Get-Content -Raw -LiteralPath $log) -notmatch 'FAST_QA_PLAYER_STARTUP: PASS') { throw "Player startup probe did not report PASS: $log" }
    return $elapsed
}

function Read-CacheState {
    $path = Join-Path $script:CacheRoot 'player-cache.json'
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    try { return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json } catch { return $null }
}

function Get-PlayerBaseDataManifest {
    $root = Split-Path $script:PlayerExe -Parent
    $data = Join-Path $root 'FamilyCompany_FastQa_Data'
    $paths = @(
        (Join-Path $root 'UnityPlayer.dll'),
        (Join-Path $data 'globalgamemanagers'),
        (Join-Path $data 'resources.assets'),
        (Join-Path $data 'sharedassets0.assets'))
    $manifest = @()
    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Fast QA player base-data file is missing: $path" }
        $manifest += [ordered]@{ relativePath=$path.Substring($root.Length).TrimStart('\'); sha256=(Get-FileSha256 $path) }
    }
    return $manifest
}

function Test-PlayerCachePayload([object]$State) {
    if ($null -eq $State -or $State.schemaVersion -ne 1 -or $State.unityVersion -ne $script:Unity.Version -or
        $State.target -ne 'StandaloneWindows64' -or
        [string]::IsNullOrWhiteSpace([string]$State.output) -or
        -not [string]::Equals([IO.Path]::GetFullPath([string]$State.output), $script:PlayerExe, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $script:PlayerExe -PathType Leaf) -or $null -eq $State.baseDataFiles) { return $false }
    $root = Split-Path $script:PlayerExe -Parent
    foreach ($entry in $State.baseDataFiles) {
        $path = Join-Path $root ([string]$entry.relativePath)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            -not [string]::Equals((Get-FileSha256 $path), [string]$entry.sha256, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    }
    return @($State.baseDataFiles).Count -eq 4
}

function Write-CacheState([string]$Fingerprint, [string]$Head) {
    New-Item -ItemType Directory -Force -Path $script:CacheRoot | Out-Null
    [ordered]@{ schemaVersion=1; compatibilityFingerprint=$Fingerprint; head=$Head; unityVersion=$script:Unity.Version;
        target='StandaloneWindows64'; output=$script:PlayerExe; baseDataFiles=@(Get-PlayerBaseDataManifest);
        updatedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $script:CacheRoot 'player-cache.json') -Encoding UTF8
}

function Write-Result([bool]$Passed, [string]$SelectedProfile, [string]$Reason, [string[]]$Files, [string]$Head, [string]$ErrorText) {
    $script:Timings.totalMs = [int64]((Get-Date) - $script:StartTime).TotalMilliseconds
    $slo = switch ($SelectedProfile) { 'simulation-pure' {15}; 'editor-validation' {45}; 'player-scripts' {60}; 'player-startup' {15}; 'd3d-capture' {30}; 'diagnose' {60}; default {$null} }
    $totalSeconds = [Math]::Round($script:Timings.totalMs / 1000.0, 3)
    $sloObserved = if ($script:Iterations.Count -gt 0) {
        [double](($script:Iterations | ForEach-Object { $_.elapsedSeconds } | Measure-Object -Maximum).Maximum)
    } else { $totalSeconds }
    $result = [ordered]@{
        schemaVersion=1; passed=$Passed; sloMet=$(if($null -eq $slo){$null}else{$Passed -and $sloObserved -le $slo}); sloSeconds=$slo;
        sloObservedSeconds=$sloObserved; totalSeconds=$totalSeconds
        profile=$SelectedProfile; selectionReason=$Reason; fallbackReasons=@($script:FallbackReasons); head=$Head; baseRef=$BaseRef
        unityVersion=$script:Unity.Version; unityEditor=$script:Unity.Path; projectPath=$script:ProjectRoot
        cache=[ordered]@{ status=$script:CacheStatus; playerExists=(Test-Path -LiteralPath $script:PlayerExe); compatibilityFingerprint=$script:CompatibilityFingerprint }
        timings=[ordered]@{ startupAndOtherSeconds=[Math]::Round($script:Timings.startupAndOtherMs/1000.0,3);
            licenseSeconds=[Math]::Round($script:Timings.licenseMs/1000.0,3);
            assetImportSeconds=[Math]::Round($script:Timings.assetImportMs/1000.0,3); scriptCompilationSeconds=[Math]::Round($script:Timings.scriptCompilationMs/1000.0,3);
            domainReloadSeconds=[Math]::Round($script:Timings.domainReloadMs/1000.0,3); editorMethodSeconds=[Math]::Round($script:Timings.editorMethodMs/1000.0,3);
            compileSeconds=[Math]::Round($script:Timings.compileMs/1000.0,3); buildSeconds=[Math]::Round($script:Timings.buildMs/1000.0,3);
            playerSeconds=[Math]::Round($script:Timings.playerMs/1000.0,3); captureSeconds=[Math]::Round($script:Timings.captureMs/1000.0,3) }
        changedFiles=$Files; error=$ErrorText; runDirectory=$script:RunRoot; completedUtc=[DateTime]::UtcNow.ToString('o')
        iterations=@($script:Iterations)
    }
    New-Item -ItemType Directory -Force -Path $script:RunRoot | Out-Null
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $script:RunRoot 'result.json') -Encoding UTF8
    $summary = @(
        "FAST QA: $(if($Passed){'PASS'}else{'FAIL'})",
        "profile=$SelectedProfile reason=$Reason",
        "totalSeconds=$totalSeconds sloSeconds=$(if($null -eq $slo){'N/A'}else{$slo}) slo=$(if($null -eq $slo){'N/A'}elseif($result.sloMet){'MET'}else{'MISS'})",
        "compileSeconds=$($result.timings.compileSeconds) buildSeconds=$($result.timings.buildSeconds) playerSeconds=$($result.timings.playerSeconds) captureSeconds=$($result.timings.captureSeconds)",
        "head=$Head baseRef=$BaseRef", "artifacts=$script:RunRoot")
    if ($ErrorText) { $summary += "error=$ErrorText" }
    $summary | Set-Content -LiteralPath (Join-Path $script:RunRoot 'summary.txt') -Encoding UTF8
    $summary | ForEach-Object { Write-Host $_ }
}

$selected = $null; $changed = @(); $head = ''; $failure = $null
try {
    New-Item -ItemType Directory -Force -Path $script:RunRoot | Out-Null
    Acquire-ProjectLock
    $script:Unity = Resolve-UnityEditor
    $head = (Invoke-Git @('rev-parse','HEAD') | Select-Object -First 1).Trim()
    $changed = @(Get-ChangedFiles)
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $script:ProjectRoot 'Tools\FastQa\fast-qa-manifest.json') | ConvertFrom-Json
    $selected = Select-FastQaProfile $changed $manifest
    $script:CompatibilityFingerprint = Get-CompatibilityFingerprint $script:Unity.Version
    Write-Section "HEAD $head | Unity $($script:Unity.Version)"
    Write-Section "profile=$($selected.Name) | $($selected.Reason) | changed=$($changed.Count)"
    if ($changed.Count -gt 0) { $changed | ForEach-Object { Write-Host "  $_" } }
    for ($iteration = 1; $iteration -le [Math]::Max(1,$Repeat); $iteration++) {
        $script:CurrentIteration = $iteration
        Write-Section "iteration $iteration/$([Math]::Max(1,$Repeat))"
        $iterationTimer = [Diagnostics.Stopwatch]::StartNew()
        switch ($selected.Name) {
            'diagnose' {
                [void](Invoke-ExternalEditorEntryCompile $script:Unity)
                Write-Section "diagnostics PASS | editor=$($script:Unity.Path) playerCache=$(Test-Path -LiteralPath $script:PlayerExe)"
            }
            'simulation-pure' { [void](Invoke-PureSimulation $script:Unity) }
            'editor-validation' { [void](Invoke-Unity $script:Unity 'validate' @($selected.Methods)) }
            'editor-broad' { [void](Invoke-Unity $script:Unity 'validate' @($selected.Methods)) }
            'player-scripts' {
                $state = Read-CacheState
                if ($null -eq $state -or -not (Test-Path -LiteralPath $script:PlayerExe)) {
                    $script:CacheStatus = 'miss-normal-seed'
                    $script:FallbackReasons.Add('scripts-only cache absent; seeded with normal data build')
                    [void](Invoke-Unity $script:Unity 'build-normal' @())
                    Write-CacheState $script:CompatibilityFingerprint $head
                } elseif (-not (Test-PlayerCachePayload $state) -or
                    $state.compatibilityFingerprint -ne $script:CompatibilityFingerprint) {
                    $script:CacheStatus = 'mismatch-clean-seed'
                    $script:FallbackReasons.Add('scripts-only cache fingerprint or base-data hash mismatch; forced clean data rebuild')
                    [void](Invoke-Unity $script:Unity 'build-clean' @())
                    Write-CacheState $script:CompatibilityFingerprint $head
                } else { $script:CacheStatus = 'hit-scripts-only'; [void](Invoke-Unity $script:Unity 'build-scripts' @()) }
                if (-not $NoPlayerSmoke) { [void](Invoke-PlayerCapture) }
            }
            'asset-capture' {
                $script:CacheStatus = 'data-build-refresh'
                [void](Invoke-Unity $script:Unity 'build-normal' @())
                Write-CacheState $script:CompatibilityFingerprint $head
                if (-not $NoPlayerSmoke) { [void](Invoke-PlayerCapture) }
            }
            'full-fallback' {
                $script:CacheStatus = 'full-fallback-refresh'
                [void](Invoke-Unity $script:Unity 'validate' @($selected.Methods))
                [void](Invoke-Unity $script:Unity 'build-clean' @())
                Write-CacheState $script:CompatibilityFingerprint $head
                if (-not $NoPlayerSmoke) { [void](Invoke-PlayerCapture) }
            }
            'd3d-capture' { [void](Invoke-PlayerCapture) }
            'player-startup' { [void](Invoke-PlayerStartupProbe) }
            'clean-build' {
                $script:CacheStatus = 'clean-rebuild'
                [void](Invoke-Unity $script:Unity 'build-clean' @())
                Write-CacheState $script:CompatibilityFingerprint $head
            }
        }
        $iterationTimer.Stop()
        $script:Iterations.Add([ordered]@{ number=$iteration; elapsedSeconds=[Math]::Round($iterationTimer.Elapsed.TotalSeconds,3) })
    }
    Write-Result $true $selected.Name $selected.Reason $changed $head $null
    exit 0
} catch {
    $failure = $_.Exception.ToString()
    if ($null -eq $script:Unity) { $script:Unity = [pscustomobject]@{ Path=''; Version='' } }
    if ($null -eq $script:CompatibilityFingerprint) { $script:CompatibilityFingerprint = '' }
    $profileName = if ($null -ne $selected) { $selected.Name } else { 'startup' }
    $profileReason = if ($null -ne $selected) { $selected.Reason } else { 'failed before selection' }
    Write-Result $false $profileName $profileReason $changed $head $failure
    exit 1
} finally {
    foreach ($owned in @($script:OwnedProcesses)) {
        Stop-OwnedProcessTree $owned
        try { $owned.Dispose() } catch { }
    }
    if ($null -ne $script:LockStream) { $script:LockStream.Dispose() }
}
