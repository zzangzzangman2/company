[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ExpectedHead)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../FamilyCompanyBuild.Common.ps1')
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
[void](Assert-FamilyCompanyDeployableHead $repo 'main' $ExpectedHead)
$snapshot=Get-CanonicalBuildSnapshot $repo
$defaults=Get-FamilyCompanyBuildDefaults
$unity=Assert-ExactUnityEditor $defaults.UnityEditorPath $repo
$candidateParent=Join-Path $repo 'Artifacts/PatchCandidates'
$run=Join-Path $candidateParent ($ExpectedHead.Substring(0,8)+'-'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $run 'payload'
Assert-PatchNoReparse $candidateParent
if(Test-Path -LiteralPath $run){throw 'Candidate root already exists.'}
[void][IO.Directory]::CreateDirectory($payload)
$started=[DateTime]::UtcNow
$identity=@{schemaVersion=1;commit=$ExpectedHead;fingerprint=$snapshot.Fingerprint;unityVersion='6000.3.21f1';
    revision='c02631ffc030';payload=$payload;startedUtc=$started.ToString('o');productionEligible=$false}
Write-PatchJsonAtomic (Join-Path $run 'identity.json') $identity
$lock=Open-ExclusiveLock $defaults.GlobalBuildLockPath
if(!$lock){throw 'Another build owns the Unity build lock; empty candidate is not promoted.'}
$owner=$null
try {
    if(!('CompanyQaDesktop' -as [type])){Add-Type -Path (Join-Path $repo 'Tools/Background/CompanyQaDesktop.cs')}
    $arguments='-batchmode -nographics -quit -projectPath "'+$repo+'" -buildTarget Win64 '+
        '-executeMethod FamilyCompany.Editor.WindowsPlayerBuild.BuildWindowsX64 -familyCompanyBuildOutput "'+
        (Join-Path $payload 'FamilyCompany.exe')+'" -logFile "'+(Join-Path $run 'unity.log')+'"'
    $owner=[CompanyQaDesktop]::Start($unity,$arguments,$repo)
    $process=$owner.Process; $watch=[Diagnostics.Stopwatch]::StartNew()
    Write-Host "ISOLATED RELEASE CANDIDATE pid=$($process.Id) run=$run"
    while(!$process.WaitForExit(250)){if($watch.Elapsed.TotalMinutes -gt 30){throw 'Candidate build timed out.'}}
    if($process.ExitCode -ne 0){throw "Unity build failed: $($process.ExitCode)"}
    [void](Assert-FamilyCompanyDeployableHead $repo 'main' $ExpectedHead)
    if((Get-CanonicalBuildSnapshot $repo).Fingerprint -cne $snapshot.Fingerprint){throw 'Build inputs changed.'}
    if(!(Select-String -LiteralPath (Join-Path $run 'unity.log') -SimpleMatch 'FAMILY_COMPANY_WINDOWS_BUILD: PASS' -Quiet)){
        throw 'Unity did not record a completed non-Development build.'
    }
    foreach($required in @('FamilyCompany.exe','UnityPlayer.dll','FamilyCompany_Data/globalgamemanagers',
        'FamilyCompanyPatch/FamilyCompany.Update.ps1','FamilyCompanyPatch/FamilyCompany.InGame.ps1','FamilyCompanyPatch/FamilyCompany.Restart.ps1')){
        if(!(Test-Path -LiteralPath (Join-Path $payload $required))){throw "Missing candidate file: $required"}
    }
    @('Family Company Windows x64 patch candidate',"Commit: $ExpectedHead",'Branch: main','WorkingTreeDirty: False',
        "BuildInputFingerprint: $($snapshot.Fingerprint)","BuildStartedUtc: $($started.ToString('o'))",
        "BuildCompletedUtc: $([DateTime]::UtcNow.ToString('o'))",'UnityVersion: 6000.3.21f1',
        'UnityRevision: c02631ffc030','BuildTarget: StandaloneWindows64','BuildType: Release (non-Development)') |
        Set-Content -LiteralPath (Join-Path $payload 'BUILD_INFO.txt') -Encoding UTF8
    $files=@(Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
        @{path=$_.FullName.Substring($payload.Length+1);bytes=$_.Length;sha256=(Get-PatchHash $_.FullName)}})
    Write-PatchJsonAtomic (Join-Path $run 'candidate.json') @{commit=$ExpectedHead;payload=$payload;files=$files;
        buildPassed=$true;gameplayGates='PENDING';productionEligible=$false;seconds=$watch.Elapsed.TotalSeconds}
    Write-Host "CANDIDATE BUILT, NOT INSTALLED OR PUBLISHED: $payload"
} catch {
    $failureMessage=$_.Exception.Message
    if($owner){$owner.Dispose();$owner=$null}
    $actual=(Resolve-Path -LiteralPath $payload).Path
    if($actual -cne $payload -or [IO.Directory]::GetParent($actual).FullName -cne $run -or
        [IO.Directory]::GetParent($run).FullName -cne $candidateParent){throw 'Failed candidate root fence failed.'}
    Assert-PatchNoReparse $actual
    $files=@(Get-ChildItem -LiteralPath $actual -Recurse -File | ForEach-Object {
        @{path=$_.FullName.Substring($actual.Length+1);bytes=$_.Length;sha256=(Get-PatchHash $_.FullName)}})
    Write-PatchJsonAtomic (Join-Path $run 'failure.json') @{identity=$identity;error=$failureMessage;
        files=$files;classification='failed candidate build';rollback='none';capturedUtc=[DateTime]::UtcNow.ToString('o')}
    Add-Type -AssemblyName Microsoft.VisualBasic
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($actual,[Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
        [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin,[Microsoft.VisualBasic.FileIO.UICancelOption]::ThrowException)
    if(Test-Path -LiteralPath $actual){throw 'Failed executable candidate remains.'}
    Write-PatchJsonAtomic (Join-Path $run 'deletion.json') @{payload=$actual;remaining=0;recycled=$true;utc=[DateTime]::UtcNow.ToString('o')}
    throw $failureMessage
} finally {if($owner){$owner.Dispose()};$lock.Dispose()}
