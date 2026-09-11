# Build the OBFUSCATED, signed-ready release installer.
#   powershell -ExecutionPolicy Bypass -File build-release.ps1
#
# Difference vs build-installer.ps1:
#   - publishes as a FOLDER (loose DLLs) instead of single-file, so the managed
#     assemblies can be obfuscated before Inno packages them;
#   - runs Obfuscar over FontSoftware.Licensing.dll (see obfuscar.xml) to harden the
#     license checks;
#   - then compiles the Inno installer exactly as before.
#
# Prereqs: .NET SDK, Obfuscar global tool (dotnet tool install --global Obfuscar.GlobalTool),
#          Inno Setup 6 (ISCC.exe).
#
# BEFORE a real release, run scripts\set-prod-config.ps1 to embed the PRODUCTION
# Ed25519 public key and the HTTPS API URL.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app  = Join-Path $root "src\FontSoftware.App\FontSoftware.App.csproj"
$publishDir = Join-Path $root "src\FontSoftware.App\bin\Release\net8.0-windows\win-x64\publish"

Write-Host "== Publishing (self-contained folder) ==" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $app -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

foreach ($ttf in @("config\fonts\S0700810.TTF","config\fonts\S1120810.TTF")) {
    if (-not (Test-Path (Join-Path $publishDir $ttf))) {
        throw "Expected bundled font missing from publish output: $ttf"
    }
}

Write-Host "== Obfuscating FontSoftware.Licensing.dll ==" -ForegroundColor Cyan
$obfuscar = (Get-Command obfuscar.console -ErrorAction SilentlyContinue).Source
if (-not $obfuscar) {
    $obfuscar = Join-Path $env:USERPROFILE ".dotnet\tools\obfuscar.console.exe"
}
if (-not (Test-Path $obfuscar)) {
    throw "Obfuscar not found. Install it: dotnet tool install --global Obfuscar.GlobalTool"
}

Copy-Item (Join-Path $PSScriptRoot "obfuscar.xml") (Join-Path $publishDir "obfuscar.xml") -Force
Push-Location $publishDir
try {
    & $obfuscar "obfuscar.xml"
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar failed" }

    $obf = Join-Path $publishDir "obfuscated\FontSoftware.Licensing.dll"
    if (-not (Test-Path $obf)) { throw "Obfuscated Licensing.dll not produced" }
    Copy-Item $obf (Join-Path $publishDir "FontSoftware.Licensing.dll") -Force

    Remove-Item (Join-Path $publishDir "obfuscated") -Recurse -Force
    Remove-Item (Join-Path $publishDir "obfuscar.xml") -Force
}
finally {
    Pop-Location
}

# Locate ISCC (same discovery as build-installer.ps1).
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (-not $iscc) { throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php" }

Write-Host "== Compiling installer ==" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot "setup.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Write-Host "== Done. Obfuscated installer is in installer\output\ ==" -ForegroundColor Green
Write-Host "   Test-launch the installed app before shipping (obfuscation is not UI-tested by this script)." -ForegroundColor Yellow
