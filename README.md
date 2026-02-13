# Galen Integration - CSV Processing Azure Function

A production-grade Azure Function that processes partner CSV files from Azure Blob Storage with healthcare-grade reliability, idempotency, and observability.

## Features

| Requirement | Implementation |
|-------------|----------------|
| **Idempotency** | Blob metadata check (`ProcessedAt`), MERGE in SQL on `(SourceFile, SourceRowIndex)` |
| **Batch Inserts** | Table-Valued Parameters (TVP), 500 rows per batch |
| **Retry & Resiliency** | Polly retry + circuit breaker for SQL; exponential backoff with jitter |
| **Observability** | Serilog + Application Insights, Activity tracing, structured logs |
| **File Lifecycle** | Move to `processed/` on success, `quarantine/` on failure |
| **Security** | Connection strings from config/Key Vault, input validation, field length limits |
| **Streaming** | CsvHelper with stream-based parsing, never loads full file |
| **Dead-Letter** | `.errors.json` companion file for rejected rows; quarantine for failed files |
| **Concurrency** | Blob lease (60s) for exclusive processing |
| **Corruption Handling** | Per-row try/catch, encoding fallback (UTF-8/Latin1), malformed row rejection |

## Architecture

```
src/
├── Galen.Integration.Domain/        # Entities, value objects
├── Galen.Integration.Application/   # Interfaces, validation, use cases
├── Galen.Integration.Infrastructure/# CSV, SQL, Blob, Polly
└── Galen.Integration.Functions/     # Azure Function entry point
```

## Quick Start

### Prerequisites

- **.NET 8 SDK** (required). If using `func start` and it fails with restore errors, install **.NET 6 SDK** as well (see [docs/RUN-WITHOUT-DOCKER.md](docs/RUN-WITHOUT-DOCKER.md#troubleshooting)).
- Docker & Docker Compose (for local run)
- Azure Storage Emulator (Azurite) or real Azure Storage
- SQL Server (local or Azure SQL)

### 1. Run with Docker (one command)

```powershell
# Prerequisites: Docker Desktop installed and running

.\scripts\run-docker.ps1
```

This script starts Azurite, SQL Server, and the Function, initializes the database, uploads `sample-data/sample.csv`, and triggers processing. View logs with `docker compose logs -f functions`.

**Manual steps (if needed):**
```powershell
# Start all services
docker compose up -d azurite sqlserver functions

# Initialize DB (after ~30s)
Get-Content scripts\CreateStoredProcedure.sql | docker exec -i galen-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C

# Upload sample (triggers Function)
docker compose --profile init run --rm upload-sample
```

### 2. Run Without Docker

See **[docs/RUN-WITHOUT-DOCKER.md](docs/RUN-WITHOUT-DOCKER.md)** for the full guide.

**Prerequisites:** .NET 8 SDK, Azure Functions Core Tools, Node.js, Azurite, SQL Server

```powershell
.\scripts\run-without-docker.ps1
```

### 3. Test with Real Azure

See **[docs/AZURE-SETUP-GUIDE.md](docs/AZURE-SETUP-GUIDE.md)** for step-by-step setup: create an Azure account, provision Storage + SQL, and run the Function against real Azure resources.

### 4. Production Deployment (Kubernetes / Azure Functions)

See **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** for production deployment to AKS or Azure Functions.

**Quick K8s deploy:**
```powershell
# Build and push image to your registry
docker build -t <your-registry>/galen-integration-functions:v1.0.0 .
docker push <your-registry>/galen-integration-functions:v1.0.0

# Create secrets (see k8s/secret.yaml.template)
kubectl create secret generic galen-integration-secrets -n galen-integration \
  --from-literal=AzureWebJobsStorage='...' --from-literal=SqlConnectionString='...'

# Deploy (namespace, configmap, deployment, HPA)
./scripts/deploy-k8s.ps1
# or: kubectl apply -f k8s/
```

## Sample CSV Format

Expected columns: `id`, `patient_id`, `doc_type`, `doc_date`, `description`, `source_system`

| Column       | Required | Notes                                      |
|-------------|----------|--------------------------------------------|
| id          | Yes      | External record ID                         |
| patient_id  | Yes      | Patient identifier                         |
| doc_type    | No       | Lab, Radiology, Clinical, Administrative, Other |
| doc_date    | No       | ISO date (yyyy-MM-dd)                      |
| description | No       | Max 1000 chars                             |
| source_system| No      | Partner system name                        |

See `sample-data/sample.csv` for examples. Rows 5–7 demonstrate validation (missing date, missing patient_id, invalid doc_type) and will produce `sample.errors.json`.

## CI/CD

GitHub Actions workflow (`.github/workflows/ci-cd.yml`):

- **build-and-test**: Restore, build, unit tests
- **integration-tests**: Azurite + integration tests
- **build-image**: Docker build (on push to main)
- **deploy-staging**: Add your deployment steps (ACR push, `kubectl apply`, or `func publish`) – see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)

## Configuration

| Setting                           | Description                    | Default   |
|----------------------------------|--------------------------------|-----------|
| AzureWebJobsStorage              | Blob storage connection        | -         |
| BlobContainerName                | Inbound container name         | inbound   |
| RecordRepository:ConnectionString| SQL connection string          | -         |
| RecordRepository:CommandTimeoutSeconds | SQL command timeout      | 120       |
| FileProcessor:BatchSize          | Rows per SQL batch             | 500       |
| CsvProcessor:MaxRowsPerFile      | Max rows per file (safety cap) | 100,000   |

## Testing

```powershell
dotnet test tests/Galen.Integration.Tests.Unit
dotnet test tests/Galen.Integration.Tests.Integration  # Requires Azurite
```

## Security Notes

- Use Managed Identity for Azure resources in production
- Store connection strings in Azure Key Vault
- Apply least-privilege SQL roles (execute only on `usp_ImportCanonicalRecords`)
- Enable encryption at rest and in transit

## License

Assessment project for Galen Healthcare Solutions / RLDatix.
