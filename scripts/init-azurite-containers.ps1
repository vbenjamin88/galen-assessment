# Initialize Azurite blob containers for local development
# Run after Azurite is up: docker exec galen-azurite azurite-blob --help
# Use Azure Storage Explorer or Azurite REST API to create container

$connectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"

# Using Az.Storage PowerShell module (install: Install-Module Az.Storage)
try {
    $ctx = New-AzStorageContext -ConnectionString $connectionString
    New-AzStorageContainer -Name "inbound" -Context $ctx -ErrorAction SilentlyContinue
    Write-Host "Container 'inbound' created or already exists."
} catch {
    Write-Host "Note: Install Az.Storage module for container creation: Install-Module Az.Storage"
    Write-Host "Or create manually via Azure Storage Explorer (connect to Azurite)."
}
