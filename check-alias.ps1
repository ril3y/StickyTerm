$aliasPath = "$env:LocalAppData\Microsoft\WindowsApps\StickyTerm.exe"
Write-Host "LocalAppData: $env:LocalAppData"
Write-Host "Alias path: $aliasPath"
Write-Host "Alias exists: $(Test-Path $aliasPath)"

Write-Host "`nSearching for StickyTerm in WindowsApps..."
Get-ChildItem "$env:LocalAppData\Microsoft\WindowsApps" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "Sticky|ComPort" }

Write-Host "`nAll items in WindowsApps (first 20):"
Get-ChildItem "$env:LocalAppData\Microsoft\WindowsApps" -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { $_.Name }
