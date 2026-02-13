#!/bin/bash
# Deploy to Kubernetes (AKS or other)
# Usage: ./deploy-k8s.sh [image-tag] [image-registry]

set -e
IMAGE_TAG=${1:-latest}
IMAGE_REGISTRY=${2:-galen-integration-functions}
K8S_DIR="$(cd "$(dirname "$0")/../k8s" && pwd)"

echo "Deploying Galen Integration to Kubernetes..."
echo "  Image: $IMAGE_REGISTRY:$IMAGE_TAG"

for manifest in namespace.yaml configmap.yaml deployment.yaml hpa.yaml; do
  path="$K8S_DIR/$manifest"
  if [ -f "$path" ]; then
    echo "Applying $manifest..."
    kubectl apply -f "$path"
  fi
done

echo ""
echo "Note: Ensure secrets exist. Create with:"
echo '  kubectl create secret generic galen-integration-secrets -n galen-integration \'
echo '    --from-literal=AzureWebJobsStorage="..." \'
echo '    --from-literal=SqlConnectionString="..."'
echo ""
echo "Deployment complete. Check status: kubectl get pods -n galen-integration"
