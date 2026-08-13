#Requires -Version 5.1
<#
.SYNOPSIS
    빌드를 다시 만들지 않고 콘텐츠 JSON을 고쳐 볼 수 있도록 LiveData 링크를 건다.

.DESCRIPTION
    프로젝트의 Assets\FamilyCompany\Content 폴더를 플레이테스트 빌드 옆에 디렉터리
    정션으로 연결한다. 게임(LiveContentPath.cs)은 이 폴더를 먼저 읽고, 없으면 빌드에
    내장된 데이터로 되돌아간다.

    링크는 빌드 출력 폴더 '안'이 아니라 '옆'에 만든다. Build-FamilyCompanyWindows.ps1은
    승격 단계에서 최종 출력 폴더를 통째로 교체하므로 안에 두면 빌드마다 사라진다.

        Downloads\
        ├─ FamilyCompany_Playtest\      <- 빌드가 매번 교체
        └─ FamilyCompany_LiveData\  ->  Assets\FamilyCompany\Content   (정션)

    심볼릭 링크가 아니라 정션을 쓴다. 정션은 로컬 드라이브에서 관리자 권한 없이 만들 수 있다.

.PARAMETER LinkPath
    만들 링크의 경로. 기본값은 빌드 출력 폴더의 형제인 FamilyCompany_LiveData다.

.PARAMETER TargetPath
    링크가 가리킬 실제 콘텐츠 폴더. 기본값은 이 저장소의 Assets\FamilyCompany\Content다.

.PARAMETER Remove
    링크를 만들지 않고 제거한다.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Link-LiveContent.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Link-LiveContent.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$LinkPath = 'C:\Users\godho\Downloads\FamilyCompany_LiveData',
    [string]$TargetPath = '',
    [switch]$Remove
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-FullPath {
    param([string]$Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Get-ReparseTarget {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path -Force
    if ($null -eq $item) { return $null }
    # PowerShell 5.1 은 정션에 Target 을, 심링크에도 Target 을 채운다.
    if ($item.PSObject.Properties.Name -contains 'Target') {
        $target = @($item.Target)
        if ($target.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($target[0])) {
            return Get-FullPath $target[0]
        }
    }
    return $null
}

function Test-IsReparsePoint {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path -Force
    return (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
}

# ---------------------------------------------------------------- 경로 결정

if ([string]::IsNullOrWhiteSpace($TargetPath)) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $TargetPath = Join-Path $repositoryRoot 'Assets\FamilyCompany\Content'
}

$linkFull = Get-FullPath $LinkPath
$targetFull = Get-FullPath $TargetPath

# ---------------------------------------------------------------- 제거 모드

if ($Remove) {
    if (-not (Test-Path -LiteralPath $linkFull)) {
        Write-Output "링크가 이미 없다: $linkFull"
        exit 0
    }

    if (-not (Test-IsReparsePoint $linkFull)) {
        Write-Error "실제 폴더라서 제거하지 않는다. 직접 확인할 것: $linkFull"
        exit 2
    }

    # 정션은 Directory.Delete 로 지워야 대상 폴더의 내용이 지워지지 않는다.
    [System.IO.Directory]::Delete($linkFull, $false)
    Write-Output "링크를 제거했다: $linkFull"
    exit 0
}

# ---------------------------------------------------------------- 대상 검사

if (-not (Test-Path -LiteralPath $targetFull -PathType Container)) {
    Write-Error "연결할 콘텐츠 폴더가 없다: $targetFull"
    exit 3
}

$registryProbe = Join-Path $targetFull 'History\company_registry_korea_2000_2026.json'
if (-not (Test-Path -LiteralPath $registryProbe -PathType Leaf)) {
    Write-Warning "대상 폴더에서 등록부 JSON을 찾지 못했다. 경로가 맞는지 확인할 것: $registryProbe"
}

# ---------------------------------------------------------------- 기존 링크 처리

if (Test-Path -LiteralPath $linkFull) {
    if (-not (Test-IsReparsePoint $linkFull)) {
        Write-Error @"
같은 경로에 실제 폴더가 있어서 덮어쓰지 않는다.
안에 필요한 파일이 있는지 확인하고 직접 옮기거나 지운 뒤 다시 실행할 것:
  $linkFull
"@
        exit 4
    }

    $existingTarget = Get-ReparseTarget $linkFull
    if ($null -ne $existingTarget -and
        [string]::Equals($existingTarget, $targetFull, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Output "이미 올바르게 연결되어 있다."
        Write-Output "  $linkFull  ->  $targetFull"
        exit 0
    }

    Write-Output "다른 곳을 가리키던 링크를 다시 건다. 이전 대상: $existingTarget"
    [System.IO.Directory]::Delete($linkFull, $false)
}

# ---------------------------------------------------------------- 링크 생성

$linkParent = Split-Path -Parent $linkFull
if (-not (Test-Path -LiteralPath $linkParent -PathType Container)) {
    New-Item -ItemType Directory -Path $linkParent -Force | Out-Null
}

try {
    New-Item -ItemType Junction -Path $linkFull -Target $targetFull | Out-Null
}
catch {
    Write-Error @"
정션 생성에 실패했다: $($_.Exception.Message)

링크와 대상이 서로 다른 드라이브에 있으면 정션을 만들 수 없다.
그럴 때는 게임이 읽는 폴더를 환경 변수로 직접 지정하면 된다.

  setx FAMILYCOMPANY_LIVE_CONTENT "$targetFull"

새 콘솔이나 재로그인 뒤부터 적용된다.
"@
    exit 5
}

Write-Output "연결했다."
Write-Output "  $linkFull  ->  $targetFull"
Write-Output ''
Write-Output '이제 개발 빌드에서 콘텐츠 JSON을 고치고 게임에서 F5를 누르면 즉시 반영된다.'
Write-Output '링크는 빌드 출력 폴더 밖에 있으므로 다시 빌드해도 살아남는다.'
exit 0
