# Generate MSIX Package Assets from app.ico
# Requires: .NET (System.Drawing)

param(
    [string]$SourceIcon = "..\StickyTerm\Resources\app.ico"
)

Add-Type -AssemblyName System.Drawing

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$imagesDir = Join-Path $scriptDir "Images"

if (-not (Test-Path $imagesDir)) {
    New-Item -ItemType Directory -Path $imagesDir | Out-Null
}

$iconPath = Join-Path $scriptDir $SourceIcon
if (-not (Test-Path $iconPath)) {
    Write-Host "Warning: Source icon not found at $iconPath" -ForegroundColor Yellow
    Write-Host "Creating placeholder images..." -ForegroundColor Yellow
    $useIcon = $false
} else {
    Write-Host "Found source icon: $iconPath" -ForegroundColor Green
    $useIcon = $true
}

# Required MSIX asset sizes
$assets = @{
    "StoreLogo.png" = @{ Width = 50; Height = 50 }
    "Square44x44Logo.png" = @{ Width = 44; Height = 44 }
    "Square150x150Logo.png" = @{ Width = 150; Height = 150 }
    "Wide310x150Logo.png" = @{ Width = 310; Height = 150 }
    "Square310x310Logo.png" = @{ Width = 310; Height = 310 }
    "SmallTile.png" = @{ Width = 71; Height = 71 }
}

# App brand color (dark blue/teal)
$brandColor = [System.Drawing.Color]::FromArgb(255, 0, 120, 140)
$textColor = [System.Drawing.Color]::White

function Create-PlaceholderImage {
    param(
        [string]$OutputPath,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$BackColor,
        [System.Drawing.Color]$ForeColor,
        [string]$Text = "ST"
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Fill background
    $brush = New-Object System.Drawing.SolidBrush($BackColor)
    $graphics.FillRectangle($brush, 0, 0, $Width, $Height)

    # Draw text
    $fontSize = [Math]::Min($Width, $Height) * 0.4
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush($ForeColor)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

    $rect = New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)
    $graphics.DrawString($Text, $font, $textBrush, $rect, $format)

    # Save
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # Cleanup
    $font.Dispose()
    $brush.Dispose()
    $textBrush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Create-ImageFromIcon {
    param(
        [string]$IconPath,
        [string]$OutputPath,
        [int]$Width,
        [int]$Height
    )

    try {
        $icon = New-Object System.Drawing.Icon($IconPath, 256, 256)
        $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        # Transparent background
        $graphics.Clear([System.Drawing.Color]::Transparent)

        # Calculate centered position for square output
        $iconBitmap = $icon.ToBitmap()

        if ($Width -eq $Height) {
            # Square: center the icon
            $graphics.DrawImage($iconBitmap, 0, 0, $Width, $Height)
        } else {
            # Wide tile: center horizontally with padding
            $iconSize = [Math]::Min($Width, $Height) - 20
            $x = ($Width - $iconSize) / 2
            $y = ($Height - $iconSize) / 2
            $graphics.DrawImage($iconBitmap, $x, $y, $iconSize, $iconSize)
        }

        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

        $iconBitmap.Dispose()
        $icon.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()

        return $true
    } catch {
        Write-Host "Error creating image from icon: $_" -ForegroundColor Red
        return $false
    }
}

Write-Host ""
Write-Host "Generating MSIX package assets..." -ForegroundColor Cyan
Write-Host ""

foreach ($asset in $assets.GetEnumerator()) {
    $outputPath = Join-Path $imagesDir $asset.Key
    $width = $asset.Value.Width
    $height = $asset.Value.Height

    $success = $false
    if ($useIcon) {
        $success = Create-ImageFromIcon -IconPath $iconPath -OutputPath $outputPath -Width $width -Height $height
    }

    if (-not $success) {
        Create-PlaceholderImage -OutputPath $outputPath -Width $width -Height $height -BackColor $brandColor -ForeColor $textColor
    }

    Write-Host "  Created: $($asset.Key) (${width}x${height})" -ForegroundColor Green
}

Write-Host ""
Write-Host "Asset generation complete!" -ForegroundColor Green
Write-Host "Assets created in: $imagesDir" -ForegroundColor Cyan
