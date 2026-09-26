#!/usr/bin/env bash
# Local test wrapper: detects the matching local Docker containers, starts them if present, then runs dotnet test.
# Without Docker or without a matching container the start step is skipped, and [DbFact(DatabaseType.X)] tests
# skip themselves depending on whether each POLHEM_TEST_CONNSTR_{DBTYPE} in .runsettings is set.
#
# Container names can be overridden with environment variables (defaults apply when unset):
#   POLHEM_TEST_SQL_CONTAINER=my-mssql    ./test.sh   # default sql2025
#   POLHEM_TEST_PG_CONTAINER=my-pg        ./test.sh   # default pgvector-db
#   POLHEM_TEST_MYSQL_CONTAINER=my-mysql  ./test.sh   # default mysql8
#   POLHEM_TEST_ORACLE_CONTAINER=my-ora   ./test.sh   # default oracle23ai
set -euo pipefail

SQL_CONTAINER="${POLHEM_TEST_SQL_CONTAINER:-sql2025}"
PG_CONTAINER="${POLHEM_TEST_PG_CONTAINER:-pgvector-db}"
MYSQL_CONTAINER="${POLHEM_TEST_MYSQL_CONTAINER:-mysql8}"
ORACLE_CONTAINER="${POLHEM_TEST_ORACLE_CONTAINER:-oracle23ai}"

start_container() {
  local name="$1"
  local port="$2"
  local timeout="${3:-30}"

  if ! command -v docker >/dev/null 2>&1; then
    return
  fi
  if ! docker inspect "$name" >/dev/null 2>&1; then
    return
  fi
  if docker ps --format '{{.Names}}' | grep -qx "$name"; then
    return
  fi

  echo "Starting container $name..."
  docker start "$name" >/dev/null
  echo -n "Waiting for $name on localhost:$port"
  for _ in $(seq 1 "$timeout"); do
    if nc -z localhost "$port" 2>/dev/null; then
      echo " ready."
      return
    fi
    echo -n "."
    sleep 1
  done
  echo " timeout (DbFact tests for this DB may be skipped)."
}

# Makes sure the Docker daemon is ready. When it is already running this returns at no cost; on macOS a stopped
# daemon is started through Docker Desktop and awaited. Without the docker CLI, or off macOS, it does nothing, so
# environments without Docker keep skipping automatically. Any failure only warns and never aborts, so DbFact
# tests skip or fail as usual.
ensure_docker_daemon() {
  if ! command -v docker >/dev/null 2>&1; then
    return
  fi
  if docker info >/dev/null 2>&1; then
    return
  fi
  # Only macOS starts Docker Desktop (a GUI app) automatically. On Linux the daemon is a systemd service,
  # and CI on Linux does not use this script.
  if [[ "$(uname)" != "Darwin" ]]; then
    echo "Docker daemon is not running (not macOS, not started automatically); DbFact tests may fail or be skipped."
    return
  fi
  echo "Docker daemon is not running; starting Docker Desktop..."
  if ! open -a Docker 2>/dev/null; then
    echo "Could not start Docker Desktop (it may not be installed); DbFact tests may fail or be skipped."
    return
  fi
  echo -n "Waiting for Docker daemon"
  # A cold start of Docker Desktop can take 1-2 minutes.
  for _ in $(seq 1 120); do
    if docker info >/dev/null 2>&1; then
      echo " ready."
      return
    fi
    echo -n "."
    sleep 1
  done
  echo " timeout (the Docker daemon is still not ready); DbFact tests may fail or be skipped."
}

ensure_docker_daemon

start_container "$SQL_CONTAINER"    1433
start_container "$PG_CONTAINER"     5432
start_container "$MYSQL_CONTAINER"  3306
# A cold start of Oracle 23ai can take 1-2 minutes, so it gets a longer timeout.
start_container "$ORACLE_CONTAINER" 1521 180

dotnet test --configuration Release --settings .runsettings "$@"
