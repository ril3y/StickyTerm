# StickyTerm MSIX Package Build Script
# Requires: .NET 8 SDK, Windows 10 SDK, Visual Studio Build Tools

param(
    [switch]$Debug,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building StickyTerm MSIX Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$configuration = if ($Debug) { "Debug" } else { "Release" }
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Check for MSBuild
$msbuildPaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
)

$msbuildPath = $null
foreach ($path in $msbuildPaths) {
    if (Test-Path $path) {
        $msbuildPath = $path
        break
    }
}

if (-not $msbuildPath) {
    Write-Host "MSBuild not found. Trying dotnet msbuild..." -ForegroundColor Yellow
    $msbuildPath = "dotnet"
    $useDotnet = $true
} else {
    Write-Host "Found MSBuild at: $msbuildPath" -ForegroundColor Green
    $useDotnet = $false
}

Write-Host ""

# Clean previous build
Write-Host "[1/3] Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "StickyTerm.Package\bin") { Remove-Item -Recurse -Force "StickyTerm.Package\bin" }
if (Test-Path "StickyTerm.Package\obj") { Remove-Item -Recurse -Force "StickyTerm.Package\obj" }

# Restore NuGet packages
Write-Host "[2/3] Restoring packages..." -ForegroundColor Yellow
dotnet restore StickyTerm
if ($LASTEXITCODE -ne 0) { throw "Package restore failed!" }

# Build the MSIX package
Write-Host "[3/3] Building MSIX package..." -ForegroundColor Yellow

if ($useDotnet) {
    # Use dotnet msbuild
    dotnet msbuild "StickyTerm.Package\StickyTerm.Package.wapproj" `
        /p:Configuration=$configuration `
        /p:Platform=x64 `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:AppxBundle=Never `
        /p:GenerateAppxPackageOnBuild=true `
        /restore
} else {
    # Use MSBuild directly
    & $msbuildPath "StickyTerm.Package\StickyTerm.Package.wapproj" `
        /p:Configuration=$configuration `
        /p:Platform=x64 `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:AppxBundle=Never `
        /p:GenerateAppxPackageOnBuild=true `
        /restore
}

if ($LASTEXITCODE -ne 0) { throw "MSIX build failed!" }

# Find the output
$packageDir = "StickyTerm.Package\bin\$configuration\AppPackages"
if (Test-Path $packageDir) {
    $packages = Get-ChildItem -Path $packageDir -Filter "*.msix" -Recurse

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    if ($packages) {
        Write-Host "MSIX packages created:" -ForegroundColor Green
        foreach ($pkg in $packages) {
            Write-Host "  $($pkg.FullName)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "Package directory: $packageDir" -ForegroundColor Cyan
    }

    if ($OpenOutput) {
        Start-Process "explorer.exe" -ArgumentList $packageDir
    }
} else {
    Write-Host ""
    Write-Host "Build may have completed. Check StickyTerm.Package\bin for output." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "To install the MSIX package:" -ForegroundColor Yellow
Write-Host "1. First, trust the certificate (run as Admin):" -ForegroundColor White
Write-Host "   Import-Certificate -FilePath StickyTerm.Package\StickyTerm_Test.pfx -CertStoreLocation Cert:\LocalMachine\Root" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Then install the .msix file by double-clicking it" -ForegroundColor White
Write-Host ""
