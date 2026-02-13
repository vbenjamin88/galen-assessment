# Local setup script - creates Azurite container and initializes SQL database
# Run after: docker-compose up -d azurite sqlserver
# Wait ~30s for SQL to be ready, then run this script

param(
    [string]$SqlPassword = "YourStrong@Passw0rd"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating blob container in Azurite..."
# Azurite creates container on first access - use curl to create
$headers = @{
    "x-ms-version" = "2020-10-02"
}
try {
    Invoke-WebRequest -Uri "http://127.0.0.1:10000/devstoreaccount1/inbound?restype=container" `
        -Method Put -Headers $headers -UseBasicParsing | Out-Null
    Write-Host "Container 'inbound' created."
} catch {
    Write-Host "Container may exist: $_"
}

Write-Host "Initializing SQL database..."
$scriptPath = Join-Path $PSScriptRoot "..\scripts\CreateStoredProcedure.sql"
docker exec -i galen-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -i /dev/stdin 2>$null < $scriptPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Note: sqlcmd may not be in path. Run SQL script manually:"
    Write-Host "  Get-Content scripts\CreateStoredProcedure.sql | docker exec -i galen-sqlserver sqlcmd -S localhost -U sa -P $SqlPassword -C"
} else {
    Write-Host "Database initialized."
}

Write-Host "Setup complete. Upload sample.csv to inbound container to test."
