param([string]$InstallRoot, [string]$GameDirectory, [string]$ResultPath, [string]$CancelPath, [switch]$OfflineOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'FamilyCompany.Update.ps1')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if (!$ResultPath -or !$InstallRoot) { throw 'Inert in-game UI test requires a result path and isolated install root.' }
$fixture = [IO.Path]::GetDirectoryName($InstallRoot)
$identity = Get-Content -LiteralPath (Join-Path $fixture 'test-identity.json') -Raw | ConvertFrom-Json
if ($identity.installRoot -cne $InstallRoot) { throw 'Test install fence failed.' }
$script:PatchCancellationCheck = { Test-Path -LiteralPath $CancelPath }
$script:PatchProgressSink = { param($event)
    $event.detail = '[LOCAL STREAM TEST] ' + $event.detail
    [Console]::Out.WriteLine('FC_PROGRESS ' + ($event | ConvertTo-Json -Compress)); [Console]::Out.Flush()
}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Threading;
public sealed class PacedPatchTestStream : Stream {
    readonly Stream source;
    public PacedPatchTestStream(Stream value) { source=value; }
    public override int Read(byte[] b,int o,int n) { Thread.Sleep(220); return source.Read(b,o,Math.Min(n,32768)); }
    public override bool CanRead {get{return true;}} public override bool CanWrite {get{return false;}} public override bool CanSeek {get{return false;}}
    public override long Length {get{return source.Length;}} public override long Position {get{return source.Position;}set{throw new NotSupportedException();}}
    public override void Flush() {} public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}
    public override void SetLength(long n){throw new NotSupportedException();} public override void Write(byte[] b,int o,int n){throw new NotSupportedException();}
}
'@
$script:actualCopyStream = ${function:Copy-PatchStream}
function Copy-PatchStream($InputStream, $OutputStream, [long]$ExpectedSize, [scriptblock]$OnBytes) {
    & $script:actualCopyStream ([PacedPatchTestStream]::new($InputStream)) $OutputStream $ExpectedSize $OnBytes
}
try {
    $result = Install-CompanyPatch $InstallRoot $identity.manifest $identity.manifestSha256 -LocalFeed (Join-Path $fixture 'feed') -PrepareOnly
    Write-PatchJsonAtomic (Join-Path $fixture 'installed-result.json') $result
    Write-PatchJsonAtomic $ResultPath @{status='prepared'; directory=$result.Directory; manifestHash=$identity.manifestSha256}
    Send-PatchProgress 'complete' 'Local stream fixture complete; no game is run.' 1 1
    exit 0
} catch {
    $script:PatchCancellationCheck = $null
    Send-PatchProgress 'error' $_.Exception.Message
    exit 1
}
