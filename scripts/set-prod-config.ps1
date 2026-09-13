<#
.SYNOPSIS
  Stamp the PRODUCTION license public key and API URL into the client source.

.DESCRIPTION
  Under the online model the client embeds the server's Ed25519 PUBLIC key and
  points at the license API. The repo ships with a DEV keypair + localhost URL so
  the app is runnable out of the box. Before a real release, run this to swap in the
  production values, then build with installer\build-release.ps1.

  Get the public key from the server:  cd server; npm run genkeys   (use the
  "PublicKeyBase64" line — the raw 32-byte key, base64).

.EXAMPLE
  powershell -File scripts\set-prod-config.ps1 `
    -PublicKey "AbC...=" -ApiUrl "https://api.yourdomain.com"
#>
param(
    [Parameter(Mandatory = $true)][string]$PublicKey,
    [Parameter(Mandatory = $true)][string]$ApiUrl
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($ApiUrl -notmatch '^https://') {
    throw "API URL must be HTTPS for production (got '$ApiUrl'). Tokens/keys must not travel over plain HTTP."
}
try { [Convert]::FromBase64String($PublicKey) | Out-Null } catch { throw "PublicKey is not valid base64." }
if ([Convert]::FromBase64String($PublicKey).Length -ne 32) {
    throw "PublicKey must decode to 32 bytes (a raw Ed25519 public key)."
}

$verifier = Join-Path $root "src\FontSoftware.Licensing\Ed25519Verifier.cs"
$endpoints = Join-Path $root "src\FontSoftware.Licensing\LicenseEndpoints.cs"
$iss = Join-Path $root "installer\setup.iss"

# 1) Embedded public key.
$v = Get-Content $verifier -Raw
$v = [regex]::Replace($v,
    'public const string DefaultPublicKeyBase64 = "[^"]*";',
    "public const string DefaultPublicKeyBase64 = `"$PublicKey`";")
Set-Content $verifier $v -Encoding utf8
Write-Host "Set public key in Ed25519Verifier.cs" -ForegroundColor Green

# 2) API URL — a compile-time constant in the app, and the installer's precheck target.
$ApiUrl = $ApiUrl.TrimEnd('/')
$s = Get-Content $endpoints -Raw
$s = [regex]::Replace($s,
    'public const string BaseUrl = "[^"]*";',
    "public const string BaseUrl = `"$ApiUrl`";")
Set-Content $endpoints $s -Encoding utf8
Write-Host "Set API URL in LicenseEndpoints.cs" -ForegroundColor Green

$i = Get-Content $iss -Raw
$i = [regex]::Replace($i,
    '#define ApiUrl\s+"[^"]*"',
    "#define ApiUrl         `"$ApiUrl`"")
Set-Content $iss $i -Encoding utf8
Write-Host "Set API URL in installer\setup.iss" -ForegroundColor Green

Write-Host "Done. Now: installer\build-release.ps1" -ForegroundColor Cyan
