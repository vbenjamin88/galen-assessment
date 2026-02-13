# Run the project WITHOUT Docker
# Prerequisites: .NET 8 SDK, Azure Functions Core Tools, Node.js, Azurite, SQL Server

param(
    [string]$SqlPassword = "YourStrong@Passw0rd",
    [switch]$SkipAzurite,
    [switch]$SkipSqlInit
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$FunctionsPath = Join-Path $ProjectRoot "src\Galen.Integration.Functions"

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Cyan
$missing = @()
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += ".NET 8 SDK" }
if (-not (Get-Command func -ErrorAction SilentlyContinue)) { $missing += "Azure Functions Core Tools (func)" }
if (-not (Get-Command azurite -ErrorAction SilentlyContinue) -and -not $SkipAzurite) { $missing += "Azurite (npm install -g azurite)" }

if ($missing.Count -gt 0) {
    Write-Host "Missing: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "See docs\RUN-WITHOUT-DOCKER.md for installation instructions." -ForegroundColor Yellow
    exit 1
}

Write-Host "Prerequisites OK.`n" -ForegroundColor Green

# 1. Ensure local.settings.json exists
$localSettings = Join-Path $FunctionsPath "local.settings.json"
$exampleSettings = Join-Path $ProjectRoot "local.settings.json.example"
if (-not (Test-Path $localSettings)) {
    if (Test-Path $exampleSettings) {
        Copy-Item $exampleSettings $localSettings
        Write-Host "Created local.settings.json from example. Update RecordRepository__ConnectionString with your SQL password.`n" -ForegroundColor Yellow
    } else {
        Write-Host "Create src\Galen.Integration.Functions\local.settings.json - see docs\RUN-WITHOUT-DOCKER.md" -ForegroundColor Red
        exit 1
    }
}

# 2. Build
Write-Host "[1/4] Building project..." -ForegroundColor Green
Push-Location $ProjectRoot
dotnet build -c Release -v q
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
Pop-Location

# 3. Start Azurite in background (optional)
if (-not $SkipAzurite) {
    Write-Host "`n[2/4] Starting Azurite (Blob emulator) in background..." -ForegroundColor Green
    $azuriteJob = Start-Job -ScriptBlock {
        azurite --silent --location $env:TEMP\azurite 2>&1
    }
    Start-Sleep -Seconds 3
    Write-Host "  Azurite started (Blob on port 10000)." -ForegroundColor Gray
} else {
    Write-Host "`n[2/4] Skipping Azurite (ensure it's already running)." -ForegroundColor Gray
}

# 4. Create inbound container (requires Azure CLI or will skip)
Write-Host "`n[3/4] Creating inbound container..." -ForegroundColor Green
$connStr = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
if (Get-Command az -ErrorAction SilentlyContinue) {
    az storage container create --name inbound --connection-string $connStr 2>$null
    Write-Host "  Container 'inbound' created." -ForegroundColor Gray
} else {
    Write-Host "  Azure CLI not found. Create 'inbound' container manually via Azure Storage Explorer (http://127.0.0.1:10000)." -ForegroundColor Yellow
}

# 5. SQL init (optional - user may run manually)
if (-not $SkipSqlInit -and (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "`n  Initializing SQL database..." -ForegroundColor Gray
    $sqlScript = Join-Path $ProjectRoot "scripts\CreateStoredProcedure.sql"
    sqlcmd -S localhost -U sa -P $SqlPassword -i $sqlScript -C 2>$null
    if ($LASTEXITCODE -eq 0) { Write-Host "  Database initialized." -ForegroundColor Gray }
}

Write-Host "`n[4/4] Starting Azure Function..." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Next: Upload sample-data\sample.csv to 'inbound' container to trigger processing." -ForegroundColor White
Write-Host "  Use: az storage blob upload -f sample-data\sample.csv -c inbound -n sample.csv --connection-string `"...`"" -ForegroundColor Gray
Write-Host "  Or Azure Storage Explorer: http://127.0.0.1:10000" -ForegroundColor Gray
Write-Host "========================================`n" -ForegroundColor Cyan

# Run the Function (foreground)
Push-Location $FunctionsPath
func start
