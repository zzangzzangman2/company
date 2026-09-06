param([Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('chair','walk')][string]$Scenario)
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskPlayer=(Resolve-Path -LiteralPath $Player).Path
$taskOutput=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new evidence directory.'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
Copy-Item -LiteralPath (Join-Path (Split-Path $taskPlayer -Parent) 'BUILD_INFO.txt') -Destination $taskOutput
Add-Type -Path (Join-Path $PSScriptRoot 'CompanyQaDesktop.cs')
$taskArgs='-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -familyCompanyManualGameplayObservation "'+$taskOutput+'" -logFile "'+(Join-Path $taskOutput 'player.log')+'"'
if($Scenario -eq 'chair'){
    $taskArgs+=' -familyCompanyOpeningShopQa -familyCompanyChairFitQa -familyCompanyOpeningShopArtifacts "'+$taskOutput+'"'
}else{$taskArgs+=' -familyCompanyOpeningWalkAudit -familyCompanyOpeningWalkArtifacts "'+$taskOutput+'"'}
$taskHash=(Get-FileHash $taskPlayer -Algorithm SHA256).Hash
$taskOwner=[CompanyQaDesktop]::Start($taskPlayer,$taskArgs,$taskRepo)
$taskTimer=[Diagnostics.Stopwatch]::StartNew();$taskForced=$false
Write-Output "RELEASE $Scenario running: $taskOutput"
try{
    while(!$taskOwner.Process.WaitForExit(250)){
        if($taskTimer.Elapsed.TotalSeconds -gt 250){throw 'Release observation timed out.'}
    }
    if($taskOwner.Process.ExitCode -ne 0){throw 'Release observation failed.'}
    if((Get-FileHash $taskPlayer -Algorithm SHA256).Hash -cne $taskHash){throw 'Player changed during observation.'}
    if(Select-String -LiteralPath (Join-Path $taskOutput 'player.log') -Pattern 'Exception:|error CS|: FAIL|FAIL_CLOSED' -Quiet){throw 'Runtime error recorded.'}
    Write-Output 'CAPTURE COMPLETED; independent analysis still required.'
}finally{
    if(!$taskOwner.Process.HasExited){$taskForced=$true;$taskOwner.Process.Kill();$taskOwner.Process.WaitForExit()}
    @{pid=$taskOwner.Process.Id;exitCode=$taskOwner.Process.ExitCode;forcedStop=$taskForced;
        seconds=$taskTimer.Elapsed.TotalSeconds;playerSha256=$taskHash;privateDesktop=$taskOwner.DesktopName;
        inputDesktopAtStart=$taskOwner.InteractiveDesktopAtStart;desktopSwitchAllowed=$false;
        inputDesktopAtEnd=[CompanyQaDesktop]::ReadInteractiveDesktopName();scenario=$Scenario}|
        ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'process.json') -Encoding UTF8
    $taskOwner.Dispose()
}
