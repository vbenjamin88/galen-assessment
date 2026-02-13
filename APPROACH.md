# Integration Assessment - Technical Approach

## Executive Summary

This solution implements a production-grade Azure Function for processing partner CSV files with healthcare-grade reliability, idempotency, and observability. The architecture follows Clean Architecture principles and addresses all critical requirements for healthcare integrations.

---

## Step-by-Step Approach

### 1. **Idempotency (CRITICAL for Healthcare)**

- **Blob Metadata Check**: Before processing, check blob metadata for `ProcessedAt` and `ProcessingId` to skip already-processed files
- **Processing Lock via Blob Lease**: Acquire a 60s blob lease to prevent concurrent processing of the same file
- **Idempotency Key**: Use `{BlobName}_{BlobLastModified:O}` as deterministic idempotency key
- **Database-Level Idempotency**: Stored procedure accepts batch with source file reference; uses MERGE/UPSERT semantics to avoid duplicates based on business keys

### 2. **Batch Inserts Instead of Row-by-Row**

- **Table-Valued Parameters (TVP)**: Pass entire batch to SQL via `SqlDataRecord` TVP - single round-trip per batch
- **Configurable Batch Size**: 500-1000 rows per batch (configurable) to balance memory and round-trips
- **Stored Procedure**: `usp_ImportCanonicalRecords` accepts TVP, performs batch MERGE

### 3. **Retry & Resiliency (Polly)**

- **SQL Connection**: Retry on transient faults (timeout, connection reset) - 3 retries with exponential backoff
- **Blob Operations**: Retry on 429/503 with jitter
- **Circuit Breaker**: After 5 consecutive failures, open circuit for 30s to protect downstream systems
- **Fallback**: Dead-letter to quarantine container on final failure

### 4. **Proper Observability & Metrics**

- **Structured Logging**: Serilog with Application Insights sink; correlation IDs for trace
- **Custom Metrics**: Rows processed, rows rejected, batches committed, processing duration, file size
- **Activity Tracing**: Distributed tracing with `Activity` for Azure Monitor
- **Health Checks**: Ready for K8s liveness/readiness probes

### 5. **File Lifecycle Management**

- **Processing**: Work on stream/copy; never modify original until success
- **On Success**: Move to `processed/` prefix with metadata; optional soft-delete after retention days
- **On Failure**: Move to `quarantine/` with `.errors.json` companion
- **Concurrency**: Blob lease ensures only one processor handles a file

### 6. **Security Hardening**

- **Managed Identity**: Use Azure AD auth for SQL (when in Azure); connection string in Key Vault
- **Least Privilege**: Blob SAS with minimal scope; SQL user with only execute on SP
- **Input Validation**: Sanitize all CSV inputs; reject oversized rows; limit file size (e.g., 50MB)
- **No PII in Logs**: Redact sensitive fields in log output

### 7. **Unit + Integration Testing**

- **Unit**: Validation logic, normalization, CSV parsing with `CsvHelper`
- **Integration**: Test against Azurite + SQL Server in Docker
- **Test Fixtures**: Sample CSV files; mocked blob/SQL for isolated tests

### 8. **Clean Architecture Refactor**

```
src/
  Galen.Integration.Domain/        # Entities, Value Objects
  Galen.Integration.Application/   # Use Cases, Interfaces
  Galen.Integration.Infrastructure/# Azure, SQL implementations
  Galen.Integration.Functions/     # Azure Function entry points
tests/
  Galen.Integration.Tests.Unit/
  Galen.Integration.Tests.Integration/
```

### 9. **Streaming Processing (Memory Efficient)**

- **Stream-Based CSV**: Use `CsvHelper` with `StreamReader` - never load full file into memory
- **Yield/Async Stream**: Process rows in batches as they're read
- **Configurable Row Limit**: Cap max rows per file to prevent runaway (e.g., 100k)

### 10. **Dead-Letter Strategy**

- **Rejected Rows**: Write to `{originalname}.errors.json` in same container with row index, raw data, validation errors
- **Failed Files**: Move to `quarantine/{timestamp}-{filename}` with full error context
- **Retry Policy**: Failed files can be manually re-queued from quarantine

### 11. **Concurrency Control**

- **Blob Lease**: Acquire lease before processing; release on completion
- **Single-Instance per File**: Azure Blob trigger naturally handles this; lease adds safety
- **Database**: Stored procedure uses proper transaction isolation

### 12. **Corruption Handling**

- **Malformed Rows**: Catch parse exceptions; add to rejected batch with error message
- **Encoding Detection**: Try UTF-8, then fallback to Latin1 with `Encoding.GetEncoding(28591)`
- **Truncated Files**: Process what we can; reject incomplete rows; log corruption metrics

---

## Technology Stack

| Component | Choice |
|-----------|--------|
| Runtime | .NET 8 (LTS) |
| Function Host | Azure Functions v4 (isolated) |
| CSV | CsvHelper (streaming) |
| Retry | Polly v8 |
| Logging | Serilog + App Insights |
| Testing | xUnit, FluentAssertions, NSubstitute |
| Container | Docker (mcr.microsoft.com/azure-functions/dotnet) |
| Orchestration | Kubernetes (Deployment + HPA) |
| CI/CD | GitHub Actions (build, test, container push) |

---

## Deliverables

1. **Source Code** - Full solution with Clean Architecture
2. **Dockerfile** - Multi-stage build for Azure Functions
3. **Kubernetes Manifests** - Deployment, ConfigMap, Secrets template
4. **CI/CD** - GitHub Actions workflow
5. **README** - Setup and run instructions
6. **Sample Data** - Test CSV and expected outputs
