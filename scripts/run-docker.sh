#!/bin/bash
# Run the entire project with Docker (Linux / Mac)
# Prerequisites: Docker installed and running

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Check Docker
if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running or not installed. Please start Docker."
  echo "Download: https://www.docker.com/products/docker-desktop/"
  exit 1
fi

echo "Starting Galen Integration with Docker..."
cd "$PROJECT_ROOT"

# 1. Start infrastructure + Functions
echo ""
echo "[1/4] Starting Azurite, SQL Server, and Azure Function..."
docker compose up -d azurite sqlserver functions

# 2. Wait for services
echo ""
echo "[2/4] Waiting for services (45s for SQL + Function startup)..."
sleep 45

# 3. Initialize SQL database
echo ""
echo "[3/4] Initializing SQL database..."
./scripts/init-database.sh 2>/dev/null && echo "  Database initialized." || echo "  SQL init skipped. Run scripts/CreateStoredProcedure.sql manually."

# 4. Upload sample CSV (triggers the Function)
echo ""
echo "[4/4] Uploading sample.csv to inbound container..."
docker compose --profile init run --rm upload-sample || echo "  Upload failed. Manually upload sample-data/sample.csv"

echo ""
echo "========================================"
echo "  Galen Integration is running!"
echo "========================================"
echo ""
echo "View logs:   docker compose logs -f functions"
echo "Stop all:    docker compose down"
echo ""
echo "Expected: 4 rows accepted, 3 rejected -> sample.errors.json"
