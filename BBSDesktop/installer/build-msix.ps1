#Requires -Version 5.1
<#
.SYNOPSIS
  Build AQC-Core as a signed MSIX package for sideload or Microsoft Store upload.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File BBSDesktop\installer\build-msix.ps1

.EXAMPLE
  powershell -File build-msix.ps1 -AppVersion 1.0.1 -SkipEngineBuild
#>
[CmdletBinding()]
param(
    [string] $AppVersion = "",
    [switch] $SkipEngineBuild,
    [switch] $Install
)

$ErrorActionPreference = "Stop"

$InstallerDir = $PSScriptRoot
$DesktopDir = Split-Path $InstallerDir -Parent
$AppDir = Join-Path $DesktopDir "BBSApp"
$AppProj = Join-Path $AppDir "BBSApp.csproj"
$EngineDll = Join-Path $DesktopDir "build\bbs_engine.dll"
$MsixOutDir = Join-Path $DesktopDir "artifacts\msix"
$PfxPath = Join-Path $AppDir "AQCCore_TemporaryKey.pfx"
$PfxPassword = "aqc-core-dev"
$Publisher = "CN=Human Centric Works"

function Get-AppVersionFromCsproj {
    param([string] $Csproj)
    [xml] $xml = Get-Content -LiteralPath $Csproj -Raw
    $ver = $xml.SelectSingleNode("//Version")
    if ($ver -and $ver.InnerText.Trim()) { return $ver.InnerText.Trim() }
    return "1.0.0"
}

function Ensure-DevCertificate {
    $existing = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Publisher -and $_.FriendlyName -eq "AQC-Core Dev (MSIX)" } |
        Select-Object -First 1

    if ($existing) {
        Write-Host "Signing : thumbprint $($existing.Thumbprint) (store)"
        return $existing.Thumbprint
    }

    Write-Host "Creating self-signed MSIX certificate for local sideload..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -KeyUsage DigitalSignature `
        -FriendlyName "AQC-Core Dev (MSIX)" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyExportPolicy Exportable `
        -TextExtension @(
            "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
            "2.5.29.19={text}"
        )

    if (-not (Test-Path -LiteralPath $PfxPath)) {
        $secure = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $secure | Out-Null
        Write-Host "Exported : $PfxPath (password: $PfxPassword)"
    }

    Write-Host "Created  : thumbprint $($cert.Thumbprint)"
    Write-Host "For sideload, trust this cert (Trusted People) or use Partner Center for Store." -ForegroundColor DarkYellow
    return $cert.Thumbprint
}

function Sync-ManifestVersion {
    param([string] $Version)
    $manifestPath = Join-Path $AppDir "Package.appxmanifest"
    [xml] $xml = Get-Content -LiteralPath $manifestPath -Raw
    $parts = $Version.Split('.')
    while ($parts.Count -lt 4) { $parts += "0" }
    $identityVersion = ($parts[0..3] -join '.')
    $xml.Package.Identity.Version = $identityVersion
    $xml.Save($manifestPath)
    Write-Host "Manifest : Identity.Version = $identityVersion"
}

if (-not $AppVersion) {
    $AppVersion = Get-AppVersionFromCsproj -Csproj $AppProj
}

Write-Host "=== AQC-Core MSIX ===" -ForegroundColor Cyan
Write-Host "Version : $AppVersion"
Write-Host "Output  : $MsixOutDir"
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

$thumbprint = Ensure-DevCertificate
Sync-ManifestVersion -Version $AppVersion

if (Test-Path -LiteralPath $MsixOutDir) {
    Remove-Item -LiteralPath $MsixOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $MsixOutDir -Force | Out-Null

Write-Host "Building MSIX (Release | x64)..." -ForegroundColor Yellow
& dotnet build $AppProj `
    -c Release `
    -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir="$MsixOutDir\" `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint=$thumbprint `
    -p:AppxBundle=Never

if ($LASTEXITCODE -ne 0) { throw "dotnet build (MSIX) failed ($LASTEXITCODE)" }

$msix = Get-ChildItem -LiteralPath $MsixOutDir -Filter "*.msix" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    # Some toolchains emit .msixbundle or place under AppPackages
    $msix = Get-ChildItem -LiteralPath $MsixOutDir -Include "*.msix","*.msixbundle" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $msix) {
    throw "No .msix produced under $MsixOutDir"
}

Write-Host ""
Write-Host "MSIX ready:" -ForegroundColor Green
Write-Host "  $($msix.FullName)"
Write-Host ""
Write-Host "Store upload: Partner Center → Packages → upload this .msix (Store re-signs)."
Write-Host "Sideload   : Add-AppxPackage -Path `"$($msix.FullName)`"  (trust the cert first)"
Write-Host ""

if ($Install) {
    Write-Host "Installing package..." -ForegroundColor Yellow
    Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion
    Write-Host "Installed." -ForegroundColor Green
}
