$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $repo 'Assets/FamilyCompany/Presentation.Unity/PatchDisplayProgress.cs') -Raw)
$progress = [FamilyCompany.Presentation.Unity.PatchDisplayProgress]::new()
$cases = @(
    @('check',0,0,0), @('check-files',8,10,0), @('download',10,100,8.5),
    @('expand',0,0,8.5), @('reuse',0,0,8.5), @('download',30,100,25.5),
    @('download',5,100,25.5), @('expand',1,1,25.5), @('download',100,100,85),
    @('verify',1,100,85.14), @('verify',50,100,92), @('verify',100,100,99),
    @('activate',0,0,99), @('ready',1,1,99), @('verify',0,0,99), @('error',0,0,99)
)
foreach($case in $cases) {
    $value = $progress.Observe($case[0],$case[1],$case[2])
    if ([Math]::Abs($value - $case[3]) -gt .000001) {throw "Incorrect overall progress: $($case[0]) $value != $($case[3])"}
}
if($progress.Complete() -ne 100){throw 'Validated completion is not 100.'}
$progress.Reset()
if($progress.Percent -ne 0){throw 'Explicit retry must reset the attempt.'}
Write-Output 'DISPLAY PROGRESS PASS: 18 assertions; multi-file interleaving, stage transitions, out-of-order counts, validation gate, retry.'
