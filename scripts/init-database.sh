#!/bin/bash
# Run SQL init script against SQL Server in Docker
# Usage: ./init-database.sh [host] [password]

HOST=${1:-localhost}
PASSWORD=${2:-YourStrong@Passw0rd}

echo "Initializing database on $HOST..."
docker exec -i galen-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PASSWORD" -C -i /dev/stdin < "$(dirname "$0")/../scripts/CreateStoredProcedure.sql" 2>/dev/null || {
  # Fallback: use docker run with sqlcmd
  cat "$(dirname "$0")/../scripts/CreateStoredProcedure.sql" | docker run -i --rm --network galen-assessment_default mcr.microsoft.com/mssql-tools /opt/mssql-tools18/bin/sqlcmd -S "$HOST" -U sa -P "$PASSWORD" -C
}
