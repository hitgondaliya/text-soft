# Build the Windows installer end-to-end. Run on Windows in this folder:
#   powershell -ExecutionPolicy Bypass -File build-installer.ps1
#
# 1) Publishes the WPF app self-contained, single-file + compressed (~74 MB,
#    no .NET needed on the target machine). See docs/APP-SIZE.md.
# 2) Compiles setup.iss with Inno Setup's ISCC into installer\output\.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app  = Join-Path $root "src\FontSoftware.App\FontSoftware.App.csproj"

Write-Host "== Publishing $app (single-file, compressed) ==" -ForegroundColor Cyan
# Clean the publish folder first. Incremental publishes can recreate the config\
# folder tree while dropping the content files inside it, which then breaks the
# font-install step below. A clean publish guarantees a complete output.
$publishDir = Join-Path $root "src\FontSoftware.App\bin\Release\net8.0-windows\win-x64\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $app -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Fail fast if the bundled fonts didn't make it into the publish output.
foreach ($ttf in @("config\fonts\S0700810.TTF","config\fonts\S1120810.TTF")) {
    if (-not (Test-Path (Join-Path $publishDir $ttf))) {
        throw "Expected bundled font missing from publish output: $ttf"
    }
}

# Locate ISCC (Inno Setup Compiler). Covers the machine-wide installs and the
# per-user install winget uses (%LOCALAPPDATA%\Programs), then falls back to PATH.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
}

if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}

Write-Host "== Compiling installer ==" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot "setup.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Write-Host "== Done. Installer is in installer\output\ ==" -ForegroundColor Green
