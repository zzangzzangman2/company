param([string]$InstallRoot, [string]$GameDirectory, [string]$ResultPath, [string]$CancelPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'unity-test-root.json') -Raw | ConvertFrom-Json
$fixture = [IO.Path]::GetFullPath($config.root)
if ($fixture -notmatch '[\\/]Artifacts[\\/]UnityPatchRestartTests[\\/][0-9a-f]{32}$') { throw 'Isolated test root required.' }
$identity = Get-Content -LiteralPath (Join-Path $fixture 'identity.json') -Raw | ConvertFrom-Json
$InstallRoot = Join-Path $fixture 'install'
$script:PatchCancellationCheck = { Test-Path -LiteralPath $CancelPath }
$script:PatchProgressSink = { param($event)
    $event.detail = '[LOCAL UNITY PATCH TEST] ' + $event.detail
    [Console]::Out.WriteLine('FC_PROGRESS ' + ($event | ConvertTo-Json -Compress)); [Console]::Out.Flush()
}
Add-Type -TypeDefinition @'
using System; using System.IO; using System.Threading;
public sealed class UnityPatchMeasuredStream : Stream {
    readonly Stream source; public UnityPatchMeasuredStream(Stream s) {source=s;}
    public override int Read(byte[] b,int o,int n) {Thread.Sleep(120); return source.Read(b,o,Math.Min(n,32768));}
    public override bool CanRead {get{return true;}} public override bool CanSeek {get{return false;}} public override bool CanWrite {get{return false;}}
    public override long Length {get{return source.Length;}} public override long Position {get{return source.Position;}set{throw new NotSupportedException();}}
    public override void Flush(){} public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}
    public override void SetLength(long n){throw new NotSupportedException();} public override void Write(byte[] b,int o,int n){throw new NotSupportedException();}
}
'@
$script:realCopy = ${function:Copy-PatchStream}
function Copy-PatchStream($InputStream, $OutputStream, [long]$ExpectedSize, [scriptblock]$OnBytes) {
    & $script:realCopy ([UnityPatchMeasuredStream]::new($InputStream)) $OutputStream $ExpectedSize $OnBytes
}
try {
    $current = Get-PatchCurrent $InstallRoot
    if ($current -and $current.Hash -ceq $identity.manifestHash) {
        Assert-PatchInstalled $current.Directory $current.Manifest
        Write-PatchJsonAtomic $ResultPath @{status='current';directory=$current.Directory;manifestHash=$current.Hash}
        Send-PatchProgress 'ready' 'Verified actual Unity snapshot' 1 1
        exit 0
    }
    $installed = Install-CompanyPatch $InstallRoot $identity.manifest $identity.manifestHash -LocalFeed (Join-Path $fixture 'feed') -SeedDirectory (Join-Path $fixture 'base') -PrepareOnly
    Write-PatchJsonAtomic $ResultPath @{status=$installed.Status;directory=$installed.Directory;manifestHash=$identity.manifestHash}
    Send-PatchProgress 'ready' 'Actual Unity payload prepared; normal restart follows' 1 1
} catch { $script:PatchCancellationCheck=$null; Send-PatchProgress 'error' $_.Exception.Message; exit 1 }
