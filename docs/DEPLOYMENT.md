# Production Deployment Guide

This guide covers deploying the Galen Integration Azure Function to production environments.

## Prerequisites

- **Azure Storage Account** – Blob container `inbound` for partner CSV uploads
- **Azure SQL Database** – Database with schema from `scripts/CreateStoredProcedure.sql`
- **Container Registry** – ACR or other registry for the Function image
- **Kubernetes cluster** (AKS) or **Azure Functions** (Premium/Consumption)

---

## Option A: Deploy to Azure Kubernetes Service (AKS)

### 1. Build and push image

```bash
# Login to your registry (e.g., Azure Container Registry)
az acr login --name <your-acr-name>

# Build and tag
docker build -t <your-acr>.azurecr.io/galen-integration-functions:v1.0.0 .

# Push
docker push <your-acr>.azurecr.io/galen-integration-functions:v1.0.0
```

### 2. Run database migrations

Execute `scripts/CreateStoredProcedure.sql` against your Azure SQL instance to create:

- `GalenIntegration` database (if not exists)
- `CanonicalRecords` table
- `dbo.CanonicalRecordType` (TVP)
- `dbo.usp_ImportCanonicalRecords` stored procedure

### 3. Create Kubernetes namespace and secrets

```bash
# Apply namespace
kubectl apply -f k8s/namespace.yaml

# Create secrets (use real values from Key Vault or secure storage)
kubectl create secret generic galen-integration-secrets -n galen-integration \
  --from-literal=AzureWebJobsStorage='DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net' \
  --from-literal=SqlConnectionString='Server=tcp:yourserver.database.windows.net;Database=GalenIntegration;User ID=...;Password=...;Encrypt=true;'
```

### 4. Update image reference in deployment

Edit `k8s/deployment.yaml` and set the image to your registry:

```yaml
image: <your-acr>.azurecr.io/galen-integration-functions:v1.0.0
imagePullPolicy: Always
```

If using a private registry, create an image pull secret:

```bash
kubectl create secret docker-registry acr-secret -n galen-integration \
  --docker-server=<your-acr>.azurecr.io \
  --docker-username=<sp-client-id> \
  --docker-password=<sp-password>
```

Add to the deployment pod spec:

```yaml
imagePullSecrets:
  - name: acr-secret
```

### 5. Deploy

```bash
# Apply all manifests (order matters: namespace first, then config, secrets, deployment)
kubectl apply -f k8s/
```

Or apply individually:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
# secrets created via kubectl create above
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/hpa.yaml
```

### 6. Verify

```bash
kubectl get pods -n galen-integration
kubectl logs -f deployment/galen-integration-functions -n galen-integration
```

---

## Option B: Deploy to Azure Functions (Premium / Dedicated)

### 1. Create Function App

```bash
az functionapp create \
  --resource-group <rg> \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --functions-version 4 \
  --name galen-integration-functions \
  --storage-account <storage-account-name>
```

For Premium plan:

```bash
az functionapp plan create --name galen-integration-plan --resource-group <rg> --sku EP1 --is-linux
az functionapp create --name galen-integration-functions --resource-group <rg> --plan galen-integration-plan ...
```

### 2. Configure app settings

```bash
az functionapp config appsettings set --name galen-integration-functions --resource-group <rg> \
  --settings \
    AzureWebJobsStorage="<connection-string>" \
    BlobContainerName="inbound" \
    RecordRepository__ConnectionString="<sql-connection-string>"
```

### 3. Deploy

```bash
cd src/Galen.Integration.Functions
func azure functionapp publish galen-integration-functions
```

---

## CI/CD Integration

The repository includes a GitHub Actions workflow (`.github/workflows/ci-cd.yml`). To enable deployment:

1. Add repository secrets:
   - `AZURE_CREDENTIALS` – Service principal JSON for `az login`
   - `ACR_LOGIN_SERVER`, `ACR_USERNAME`, `ACR_PASSWORD` – If pushing to ACR
   - `KUBE_CONFIG` – Base64-encoded kubeconfig for AKS (or use OIDC)

2. Update the `deploy-staging` job in the workflow with your target (ACR push, `kubectl apply`, or `func azure functionapp publish`).

---

## Security Checklist

- [ ] Use **Managed Identity** for Azure resources where possible
- [ ] Store connection strings in **Azure Key Vault**; reference via CSI secrets store or app settings
- [ ] Enable **encryption at rest** for Storage and SQL
- [ ] Apply **least-privilege** SQL roles (execute only on `usp_ImportCanonicalRecords`)
- [ ] Restrict **network access** (VNet integration, private endpoints)
- [ ] Enable **audit logging** for Blob and SQL

---

## Monitoring

- **Application Insights**: Enable in Function App or add instrumentation key to config
- **Logs**: Structured Serilog output; query via Log Analytics
- **Alerts**: Configure on failed function executions, SQL errors, blob processing latency
