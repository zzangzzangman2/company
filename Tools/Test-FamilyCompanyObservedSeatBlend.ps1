[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$rows = @(Import-Csv -LiteralPath (Join-Path $EvidenceDirectory 'observer/chair-geometry.csv') |
    Where-Object { $_.member -ne 'none' -and $_.ready -eq 'True' })
if (!$rows.Count -or !($rows[0].PSObject.Properties.Name -contains 'seatedBlend')) { throw 'Full live blend evidence missing.' }
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number([string]$value) { [double]::Parse($value, $culture) }
$settled = @($rows | Where-Object { $_.phase -eq 'Working' -and (Number $_.seatedBlend) -ge 0.999999 })
$transitionOutliers = @($rows | Where-Object { $_.phase -eq 'Working' -and (Number $_.seatedBlend) -lt 0.999999 -and (Number $_.handMidpointError) -gt 0.05 })
$failures = @($settled | Where-Object {
    (Number $_.leftHandError) -gt 0.015 -or (Number $_.rightHandError) -gt 0.015 -or
    (Number $_.leftKneeDegrees) -lt 80 -or (Number $_.leftKneeDegrees) -gt 140 -or
    (Number $_.rightKneeDegrees) -lt 80 -or (Number $_.rightKneeDegrees) -gt 140
})
$transitions = @()
foreach ($group in ($rows | Group-Object member)) {
    $started = $null; $previous = $null
    foreach ($row in $group.Group) {
        $blend = Number $row.seatedBlend; $time = Number $row.seconds
        if ($previous -and $time - (Number $previous.seconds) -gt 0.2) { $started = $null }
        if ($row.phase -in @('Working','SittingDown','FinishingWork') -and $blend -gt 0 -and $blend -lt 0.999999 -and $null -eq $started) { $started = $time }
        if ($null -ne $started -and $blend -ge 0.999999) {
            $transitions += @{member=$group.Name;secondsFromFirstPositiveSample=$time-$started;end=$time}
            $started = $null
        }
        if ($blend -le 0 -or $row.phase -notin @('Working','SittingDown','FinishingWork')) { $started = $null }
        $previous = $row
    }
}
$maximumHandError = ($settled | ForEach-Object { [Math]::Max((Number $_.leftHandError), (Number $_.rightHandError)) } | Measure-Object -Maximum).Maximum
$coverage = @($settled | Group-Object member | ForEach-Object { @{member=$_.Name;samples=$_.Count;turns=@($_.Group.turn | Sort-Object -Unique)} })
$passed = $coverage.Count -eq 4 -and @($settled.turn | Sort-Object -Unique).Count -eq 4 -and
    $failures.Count -eq 0 -and $transitions.Count -ge 4 -and @($transitions | Where-Object secondsFromFirstPositiveSample -gt 0.52).Count -eq 0
@{scope='Independent real normal Player CSV analysis; not skin-intersection, foot-slip, full visual or Release approval';
    passed=$passed;settledSamples=$settled.Count;settledFailureSamples=$failures.Count;maximumSettledIndividualHandError=$maximumHandError;
    transitionMidpointOutliers=$transitionOutliers.Count;blendContractSeconds=0.42;transitions=$transitions;coverage=$coverage;
    productionEligible=$false} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'independent-seat-blend.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'independent-seat-blend.json') -Raw
if (!$passed) { throw 'Observed seat blend/settled pose regression or missing coverage.' }
