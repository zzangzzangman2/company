[CmdletBinding()]
param([int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$player = Join-Path $project 'Artifacts/FastQa/cache/WindowsPlayer/FamilyCompany_FastQa.exe'
$output = Join-Path $project ('Artifacts/FastQa/PlayerFather-centres-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
[void][IO.Directory]::CreateDirectory($output)
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $player; $start.WorkingDirectory = $project; $start.UseShellExecute = $false
$start.CreateNoWindow = $true; $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.Arguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 ' +
    '-familyCompanyPlayerFather3DInteractionQa -familyCompanyPlayerFather3DInteractionArtifacts "' + $output + '" -logFile "' + $output + '\player.log"'
$beforeHash = (Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash
$managed = Join-Path (Split-Path -Parent $player) 'FamilyCompany_FastQa_Data/Managed'
$managedHashes = @{}
foreach ($assembly in @('FamilyCompany.Presentation.Unity.dll', 'FamilyCompany.Simulation.dll')) {
    $managedHashes[$assembly] = (Get-FileHash -LiteralPath (Join-Path $managed $assembly) -Algorithm SHA256).Hash
}
$cacheIdentity = Get-Content -LiteralPath (Join-Path $project 'Artifacts/FastQa/cache/player-cache.json') -Raw | ConvertFrom-Json
$owned = [Diagnostics.Process]::Start($start)
$watch = [Diagnostics.Stopwatch]::StartNew(); $samples = 0; $passed = $false
Write-Host "[PAIR QA] hidden default profile; pid=$($owned.Id); artifacts=$output"
try {
    while (!$owned.WaitForExit(200)) {
        $owned.Refresh(); $samples++
        if ($owned.MainWindowHandle -ne [IntPtr]::Zero) { throw 'Unexpected visible QA window.' }
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { throw 'Pair QA timeout.' }
    }
    $owned.WaitForExit()
    $final = Get-Content -LiteralPath (Join-Path $output 'player-father-3d-interaction-final.txt') -Raw
    Write-Host $final
    if ($owned.ExitCode -ne 0 -or $final -notmatch '^FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: PASS') { throw 'Pair QA failed; preserve failure evidence.' }
    if (Select-String -LiteralPath (Join-Path $output 'player.log') -Pattern 'Exception:|error CS|: FAIL|FAIL_CLOSED' -Quiet) { throw 'Runtime log has errors.' }
    # The default-profile legacy fixture only reports these measurements; do not turn a
    # self-PASS with visible furniture intersections into an accepted collision result.
    $measurements = Get-Content -LiteralPath (Join-Path $output 'player-father-3d-interaction-result.txt') -Raw
    foreach ($gate in @('productionActorOverlapPixels=0', 'deskDetourMeshPenetrationFrames=0/0',
                        'deskDetourMaxPenetratingVertices=0/0', 'deskDetourReached=True/True',
                        'playerPhase=Working', 'fatherPhase=Working', 'retiredVisible=0')) {
        if ($measurements -notmatch ('(?m)^' + [regex]::Escape($gate) + '\r?$')) {
            throw "Pair visible-geometry gate failed or missing: $gate. Preserve measurements before cleanup."
        }
    }
    if ((Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash -cne $beforeHash) { throw 'Player changed during QA.' }
    foreach ($assembly in $managedHashes.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $managed $assembly) -Algorithm SHA256).Hash -cne $managedHashes[$assembly]) {
            throw 'Game assembly changed during QA.'
        }
    }
    $passed = $true
} finally {
    if (!$owned.HasExited) { $owned.Kill(); [void]$owned.WaitForExit(10000) }
    # The legacy in-player fixture calls itself releasePlayer=true. This runner records actual provenance.
    $result = @{passed=$passed; executable=$player; executableSha256=$beforeHash; profile='FastQA-default';
        releasePlayer=$false; productionEligible=$false; independentReleaseGate=$false; windowHandleZeroSamples=$samples;
        managedAssemblySha256=$managedHashes; cacheIdentity=$cacheIdentity;
        timeUtc=[DateTime]::UtcNow.ToString('o'); elapsedSeconds=$watch.Elapsed.TotalSeconds}
    [IO.File]::WriteAllText((Join-Path $output 'external-runner.json'), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $owned.Dispose()
}
Write-Host "[PAIR QA] PASS; hidden window samples=$samples. Gameplay fixture only, not independent Release approval."
