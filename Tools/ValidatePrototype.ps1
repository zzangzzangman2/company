param(
    [string]$UnityEditor = 'C:\Users\godho\Documents\Codex\UnityEditors\6000.3.21f1\Editor\Unity.exe'
)

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$logPath = Join-Path $projectRoot 'Logs\prototype-validation.log'
& $UnityEditor -batchmode -nographics -quit -projectPath $projectRoot -executeMethod FamilyCompany.Editor.PrototypeValidation.Run -logFile $logPath
exit $LASTEXITCODE

