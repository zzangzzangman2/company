[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Package.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root=Join-Path $repo ('Artifacts/UpdaterRestartTests/'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $root 'source'
[void][IO.Directory]::CreateDirectory((Join-Path $source 'FamilyCompany_Data'))
$compiler=Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe ('/out:'+(Join-Path $source 'FamilyCompany.exe')) (Join-Path $PSScriptRoot 'Tests/RestartProbe.cs')
if($LASTEXITCODE -ne 0){throw 'Probe compile failed'}
[IO.File]::WriteAllText((Join-Path $source 'UnityPlayer.dll'),'INERT test, not a Unity DLL')
[IO.File]::WriteAllText((Join-Path $source 'FamilyCompany_Data/level0'),'INERT test, not a Unity scene')
$package=New-CompanyPatchPackage $source (Join-Path $root 'feed') 'fc-win-20260906.1' 1 ('2'*40)
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Value){$checks.Add(@{name=$Name;passed=$Value}); if(!$Value){throw $Name}; Write-Host "PASS: $Name"}
function Start-Hidden([string]$Path,[string]$Arguments,[string]$Signal){
    $info=[Diagnostics.ProcessStartInfo]::new($Path,$Arguments)
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.WindowStyle='Hidden'
    $info.EnvironmentVariables['FC_RESTART_PROBE_SIGNAL']=$Signal
    return [Diagnostics.Process]::Start($info)
}
$success=$false
try {
    foreach($scenario in @('success','wrong-parent','corrupted-payload')){
        $install=Join-Path $root ($scenario+'/install')
        $prepared=Install-CompanyPatch $install $package.ManifestPath $package.ManifestHash -LocalFeed (Join-Path $root 'feed') -PrepareOnly
        $signal=Join-Path $root ($scenario+'/signal')
        $ready=Join-Path $root ($scenario+'/ready.json')
        $parent=Start-Hidden (Join-Path $source 'FamilyCompany.exe') '' $signal
        $helper=$null
        try {
            $ticks=$parent.StartTime.ToUniversalTime().Ticks
            if($scenario -eq 'wrong-parent'){$ticks++}
            if($scenario -eq 'corrupted-payload'){[IO.File]::AppendAllText((Join-Path $prepared.Directory 'FamilyCompany_Data/level0'),' altered')}
            $arguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+(Join-Path $PSScriptRoot 'FamilyCompany.Restart.ps1')+'" -ParentId '+$parent.Id+' -ParentStartTicks '+$ticks+' -GameDirectory "'+$source+'" -InstallRoot "'+$install+'" -PendingDirectory "'+$prepared.Directory+'" -ExpectedManifestHash '+$package.ManifestHash+' -ReadyPath "'+$ready+'"'
            $helper=Start-Hidden (Join-Path $PSHOME 'powershell.exe') $arguments $signal
            $deadline=[DateTime]::UtcNow.AddSeconds(20)
            while(!(Test-Path -LiteralPath $ready) -and !$helper.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 50}
            if($scenario -eq 'success'){
                Check 'helper sends ready before parent normal exit' ((Test-Path -LiteralPath $ready) -and !$parent.HasExited)
                Check 'current pointer absent while parent still runs' (!(Test-Path -LiteralPath (Join-Path $install 'current.json')))
                [IO.File]::WriteAllText(($signal+'.exit'),'normal exit')
                Check 'parent exits normally without forced termination' ($parent.WaitForExit(5000) -and $parent.ExitCode -eq 0)
                Check 'restart helper completes' ($helper.WaitForExit(20000) -and $helper.ExitCode -eq 0)
                $deadline=[DateTime]::UtcNow.AddSeconds(10)
                while(!(Test-Path -LiteralPath ($signal+'.launched')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 50}
                Check 'new process launches exact verified snapshot' ((Test-Path -LiteralPath ($signal+'.launched')) -and [IO.File]::ReadAllText($signal+'.launched') -ceq (Join-Path $prepared.Directory 'FamilyCompany.exe'))
                Check 'atomic current matches prepared manifest' ((Get-PatchCurrent $install).Hash -ceq $package.ManifestHash)
            } else {
                Check ($scenario+' rejected before ready or activation') ($helper.WaitForExit(5000) -and $helper.ExitCode -ne 0 -and !(Test-Path -LiteralPath $ready) -and !(Test-Path -LiteralPath (Join-Path $install 'current.json')) -and !(Test-Path -LiteralPath ($signal+'.launched')))
                Check ($scenario+' leaves parent alive') (!$parent.HasExited)
            }
        } finally {
            [IO.File]::WriteAllText(($signal+'.exit'),'normal test cleanup')
            if(!$parent.WaitForExit(5000)){throw 'Probe did not exit; no unrelated termination attempted'}
            $parent.Dispose()
            if($helper){[void]$helper.WaitForExit(5000); $helper.Dispose()}
        }
    }
    $success=$true
} finally {
    Write-PatchJsonAtomic (Join-Path $root 'result.json') @{passed=$success; checks=@($checks.ToArray());
        scope='Actual production restart helper with windowless probe, NOT real Unity/GitHub end-to-end';
        restartScriptSha256=(Get-PatchHash (Join-Path $PSScriptRoot 'FamilyCompany.Restart.ps1')); root=$root}
    Write-Host "RESTART TEST: $success $root"
}
