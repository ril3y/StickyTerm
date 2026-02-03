# Import StickyTerm Test Certificate to Trusted Root
# Must be run as Administrator

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Installing StickyTerm Test Certificate" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host "Then run this script again." -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pfxPath = Join-Path $scriptDir "StickyTerm.Package\StickyTerm_Test.pfx"
$password = Read-Host -Prompt "Enter certificate password" -AsSecureString
$password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

if (-not (Test-Path $pfxPath)) {
    Write-Host "ERROR: Certificate not found at: $pfxPath" -ForegroundColor Red
    exit 1
}

Write-Host "Certificate file: $pfxPath" -ForegroundColor Green
Write-Host ""

try {
    # Import the certificate to Trusted Root
    Write-Host "Importing certificate to Trusted Root Certification Authorities..." -ForegroundColor Yellow

    $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
    $cert = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\LocalMachine\Root -Password $securePassword

    Write-Host ""
    Write-Host "Certificate imported successfully!" -ForegroundColor Green
    Write-Host "  Subject: $($cert.Subject)" -ForegroundColor Cyan
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Cyan
    Write-Host "  Expires: $($cert.NotAfter)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "You can now install the MSIX package!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Double-click: Output\StickyTerm_1.0.0.0_x64.msix" -ForegroundColor Yellow
    Write-Host ""

} catch {
    Write-Host "ERROR: Failed to import certificate" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

pause
