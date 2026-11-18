#!/bin/bash
# One database per service (ADR 0013). Runs only when the data volume is empty.
set -euo pipefail

for db in ad matching deal reputation communication; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
    -c "CREATE DATABASE $db OWNER $POSTGRES_USER"
done
