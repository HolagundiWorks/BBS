#Requires -Version 5.1
<#
.SYNOPSIS
  Publish BOQ Core (self-contained win-x64) and build Setup.exe with Inno Setup 6.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File BBSDesktop\installer\build-installer.ps1

.EXAMPLE
  powershell -File build-installer.ps1 -AppVersion 1.0.1 -SkipEngineBuild
#>
[CmdletBinding()]
param(
    [string] $AppVersion = "",
    [switch] $SkipEngineBuild,
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"

$InstallerDir = $PSScriptRoot
$DesktopDir = Split-Path $InstallerDir -Parent
$RepoRoot = Split-Path $DesktopDir -Parent
$AppProj = Join-Path $DesktopDir "BBSApp\BBSApp.csproj"
$EngineDll = Join-Path $DesktopDir "build\bbs_engine.dll"
$PublishDir = Join-Path $DesktopDir "artifacts\publish"
$InstallerOutDir = Join-Path $DesktopDir "artifacts\installer"
$IssPath = Join-Path $InstallerDir "BOQCore.iss"

function Get-AppVersionFromCsproj {
    param([string] $Csproj)
    [xml] $xml = Get-Content -LiteralPath $Csproj -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ver = $xml.SelectSingleNode("//Version")
    if ($ver -and $ver.InnerText.Trim()) { return $ver.InnerText.Trim() }
    return "1.0.0"
}

function Find-ISCC {
    $candidates = [System.Collections.Generic.List[string]]::new()
    $fromPath = Get-Command ISCC -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
    if ($fromPath) { [void]$candidates.Add($fromPath) }
    foreach ($p in @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
            "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
        )) {
        if ($p -and (Test-Path -LiteralPath $p)) { [void]$candidates.Add($p) }
    }
    if ($candidates.Count -gt 0) { return $candidates[0] }
    return $null
}

if (-not $AppVersion) {
    $AppVersion = Get-AppVersionFromCsproj -Csproj $AppProj
}

Write-Host "=== BOQ Core installer ===" -ForegroundColor Cyan
Write-Host "Version : $AppVersion"
Write-Host "Publish : $PublishDir"
Write-Host ""

# --- Engine DLL ---
if (-not (Test-Path -LiteralPath $EngineDll)) {
    if ($SkipEngineBuild) {
        throw "bbs_engine.dll not found at '$EngineDll'. Build the engine or omit -SkipEngineBuild."
    }
    Write-Host "Building bbs_engine.dll..." -ForegroundColor Yellow
    $buildDir = Join-Path $DesktopDir "build"
    if (-not (Test-Path -LiteralPath (Join-Path $buildDir "CMakeCache.txt"))) {
        & cmake -S $DesktopDir -B $buildDir -G Ninja
        if ($LASTEXITCODE -ne 0) { throw "cmake configure failed ($LASTEXITCODE)" }
    }
    & cmake --build $buildDir --config Release --target bbs_engine
    if ($LASTEXITCODE -ne 0) { throw "cmake build bbs_engine failed ($LASTEXITCODE)" }
    if (-not (Test-Path -LiteralPath $EngineDll)) {
        throw "bbs_engine.dll still missing after build: $EngineDll"
    }
} else {
    Write-Host "Engine  : $EngineDll (ok)"
}

# --- Publish ---
if (-not $SkipPublish) {
    Write-Host "Publishing self-contained win-x64..." -ForegroundColor Yellow
    if (Test-Path -LiteralPath $PublishDir) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

    & dotnet publish $AppProj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:Platform=x64 `
        -p:PublishSingleFile=false `
        -o $PublishDir

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}

$exe = Join-Path $PublishDir "BOQCore.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Publish output missing BOQCore.exe at $PublishDir"
}

# Ensure native engine is next to the exe (Publish target may miss it)
$pubEngine = Join-Path $PublishDir "bbs_engine.dll"
if (-not (Test-Path -LiteralPath $pubEngine)) {
    Write-Host "Copying bbs_engine.dll into publish folder..." -ForegroundColor Yellow
    Copy-Item -LiteralPath $EngineDll -Destination $pubEngine -Force
}

Write-Host "Publish : BOQCore.exe + bbs_engine.dll ready"

# --- Inno Setup ---
$iscc = Find-ISCC
if (-not $iscc) {
    throw @"
Inno Setup 6 not found (ISCC.exe).
Install from https://jrsoftware.org/isdl.php then re-run this script.
Or add ISCC to PATH.
"@
}

Write-Host "Compiling installer with $iscc ..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $InstallerOutDir -Force | Out-Null

& $iscc "/DAppVersion=$AppVersion" $IssPath
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Join-Path $InstallerOutDir "BOQCore-Setup-$AppVersion.exe"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Expected installer not found: $setup"
}

Write-Host ""
Write-Host "Installer ready:" -ForegroundColor Green
Write-Host "  $setup"
Write-Host ""
