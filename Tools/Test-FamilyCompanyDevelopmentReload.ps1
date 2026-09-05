[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $project ('Artifacts/FastQa/DevReload-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[void][IO.Directory]::CreateDirectory($output)
$settings = Join-Path $output 'settings.json'
$sample = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'FastQa/development-settings.example.json') -Raw | ConvertFrom-Json
function Write-Settings($Value) { [IO.File]::WriteAllText($settings, ($Value | ConvertTo-Json), [Text.UTF8Encoding]::new($false)) }
Write-Settings $sample
$exe = Join-Path $project 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe'
$before = (Get-FileHash -LiteralPath $exe).Hash
$log = Join-Path $output 'player.log'
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName=$exe; $start.WorkingDirectory=$project; $start.UseShellExecute=$false
$start.CreateNoWindow=$true; $start.WindowStyle='Hidden'
$start.Arguments='-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -familyCompanyOpeningWalkAudit -familyCompanyOpeningWalkArtifacts "'+$output+'" -familyCompanyDevSettings "'+$settings+'" -logFile "'+$log+'"'
$process = [Diagnostics.Process]::Start($start)
$timer = [Diagnostics.Stopwatch]::StartNew()
$phase = 0; $samples = 0
try {
    while (!$process.WaitForExit(200)) {
        $process.Refresh(); $samples++
        if ($process.MainWindowHandle -ne [IntPtr]::Zero -or $timer.Elapsed.TotalSeconds -gt 120) { throw 'Hidden QA window/timeout failure.' }
        if (!(Test-Path -LiteralPath (Join-Path $output 'projection.csv'))) { continue }
        if ($phase -eq 0) { $captureStart=$timer.Elapsed.TotalSeconds; $phase=1 }
        $elapsed=$timer.Elapsed.TotalSeconds-$captureStart
        if ($phase -eq 1 -and $elapsed -gt 4) {
            $sample.moveSpeed=0.5; $sample.workstationPriceWon=480003; Write-Settings $sample; $phase=2
        }
        if ($phase -eq 2 -and $elapsed -gt 9) {
            [IO.File]::WriteAllText($settings, '{"moveSpeed":', [Text.UTF8Encoding]::new($false)); $phase=3
        }
        if ($phase -eq 3 -and $elapsed -gt 14) {
            $sample.moveSpeed=1.0; $sample.workstationPriceWon=400000; Write-Settings $sample; $phase=4
        }
    }
    $process.WaitForExit()
    $text=Get-Content -LiteralPath $log -Raw
    $applied=@([regex]::Matches($text,'FAMILY_DEV_SETTINGS: APPLIED[^\r\n]*') | ForEach-Object Value)
    $rejected=@([regex]::Matches($text,'FAMILY_DEV_SETTINGS: REJECTED[^\r\n]*') | ForEach-Object Value)
    if ($process.ExitCode -ne 0 -or $phase -ne 4 -or $applied.Count -ne 3 -or $rejected.Count -ne 1 -or
        $text -match 'Exception:|error CS|: FAIL|FAIL_CLOSED' -or (Get-FileHash -LiteralPath $exe).Hash -ne $before) { throw 'Reload/log/unchanged-binary gate failed.' }
    $rows=Import-Csv -LiteralPath (Join-Path $output 'walk-trace.csv')
    $speeds=@()
    foreach ($member in @('player','older_sister','father','mother')) {
        $entry=@{member=$member}
        # Measure cruising p90 after capture warm-up; normal yielding/turn deceleration may stop an actor.
        # The post-invalid window must still cruise at the prior 0.5 value, then return to 1.0.
        foreach ($window in @(@('invalidKeepsSlow',11,13.5),@('restored',17,22))) {
            $values=@($rows | Where-Object { $_.member -eq $member -and [double]$_.seconds -gt $window[1] -and [double]$_.seconds -lt $window[2] -and [double]$_.dt -gt 0 -and [double]$_.displacement -gt 0.001 } |
                ForEach-Object { [double]$_.displacement / [double]$_.dt } | Sort-Object)
            if ($values.Count -lt 10) { throw "Insufficient actual movement: $member $($window[0])" }
            $entry[$window[0]]=$values[[int][Math]::Floor($values.Count*0.9)]
        }
        if ($entry.invalidKeepsSlow -lt 0.4 -or $entry.invalidKeepsSlow -gt 0.6 -or
            $entry.restored -lt 0.85 -or $entry.restored -gt 1.15 -or $entry.restored -lt $entry.invalidKeepsSlow*1.7) {
            throw "Live motion did not match complete settings snapshots: $($entry|ConvertTo-Json -Compress)"
        }
        $speeds+=$entry
    }
    @{passed=$true; applied=$applied; rejected=$rejected; actualSpeeds=$speeds; playerSha256=$before;
        buildCount=0; windowHandleZeroSamples=$samples; nativePointerTested=$false} | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $output 'reload-result.json') -Encoding UTF8
    Write-Output "DEVELOPMENT RELOAD: PASS (same EXE, zero builds, 4 bodies, invalid edit retained); $output"
} finally {
    if (!$process.HasExited) { $process.Kill(); [void]$process.WaitForExit(10000) }
    $process.Dispose()
}
