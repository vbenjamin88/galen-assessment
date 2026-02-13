# Manual run script - Execute these steps on YOUR VPS in order
# Run each section in a SEPARATE PowerShell window, or run sections 1-4 first, then 5-6

$ErrorActionPreference = "Stop"
$ProjectRoot = "c:\Users\Administrator\.cursor\galen-assessment"

Write-Host @"

========================================
  Galen Integration - Manual Run Steps
========================================

Run these commands on your VPS.
Ensure: Azurite running, SQL initialized, local.settings.json configured.

"@ -ForegroundColor Cyan

# Step 1: Restore
Write-Host "[1] Restore packages..." -ForegroundColor Green
Set-Location $ProjectRoot
dotnet restore
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed. Check internet/NuGet." -ForegroundColor Red; exit 1 }

# Step 2: Build
Write-Host "`n[2] Build..." -ForegroundColor Green
dotnet build src\Galen.Integration.Functions\Galen.Integration.Functions.csproj -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }

# Step 3: Run Function
Write-Host "`n[3] Starting Azure Function..." -ForegroundColor Green
Write-Host "    Leave this window open. Upload sample.csv to trigger." -ForegroundColor Yellow
Set-Location "$ProjectRoot\src\Galen.Integration.Functions"
func start
