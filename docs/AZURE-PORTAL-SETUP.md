# Azure Portal Setup – Options & Data to Import

This guide shows exactly what options to select when creating each Azure resource in the Portal, and what data you must import into the source code.

---

## Overview: What You Create and What You Import

| Resource           | Where to create       | What you copy into source code                                  |
|--------------------|-----------------------|------------------------------------------------------------------|
| Storage account    | Azure Portal          | **Connection string** → `AzureWebJobsStorage`                    |
| SQL Database       | Azure Portal          | **Connection string** (or server + user + password) → `RecordRepository__ConnectionString` |
| Function App       | Azure Portal (optional) | App settings (connection strings) configured in Portal        |

---

## 1. Resource Group (Create First)

**Create a resource** → search **Resource group** → Create

| Field        | Value (example)     |
|-------------|---------------------|
| Subscription| Your subscription   |
| Resource group | `galen-integration-rg` |
| Region      | `East US` (or your preferred region) |

---

## 2. Storage Account

**Create a resource** → search **Storage account** → Create

### Basics tab

| Field              | Value / Choice        | Notes |
|--------------------|-----------------------|-------|
| Subscription       | Your subscription     | |
| Resource group     | `galen-integration-rg`| Use the one you created |
| Storage account name | `galenintegrationst` | 3–24 chars, lowercase, numbers only. Must be globally unique. |
| Region             | `East US`             | Same as resource group recommended |
| Performance        | **Standard**          | |
| Redundancy         | **LRS** (Locally-redundant storage) | Cheapest option |
| Access tier        | **Hot**               | |

### Advanced tab (defaults are fine)

| Field                    | Value / Choice       |
|--------------------------|----------------------|
| Secure transfer required | Enabled (default)    |
| Allow blob public access | Disabled (default)   |

Click **Review** → **Create**.

### After creation: Create container

1. Go to your Storage account → **Data storage** → **Containers**
2. **+ Container**
3. Name: **`inbound`**
4. Public access level: **Private**
5. Create

### Data to copy: Connection string

1. Storage account → **Security + network** → **Access keys** (or **Access keys** in left menu)
2. Under **key1**, click **Show** next to Connection string
3. Click **Copy** – this is your **Storage connection string**

**Example format:**
```
DefaultEndpointsProtocol=https;AccountName=galenintegrationst;AccountKey=...;EndpointSuffix=core.windows.net
```

---

## 3. SQL Database (and Logical Server)

**Create a resource** → search **SQL Database** → Create

### Basics tab

| Field              | Value / Choice        | Notes |
|--------------------|-----------------------|-------|
| Subscription       | Your subscription     | |
| Resource group     | `galen-integration-rg`| |
| Database name      | `GalenIntegration`    | |
| Server             | **Create new**        | |

#### New server (click **Create new**)

| Field              | Value / Choice        | Notes |
|--------------------|-----------------------|-------|
| Server name        | `galen-sql-server-xyz`| Must be globally unique. Use numbers/suffix if taken. |
| Location           | `East US`             | |
| Authentication method | **Use SQL authentication** | |
| Server admin login | `sqladmin`            | Remember this |
| Password           | `YourStr0ng!P@ssw0rd` | Min 8 chars, upper, lower, number, special. Remember this. |

#### Back to Basics

| Field              | Value / Choice        |
|--------------------|-----------------------|
| Want to use SQL elastic pool? | **No** |
| Compute + storage  | **Basic** (cheapest) or **Serverless** |

Click **Review + create** → **Create**.

### After creation: Configure firewall

1. Go to your **SQL server** (not the database) in the Portal
2. **Security** → **Networking**
3. Under **Firewall rules**:
   - **Add your client IPv4 address** (for your PC), or
   - For testing: add rule `AllowAll` with Start IP `0.0.0.0`, End IP `255.255.255.255` (insecure – use only for testing)

### After creation: Run schema script

1. SQL Database → **Query editor**
2. Log in with: `sqladmin` / your password
3. Paste contents of `scripts/CreateStoredProcedure.sql`
4. Run

### Data to copy: SQL connection string

Build it from your values:

```
Server=tcp:<SERVER_NAME>.database.windows.net;Database=GalenIntegration;User ID=<ADMIN_USER>;Password=<YOUR_PASSWORD>;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;
```

**Example:**
```
Server=tcp:galen-sql-server-xyz.database.windows.net;Database=GalenIntegration;User ID=sqladmin;Password=YourStr0ng!P@ssw0rd;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;
```

Values to substitute:
- `<SERVER_NAME>` = SQL server name (e.g. `galen-sql-server-xyz`)
- `<ADMIN_USER>` = Server admin login (e.g. `sqladmin`)
- `<YOUR_PASSWORD>` = Server admin password

---

## 4. Function App (Optional – for deploying to Azure)

**Create a resource** → search **Function App** → Create

### Basics tab

| Field              | Value / Choice        | Notes |
|--------------------|-----------------------|-------|
| Subscription       | Your subscription     | |
| Resource group     | `galen-integration-rg`| |
| Function App name  | `galen-integration-fn-xyz` | Globally unique |
| Runtime stack      | **.NET**              | |
| Version            | **8 (Isolated)**      | Must match project (net8.0) |
| Region             | `East US`             | |
| Operating System   | **Windows** (or Linux) | |
| Plan type          | **Consumption (Serverless)** | Free tier |

### Hosting tab

| Field              | Value / Choice        |
|--------------------|-----------------------|
| Storage account    | Select the Storage account you created (e.g. `galenintegrationst`) |

*Important: Use the same Storage account that has the `inbound` container so the blob trigger can see uploads.*

### Monitoring tab (optional)

| Field              | Value / Choice        |
|--------------------|-----------------------|
| Enable Application Insights | No (or Yes if you want monitoring) |

Click **Review + create** → **Create**.

### After creation: Configure App settings

1. Function App → **Settings** → **Configuration** → **Application settings**
2. **+ New application setting** for each:

| Name | Value |
|------|-------|
| `AzureWebJobsStorage` | Paste your **Storage connection string** (from step 2) |
| `BlobContainerName` | `inbound` |
| `RecordRepository__ConnectionString` | Paste your **SQL connection string** (from step 3) |
| `RecordRepository__CommandTimeoutSeconds` | `120` |

3. **Save**

---

## 5. Data to Import into Source Code (for local run)

For running the Function **locally** (`func start`), create or edit:

**File:** `src/Galen.Integration.Functions/local.settings.json`

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<PASTE_STORAGE_CONNECTION_STRING>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BlobContainerName": "inbound",
    "RecordRepository__ConnectionString": "<PASTE_SQL_CONNECTION_STRING>",
    "RecordRepository__CommandTimeoutSeconds": "120"
  }
}
```

### What to paste where

| Placeholder | Where to get it |
|-------------|-----------------|
| `<PASTE_STORAGE_CONNECTION_STRING>` | Storage account → Access keys → key1 → Connection string |
| `<PASTE_SQL_CONNECTION_STRING>` | Build from: `Server=tcp:<server>.database.windows.net;Database=GalenIntegration;User ID=<user>;Password=<pass>;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;` |

### Important

- Use **double underscore** `__` in `RecordRepository__ConnectionString` (not colon).
- `local.settings.json` is in `.gitignore` – do not commit it.
- Keep the connection strings secret.

---

## 6. Summary Checklist

| Step | Action | Data for source code |
|------|--------|----------------------|
| 1 | Create Resource group | – |
| 2 | Create Storage account | Copy connection string → `AzureWebJobsStorage` |
| 3 | Create container `inbound` | – |
| 4 | Create SQL Database (+ server) | Build connection string → `RecordRepository__ConnectionString` |
| 5 | Add firewall rule (your IP) | – |
| 6 | Run `scripts/CreateStoredProcedure.sql` | – |
| 7 | (Optional) Create Function App | Configure app settings in Portal |
| 8 | Create `local.settings.json` | Paste Storage + SQL connection strings |
