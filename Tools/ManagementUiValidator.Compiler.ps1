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
    if ($Argument.StartsWith('@', [StringComparison]::Ordinal)) {
        throw "C# response-file arguments must not begin with '@'; Roslyn treats them as nested response directives."
    }

    # Roslyn response files use the same quote/backslash rules as the compiler
    # command line. Always quoting keeps spaces, non-ASCII text, and an @ path
    # component literal. A leading @ is rejected above because quotes do not
    # stop Roslyn from interpreting it as a nested response-file directive.
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

function Resolve-ManagementUiCSharpResponseArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string[]]$RequiredInputPaths = @()
    )

    $canonicalByOriginal = [Collections.Generic.Dictionary[string,string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($required in @($RequiredInputPaths)) {
        if ([string]::IsNullOrWhiteSpace($required) -or
            -not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Management UI compiler input is missing: $required"
        }
        $canonical = [IO.Path]::GetFullPath((Get-Item -LiteralPath $required -Force).FullName)
        $canonicalByOriginal[[string]$required] = $canonical
    }

    $pathSwitches = @('-r:', '/r:', '-reference:', '/reference:')
    @($Arguments | ForEach-Object {
        $argument = [string]$_
        if ($canonicalByOriginal.ContainsKey($argument)) {
            $argument = $canonicalByOriginal[$argument]
        }
        else {
            foreach ($prefix in $pathSwitches) {
                if (-not $argument.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
                $pathValue = $argument.Substring($prefix.Length)
                if ($canonicalByOriginal.ContainsKey($pathValue)) {
                    $argument = $argument.Substring(0, $prefix.Length) + $canonicalByOriginal[$pathValue]
                }
                break
            }
        }
        if ($argument.StartsWith('@', [StringComparison]::Ordinal)) {
            throw "C# response-file arguments must not begin with '@'; Roslyn treats them as nested response directives."
        }
        $argument
    })
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

    foreach ($required in @($DotNetPath, $CompilerPath)) {
        if ([string]::IsNullOrWhiteSpace($required) -or
            -not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Management UI compiler input is missing: $required"
        }
    }
    $canonicalDotNetPath = [IO.Path]::GetFullPath((Get-Item -LiteralPath $DotNetPath -Force).FullName)
    $canonicalCompilerPath = [IO.Path]::GetFullPath((Get-Item -LiteralPath $CompilerPath -Force).FullName)
    $resolvedArguments = @(Resolve-ManagementUiCSharpResponseArguments `
        -Arguments $Arguments `
        -RequiredInputPaths $RequiredInputPaths)

    $responseDirectory = New-ManagementUiFencedTempDirectory $script:ManagementUiResponseTempPrefix
    $responsePath = Join-Path $responseDirectory 'runtime.rsp'
    try {
        $responseLines = @($resolvedArguments | ForEach-Object {
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
        $compilerOutput = @(& $canonicalDotNetPath $canonicalCompilerPath "@$responsePath" 2>&1)
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
