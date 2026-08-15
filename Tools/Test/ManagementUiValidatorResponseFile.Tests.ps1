[CmdletBinding()]
param(
    [string]$UnityEditorRoot = 'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor',
    [string]$ResultPath = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$helperPath = Join-Path $projectRoot 'Tools\ManagementUiValidator.Compiler.ps1'
. $helperPath

$dataRoot = Join-Path $UnityEditorRoot 'Data'
$dotnet = Join-Path $dataRoot 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $dataRoot 'DotNetSdkRoslyn\csc.dll'
$netstandard = Join-Path $dataRoot 'NetStandard\ref\2.1.0\netstandard.dll'
$fixturePrefix = 'family-company-management-ui-fixtures-'
$fixtureRoot = New-ManagementUiFencedTempDirectory $fixturePrefix
$results = [Collections.Generic.List[object]]::new()
$started = [DateTime]::UtcNow
$sourceStatusBefore = @(& git -C $projectRoot status --porcelain=v2 -uall)

function Assert-Fixture([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Add-Fixture([string]$Id, [string]$Evidence) {
    $results.Add([pscustomobject][ordered]@{ id = $Id; status = 'PASS'; evidence = $Evidence })
}

function Write-Utf8Fixture([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Get-ResponseResidue {
    @(
        Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory `
            -Filter ($script:ManagementUiResponseTempPrefix + '*') -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    )
}

$status = 'FAIL'
$failure = ''
try {
    foreach ($required in @($dotnet, $csc, $netstandard, $helperPath)) {
        Assert-Fixture (Test-Path -LiteralPath $required -PathType Leaf) "Required fixture input is missing: $required"
    }

    $baseCommit = '5ba894c93f83d1659c9969a9fce4f3904b45459f'
    $baseValidator = (& git -C $projectRoot show "$baseCommit`:Tools/Validate-ManagementUiV2.ps1") -join "`n"
    Assert-Fixture ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($baseValidator)) 'Could not read the exact base Management UI validator.'
    $currentValidator = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Tools\Validate-ManagementUiV2.ps1') -Encoding UTF8
    $identityPattern = '(?m)^\s*\$(dotnet|csc|mono|framework|netstandard|unityModules|templateAssemblies)\s*=.*$'
    $baseIdentity = @([regex]::Matches($baseValidator, $identityPattern) | ForEach-Object { $_.Value.Trim() })
    $currentIdentity = @([regex]::Matches($currentValidator, $identityPattern) | ForEach-Object { $_.Value.Trim() })
    Assert-Fixture (($baseIdentity -join "`n") -ceq ($currentIdentity -join "`n")) 'Compiler identity/path expressions drifted from the exact base.'
    $referencePattern = '(?s)\$referencePaths\s*=\s*@\((?<body>.*?)\)\s*\r?\n\s*\$referenceArguments'
    $baseReferenceMatch = [regex]::Match($baseValidator, $referencePattern)
    $currentReferenceMatch = [regex]::Match($currentValidator, $referencePattern)
    Assert-Fixture ($baseReferenceMatch.Success -and $currentReferenceMatch.Success) 'Could not extract compiler reference blocks.'
    $baseReferenceBody = [regex]::Replace($baseReferenceMatch.Groups['body'].Value, '\s+', '')
    $currentReferenceBody = [regex]::Replace($currentReferenceMatch.Groups['body'].Value, '\s+', '')
    Assert-Fixture ($baseReferenceBody -ceq $currentReferenceBody) 'Compiler reference identity or ordering drifted from the exact base.'
    $sourcePattern = '(?m)^\s*& rg --files .*? -g ''\*\.cs''\s*$'
    $baseSources = @([regex]::Matches($baseValidator, $sourcePattern) | ForEach-Object { [regex]::Replace($_.Value.Trim(), '\s+', ' ') })
    $currentSources = @([regex]::Matches($currentValidator, $sourcePattern) | ForEach-Object { [regex]::Replace($_.Value.Trim(), '\s+', ' ') })
    Assert-Fixture ($baseSources.Count -eq 4 -and ($baseSources -join "`n") -ceq ($currentSources -join "`n")) 'Compiler source-root ordering drifted from the exact base.'
    $orderedOptions = @("'-nologo'", "'-langversion:latest'", "'-target:library'", "'-nostdlib+'", "'-warn:4'", '"-out:$runtimeOutput"')
    $optionCursor = -1
    foreach ($option in $orderedOptions) {
        $next = $currentValidator.IndexOf($option, $optionCursor + 1, [StringComparison]::Ordinal)
        Assert-Fixture ($next -gt $optionCursor) "Compiler option missing or out of order: $option"
        $optionCursor = $next
    }
    Add-Fixture 'CONTRACT-01' '5ba compiler identity/reference/source-root/option order preserved'

    $escapingCases = [ordered]@{
        'plain' = '"plain"'
        'space value' = '"space value"'
        'C:\한글 space\@leading\file.cs' = '"C:\한글 space\@leading\file.cs"'
        'a"b' = '"a\"b"'
        'C:\trailing\' = '"C:\trailing\\"'
    }
    foreach ($case in $escapingCases.GetEnumerator()) {
        $actual = ConvertTo-ManagementUiCSharpResponseToken $case.Key
        Assert-Fixture ($actual -ceq $case.Value) "Response escaping mismatch for '$($case.Key)': $actual"
    }
    $lineBreakRejected = $false
    try { [void](ConvertTo-ManagementUiCSharpResponseToken "bad`nargument") } catch { $lineBreakRejected = $true }
    Assert-Fixture $lineBreakRejected 'A response argument containing LF was not rejected.'
    $leadingAtRejected = $false
    try { [void](ConvertTo-ManagementUiCSharpResponseToken '@leading') } catch {
        $leadingAtRejected = $_.Exception.Message -ceq "C# response-file arguments must not begin with '@'; Roslyn treats them as nested response directives."
    }
    Assert-Fixture $leadingAtRejected 'A leading @ response directive was not deterministically rejected.'
    Add-Fixture 'ESCAPE-01' 'space/non-ASCII/@-component/quote/trailing-backslash exact; LF and leading @ rejected'

    $nestedMissing = Join-Path $fixtureRoot 'definitely-not-a-real-response.rsp'
    $legacyLeadingResponse = Join-Path $fixtureRoot 'legacy-leading-at.rsp'
    Write-Utf8Fixture $legacyLeadingResponse ('"@{0}"' -f $nestedMissing)
    $legacyLeadingOutput = @(& $dotnet $csc "@$legacyLeadingResponse" 2>&1)
    $legacyLeadingExit = $LASTEXITCODE
    Assert-Fixture ($legacyLeadingExit -ne 0 -and ($legacyLeadingOutput -join "`n") -match 'CS2011') `
        'Actual Roslyn did not reproduce quoted leading-@ as a nested response directive.'
    $residueBeforeLeadingAt = @(Get-ResponseResidue)
    $helperLeadingRejected = $false
    try {
        [void](Invoke-ManagementUiCSharpResponseCompile `
            -DotNetPath $dotnet -CompilerPath $csc `
            -Arguments @('@definitely-not-a-real-response.rsp') -RequiredInputPaths @())
    }
    catch {
        $helperLeadingRejected = $_.Exception.Message -ceq "C# response-file arguments must not begin with '@'; Roslyn treats them as nested response directives."
    }
    Assert-Fixture $helperLeadingRejected 'Production helper did not reject a raw leading-@ argument before compiler launch.'
    Assert-Fixture (@(Get-ResponseResidue).Count -eq $residueBeforeLeadingAt.Count) 'Leading-@ rejection created response residue.'
    Add-Fixture 'DIRECTIVE-01' "actual Roslyn quoted token failed CS2011 exit=$legacyLeadingExit; helper prelaunch rejection exact; residue delta0"

    $specialSource = Join-Path $fixtureRoot '한글 space\@leading\special source.cs'
    $specialOutput = Join-Path $fixtureRoot '한글 space\@leading\special output.dll'
    Write-Utf8Fixture $specialSource 'public static class SpecialPathFixture { public const string Value = "통과"; }'
    $specialArguments = @(
        '-nologo', '-target:library', '-nostdlib+',
        "-out:$specialOutput", "-r:$netstandard", $specialSource
    )
    $special = Invoke-ManagementUiCSharpResponseCompile $dotnet $csc $specialArguments @($netstandard, $specialSource)
    Assert-Fixture ($special.ExitCode -eq 0 -and (Test-Path -LiteralPath $specialOutput -PathType Leaf)) 'Special-path response compilation failed.'
    Assert-Fixture (-not $special.ResponseHasUtf8Bom) 'Response file unexpectedly had a UTF-8 BOM.'
    Assert-Fixture ($special.ResponseLineCount -eq $specialArguments.Count) 'Response line count did not preserve the argument count.'
    Assert-Fixture (-not (Test-Path -LiteralPath $special.ResponseDirectory)) 'Special-path response directory was not cleaned.'
    Add-Fixture 'PATH-01' "Korean/space/@ path compiled; rspBytes=$($special.ResponseLength); bom=false; cleanup=true"

    $relativeAtSourceName = '@leading canonical source.cs'
    $relativeAtSource = Join-Path $fixtureRoot $relativeAtSourceName
    $relativeAtOutput = Join-Path $fixtureRoot 'leading-at-canonical-output.dll'
    Write-Utf8Fixture $relativeAtSource 'public static class CanonicalLeadingAtFixture { public const int Value = 1; }'
    Push-Location $fixtureRoot
    try {
        $relativeAt = Invoke-ManagementUiCSharpResponseCompile `
            -DotNetPath $dotnet -CompilerPath $csc `
            -Arguments @('-nologo','-target:library','-nostdlib+',"-out:$relativeAtOutput","-r:$netstandard",$relativeAtSourceName) `
            -RequiredInputPaths @($netstandard,$relativeAtSourceName)
    }
    finally { Pop-Location }
    Assert-Fixture ($relativeAt.ExitCode -eq 0 -and (Test-Path -LiteralPath $relativeAtOutput -PathType Leaf)) `
        'A declared relative file beginning with @ was not canonicalized to an absolute path before serialization.'
    Assert-Fixture (-not (Test-Path -LiteralPath $relativeAt.ResponseDirectory)) 'Canonical leading-@ path response directory was not cleaned.'
    Add-Fixture 'PATH-02' 'declared relative @ source canonicalized to absolute drive path; actual Roslyn compile PASS; cleanup=true'

    $specialRepeat = Invoke-ManagementUiCSharpResponseCompile $dotnet $csc $specialArguments @($netstandard, $specialSource)
    Assert-Fixture ($specialRepeat.ExitCode -eq 0) 'Repeated identical response compilation failed.'
    Assert-Fixture ($special.ResponseSha256 -ceq $specialRepeat.ResponseSha256 -and
        $special.ResponseLength -eq $specialRepeat.ResponseLength -and
        $special.ResponseLineCount -eq $specialRepeat.ResponseLineCount) 'Identical ordered arguments did not produce identical response bytes.'
    Assert-Fixture ($special.ResponseDirectory -cne $specialRepeat.ResponseDirectory -and
        -not (Test-Path -LiteralPath $specialRepeat.ResponseDirectory)) 'Repeated compile did not use and clean a unique response directory.'
    Add-Fixture 'DETERMINISM-01' "same ordered args produced sha=$($special.ResponseSha256); unique temp paths; cleanup=true"

    $longRoot = Join-Path $fixtureRoot 'long argv 한글 space\@sources'
    New-Item -ItemType Directory -Path $longRoot -Force | Out-Null
    $longSources = [Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt 420; $index++) {
        $name = ('LongArgumentFixture_{0:D4}_{1}.cs' -f $index, ('x' * 48))
        $path = Join-Path $longRoot $name
        Write-Utf8Fixture $path ("public static class LongArgumentFixture_{0:D4} {{ public const int Value = {0}; }}" -f $index)
        $longSources.Add($path)
    }
    $longOutput = Join-Path $fixtureRoot 'long-response-output.dll'
    $longArguments = @('-nologo', '-target:library', '-nostdlib+', "-out:$longOutput", "-r:$netstandard") + $longSources.ToArray()
    $directCommandLength = $dotnet.Length + 1 + $csc.Length + 1 + (($longArguments | ForEach-Object { ([string]$_).Length + 3 }) | Measure-Object -Sum).Sum
    Assert-Fixture ($directCommandLength -gt 32767) "Long argv fixture did not exceed the Windows limit: $directCommandLength"
    $directFailed = $false
    $directFailureText = ''
    try {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $dotnet
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        [void]$startInfo.ArgumentList.Add($csc)
        foreach ($argument in $longArguments) { [void]$startInfo.ArgumentList.Add([string]$argument) }
        $directProcess = [Diagnostics.Process]::Start($startInfo)
        $stdout = $directProcess.StandardOutput.ReadToEnd()
        $stderr = $directProcess.StandardError.ReadToEnd()
        $directProcess.WaitForExit()
        if ($directProcess.ExitCode -ne 0) {
            $directFailed = $true
            $directFailureText = "exit=$($directProcess.ExitCode) $stdout $stderr".Trim()
        }
        $directProcess.Dispose()
    }
    catch { $directFailed = $true; $directFailureText = $_.Exception.ToString() }
    $isWindowsLengthFailure = $directFailureText -match 'Win32Exception \(206\)|error code 206|filename or extension is too long|file name or extension is too long|파일 이름이나 확장명이 너무 깁니다'
    Assert-Fixture ($directFailed -and $isWindowsLengthFailure) "The over-limit direct argv did not fail at the Windows command-line boundary: $directFailureText"
    Add-Fixture 'LONG-01' "direct argv rejected by Windows length boundary; computedChars=$directCommandLength; evidence=$directFailureText"
    $longResult = Invoke-ManagementUiCSharpResponseCompile $dotnet $csc $longArguments (@($netstandard) + $longSources.ToArray())
    Assert-Fixture ($longResult.ExitCode -eq 0 -and (Test-Path -LiteralPath $longOutput -PathType Leaf)) 'The same long source list failed through the response file.'
    Assert-Fixture ($longResult.ArgumentCount -eq $longArguments.Count -and $longResult.ResponseLineCount -eq $longArguments.Count) 'Long response argument order/count changed.'
    Assert-Fixture (-not (Test-Path -LiteralPath $longResult.ResponseDirectory)) 'Long response temp directory was not cleaned.'
    Add-Fixture 'LONG-02' "same list passed via rsp; args=$($longResult.ArgumentCount); bytes=$($longResult.ResponseLength); sha=$($longResult.ResponseSha256)"

    $brokenSource = Join-Path $fixtureRoot 'deliberate compiler error.cs'
    Write-Utf8Fixture $brokenSource 'public class DeliberatelyBroken {'
    $brokenOutput = Join-Path $fixtureRoot 'broken.dll'
    $broken = Invoke-ManagementUiCSharpResponseCompile $dotnet $csc @('-nologo','-target:library','-nostdlib+',"-out:$brokenOutput","-r:$netstandard",$brokenSource) @($netstandard,$brokenSource)
    Assert-Fixture ($broken.ExitCode -ne 0) 'Deliberate compiler error returned exit 0.'
    Assert-Fixture (($broken.Output -join "`n") -match 'CS1513') 'Deliberate compiler error did not report CS1513.'
    Assert-Fixture (-not (Test-Path -LiteralPath $broken.ResponseDirectory)) 'Failed compile response directory was not cleaned.'
    Add-Fixture 'ERROR-01' "compiler error propagated exit=$($broken.ExitCode); CS1513; cleanup=true"

    $residueBeforeMissing = @(Get-ResponseResidue)
    $missingCompilerRejected = $false
    try { [void](Invoke-ManagementUiCSharpResponseCompile (Join-Path $fixtureRoot 'missing dotnet.exe') $csc @('-nologo') @()) } catch { $missingCompilerRejected = $_.Exception.Message -match 'input is missing' }
    Assert-Fixture $missingCompilerRejected 'Missing compiler host was not rejected before invocation.'
    Assert-Fixture (@(Get-ResponseResidue).Count -eq $residueBeforeMissing.Count) 'Missing compiler fixture created response residue.'
    Add-Fixture 'MISSING-01' 'missing dotnet rejected before response creation'

    $missingReference = Join-Path $fixtureRoot 'missing reference.dll'
    $missingReferenceRejected = $false
    try { [void](Invoke-ManagementUiCSharpResponseCompile $dotnet $csc @('-nologo',"-r:$missingReference",$specialSource) @($missingReference,$specialSource)) } catch { $missingReferenceRejected = $_.Exception.Message -match 'input is missing' }
    Assert-Fixture $missingReferenceRejected 'Missing reference was not rejected before compiler invocation.'
    Assert-Fixture (@(Get-ResponseResidue).Count -eq $residueBeforeMissing.Count) 'Missing reference fixture created response residue.'
    Add-Fixture 'MISSING-02' 'missing reference rejected before response creation'

    $invalidHost = Join-Path $fixtureRoot 'invalid compiler host.exe'
    Write-Utf8Fixture $invalidHost 'not a Windows executable'
    $residueBeforeLaunchException = @(Get-ResponseResidue)
    $launchExceptionObserved = $false
    try {
        [void](Invoke-ManagementUiCSharpResponseCompile `
            -DotNetPath $invalidHost -CompilerPath $csc -Arguments @('-nologo') -RequiredInputPaths @())
    }
    catch { $launchExceptionObserved = $true }
    Assert-Fixture $launchExceptionObserved 'Invalid compiler host did not produce a launch exception.'
    Assert-Fixture (@(Get-ResponseResidue).Count -eq $residueBeforeLaunchException.Count) 'Launch exception leaked a response directory.'
    Add-Fixture 'LAUNCH-01' 'actual compiler launch exception propagated; finally cleanup residue delta0'

    $stalePath = Join-Path $fixtureRoot 'runtime.rsp'
    Write-Utf8Fixture $stalePath '/this-stale-response-must-not-run'
    $staleHashBefore = (Get-FileHash -Algorithm SHA256 -LiteralPath $stalePath).Hash
    $staleOutput = Join-Path $fixtureRoot 'stale-independent.dll'
    $stale = Invoke-ManagementUiCSharpResponseCompile $dotnet $csc @('-nologo','-target:library','-nostdlib+',"-out:$staleOutput","-r:$netstandard",$specialSource) @($netstandard,$specialSource)
    $staleHashAfter = (Get-FileHash -Algorithm SHA256 -LiteralPath $stalePath).Hash
    Assert-Fixture ($stale.ExitCode -eq 0 -and $staleHashBefore -ceq $staleHashAfter) 'Stale response was consumed or changed.'
    Add-Fixture 'STALE-01' 'pre-existing runtime.rsp unchanged and not consumed'

    $jobScript = {
        param($Helper, $DotNet, $Compiler, $Netstandard, $Source, $Output)
        $ErrorActionPreference = 'Stop'
        . $Helper
        $result = Invoke-ManagementUiCSharpResponseCompile $DotNet $Compiler @('-nologo','-target:library','-nostdlib+',"-out:$Output","-r:$Netstandard",$Source) @($Netstandard,$Source)
        [pscustomobject]@{ ExitCode=$result.ExitCode; OutputExists=(Test-Path -LiteralPath $Output -PathType Leaf); ResponseDirectory=$result.ResponseDirectory; Cleaned=(-not (Test-Path -LiteralPath $result.ResponseDirectory)); Sha=$result.ResponseSha256 }
    }
    $jobAOutput = Join-Path $fixtureRoot 'concurrent-a.dll'
    $jobBOutput = Join-Path $fixtureRoot 'concurrent-b.dll'
    $jobs = @(
        Start-Job -ScriptBlock $jobScript -ArgumentList $helperPath,$dotnet,$csc,$netstandard,$specialSource,$jobAOutput
        Start-Job -ScriptBlock $jobScript -ArgumentList $helperPath,$dotnet,$csc,$netstandard,$specialSource,$jobBOutput
    )
    try {
        $null = Wait-Job -Job $jobs -Timeout 60
        Assert-Fixture (@($jobs | Where-Object State -ne 'Completed').Count -eq 0) 'Concurrent response compilation timed out.'
        $jobResults = @($jobs | Receive-Job)
        Assert-Fixture ($jobResults.Count -eq 2) "Expected two concurrent results, got $($jobResults.Count)."
        Assert-Fixture (@($jobResults | Where-Object { $_.ExitCode -ne 0 -or -not $_.OutputExists -or -not $_.Cleaned }).Count -eq 0) 'A concurrent response compile failed or leaked temp state.'
        Assert-Fixture ($jobResults[0].ResponseDirectory -cne $jobResults[1].ResponseDirectory) 'Concurrent runs reused a response directory.'
        Add-Fixture 'CONCURRENT-01' "two runs passed; uniqueDirs=true; cleanup=true"
    }
    finally { $jobs | Remove-Job -Force -ErrorAction SilentlyContinue }

    $finalResidue = @(Get-ResponseResidue)
    Assert-Fixture ($finalResidue.Count -eq $residueBeforeMissing.Count) "Response temp residue count changed from $($residueBeforeMissing.Count) to $($finalResidue.Count)."
    Add-Fixture 'CLEANUP-01' 'success/error/missing/concurrent response temp residue delta0'
    $status = 'PASS'
}
catch {
    $failure = $_.Exception.ToString()
}
finally {
    Remove-ManagementUiFencedTempDirectory $fixtureRoot $fixturePrefix
}

$sourceStatusAfter = @(& git -C $projectRoot status --porcelain=v2 -uall)
$sourceMutation = ($sourceStatusBefore -join "`n") -cne ($sourceStatusAfter -join "`n")
if ($sourceMutation) {
    $status = 'FAIL'
    $failure += "`nRepository status changed while response fixtures ran."
}

$summary = [pscustomobject][ordered]@{
    schema = 'family-company.management-ui-validator-response-fixtures.v1'
    status = $status
    startedUtc = $started.ToString('o')
    completedUtc = [DateTime]::UtcNow.ToString('o')
    fixtureCount = $results.Count
    expectedFixtureCount = 15
    unityLaunched = $false
    playerLaunched = $false
    buildExecuted = $false
    sourceMutation = $sourceMutation
    failure = $failure
    fixtures = $results.ToArray()
}
if (-not [string]::IsNullOrWhiteSpace($ResultPath)) {
    $resultFull = [IO.Path]::GetFullPath($ResultPath)
    $resultParent = Split-Path -Parent $resultFull
    if (-not (Test-Path -LiteralPath $resultParent)) { New-Item -ItemType Directory -Path $resultParent -Force | Out-Null }
    [IO.File]::WriteAllText($resultFull, ($summary | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}
if ($status -ne 'PASS' -or $results.Count -ne 15) {
    Write-Error "MANAGEMENT_UI_VALIDATOR_RESPONSE_FIXTURES: FAIL count=$($results.Count) $failure" -ErrorAction Continue
    exit 1
}
Write-Output 'MANAGEMENT_UI_VALIDATOR_RESPONSE_FIXTURES: PASS count=15'
