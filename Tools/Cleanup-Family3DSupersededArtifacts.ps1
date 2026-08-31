param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$artifactRoot = Join-Path $ProjectRoot "Artifacts/Family3DStarterOfficeCandidateQaV1"
$resolvedProject = (Resolve-Path -LiteralPath $ProjectRoot).Path
$resolvedArtifactRoot = (Resolve-Path -LiteralPath $artifactRoot).Path

if (-not $resolvedArtifactRoot.StartsWith(
        $resolvedProject + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Artifact root is outside the project: $resolvedArtifactRoot"
}

$keep = @(
    "FatherV19MeshyOnePackage613MapBuildV26AtomicOriginalChair",
    "FatherV19MeshyOnePackage613MapRuntimeV31AtomicOriginalChair-CompanyPullFull",
    "PlayerV6MeshyOnePackage613MapBuildV8PlayerOnlyBalancedColor",
    "PlayerV6MeshyOnePackage613MapRuntimeV8PlayerOnlyBalancedColor"
)

foreach ($name in $keep) {
    $path = Join-Path $resolvedArtifactRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required current proof is missing: $path"
    }
}

$targets = Get-ChildItem -LiteralPath $resolvedArtifactRoot -Directory |
    Where-Object { $keep -notcontains $_.Name }

$bytes = 0L
foreach ($target in $targets) {
    $resolvedTarget = (Resolve-Path -LiteralPath $target.FullName).Path
    if (-not $resolvedTarget.StartsWith(
            $resolvedArtifactRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing unsafe target: $resolvedTarget"
    }
    if ($resolvedTarget -match '(?i)(\\production\\|\\default\\|\\downloads\\)') {
        throw "Refusing protected target: $resolvedTarget"
    }
    $bytes += (Get-ChildItem -LiteralPath $resolvedTarget -Recurse -File -Force |
        Measure-Object Length -Sum).Sum
}

Write-Output ("Verified {0} superseded QA directories ({1:N2} GiB)." -f
    $targets.Count, ($bytes / 1GB))

foreach ($target in $targets) {
    Remove-Item -LiteralPath $target.FullName -Recurse -Force
}

Write-Output "Kept current approved proof directories:"
Get-ChildItem -LiteralPath $resolvedArtifactRoot -Directory |
    Sort-Object Name |
    Select-Object -ExpandProperty Name
