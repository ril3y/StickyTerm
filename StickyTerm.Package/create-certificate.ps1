# Create Self-Signed Certificate for MSIX Testing
# Run this script as Administrator

param(
    [string]$CertificateName = "StickyTerm",
    [string]$OutputPath = "StickyTerm_Test.pfx",
    [string]$Password
)

$ErrorActionPreference = "Stop"

if (-not $Password) {
    $secInput = Read-Host -Prompt "Enter certificate password" -AsSecureString
    $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secInput))
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Creating Self-Signed Certificate for MSIX" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Certificate subject must match Publisher in Package.appxmanifest
$subject = "CN=$CertificateName"

Write-Host "Certificate Subject: $subject" -ForegroundColor Yellow
Write-Host "Output File: $OutputPath" -ForegroundColor Yellow
Write-Host ""

# Check if running as admin (required for cert store access)
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "WARNING: Not running as Administrator." -ForegroundColor Yellow
    Write-Host "The certificate will be created but may not be automatically trusted." -ForegroundColor Yellow
    Write-Host ""
}

try {
    # Create the self-signed certificate
    Write-Host "Creating certificate..." -ForegroundColor Green

    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName "$CertificateName Code Signing Certificate" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
        -NotAfter (Get-Date).AddYears(5)

    Write-Host "Certificate created successfully!" -ForegroundColor Green
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Cyan
    Write-Host ""

    # Export to PFX
    Write-Host "Exporting to PFX file..." -ForegroundColor Green
    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    $pfxPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) $OutputPath

    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null

    Write-Host "PFX exported to: $pfxPath" -ForegroundColor Green
    Write-Host ""

    # Trust the certificate (add to Trusted Root if admin)
    if ($isAdmin) {
        Write-Host "Adding certificate to Trusted Root..." -ForegroundColor Green

        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $rootStore.Open("ReadWrite")
        $rootStore.Add($cert)
        $rootStore.Close()

        Write-Host "Certificate added to Trusted Root!" -ForegroundColor Green
    } else {
        Write-Host "To trust this certificate for MSIX installation, run as Administrator" -ForegroundColor Yellow
        Write-Host "or manually import the certificate to Trusted Root Certification Authorities." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Certificate Setup Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. In Visual Studio, go to Package.appxmanifest > Packaging" -ForegroundColor White
    Write-Host "2. Click 'Choose Certificate...' and select '$OutputPath'" -ForegroundColor White
    Write-Host "3. Build the package with: dotnet publish -c Release" -ForegroundColor White
    Write-Host ""
    Write-Host "Or use MSBuild directly:" -ForegroundColor Yellow
    Write-Host "  msbuild StickyTerm.Package.wapproj /p:Configuration=Release /p:Platform=x64" -ForegroundColor White
    Write-Host ""

    # Output certificate info for wapproj
    Write-Host "Certificate Thumbprint (for wapproj): $($cert.Thumbprint)" -ForegroundColor Cyan

} catch {
    Write-Host "ERROR: Failed to create certificate" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
