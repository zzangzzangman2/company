param(
    [string]$UnityEditor = ''
)

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'FamilyCompanyBuild.Common.ps1')
if ([string]::IsNullOrWhiteSpace($UnityEditor)) {
    $UnityEditor = Find-FamilyCompanyUnityEditor
}
$UnityEditor = Assert-ExactUnityEditor $UnityEditor $projectRoot
$logPath = Join-Path $projectRoot 'Logs\prototype-validation.log'
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $projectRoot + '"'),
    '-executeMethod', 'FamilyCompany.Editor.PrototypeValidation.Run',
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
exit $process.ExitCode

