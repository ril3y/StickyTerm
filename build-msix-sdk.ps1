# StickyTerm MSIX Package Build Script (Using Windows SDK)
# Builds MSIX package using makeappx.exe and signtool.exe from Windows SDK

param(
    [switch]$SkipPublish,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building StickyTerm MSIX Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Find Windows SDK tools
$sdkBin = "C:\Program Files (x86)\Windows Kits\10\bin"
$makeappx = $null
$signtool = $null

if (Test-Path $sdkBin) {
    # Find the latest SDK version
    $versions = Get-ChildItem $sdkBin -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | Sort-Object Name -Descending
    foreach ($ver in $versions) {
        $x64Path = Join-Path $ver.FullName "x64"
        if (Test-Path $x64Path) {
            $makeappxPath = Join-Path $x64Path "makeappx.exe"
            $signtoolPath = Join-Path $x64Path "signtool.exe"
            if ((Test-Path $makeappxPath) -and (Test-Path $signtoolPath)) {
                $makeappx = $makeappxPath
                $signtool = $signtoolPath
                Write-Host "Found Windows SDK at: $x64Path" -ForegroundColor Green
                break
            }
        }
    }
}

if (-not $makeappx) {
    Write-Host "ERROR: Windows SDK not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Windows SDK from:" -ForegroundColor Yellow
    Write-Host "https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host ""

# Configuration
$appName = "StickyTerm"
$version = "1.0.16.0"
$publisher = "CN=StickyTerm"
$publishDir = Join-Path $scriptDir "msix-publish"
$msixDir = Join-Path $scriptDir "msix-output"
$pfxFile = Join-Path $scriptDir "StickyTerm.Package\StickyTerm_Test.pfx"
$pfxPassword = $env:MSIX_CERT_PASSWORD
if (-not $pfxPassword) {
    $secInput = Read-Host -Prompt "Enter certificate password" -AsSecureString
    $pfxPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secInput))
}

# Step 1: Publish the application
if (-not $SkipPublish) {
    Write-Host "[1/5] Publishing application..." -ForegroundColor Yellow
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    dotnet publish StickyTerm -c Release -r win-x64 --self-contained -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed!" }
} else {
    Write-Host "[1/5] Skipping publish (using existing files)..." -ForegroundColor Yellow
}

# Step 2: Create package structure
Write-Host "[2/5] Creating package structure..." -ForegroundColor Yellow
if (Test-Path $msixDir) { Remove-Item -Recurse -Force $msixDir }
New-Item -ItemType Directory -Path $msixDir | Out-Null

# Copy published files
Copy-Item -Path "$publishDir\*" -Destination $msixDir -Recurse

# Copy assets
$assetsDir = Join-Path $msixDir "Assets"
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
Copy-Item -Path "StickyTerm.Package\Images\*" -Destination $assetsDir -Force

# Step 3: Create AppxManifest.xml in the package
Write-Host "[3/5] Creating AppxManifest.xml..." -ForegroundColor Yellow

$manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  IgnorableNamespaces="uap uap3 rescap desktop">

  <Identity
    Name="StickyTerm"
    Publisher="$publisher"
    Version="$version"
    ProcessorArchitecture="x64" />

  <Properties>
    <DisplayName>$appName</DisplayName>
    <PublisherDisplayName>StickyTerm</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
    <Description>Serial terminal with sticky COM port assignments. Track USB serial devices and ensure they always get the same COM port number.</Description>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="StickyTerm.exe"
      EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$appName"
        Description="Serial terminal with sticky COM port assignments"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square310x310Logo="Assets\Square310x310Logo.png" Square71x71Logo="Assets\SmallTile.png">
          <uap:ShowNameOnTiles>
            <uap:ShowOn Tile="square150x150Logo"/>
            <uap:ShowOn Tile="wide310x150Logo"/>
            <uap:ShowOn Tile="square310x310Logo"/>
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
      </uap:VisualElements>
      <Extensions>
        <!-- AppExecutionAlias enables self-elevation via Process.Start with runas verb -->
        <uap3:Extension Category="windows.appExecutionAlias" Executable="StickyTerm.exe" EntryPoint="Windows.FullTrustApplication">
          <uap3:AppExecutionAlias>
            <desktop:ExecutionAlias Alias="StickyTerm.exe" />
          </uap3:AppExecutionAlias>
        </uap3:Extension>
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
    <!-- allowElevation enables the app to request UAC elevation dynamically -->
    <rescap:Capability Name="allowElevation" />
  </Capabilities>

</Package>
"@

$manifestPath = Join-Path $msixDir "AppxManifest.xml"
$manifestContent | Out-File -FilePath $manifestPath -Encoding utf8

# Step 4: Create the MSIX package
Write-Host "[4/5] Creating MSIX package..." -ForegroundColor Yellow

$outputDir = Join-Path $scriptDir "Output"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$msixPath = Join-Path $outputDir "StickyTerm_${version}_x64.msix"

& $makeappx pack /d $msixDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed!" }

# Step 5: Sign the package
Write-Host "[5/5] Signing MSIX package..." -ForegroundColor Yellow

if (Test-Path $pfxFile) {
    & $signtool sign /fd SHA256 /a /f $pfxFile /p $pfxPassword $msixPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Signing failed. Package created but unsigned." -ForegroundColor Yellow
    } else {
        Write-Host "Package signed successfully!" -ForegroundColor Green
    }
} else {
    Write-Host "WARNING: Certificate not found at $pfxFile" -ForegroundColor Yellow
    Write-Host "Package created but not signed." -ForegroundColor Yellow
}

# Cleanup
Remove-Item -Recurse -Force $msixDir

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "MSIX package created at:" -ForegroundColor Green
Write-Host "  $msixPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "To install:" -ForegroundColor Yellow
Write-Host "1. First, trust the certificate by importing it to Trusted Root:" -ForegroundColor White
Write-Host "   - Double-click StickyTerm.Package\StickyTerm_Test.pfx" -ForegroundColor Gray
Write-Host "   - Choose 'Local Machine' and install to 'Trusted Root Certification Authorities'" -ForegroundColor Gray
Write-Host "2. Then double-click the .msix file to install" -ForegroundColor White
Write-Host ""

if ($OpenOutput) {
    Start-Process "explorer.exe" -ArgumentList $outputDir
}
