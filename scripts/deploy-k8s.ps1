# Deploy to Kubernetes (AKS or other)
# Usage: ./deploy-k8s.ps1 -ImageTag "v1.0.0" -ImageRegistry "myacr.azurecr.io"

param(
    [string]$ImageTag = "latest",
    [string]$ImageRegistry = "galen-integration-functions",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$k8sDir = Join-Path $PSScriptRoot "..\k8s"

Write-Host "Deploying Galen Integration to Kubernetes..."
Write-Host "  Image: $ImageRegistry`:$ImageTag"

if ($DryRun) {
    Write-Host "  [DRY RUN]"
    kubectl apply -f $k8sDir --dry-run=client -o yaml
    exit 0
}

# Apply in order
$manifests = @(
    "namespace.yaml",
    "configmap.yaml",
    "deployment.yaml",
    "hpa.yaml"
)

foreach ($m in $manifests) {
    $path = Join-Path $k8sDir $m
    if (Test-Path $path) {
        Write-Host "Applying $m..."
        kubectl apply -f $path
    }
}

# Secrets must be created manually or from secret.yaml (do not commit!)
Write-Host ""
Write-Host "Note: Ensure secrets exist. Create with:"
Write-Host '  kubectl create secret generic galen-integration-secrets -n galen-integration \'
Write-Host '    --from-literal=AzureWebJobsStorage="..." \'
Write-Host '    --from-literal=SqlConnectionString="..."'
Write-Host ""
Write-Host "Deployment complete. Check status: kubectl get pods -n galen-integration"
