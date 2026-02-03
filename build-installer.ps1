# StickyTerm Installer Build Script
# Requires: .NET 8 SDK, Inno Setup 6

param(
    [switch]$SkipPublish,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "Building StickyTerm Installer"
Write-Host "========================================"
Write-Host ""

# Find Inno Setup
$isccPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if (-not $isccPath) {
    Write-Host "ERROR: Inno Setup 6 not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inno Setup 6 from:"
    Write-Host "https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "Found Inno Setup at: $isccPath" -ForegroundColor Green
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

if (-not $SkipPublish) {
    # Clean previous build
    Write-Host "[1/4] Cleaning previous build..." -ForegroundColor Yellow
    if (Test-Path "publish") { Remove-Item -Recurse -Force "publish" }
    if (Test-Path "Output") { Remove-Item -Recurse -Force "Output" }

    # Restore packages
    Write-Host "[2/4] Restoring packages..." -ForegroundColor Yellow
    dotnet restore StickyTerm
    if ($LASTEXITCODE -ne 0) { throw "Package restore failed!" }

    # Publish the application
    Write-Host "[3/4] Publishing application..." -ForegroundColor Yellow
    dotnet publish StickyTerm -c Release -r win-x64 --self-contained true -o publish
    if ($LASTEXITCODE -ne 0) { throw "Publish failed!" }
}

# Create output directory
if (-not (Test-Path "Output")) { New-Item -ItemType Directory -Path "Output" | Out-Null }

# Build the installer
Write-Host "[4/4] Building installer..." -ForegroundColor Yellow
& $isccPath "Installer\StickyTerm.iss"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed!" }

Write-Host ""
Write-Host "========================================"
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================"
Write-Host ""
Write-Host "Installer created at: Output\StickyTerm_Setup_1.0.0.exe"
Write-Host ""

if ($OpenOutput) {
    Start-Process "explorer.exe" -ArgumentList "Output"
}
