Set-StrictMode -Version 2.0

$script:ManagementUiValidatorTempPrefix = 'family-company-management-ui-validator-'
$script:ManagementUiResponseTempPrefix = 'family-company-management-ui-response-'

function ConvertTo-ManagementUiCSharpResponseToken {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Argument
    )

    if ($Argument.IndexOf([char]0) -ge 0 -or
        $Argument.IndexOf("`r", [StringComparison]::Ordinal) -ge 0 -or
        $Argument.IndexOf("`n", [StringComparison]::Ordinal) -ge 0) {
        throw 'C# response-file arguments must not contain NUL, CR, or LF.'
    }

    # Roslyn response files use the same quote/backslash rules as the compiler
    # command line. Always quoting also keeps spaces, non-ASCII text, and an @
    # path component literal instead of relying on token-boundary heuristics.
    $builder = [Text.StringBuilder]::new($Argument.Length + 8)
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            if ($backslashes -gt 0) { [void]$builder.Append('\', $backslashes * 2) }
            [void]$builder.Append('\')
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append('\', $backslashes)
            $backslashes = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) { [void]$builder.Append('\', $backslashes * 2) }
    [void]$builder.Append('"')
    $builder.ToString()
}

function Assert-ManagementUiFencedTempPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Prefix
    )

    if ([string]::IsNullOrWhiteSpace($Prefix) -or $Prefix.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
        throw "Invalid Management UI temp prefix: $Prefix"
    }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char]'\', [char]'/')
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd([char]'\', [char]'/')
    $parent = Split-Path -Parent $fullPath
    $leaf = Split-Path -Leaf $fullPath
    if (-not [string]::Equals($parent, $tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $leaf.StartsWith($Prefix, [StringComparison]::Ordinal)) {
        throw "Management UI temp path escaped its fence: $fullPath"
    }
    $fullPath
}

function New-ManagementUiFencedTempDirectory {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Prefix)

    $candidate = Join-Path ([IO.Path]::GetTempPath()) ($Prefix + [Guid]::NewGuid().ToString('N'))
    $fullPath = Assert-ManagementUiFencedTempPath $candidate $Prefix
    if (Test-Path -LiteralPath $fullPath) { throw "Fresh Management UI temp path already exists: $fullPath" }
    New-Item -ItemType Directory -Path $fullPath -ErrorAction Stop | Out-Null
    $item = Get-Item -LiteralPath $fullPath -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Management UI temp path is a reparse point: $fullPath"
    }
    $fullPath
}

function Remove-ManagementUiFencedTempDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Prefix
    )

    $fullPath = Assert-ManagementUiFencedTempPath $Path $Prefix
    if (-not (Test-Path -LiteralPath $fullPath)) { return }
    $item = Get-Item -LiteralPath $fullPath -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing recursive cleanup of a reparse-point temp path: $fullPath"
    }
    Remove-Item -LiteralPath $fullPath -Recurse -Force -ErrorAction Stop
    if (Test-Path -LiteralPath $fullPath) { throw "Management UI temp cleanup failed: $fullPath" }
}

function Invoke-ManagementUiCSharpResponseCompile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$DotNetPath,
        [Parameter(Mandatory = $true)][string]$CompilerPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string[]]$RequiredInputPaths = @()
    )

    foreach ($required in @($DotNetPath, $CompilerPath) + @($RequiredInputPaths)) {
        if ([string]::IsNullOrWhiteSpace($required) -or
            -not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Management UI compiler input is missing: $required"
        }
    }

    $responseDirectory = New-ManagementUiFencedTempDirectory $script:ManagementUiResponseTempPrefix
    $responsePath = Join-Path $responseDirectory 'runtime.rsp'
    try {
        $responseLines = @($Arguments | ForEach-Object {
            ConvertTo-ManagementUiCSharpResponseToken ([string]$_)
        })
        [IO.File]::WriteAllLines($responsePath, $responseLines, [Text.UTF8Encoding]::new($false))
        $responseItem = Get-Item -LiteralPath $responsePath
        $responseSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $responsePath).Hash
        $responseBytes = [IO.File]::ReadAllBytes($responsePath)
        $responseHasUtf8Bom = $responseBytes.Length -ge 3 -and
            $responseBytes[0] -eq 0xEF -and
            $responseBytes[1] -eq 0xBB -and
            $responseBytes[2] -eq 0xBF
        $compilerOutput = @(& $DotNetPath $CompilerPath "@$responsePath" 2>&1)
        $compilerExitCode = $LASTEXITCODE
        [pscustomobject][ordered]@{
            ExitCode = $compilerExitCode
            Output = $compilerOutput
            ArgumentCount = $Arguments.Count
            ResponseLength = $responseItem.Length
            ResponseSha256 = $responseSha256
            ResponseHasUtf8Bom = $responseHasUtf8Bom
            ResponseLineCount = $responseLines.Count
            ResponseDirectory = $responseDirectory
            ResponsePath = $responsePath
        }
    }
    finally {
        Remove-ManagementUiFencedTempDirectory $responseDirectory $script:ManagementUiResponseTempPrefix
    }
}
