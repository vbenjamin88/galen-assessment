# Test with Real Azure – Step-by-Step Guide

This guide walks you through creating an Azure account, setting up resources, and testing the Galen Integration project against real Azure Storage and Azure SQL.

**Prefer the Azure Portal?** See **[AZURE-PORTAL-SETUP.md](AZURE-PORTAL-SETUP.md)** for exact options on each create page and what data to import into the source code.

---

## Part 1: Create an Azure Account

### Step 1.1: Sign Up

1. Go to **[https://azure.microsoft.com/free/](https://azure.microsoft.com/free/)**
2. Click **Start free**
3. Sign in with a Microsoft account (or create one)
4. Complete identity verification (phone, card for verification – free tier does not charge without explicit upgrade)
5. Accept the agreement and create your subscription

### Step 1.2: Activate Free Credits (Optional)

- New accounts receive **$200 credit** for 30 days
- After that, many services have **always-free tiers** (e.g. Azure Functions free grant, limited SQL/Storage)
- Monitor usage in **Cost Management + Billing** to avoid charges

---

## Part 2: Install Prerequisites

1. **Azure CLI** – [Install](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)  
   - Windows: `winget install Microsoft.AzureCLI`  
   - Verify: `az --version`

2. **.NET 8 SDK** – [Download](https://dotnet.microsoft.com/download/dotnet/8.0)  
   - Verify: `dotnet --version` (8.x)

3. **Azure Functions Core Tools v4** – [Install](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)  
   - Verify: `func --version` (4.x)

---

## Part 3: Sign In to Azure

```powershell
az login
```

A browser window opens. Sign in with your Azure account. You should see your subscription listed.

---

## Part 4: Create Azure Resources

### Step 4.1: Set Variables

Choose a region and unique names (replace placeholders if desired):

```powershell
$RESOURCE_GROUP = "galen-integration-rg"
$LOCATION       = "eastus"
$STORAGE_ACCOUNT = "galenintegrationst"   # 3–24 chars, lowercase, numbers only
$SQL_SERVER     = "galen-sql-server"      # Must be globally unique
$SQL_DATABASE   = "GalenIntegration"
$FUNCTION_APP   = "galen-integration-fn"  # Must be globally unique
$FUNC_STORAGE   = "galenfnstorage"        # Storage for Function App (lowercase, no hyphens)
$SQL_ADMIN_USER = "sqladmin"
$SQL_ADMIN_PASS = "YourStr0ng!P@ssw0rd"   # Min 8 chars, complex
```

### Step 4.2: Create Resource Group

```powershell
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### Step 4.3: Create Storage Account and Container

```powershell
# Create storage account
az storage account create `
  --resource-group $RESOURCE_GROUP `
  --name $STORAGE_ACCOUNT `
  --location $LOCATION `
  --sku Standard_LRS `
  --kind StorageV2

# Create container named "inbound"
az storage container create `
  --name inbound `
  --account-name $STORAGE_ACCOUNT `
  --auth-mode login
```

### Step 4.4: Get Storage Connection String

```powershell
$STORAGE_CONN = az storage account show-connection-string `
  --resource-group $RESOURCE_GROUP `
  --name $STORAGE_ACCOUNT `
  --query connectionString -o tsv

Write-Host "Storage connection string (save this):"
$STORAGE_CONN
```

### Step 4.5: Create Azure SQL Server and Database

```powershell
# Create SQL Server
az sql server create `
  --resource-group $RESOURCE_GROUP `
  --name $SQL_SERVER `
  --location $LOCATION `
  --admin-user $SQL_ADMIN_USER `
  --admin-password $SQL_ADMIN_PASS

# Allow your IP for access (or 0.0.0.0 for testing – restrict in production)
az sql server firewall-rule create `
  --resource-group $RESOURCE_GROUP `
  --server $SQL_SERVER `
  --name AllowAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 255.255.255.255

# Create database
az sql db create `
  --resource-group $RESOURCE_GROUP `
  --server $SQL_SERVER `
  --name $SQL_DATABASE `
  --service-objective Basic
```

### Step 4.6: Run the Database Schema Script

You need to run `scripts/CreateStoredProcedure.sql` against the new database.

**Option A: Azure Data Studio or SSMS**

1. Server: `{SQL_SERVER}.database.windows.net`
2. Authentication: SQL Login
3. User: `sqladmin`, Password: `YourStr0ng!P@ssw0rd`
4. Database: `GalenIntegration`
5. Open and run `scripts/CreateStoredProcedure.sql`

**Option B: sqlcmd (if installed)**

```powershell
sqlcmd -S "$SQL_SERVER.database.windows.net" -d $SQL_DATABASE -U $SQL_ADMIN_USER -P $SQL_ADMIN_PASS -I -i scripts\CreateStoredProcedure.sql
```

**Option C: Azure Portal Query Editor**

1. Go to [portal.azure.com](https://portal.azure.com)
2. Open your SQL Database → **Query editor**
3. Log in with SQL credentials
4. Paste contents of `scripts/CreateStoredProcedure.sql` and run

---

## Part 5: Test the Function – Two Options

### Option A: Run Locally with Azure Backends (`func start`)

Configure the Function to use Azure Storage and Azure SQL when run on your machine.

#### Step 5A.1: Create local.settings.json

Create or edit `src/Galen.Integration.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<PASTE_STORAGE_CONNECTION_STRING_HERE>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BlobContainerName": "inbound",
    "RecordRepository__ConnectionString": "Server=tcp:<SQL_SERVER>.database.windows.net;Database=GalenIntegration;User ID=<SQL_ADMIN_USER>;Password=<SQL_ADMIN_PASS>;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;",
    "RecordRepository__CommandTimeoutSeconds": "120"
  }
}
```

Replace:

- `<PASTE_STORAGE_CONNECTION_STRING_HERE>` with the value of `$STORAGE_CONN`
- `<SQL_SERVER>` with your SQL server name (e.g. `galen-sql-server`)
- `<SQL_ADMIN_USER>` and `<SQL_ADMIN_PASS>` with your SQL credentials

#### Step 5A.2: Run the Function

```powershell
cd c:\Users\Administrator\.cursor\galen-assessment
cd src\Galen.Integration.Functions
func start
```

#### Step 5A.3: Upload CSV to Azure Blob

In another terminal:

```powershell
az storage blob upload `
  -f "c:\Users\Administrator\.cursor\galen-assessment\sample-data\sample.csv" `
  -c inbound `
  -n sample.csv `
  --account-name <STORAGE_ACCOUNT_NAME> `
  --auth-mode login
```

Replace `<STORAGE_ACCOUNT_NAME>` with your storage account name (e.g. `galenintegrationst`).

The blob trigger should fire, and you should see processing logs in the `func start` terminal.

---

### Option B: Deploy Function to Azure and Test in Cloud

#### Step 5B.1: Create Function App

```powershell
# Create a storage account for the Function App (required by Azure Functions)
az storage account create `
  --resource-group $RESOURCE_GROUP `
  --name $FUNC_STORAGE `
  --location $LOCATION `
  --sku Standard_LRS

# Create Function App (Consumption plan – free tier)
az functionapp create `
  --resource-group $RESOURCE_GROUP `
  --consumption-plan-location $LOCATION `
  --runtime dotnet-isolated `
  --runtime-version 8 `
  --functions-version 4 `
  --name $FUNCTION_APP `
  --storage-account $FUNC_STORAGE
```

*Note: For blob trigger to work, the Function needs access to the storage account where CSV files are uploaded. Use the same storage account (`$STORAGE_ACCOUNT`) for both `AzureWebJobsStorage` and the inbound container—see Step 5B.2 below.*

#### Step 5B.2: Configure App Settings

Use your **inbound storage** connection string for `AzureWebJobsStorage` so the blob trigger can see uploads:

```powershell
az functionapp config appsettings set `
  --name $FUNCTION_APP `
  --resource-group $RESOURCE_GROUP `
  --settings `
    AzureWebJobsStorage="$STORAGE_CONN" `
    BlobContainerName="inbound" `
    RecordRepository__ConnectionString="Server=tcp:$SQL_SERVER.database.windows.net;Database=$SQL_DATABASE;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASS;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
```

*Note: `$STORAGE_CONN` points to the storage account with the `inbound` container. Azure Functions needs a dedicated storage account for internal use; if you use the same account for both, the blob trigger will work. For production, consider separate accounts and ensure the Function has access to the inbound container's storage.*

#### Step 5B.3: Deploy the Function

```powershell
cd c:\Users\Administrator\.cursor\galen-assessment
func azure functionapp publish $FUNCTION_APP
```

#### Step 5B.4: Upload Sample CSV

Upload the sample CSV to the `inbound` container (Step 5B.2 configured `AzureWebJobsStorage` to use the same storage account):

```powershell
az storage blob upload `
  -f "c:\Users\Administrator\.cursor\galen-assessment\sample-data\sample.csv" `
  -c inbound `
  -n sample.csv `
  --account-name $STORAGE_ACCOUNT `
  --auth-mode login
```

---

## Part 6: Verify Results

### Check Azure SQL

1. In Azure Portal, open your SQL Database → **Query editor**
2. Run:

```sql
SELECT * FROM dbo.CanonicalRecords ORDER BY SourceRowIndex;
```

You should see 5 rows (EXT-001 to EXT-005 – the valid rows from the sample).

### Check Blob Storage

1. In Azure Portal, open your Storage Account → **Containers** → **inbound**
2. You should see `sample.errors.json` with the 2 rejected rows (missing patient_id, invalid doc_type)

### Check Function Logs

- **Local:** See logs in the `func start` terminal
- **Deployed:** In Azure Portal → Function App → **Monitor** → **Log stream**, or **Application Insights** if enabled

---

## Part 7: Clean Up (Avoid Ongoing Charges)

To delete all resources:

```powershell
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Cannot connect to SQL" | Add your outbound IP to SQL firewall: Azure Portal → SQL Server → Networking → Add your IP |
| "Blob trigger not firing" | Ensure `AzureWebJobsStorage` points to the same storage account where you upload; ensure container is `inbound` |
| "func start" fails | Install .NET 6 SDK in addition to .NET 8 (see [RUN-WITHOUT-DOCKER.md](RUN-WITHOUT-DOCKER.md)) |
| Storage account name taken | Choose a different name (globally unique, 3–24 chars, lowercase) |
| Function App name taken | Choose a different name (globally unique) |

---

## Summary Checklist

- [ ] Azure account created
- [ ] Resource group, storage account, `inbound` container created
- [ ] Azure SQL Server and database created
- [ ] `scripts/CreateStoredProcedure.sql` executed
- [ ] `local.settings.json` configured (for local run) or Function App settings configured (for deploy)
- [ ] Function running (local or deployed)
- [ ] `sample.csv` uploaded to `inbound` container
- [ ] 5 rows in `CanonicalRecords`, `sample.errors.json` in blob
