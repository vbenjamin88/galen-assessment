# Run the entire project with Docker
# Prerequisites: Docker Desktop installed and running

param(
    [switch]$Build   # Force rebuild of Functions image
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

# Check Docker
try {
    docker info 2>$null | Out-Null
} catch {
    Write-Host "Docker is not running or not installed. Please start Docker Desktop." -ForegroundColor Red
    Write-Host "Download: https://www.docker.com/products/docker-desktop/" -ForegroundColor Yellow
    exit 1
}

Write-Host "Starting Galen Integration with Docker..." -ForegroundColor Cyan
Push-Location $ProjectRoot

try {
    # 1. Start infrastructure + Functions
    Write-Host "`n[1/4] Starting Azurite, SQL Server, and Azure Function..." -ForegroundColor Green
    if ($Build) {
        docker compose build functions
    }
    docker compose up -d azurite sqlserver functions
    if ($LASTEXITCODE -ne 0) { throw "Failed to start containers" }

    # 2. Wait for services
    Write-Host "`n[2/4] Waiting for services (45s for SQL + Function startup)..." -ForegroundColor Green
    Start-Sleep -Seconds 45

    # 3. Initialize SQL database
    Write-Host "`n[3/4] Initializing SQL database..." -ForegroundColor Green
    $sqlScript = Join-Path $ProjectRoot "scripts\CreateStoredProcedure.sql"
    $sqlContent = Get-Content $sqlScript -Raw
    $sqlContent | docker exec -i galen-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C 2>$null
    if ($LASTEXITCODE -ne 0) {
        $sqlContent | docker exec -i galen-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C 2>$null
    }
    if ($LASTEXITCODE -eq 0) { Write-Host "  Database initialized." -ForegroundColor Gray }
    else { Write-Host "  SQL init skipped (sqlcmd path may vary). Run scripts\CreateStoredProcedure.sql manually." -ForegroundColor Yellow }

    # 4. Upload sample CSV (triggers the Function)
    Write-Host "`n[4/4] Uploading sample.csv to inbound container..." -ForegroundColor Green
    docker compose --profile init run --rm upload-sample
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Upload failed. Manually upload sample-data\sample.csv via Azure Storage Explorer (http://127.0.0.1:10000)" -ForegroundColor Yellow
    }

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  Galen Integration is running!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "`nView logs:   docker compose logs -f functions" -ForegroundColor White
    Write-Host "Stop all:    docker compose down" -ForegroundColor White
    Write-Host "`nExpected: 4 rows accepted, 3 rejected -> sample.errors.json" -ForegroundColor Gray
}
finally {
    Pop-Location
}
