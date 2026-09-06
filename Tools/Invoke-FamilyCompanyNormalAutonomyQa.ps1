[CmdletBinding()]
param([string]$EvidenceDirectory = '', [switch]$AnalyzeOnly, [switch]$NextDay, [string]$Player = '')
$ErrorActionPreference = 'Stop'
$qaRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$EvidenceDirectory) { $EvidenceDirectory = Join-Path $qaRepo ('Artifacts/NormalAutonomy/' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (!$AnalyzeOnly) {
    if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Use a new evidence directory.' }
    [void][IO.Directory]::CreateDirectory($EvidenceDirectory)
    $qaPlayer = if ($Player) { (Resolve-Path -LiteralPath $Player).Path } else { Join-Path $qaRepo 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe' }
    $start = [Diagnostics.ProcessStartInfo]::new($qaPlayer)
    $start.WorkingDirectory = $qaRepo; $start.UseShellExecute = $false
    $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    $start.Arguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 ' +
        '-familyCompanyTraceOnlyQa -familyCompanyOpeningShopQa -familyCompanyAutonomyTraceQa -familyCompanyOpeningShopArtifacts "' +
        $EvidenceDirectory + '" -familyCompanyBackgroundChairObservation "' + (Join-Path $EvidenceDirectory 'observer') +
        '" -logFile "' + (Join-Path $EvidenceDirectory 'player.log') + '"'
    if ($NextDay) { $start.Arguments += ' -familyCompanyNextDayAutonomyQa' }
    if ($Player) {
        Copy-Item -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($qaPlayer)) 'BUILD_INFO.txt') -Destination $EvidenceDirectory
        $start.Arguments += ' -familyCompanyManualGameplayObservation "' + (Join-Path $EvidenceDirectory 'observer') + '"'
    } else { Copy-Item -LiteralPath (Join-Path $qaRepo 'Artifacts/FastQa/cache/player-cache.json') -Destination (Join-Path $EvidenceDirectory 'base-data-build.json') }
    if (!('CompanyQaDesktop' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'Background/CompanyQaDesktop.cs') }
    $isolation = [CompanyQaDesktop]::Start($qaPlayer, $start.Arguments, $qaRepo)
    $process = $isolation.Process
    $watch = [Diagnostics.Stopwatch]::StartNew(); $windowChecks = 0
    $runnerFailure = ''; $forcedStop = $false; $lastWindowHandle = 0L
    Write-Host "HIDDEN NORMAL AUTONOMY pid=$($process.Id) evidence=$EvidenceDirectory"
    try {
        while (!$process.WaitForExit(250)) {
            $process.Refresh(); $windowChecks++
            $observedWindowHandle = $process.MainWindowHandle
            if ($null -eq $observedWindowHandle) {
                if ($process.HasExited) { break }
                throw 'Live process window state unavailable.'
            }
            $lastWindowHandle = $observedWindowHandle.ToInt64()
            if ($lastWindowHandle -ne 0) { throw 'Hidden-window guard failed.' }
            if ($watch.Elapsed.TotalSeconds -gt 450) { throw 'Normal observation timed out.' }
        }
        if ($process.ExitCode -ne 0) { throw 'Normal observer exited with errors.' }
    } catch {
        $runnerFailure = $_.Exception.Message
        throw
    } finally {
        try {
            if (!$process.HasExited) { $forcedStop = $true; $process.Kill(); $process.WaitForExit() }
            $desktopAtEnd = $null; $desktopReadError = ''
            try { $desktopAtEnd = [CompanyQaDesktop]::ReadInteractiveDesktopName() }
            catch { $desktopReadError = $_.Exception.Message }
            @{pid=$process.Id;exitCode=$process.ExitCode;windowChecks=$windowChecks;seconds=$watch.Elapsed.TotalSeconds;
                runnerFailure=$runnerFailure;forcedStop=$forcedStop;lastWindowHandle=$lastWindowHandle;
                privateDesktop=$isolation.DesktopName;interactiveDesktopAtStart=$isolation.InteractiveDesktopAtStart;
                interactiveDesktopAtEnd=$desktopAtEnd;desktopReadError=$desktopReadError;desktopSwitchAllowed=$false} |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'process.json') -Encoding UTF8
        } finally { $isolation.Dispose() }
    }
}
if (!(Test-Path -LiteralPath (Join-Path $EvidenceDirectory 'normal-autonomy-observed.txt'))) { throw 'Missing completed normal observation.' }
$rows = @(Import-Csv -LiteralPath (Join-Path $EvidenceDirectory 'observer/autonomy.csv'))
$worldRows = @(Import-Csv -LiteralPath (Join-Path $EvidenceDirectory 'observer/observations.csv'))
# The shop pauses the world and rebuilds its actors. A cached Navigating phase while this public
# edit workflow is paused is not an active navigation stall. The receipt records its exact clock.
$receipt = Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'normal-autonomy-observed.txt') -Raw
if ($receipt -notmatch 'finalTime=([^\r\n]+)') { throw 'Missing shop setup clock boundary.' }
$setupClock = [DateTime]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
$pausedSamples = @($rows | Where-Object { [DateTime]$_.clock -eq $setupClock -or $_.ready -eq 'False' -or $_.shopOpen -eq 'True' })
$rows = @($rows | Where-Object { [DateTime]$_.clock -ne $setupClock -and $_.ready -ne 'False' -and $_.shopOpen -ne 'True' })
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number([string]$value) { [double]::Parse($value, $culture) }
$actors = @(); $railError = 0.0
foreach ($group in ($rows | Group-Object member)) {
    $previous = $null; $stall = 0.0; $maximumStall = 0.0; $distance = 0.0
    foreach ($row in $group.Group) {
        $x = Number $row.x; $y = Number $row.y
        if ($previous) {
            $dx = $x - (Number $previous.x); $dy = $y - (Number $previous.y)
            $step = [Math]::Sqrt($dx * $dx + $dy * $dy)
            # Layout rebuild explicitly relocates actors. Only uninterrupted navigation samples
            # count as locomotion; do not count a setup/rebind as travelled distance.
            if ($row.phase -eq 'Navigating' -and $previous.phase -eq 'Navigating' -and
                (Number $row.seconds) - (Number $previous.seconds) -lt 1.0) {
                $distance += $step
                if ($step -lt 0.00001) { $stall += (Number $row.seconds) - (Number $previous.seconds) } else { $stall = 0.0 }
            } else { $stall = 0.0 }
            $maximumStall = [Math]::Max($maximumStall, $stall)
        }
        if ($row.phase -eq 'Navigating') {
            # Inverse of the actual canonical office basis: X=(8/9,-8/9), Y=(4/9,4/9), originY=4/9.
            $u = 0.5625 * $x + 1.125 * $y - 0.5
            $v = -0.5625 * $x + 1.125 * $y - 0.5
            $railError = [Math]::Max($railError, [Math]::Min([Math]::Abs($u - [Math]::Round($u)), [Math]::Abs($v - [Math]::Round($v))))
        }
        $previous = $row
    }
    $actors += [pscustomobject]@{member=$group.Name;maximumNavigatingNoProgressSeconds=$maximumStall;navigationDistance=$distance;
        workingSamples=@($group.Group | Where-Object phase -eq 'Working').Count;lastPhase=$group.Group[-1].phase}
}
$violations = @($worldRows | Where-Object { [int]$_.staticViolations -ne 0 -or [int]$_.interactionViolations -ne 0 -or
    [int]$_.agentPenetrations -ne 0 -or [int]$_.errors -ne 0 })
$passed = $actors.Count -eq 4 -and $violations.Count -eq 0 -and $railError -lt 0.0001 -and
    @($actors | Where-Object { $_.maximumNavigatingNoProgressSeconds -ge 8 }).Count -eq 0
$result = [ordered]@{scope='Independent CSV navigation analysis; programmatic shop setup; no native input/pose/route injection; not Release approval';
    navigationPassed=$passed;sampleCount=$rows.Count;excludedPausedSetupSamples=$pausedSamples.Count;maxRailFractionError=$railError;violationSamples=$violations.Count;actors=$actors;
    allNpcSeatingObserved=@($actors | Where-Object { $_.member -ne 'player' -and $_.workingSamples -eq 0 }).Count -eq 0;
    manualPlayerAutoWorkExpected=$false;productionEligible=$false}
$json = $result | ConvertTo-Json -Depth 5
$json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'independent-navigation.json') -Encoding UTF8
Write-Output $json
if (!$passed) { throw 'Normal navigation regression; preserve evidence and retire the exact failed payload.' }
