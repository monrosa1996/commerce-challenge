#!/usr/bin/env bash
# Starts SQL Server in Docker and runs the database scripts. Safe to run more than once.
# Usage: scripts/sqlserver-docker.sh [--seed] [--test]
#   --seed  load the base file straight into dbo.commerce (backend/database/seed.sql)
#   --test  run backend/database/tests.sql
# Optional environment: MSSQL_SA_PASSWORD, MSSQL_PORT, CONTAINER_NAME
set -euo pipefail

CONTAINER="${CONTAINER_NAME:-commerce-sql}"
SA_PASSWORD="${MSSQL_SA_PASSWORD:-Commerce_Sa#2026}"
PORT="${MSSQL_PORT:-1433}"
IMAGE="mcr.microsoft.com/mssql/server:2022-latest"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DB_DIR="$ROOT/backend/database"

RUN_SEED=0
RUN_TESTS=0
for arg in "$@"; do
  case "$arg" in
    --seed) RUN_SEED=1 ;;
    --test) RUN_TESTS=1 ;;
    *) echo "Unknown option: $arg"; exit 1 ;;
  esac
done

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is not installed. Install Docker Desktop first."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running. Open Docker Desktop and try again."
  exit 1
fi

if docker ps -a --format '{{.Names}}' | grep -qx "$CONTAINER"; then
  echo "Starting existing container $CONTAINER..."
  docker start "$CONTAINER" >/dev/null
else
  echo "Creating container $CONTAINER..."
  docker run --platform linux/amd64 \
    -e ACCEPT_EULA=Y \
    -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" \
    -p "$PORT:1433" \
    -v commerce-sql-data:/var/opt/mssql \
    --name "$CONTAINER" -d "$IMAGE" >/dev/null
fi

echo "Waiting for SQL Server (this can take up to a minute)..."
READY=0
for _ in $(seq 1 60); do
  if docker exec "$CONTAINER" "$SQLCMD" -C -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
    READY=1
    break
  fi
  sleep 2
done

if [ "$READY" -ne 1 ]; then
  echo "SQL Server did not start. Check: docker logs $CONTAINER"
  exit 1
fi

run_script() {
  local file="$1"
  echo "Running $file..."
  docker cp "$DB_DIR/$file" "$CONTAINER:/tmp/$file"
  docker exec "$CONTAINER" "$SQLCMD" -C -b -f 65001 -S localhost -U sa -P "$SA_PASSWORD" -i "/tmp/$file"
}

run_script 01_create_database.sql
run_script 02_schema.sql
[ "$RUN_SEED" -eq 1 ] && run_script seed.sql
[ "$RUN_TESTS" -eq 1 ] && run_script tests.sql

echo
echo "Done. Server: localhost,$PORT  Database: CommerceDb  App login: commerce_app"
