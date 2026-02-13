# Run the Project Without Docker

This guide runs the Galen Integration project using .NET, Azure Functions Core Tools, Azurite (npm), and local SQL Server.

**Quick alternative (no Azurite or `func start`):** If you only need to process a CSV locally, use the Console Runner—requires only .NET 8 SDK:

```powershell
dotnet run --project src\Galen.Integration.ConsoleRunner\Galen.Integration.ConsoleRunner.csproj -- sample-data\sample.csv
```

This writes `sample.errors.json` next to the CSV and skips SQL by default (use `--use-sql` for real DB).

---

## Prerequisites to Install

### 1. .NET SDK
- **.NET 8 SDK** (required): https://dotnet.microsoft.com/download/dotnet/8.0  
  - Install the **SDK** (not just Runtime)  
  - Verify: `dotnet --version` (should show 8.x)  
  - Required for building and running the project (Console Runner, Function app)
- **.NET 6 SDK** (optional, for `func start`): https://dotnet.microsoft.com/download/dotnet/6.0  
  - Azure Functions Core Tools has internal net6.0 components. If `func start` fails with restore/build errors (e.g. WorkerExtensions, Microsoft.NETCore.App.Ref), install .NET 6 SDK **in addition to** .NET 8. Both can coexist.

### 2. Azure Functions Core Tools v4
- Download: https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools
- Or via npm: `npm install -g azure-functions-core-tools@4 --unsafe-perm true`
- Or via winget: `winget install Microsoft.Azure.FunctionsCoreTools`
- Verify: `func --version` (should show 4.x)

### 3. Node.js (for Azurite)
- Download: https://nodejs.org/ (LTS version)
- Verify: `node --version` and `npm --version`

### 4. Azurite (Azure Storage Emulator)
- Run: `npm install -g azurite`
- Verify: `azurite --version`

### 5. SQL Server
Choose one:
- **Option A: SQL Server Express** – https://www.microsoft.com/en-us/sql-server/sql-server-downloads (free)
- **Option B: SQL Server LocalDB** – Comes with Visual Studio or install separately
- **Option C: Azure SQL Database** – Use a real Azure SQL instance (connection string in settings)

---

## Step-by-Step Run Instructions

### Step 1: Start Azurite (Blob Storage Emulator)

Open a **terminal/PowerShell** and run:

```powershell
azurite --silent --location c:\azurite
```

Leave this running. Azurite will listen on:
- Blob: http://127.0.0.1:10000

### Step 2: Create the inbound Container

Open a **second terminal** and run:

```powershell
# Install Azure CLI if you don't have it: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
az storage container create --name inbound --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
```

**Or** use Azure Storage Explorer:
- Connect to: BlobEndpoint = `http://127.0.0.1:10000`
- Create a container named `inbound`

### Step 3: Initialize SQL Database

1. Connect to your SQL Server (SSMS, Azure Data Studio, or sqlcmd)
2. Run the script: `scripts\CreateStoredProcedure.sql`
3. This creates the database, table, and stored procedure

**Connection string for local SQL:**
- Server: `localhost` or `.\SQLEXPRESS` (for Express)
- Database: `GalenIntegration` (created by script)
- User: `sa` (or your SQL user)
- Password: your SQL password

### Step 4: Configure local.settings.json

1. Copy the example:
   ```powershell
   Copy-Item src\Galen.Integration.Functions\local.settings.json.example src\Galen.Integration.Functions\local.settings.json
   ```
   Or create `src\Galen.Integration.Functions\local.settings.json` manually.

2. Edit `local.settings.json` with:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BlobContainerName": "inbound",
    "RecordRepository__ConnectionString": "Server=localhost;Database=GalenIntegration;User Id=sa;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=true;",
    "RecordRepository__CommandTimeoutSeconds": "120"
  }
}
```

Replace `YOUR_SQL_PASSWORD` with your SQL Server password. For SQL Express use `Server=localhost\\SQLEXPRESS` if needed.

### Step 5: Build and Run the Function

```powershell
cd c:\Users\Administrator\.cursor\galen-assessment

# Restore and build
dotnet restore
dotnet build -c Release

# Run the Function
cd src\Galen.Integration.Functions
func start
```

The Function will start and listen for blob triggers. You should see:
```
Functions:
    CsvBlobTrigger: blobTrigger
```

### Step 6: Upload Sample CSV to Trigger Processing

In a **third terminal**, upload the sample file:

```powershell
# Using Azure CLI
az storage blob upload -f "c:\Users\Administrator\.cursor\galen-assessment\sample-data\sample.csv" -c inbound -n sample.csv --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;" --overwrite
```

**Or** use Azure Storage Explorer to upload `sample-data\sample.csv` to the `inbound` container.

### Step 7: View Results

- **Function logs** – In the terminal where `func start` is running, you should see the trigger and processing logs
- **SQL** – Query `CanonicalRecords` table: 4 rows (EXT-001 to EXT-004)
- **Blob** – `sample.errors.json` in the `inbound` container with 3 rejected rows

---

## Quick Reference – Terminal Layout

| Terminal 1 | Terminal 2 | Terminal 3 |
|------------|------------|------------|
| `azurite --silent` | `cd src\Galen.Integration.Functions` then `func start` | Upload sample.csv (Azure CLI or Storage Explorer) |

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "Cannot connect to SQL" | Check connection string, ensure SQL Server is running, enable TCP/IP if needed |
| "Blob trigger not firing" | Ensure Azurite is running, container name is `inbound`, connection string points to `127.0.0.1:10000` |
| "az storage" not found | Install Azure CLI or use Azure Storage Explorer for upload |
| "func" not found | Install Azure Functions Core Tools v4 |
| `func start` fails with restore/build errors (WorkerExtensions, Microsoft.NETCore.App.Ref) | Azure Functions Core Tools has net6.0 components. Install **.NET 6 SDK** in addition to .NET 8 SDK. Both can coexist. Alternatively, use the **Console Runner** without `func start`: `dotnet run --project src\Galen.Integration.ConsoleRunner -- sample-data\sample.csv` |
