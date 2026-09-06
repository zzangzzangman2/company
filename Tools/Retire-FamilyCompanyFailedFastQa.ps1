[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExpectedSourceHead,
    [Parameter(Mandatory=$true)][string]$ExpectedBaseDataHead,
    [Parameter(Mandatory=$true)][string]$BuildResult,
    [Parameter(Mandatory=$true)][string]$FailureDirectory,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
$qaRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qaParent = (Resolve-Path -LiteralPath (Join-Path $qaRepo 'Artifacts/FastQa/cache')).Path
$qaTarget = (Resolve-Path -LiteralPath (Join-Path $qaParent 'WindowsPlayer')).Path
if ($qaTarget -cne (Join-Path $qaParent 'WindowsPlayer') -or [IO.Directory]::GetParent($qaTarget).FullName -cne $qaParent) { throw 'Exact cache-root fence failed.' }
if (Get-ChildItem -LiteralPath $qaTarget -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Refusing linked payload.' }
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (!$EvidenceDirectory.StartsWith((Join-Path $qaRepo 'Artifacts/FailedPayloadEvidence') + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $EvidenceDirectory)) { throw 'Use a fresh external evidence directory.' }
$build = Get-Content -LiteralPath $BuildResult -Raw | ConvertFrom-Json
$cachePath = Join-Path $qaParent 'player-cache.json'
$cache = Get-Content -LiteralPath $cachePath -Raw | ConvertFrom-Json
$processPath = Join-Path $FailureDirectory 'process.json'
$failure = if (Test-Path -LiteralPath $processPath) { Get-Content -LiteralPath $processPath -Raw | ConvertFrom-Json } else { $null }
$attendancePath = Join-Path $FailureDirectory 'normal-autonomy-observed.txt'
$attendanceFailed = (Test-Path -LiteralPath $attendancePath) -and
    (Select-String -LiteralPath $attendancePath -SimpleMatch 'nextDayAttendanceGatePassed=False' -Quiet)
# A runner guard can abort before older runners wrote process.json. Accept the actual
# completed, explicitly failed production attendance receipt, never invent an exit code.
$failedProcess = $null -ne $failure -and $null -ne $failure.exitCode -and $failure.exitCode -ne 0
if (!$build.passed -or $build.head -cne $ExpectedSourceHead -or $cache.head -cne $ExpectedBaseDataHead -or
    (!$failedProcess -and !$attendanceFailed) -or $cache.output -cne (Join-Path $qaTarget 'FamilyCompany_FastQa.exe')) { throw 'Failed-test/build identity mismatch.' }
foreach ($baseFile in $cache.baseDataFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $qaTarget $baseFile.relativePath)).Hash -cne $baseFile.sha256) { throw 'Base data mismatch.' }
}
$compiledHash = (Get-FileHash -LiteralPath (Join-Path $qaRepo 'Library/Bee/PlayerScriptAssemblies/Assembly-CSharp.dll')).Hash
if ((Get-FileHash -LiteralPath (Join-Path $qaTarget 'FamilyCompany_FastQa_Data/Managed/Assembly-CSharp.dll')).Hash -cne $compiledHash) { throw 'Latest compiled player does not match payload.' }
if (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($qaTarget + '\', [StringComparison]::OrdinalIgnoreCase) }) { throw 'Failed payload is still running.' }
$files = @(Get-ChildItem -LiteralPath $qaTarget -Recurse -File | ForEach-Object {
    [pscustomobject]@{path=$_.FullName.Substring($qaTarget.Length+1);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
if ($files.Count -lt 3) { throw 'Incomplete payload inventory.' }
$tree = & git -C $qaRepo rev-parse ($ExpectedSourceHead + '^{tree}')
if ($LASTEXITCODE -ne 0) { throw 'Unknown source tree.' }
[void][IO.Directory]::CreateDirectory($EvidenceDirectory)
@{root=$qaTarget;sourceHead=$build.head;sourceTree=$tree;baseDataHead=$cache.head;unityVersion=$build.unityVersion;
    compiledScriptsHash=$compiledHash;classification='failed gate';oracle='Hidden normal Player observation';
    expected='All required gameplay assertions pass';actualExitCode=$failure.exitCode;attendanceReceiptFailed=$attendanceFailed;
    processReceiptPresent=($null -ne $failure);rollback='none';files=$files;
    capturedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'identity-and-hashes.json') -Encoding UTF8
Copy-Item -LiteralPath $BuildResult -Destination (Join-Path $EvidenceDirectory 'build-result.json')
Copy-Item -LiteralPath $cachePath -Destination (Join-Path $EvidenceDirectory 'base-data-identity.json')
foreach ($name in @('process.json','player.log','opening-shop-final.txt','normal-autonomy-observed.txt')) {
    $inputPath = Join-Path $FailureDirectory $name
    if (Test-Path -LiteralPath $inputPath) { Copy-Item -LiteralPath $inputPath -Destination (Join-Path $EvidenceDirectory $name) }
}
$siblings = @(Get-ChildItem -LiteralPath $qaParent | Where-Object FullName -ne $qaTarget | Select-Object -ExpandProperty FullName)
foreach ($file in $files) {
    if ((Get-FileHash -LiteralPath (Join-Path $qaTarget $file.path)).Hash -cne $file.sha256) { throw 'Payload changed during evidence capture.' }
}
Add-Type -AssemblyName Microsoft.VisualBasic
[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($qaTarget, [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
    [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin, [Microsoft.VisualBasic.FileIO.UICancelOption]::ThrowException)
if (Test-Path -LiteralPath $qaTarget) { throw 'Failed payload remains.' }
foreach ($sibling in $siblings) { if (!(Test-Path -LiteralPath $sibling)) { throw 'Sibling missing.' } }
@{root=$qaTarget;remainingFiles=0;recycled=$true;files=$files.Count;siblingsPreserved=$true;
    completedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'deletion.json') -Encoding UTF8
Write-Output "RECYCLED exact failed FastQA payload: files=$($files.Count), remaining=0; $EvidenceDirectory"
