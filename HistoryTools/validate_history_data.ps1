#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ContentRoot = "",
    [switch]$FailOnWarning
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ContentRoot)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $ContentRoot = Join-Path $repoRoot 'Assets\FamilyCompany\Content\History'
}

$script:Errors = New-Object System.Collections.Generic.List[string]
$script:Warnings = New-Object System.Collections.Generic.List[string]
$script:Reviews = New-Object System.Collections.Generic.List[string]
$script:JsonText = @{}

function Add-Err([string]$Message) { $script:Errors.Add($Message) }
function Add-Warn([string]$Message) { $script:Warnings.Add($Message) }
function As-Array($Value) { if ($null -eq $Value) { return ,@() }; return ,@($Value) }
function Has-Field($Object, [string]$Name) { return $null -ne $Object.PSObject.Properties[$Name] }
function Field($Object, [string]$Name) { if ($null -eq $Object) { return $null }; $p = $Object.PSObject.Properties[$Name]; if ($null -eq $p) { return $null }; return ,$p.Value }

function To-IsoDate([string]$Value) {
    $parsed = [datetime]::MinValue
    if ([datetime]::TryParseExact($Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$parsed)) { return $parsed }
    return $null
}

function Read-Json([string]$FileName) {
    $path = Join-Path $ContentRoot $FileName
    if (-not (Test-Path -LiteralPath $path)) { Add-Err "$FileName : missing file"; return $null }
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { Add-Err "$FileName : UTF-8 BOM is forbidden" }
    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $script:JsonText[$FileName] = $text
    $lineNo = 0
    foreach ($line in ($text -split "`n")) {
        $lineNo++
        $trim = $line.TrimStart()
        if ($trim.StartsWith('//') -or $trim.StartsWith('/*')) { Add-Err "$FileName : JSON comment at line $lineNo" }
    }
    $forbidden = [regex]::Matches($text, '(?i)"(?<name>fictionalAlias|fictionalName|aliases|releaseNameKey|releaseNameMap|debugDisplayName)"\s*:')
    foreach ($match in $forbidden) { Add-Err "$FileName : forbidden field '$($match.Groups['name'].Value)'" }
    try {
        $value = $text | ConvertFrom-Json
        if ($value -is [System.Array]) { Add-Err "$FileName : top level must be an object, not an array" }
        return $value
    } catch {
        Add-Err "$FileName : JSON parse failed: $($_.Exception.Message)"
        return $null
    }
}

function Check-Review($Record, [string]$Context) {
    if ((Field $Record 'needsReview') -eq $true) {
        $note = [string](Field $Record 'reviewNote')
        if ([string]::IsNullOrWhiteSpace($note)) { Add-Err "$Context : needsReview=true requires reviewNote" }
        $script:Reviews.Add($Context)
    }
}

function Check-DateRange([string]$StartText, [string]$EndText, [string]$Context) {
    $start = To-IsoDate $StartText
    if ($null -eq $start) { Add-Err "$Context : invalid start date '$StartText'"; return }
    if ([string]::IsNullOrEmpty($EndText)) { return }
    $end = To-IsoDate $EndText
    if ($null -eq $end) { Add-Err "$Context : invalid end date '$EndText'"; return }
    if ($end -lt $start) { Add-Err "$Context : end date $EndText is earlier than start date $StartText" }
}

function Check-Unique($Records, [string]$FieldName, [string]$Context) {
    $seen = @{}
    foreach ($record in (As-Array $Records)) {
        $id = [string](Field $record $FieldName)
        if ([string]::IsNullOrWhiteSpace($id)) { Add-Err "$Context : empty $FieldName"; continue }
        if ($seen.ContainsKey($id)) { Add-Err "$Context : duplicate $FieldName '$id'" } else { $seen[$id] = $true }
    }
    return $seen
}

Write-Host 'Korea History V1 validator' -ForegroundColor Cyan
Write-Host "Content root: $ContentRoot"
Write-Host ''

$requiredFiles = @(
    'company_registry_korea_2000_2026.json',
    'company_events_korea_2000_2003.json',
    'company_entry_exit_korea_2004_2026_anchor.json',
    'macro_timeline_korea_2000_2026_anchor.json',
    'acquisition_evidence_korea.json',
    'sources.json'
)

$schema = Read-Json 'schema_version_1.json'
$registry = Read-Json 'company_registry_korea_2000_2026.json'
$eventsDoc = Read-Json 'company_events_korea_2000_2003.json'
$entryDoc = Read-Json 'company_entry_exit_korea_2004_2026_anchor.json'
$macroDoc = Read-Json 'macro_timeline_korea_2000_2026_anchor.json'
$acquisitionDoc = Read-Json 'acquisition_evidence_korea.json'
$sourcesDoc = Read-Json 'sources.json'

if ($script:Errors.Count -gt 0) {
    foreach ($e in $script:Errors) { Write-Host "ERROR $e" -ForegroundColor Red }
    Write-Host 'VALIDATION FAILED' -ForegroundColor Red
    exit 1
}

$declaredFiles = @((As-Array (Field $schema 'files')) | ForEach-Object { [string](Field $_ 'fileName') })
foreach ($file in $requiredFiles) { if ($declaredFiles -notcontains $file) { Add-Err "schema files : '$file' is not declared" } }
foreach ($file in $declaredFiles) { if ($requiredFiles -notcontains $file) { Add-Err "schema files : obsolete or unexpected file '$file' is declared" } }

$enumMap = @{}
foreach ($enum in (As-Array (Field $schema 'enums'))) { $enumMap[[string](Field $enum 'enumName')] = @(Field $enum 'values') }
$entityMap = @{}
foreach ($entity in (As-Array (Field $schema 'entities'))) { $entityMap[[string](Field $entity 'entityName')] = @(Field $entity 'fields') }

function Check-Entity($Record, [string]$EntityName, [string]$Context) {
    if (-not $entityMap.ContainsKey($EntityName)) { Add-Err "$Context : schema entity '$EntityName' is missing"; return }
    foreach ($definition in $entityMap[$EntityName]) {
        $name = [string](Field $definition 'field')
        $type = [string](Field $definition 'type')
        $required = [bool](Field $definition 'required')
        if (-not (Has-Field $Record $name)) { if ($required) { Add-Err "$Context : required field '$name' is missing" }; continue }
        $value = Field $Record $name
        if ($type -eq 'string') {
            if ($required -and [string]::IsNullOrWhiteSpace([string]$value)) { Add-Err "$Context : required string '$name' is empty" }
        } elseif ($type -eq 'date') {
            $text = [string]$value
            if ([string]::IsNullOrEmpty($text)) { if ($required) { Add-Err "$Context : required date '$name' is empty" } }
            elseif ($null -eq (To-IsoDate $text)) { Add-Err "$Context : '$name' value '$text' is not ISO YYYY-MM-DD" }
        } elseif ($type -eq 'bool') {
            if ($value -isnot [bool]) { Add-Err "$Context : '$name' must be boolean" }
        } elseif ($type -eq 'int' -or $type -eq 'long') {
            if (($value -isnot [int]) -and ($value -isnot [long])) { Add-Err "$Context : '$name' must be an integer" }
        } elseif ($type -eq 'string[]' -or $type.EndsWith('[]')) {
            if ($null -ne $value -and $value -isnot [System.Array]) { Add-Err "$Context : '$name' must be an array" }
        } elseif ($type.StartsWith('enum:')) {
            $enumName = $type.Substring(5)
            if (-not $enumMap.ContainsKey($enumName)) { Add-Err "$Context : unknown enum '$enumName'" }
            elseif ($enumMap[$enumName] -notcontains [string]$value) { Add-Err "$Context : '$name' value '$value' is not in $enumName" }
        }
    }
}

$companies = As-Array (Field $registry 'companies')
$events = As-Array (Field $eventsDoc 'events')
$entryAnchors = As-Array (Field $entryDoc 'anchors')
$macroAnchors = As-Array (Field $macroDoc 'anchors')
$candidates = As-Array (Field $acquisitionDoc 'candidates')
$sources = As-Array (Field $sourcesDoc 'sources')

$companyIds = Check-Unique $companies 'companyId' 'company registry'
$eventIds = Check-Unique $events 'eventId' 'company events'
$entryIds = Check-Unique $entryAnchors 'anchorId' 'entry/exit anchors'
$macroIds = Check-Unique $macroAnchors 'anchorId' 'macro anchors'
$candidateIds = Check-Unique $candidates 'candidateId' 'acquisition candidates'
$sourceIds = Check-Unique $sources 'sourceId' 'sources'

$sourceTier = @{}
$sourceMethod = @{}
foreach ($source in $sources) {
    $sid = [string](Field $source 'sourceId')
    Check-Entity $source 'SourceRecord' "source[$sid]"
    $sourceTier[$sid] = [string](Field $source 'sourceTier')
    $sourceMethod[$sid] = [string](Field $source 'verificationMethod')
}

function Check-SourceRefs($Values, [string]$Context, [bool]$RequireOne = $true) {
    $refs = As-Array $Values
    if ($RequireOne -and $refs.Count -lt 1) { Add-Err "$Context : at least one sourceId is required" }
    foreach ($ref in $refs) { if (-not $sourceIds.ContainsKey([string]$ref)) { Add-Err "$Context : unknown sourceId '$ref'" } }
}

function Check-CompanyRefs($Values, [string]$Context) {
    foreach ($ref in (As-Array $Values)) {
        $id = [string]$ref
        if (-not [string]::IsNullOrEmpty($id) -and -not $companyIds.ContainsKey($id)) { Add-Err "$Context : unknown companyId '$id'" }
    }
}

function Check-Honesty($Record, [string]$Context) {
    Check-Review $Record $Context
    $refs = As-Array (Field $Record 'sourceIds')
    if ($refs.Count -eq 0) { return }
    $allUnread = $true
    foreach ($ref in $refs) { if ($sourceMethod[[string]$ref] -ne 'not_reached') { $allUnread = $false } }
    if ($allUnread -and (Field $Record 'needsReview') -ne $true) { Add-Err "$Context : all sources are not_reached, so needsReview must be true" }
}

$industryIds = @((As-Array (Field (Field $schema 'vocabularies') 'industries')) | ForEach-Object { [string](Field $_ 'id') })
$technologyIds = @((As-Array (Field (Field $schema 'vocabularies') 'technologies')) | ForEach-Object { [string](Field $_ 'id') })
$marketIds = @((As-Array (Field (Field $schema 'vocabularies') 'markets')) | ForEach-Object { [string](Field $_ 'id') })
function Check-Vocab($Values, $Allowed, [string]$Kind, [string]$Context) { foreach ($value in (As-Array $Values)) { if ($Allowed -notcontains [string]$value) { Add-Err "$Context : unknown $Kind '$value'" } } }

$koreanCompanies = @($companies | Where-Object { ([string](Field $_ 'countryCode')) -eq 'KR' })
$detailedCompanies = @($companies | Where-Object { ([string](Field $_ 'detailLevel')) -eq 'detailed_2000_2003' })
if ($koreanCompanies.Count -lt 60) { Add-Err "company registry : expected at least 60 Korean companies, found $($koreanCompanies.Count)" }
if ($detailedCompanies.Count -lt 25) { Add-Err "company registry : expected at least 25 detailed companies, found $($detailedCompanies.Count)" }

foreach ($company in $companies) {
    $cid = [string](Field $company 'companyId')
    $context = "company[$cid]"
    Check-Entity $company 'KoreaCompany' $context
    Check-Honesty $company $context
    Check-SourceRefs (Field $company 'sourceIds') "$context.sourceIds"
    Check-Vocab (Field $company 'industryIds') $industryIds 'industryId' $context
    Check-CompanyRefs (Field $company 'predecessorIds') "$context.predecessorIds"
    Check-CompanyRefs (Field $company 'successorIds') "$context.successorIds"
    if (([string](Field $company 'detailLevel')) -eq 'detailed_2000_2003' -and ([string](Field $company 'countryCode')) -ne 'KR') { Add-Err "$context : overseas companies cannot be detailed in Korea History V1" }

    $ranges = New-Object System.Collections.Generic.List[object]
    $nameIndex = -1
    foreach ($nameRecord in (As-Array (Field $company 'nameHistory'))) {
        $nameIndex++
        $nameContext = $context + '.nameHistory[' + $nameIndex + ']'
        Check-Entity $nameRecord 'KoreaCompanyName' $nameContext
        Check-Honesty $nameRecord $nameContext
        Check-SourceRefs (Field $nameRecord 'sourceIds') ($nameContext + '.sourceIds')
        $legalKo = [string](Field $nameRecord 'legalNameKo')
        $legalEn = [string](Field $nameRecord 'legalNameEn')
        $displayKo = [string](Field $nameRecord 'displayNameKo')
        if ([string]::IsNullOrWhiteSpace($legalKo) -or [string]::IsNullOrWhiteSpace($legalEn) -or [string]::IsNullOrWhiteSpace($displayKo)) { Add-Err ($nameContext + ' : legalNameKo, legalNameEn and displayNameKo are required') }
        if ($displayKo -match '(?i)fictional|placeholder') { Add-Err ($nameContext + ' : displayNameKo looks fictional or placeholder') }
        $fromText = [string](Field $nameRecord 'fromDate')
        $toText = [string](Field $nameRecord 'toDate')
        Check-DateRange $fromText $toText $nameContext
        $start = To-IsoDate $fromText
        $end = if ([string]::IsNullOrEmpty($toText)) { [datetime]::MaxValue } else { To-IsoDate $toText }
        if ($null -ne $start -and $null -ne $end) { $ranges.Add([pscustomobject]@{ Start = $start; End = $end; Name = $displayKo }) }
    }
    if ($ranges.Count -lt 1) { Add-Err "$context : nameHistory cannot be empty" }
    for ($i = 0; $i -lt $ranges.Count; $i++) { for ($j = $i + 1; $j -lt $ranges.Count; $j++) { if ($ranges[$i].Start -le $ranges[$j].End -and $ranges[$j].Start -le $ranges[$i].End) { Add-Err "$context : overlapping nameHistory '$($ranges[$i].Name)' and '$($ranges[$j].Name)'" } } }

    $brandIndex = -1
    foreach ($brand in (As-Array (Field $company 'actualBrands'))) {
        $brandIndex++; $brandContext = "$context.actualBrands[$brandIndex]"
        Check-Entity $brand 'KoreaBrand' $brandContext; Check-Honesty $brand $brandContext
        Check-SourceRefs (Field $brand 'sourceIds') "$brandContext.sourceIds"; Check-CompanyRefs @((Field $brand 'operatingCompanyId')) "$brandContext.operatingCompanyId"
        Check-DateRange ([string](Field $brand 'fromDate')) ([string](Field $brand 'toDate')) $brandContext
    }
    $listingIndex = -1
    foreach ($listing in (As-Array (Field $company 'listingHistory'))) {
        $listingIndex++; $listingContext = "$context.listingHistory[$listingIndex]"
        Check-Entity $listing 'ListingRecord' $listingContext; Check-Honesty $listing $listingContext
        Check-SourceRefs (Field $listing 'sourceIds') "$listingContext.sourceIds"; Check-DateRange ([string](Field $listing 'fromDate')) ([string](Field $listing 'toDate')) $listingContext
    }
    $ownerIndex = -1
    foreach ($owner in (As-Array (Field $company 'ownershipHistory'))) {
        $ownerIndex++; $ownerContext = "$context.ownershipHistory[$ownerIndex]"
        Check-Entity $owner 'OwnershipRecord' $ownerContext; Check-Honesty $owner $ownerContext
        Check-SourceRefs (Field $owner 'sourceIds') "$ownerContext.sourceIds"; Check-CompanyRefs @((Field $owner 'ownerCompanyId')) "$ownerContext.ownerCompanyId"
        Check-DateRange ([string](Field $owner 'fromDate')) ([string](Field $owner 'toDate')) $ownerContext
    }
}

$eventRangeStart = To-IsoDate ([string](Field $eventsDoc 'rangeStart'))
$eventRangeEnd = To-IsoDate ([string](Field $eventsDoc 'rangeEnd'))
foreach ($event in $events) {
    $eid = [string](Field $event 'eventId'); $context = "event[$eid]"
    Check-Entity $event 'HistoricalEvent' $context; Check-Honesty $event $context; Check-SourceRefs (Field $event 'sourceIds') "$context.sourceIds"
    Check-CompanyRefs (Field $event 'participantCompanyIds') "$context.participantCompanyIds"; Check-CompanyRefs (Field $event 'substituteCompanyIds') "$context.substituteCompanyIds"
    $primary = [string](Field $event 'primaryCompanyId')
    if (-not $companyIds.ContainsKey($primary)) { Add-Err "$context : unknown primaryCompanyId '$primary'" }
    if ((As-Array (Field $event 'participantCompanyIds')) -notcontains $primary) { Add-Err "$context : primaryCompanyId must appear in participantCompanyIds" }
    Check-Vocab (Field $event 'industryIds') $industryIds 'industryId' $context
    $baseline = To-IsoDate ([string](Field $event 'baselineDate')); $earliestText = [string](Field $event 'earliestDate'); $latestText = [string](Field $event 'latestDate')
    Check-DateRange $earliestText $latestText "$context event window"
    if ($null -ne $baseline -and ($baseline -lt $eventRangeStart -or $baseline -gt $eventRangeEnd)) { Add-Err "$context : baselineDate is outside file range" }
    $earliest = To-IsoDate $earliestText; $latest = To-IsoDate $latestText
    if ($null -ne $baseline -and $null -ne $earliest -and $baseline -lt $earliest) { Add-Err "$context : baselineDate is before earliestDate" }
    if ($null -ne $baseline -and $null -ne $latest -and $baseline -gt $latest) { Add-Err "$context : baselineDate is after latestDate" }
    $policy = [string](Field $event 'failurePolicy'); $delay = Field $event 'delayMaxDays'; $subs = As-Array (Field $event 'substituteCompanyIds'); $transfer = [string](Field $event 'transferTargetRule')
    if ($policy -eq 'delay' -and (($delay -isnot [int]) -or $delay -le 0)) { Add-Err "$context : delay requires delayMaxDays > 0" }
    if ($policy -ne 'delay' -and $delay -ne 0) { Add-Err "$context : non-delay policy requires delayMaxDays 0" }
    if ($policy -eq 'substitute' -and $subs.Count -lt 1) { Add-Err "$context : substitute policy requires substituteCompanyIds" }
    if ($policy -ne 'substitute' -and $subs.Count -gt 0) { Add-Err "$context : substituteCompanyIds only allowed for substitute policy" }
    if ($policy -eq 'transfer' -and [string]::IsNullOrEmpty($transfer)) { Add-Err "$context : transfer policy requires transferTargetRule" }
    if ($policy -ne 'transfer' -and -not [string]::IsNullOrEmpty($transfer)) { Add-Err "$context : transferTargetRule only allowed for transfer policy" }
    foreach ($prereq in (As-Array (Field $event 'prerequisites'))) {
        Check-Entity $prereq 'Prerequisite' "$context.prerequisite"
        Check-CompanyRefs @((Field $prereq 'companyId'), (Field $prereq 'otherCompanyId')) "$context.prerequisite company refs"
        $requiredEvent = [string](Field $prereq 'requiredEventId'); if (-not [string]::IsNullOrEmpty($requiredEvent) -and -not $eventIds.ContainsKey($requiredEvent)) { Add-Err "$context : unknown requiredEventId '$requiredEvent'" }
        $tech = [string](Field $prereq 'technologyId'); if (-not [string]::IsNullOrEmpty($tech)) { Check-Vocab @($tech) $technologyIds 'technologyId' $context }
        $market = [string](Field $prereq 'marketId'); if (-not [string]::IsNullOrEmpty($market)) { Check-Vocab @($market) $marketIds 'marketId' $context }
    }
    $effects = As-Array (Field $event 'effects'); if ($effects.Count -lt 1) { Add-Err "$context : effects cannot be empty" }
    foreach ($effect in $effects) {
        Check-Entity $effect 'Effect' "$context.effect"; Check-CompanyRefs @((Field $effect 'companyId'), (Field $effect 'targetCompanyId')) "$context.effect company refs"
        $tech = [string](Field $effect 'technologyId'); if (-not [string]::IsNullOrEmpty($tech)) { Check-Vocab @($tech) $technologyIds 'technologyId' $context }
        $market = [string](Field $effect 'marketId'); if (-not [string]::IsNullOrEmpty($market)) { Check-Vocab @($market) $marketIds 'marketId' $context }
        $industry = [string](Field $effect 'industryId'); if (-not [string]::IsNullOrEmpty($industry)) { Check-Vocab @($industry) $industryIds 'industryId' $context }
    }
}

foreach ($company in $detailedCompanies) {
    $cid = [string](Field $company 'companyId')
    if (@($events | Where-Object { (As-Array (Field $_ 'participantCompanyIds')) -contains $cid }).Count -lt 1) { Add-Err "company[$cid] : detailed company has no 2000-2003 event" }
}

foreach ($anchor in $entryAnchors) {
    $aid = [string](Field $anchor 'anchorId'); $context = "entryAnchor[$aid]"
    Check-Entity $anchor 'EntryExitAnchor' $context; Check-Honesty $anchor $context; Check-SourceRefs (Field $anchor 'sourceIds') "$context.sourceIds"; Check-CompanyRefs @((Field $anchor 'companyId')) "$context.companyId"; Check-Vocab (Field $anchor 'industryIds') $industryIds 'industryId' $context
    $date = To-IsoDate ([string](Field $anchor 'eventDate')); if ($date -lt (To-IsoDate '2004-01-01') -or $date -gt (To-IsoDate '2026-12-31')) { Add-Err "$context : eventDate outside 2004-2026" }
}

foreach ($anchor in $macroAnchors) {
    $aid = [string](Field $anchor 'anchorId'); $context = "macroAnchor[$aid]"
    Check-Entity $anchor 'MacroAnchor' $context; Check-Honesty $anchor $context; Check-SourceRefs (Field $anchor 'sourceIds') "$context.sourceIds"; Check-CompanyRefs (Field $anchor 'relatedCompanyIds') "$context.relatedCompanyIds"; Check-Vocab (Field $anchor 'industryIds') $industryIds 'industryId' $context
    Check-DateRange ([string](Field $anchor 'startDate')) ([string](Field $anchor 'endDate')) $context
}

foreach ($candidate in $candidates) {
    $cid = [string](Field $candidate 'candidateId'); $context = "candidate[$cid]"
    Check-Entity $candidate 'AcquisitionCandidate' $context; Check-Honesty $candidate $context; Check-SourceRefs (Field $candidate 'sourceIds') "$context.sourceIds"; Check-CompanyRefs @((Field $candidate 'companyId')) "$context.companyId"
    Check-DateRange ([string](Field $candidate 'windowStart')) ([string](Field $candidate 'windowEnd')) $context
    $scope = [string](Field $candidate 'candidateScope'); $asset = [string](Field $candidate 'assetKind')
    if ($scope -eq 'whole_company' -and $asset -ne 'none') { Add-Err "$context : whole_company requires assetKind none" }
    if ($scope -eq 'asset_only' -and $asset -eq 'none') { Add-Err "$context : asset_only requires a concrete assetKind" }
    $hasValuation = (Field $candidate 'hasPublicValuation') -eq $true; $valuationNote = [string](Field $candidate 'publicValuationNote')
    if ($hasValuation -and [string]::IsNullOrWhiteSpace($valuationNote)) { Add-Err "$context : hasPublicValuation=true requires publicValuationNote" }
    if (-not $hasValuation -and -not [string]::IsNullOrWhiteSpace($valuationNote)) { Add-Err "$context : unsourced valuation note is forbidden" }
    foreach ($metric in (As-Array (Field $candidate 'evidenceMetrics'))) {
        Check-Entity $metric 'AcquisitionMetric' "$context.metric"; Check-Honesty $metric "$context.metric"; Check-SourceRefs (Field $metric 'sourceIds') "$context.metric.sourceIds"
        if ((Field $metric 'amountKrw') -le 0) { Add-Err "$context.metric : amountKrw must be positive" }
    }
}

$primaryDetailed = 0
foreach ($company in $detailedCompanies) {
    $cid = [string](Field $company 'companyId'); $refs = @((Field $company 'sourceIds'))
    foreach ($event in @($events | Where-Object { (As-Array (Field $_ 'participantCompanyIds')) -contains $cid })) { $refs += @((Field $event 'sourceIds')) }
    if (@($refs | Where-Object { $sourceTier[[string]$_] -like 'primary_*' }).Count -gt 0) { $primaryDetailed++ }
}
if ($primaryDetailed -lt $detailedCompanies.Count) { Add-Warn "detailed primary-source coverage is $primaryDetailed/$($detailedCompanies.Count); uncovered companies remain needsReview expansion work" }

$reachableCandidates = @($candidates | Where-Object { ([string](Field $_ 'playerAffordabilityHint')) -in @('reachable_early', 'reachable_after_growth') })
$assetCandidates = @($candidates | Where-Object { ([string](Field $_ 'candidateScope')) -eq 'asset_only' })

Write-Host 'Counts' -ForegroundColor Cyan
Write-Host ("  Korean companies              : {0}" -f $koreanCompanies.Count)
Write-Host ("  all registry rows             : {0}" -f $companies.Count)
Write-Host ("  detailed companies 2000-2003  : {0}" -f $detailedCompanies.Count)
Write-Host ("  detailed with primary source  : {0}" -f $primaryDetailed)
Write-Host ("  company events 2000-2003      : {0}" -f $events.Count)
Write-Host ("  entry/exit anchors 2004-2026  : {0}" -f $entryAnchors.Count)
Write-Host ("  macro anchors 2000-2026       : {0}" -f $macroAnchors.Count)
Write-Host ("  acquisition candidates        : {0}" -f $candidates.Count)
Write-Host ("    reachable early/after growth: {0}" -f $reachableCandidates.Count)
Write-Host ("    asset-only candidates       : {0}" -f $assetCandidates.Count)
Write-Host ("  sources                       : {0}" -f $sources.Count)
Write-Host ("  needsReview records           : {0}" -f $script:Reviews.Count)
Write-Host ''

if ($script:Warnings.Count -gt 0) { Write-Host "Warnings: $($script:Warnings.Count)" -ForegroundColor Yellow; foreach ($warning in $script:Warnings) { Write-Host "  WARN $warning" -ForegroundColor Yellow }; Write-Host '' }
if ($script:Errors.Count -gt 0) { Write-Host "Errors: $($script:Errors.Count)" -ForegroundColor Red; foreach ($errorItem in $script:Errors) { Write-Host "  ERROR $errorItem" -ForegroundColor Red }; Write-Host 'VALIDATION FAILED' -ForegroundColor Red; exit 1 }
if ($FailOnWarning -and $script:Warnings.Count -gt 0) { Write-Host 'VALIDATION FAILED because -FailOnWarning was set' -ForegroundColor Red; exit 1 }
Write-Host 'VALIDATION PASSED with 0 errors' -ForegroundColor Green
exit 0
