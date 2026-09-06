[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$qaRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$qaRoot = Join-Path $qaRepo ('Artifacts/LatestOnlyTests/' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $qaRoot 'inert-source'
[void][IO.Directory]::CreateDirectory((Join-Path $source 'FamilyCompany_Data'))
foreach ($name in @('FamilyCompany.exe','UnityPlayer.dll','FamilyCompany_Data/level0')) {
    [IO.File]::WriteAllText((Join-Path $source $name), 'INERT latest-only test data, never executed')
}
$package = New-CompanyPatchPackage $source (Join-Path $qaRoot 'feed') 'fc-win-20260906.1' 1 ('3' * 40)
$install = Join-Path $qaRoot 'install'
$installed = Install-CompanyPatch $install $package.ManifestPath $package.ManifestHash -LocalFeed (Join-Path $qaRoot 'feed')
$pointer = Join-Path $install 'current.json'
$pointerHash = Get-PatchHash $pointer
Assert-PatchInstalled $installed.Directory (Read-PatchManifest $package.ManifestPath)
$checks = @()
foreach ($legacy in @($false, $true)) {
    foreach ($scenario in @('network','missing','draft')) {
        $case = $scenario + '-' + $legacy
        $resultPath = Join-Path $qaRoot ($case + '-result.json')
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = Join-Path ([Environment]::GetFolderPath('System')) 'WindowsPowerShell/v1.0/powershell.exe'
        $info.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
            (Join-Path $PSScriptRoot 'Tests/LatestOnly.WorkerHarness.ps1') + '" -GameDirectory "' + $source +
            '" -InstallRoot "' + $install + '" -ResultPath "' + $resultPath + '" -Scenario ' + $scenario
        if ($legacy) { $info.Arguments += ' -Legacy' }
        $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.WindowStyle='Hidden'
        $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
        $process = [Diagnostics.Process]::Start($info)
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Fault test timed out.' }
        $text = $stdout.Result + $stderr.Result
        [IO.File]::WriteAllText((Join-Path $qaRoot ($case + '.log')), $text)
        $passed = $process.ExitCode -ne 0 -and !(Test-Path -LiteralPath $resultPath) -and
            (Get-PatchHash $pointer) -ceq $pointerHash -and $text -notmatch 'FC_PROGRESS .*"phase":"ready"'
        $checks += @{scenario=$scenario;legacyEntry=$legacy;exitCode=$process.ExitCode;passed=$passed;
            oldVerifiedInstallPreserved=$true;oldVersionLaunchAllowed=$false}
        $process.Dispose()
        if (!$passed) { throw ('Latest-only gate failed: ' + $case) }
    }
}
@{scope='Real production workers; API faults injected; verified inert previous install; no network or executable run';
    passed=$true;checks=$checks} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $qaRoot 'result.json') -Encoding UTF8
Write-Output "LATEST-ONLY: PASS checks=$($checks.Count); $qaRoot"
