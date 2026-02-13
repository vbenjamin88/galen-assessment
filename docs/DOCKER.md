# Docker – Windows and Linux

## CI/CD build-image vs local Docker

| Environment | Builds on | Notes |
|-------------|-----------|-------|
| **GitHub Actions (CI/CD)** | Linux (Ubuntu) | `build-image` job runs `docker build` on Linux runners. Success here means the Dockerfile is valid and builds on Linux. |
| **Your local PC (Windows)** | Depends | Docker Desktop on Windows typically uses WSL2 and runs Linux containers. If WSL2/Docker Desktop has issues (e.g. kernel errors), local `docker build` may fail even when CI succeeds. |
| **Linux / Mac** | Native | `docker build` runs natively. Same behavior as CI. |

**Summary:** CI/CD success means the Dockerfile and image are correct. Local success depends on your Docker setup (Docker Desktop, WSL2, etc.).

---

## Commands – same on Windows and Linux

These commands work identically on Windows (PowerShell/cmd) and Linux/Mac:

```bash
# Build the image
docker build -t galen-integration-functions:v1.0.0 .

# Push to registry
docker push <your-registry>/galen-integration-functions:v1.0.0

# Run services
docker compose up -d azurite sqlserver functions

# View logs
docker compose logs -f functions
```

The **Dockerfile** and **docker-compose.yml** are cross-platform; no changes needed per OS.

---

## Scripts – Windows vs Linux

| Task | Windows (PowerShell) | Linux / Mac (Bash) |
|------|----------------------|--------------------|
| Run with Docker | `.\scripts\run-docker.ps1` | Use manual steps below* |
| Deploy to K8s | `.\scripts\deploy-k8s.ps1` | `./scripts/deploy-k8s.sh` |
| Init database | In run-docker.ps1 | `./scripts/init-database.sh` |
| Init Azurite | `.\scripts\init-azurite-containers.ps1` | `./scripts/init-azurite.sh` |

\* Linux: There is no `run-docker.sh`; use the manual steps from README or run `docker compose` commands directly.

---

## Linux / Mac – run with Docker (manual)

```bash
# 1. Start services
docker compose up -d azurite sqlserver functions

# 2. Wait ~45s, then init DB
cat scripts/CreateStoredProcedure.sql | docker exec -i galen-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C

# 3. Upload sample (triggers Function)
docker compose --profile init run --rm upload-sample

# 4. View logs
docker compose logs -f functions
```

---

## Path differences

| Context | Windows | Linux / Mac |
|---------|---------|-------------|
| Path separator in scripts | `\` or `\\` | `/` |
| Run script | `.\scripts\run-docker.ps1` | `./scripts/deploy-k8s.sh` |

The Dockerfile uses forward slashes (`/`) – correct for both, as Docker runs in a Linux context.
