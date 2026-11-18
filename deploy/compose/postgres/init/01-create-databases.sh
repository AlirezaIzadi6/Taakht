#!/bin/bash
# One database per service (ADR 0013), each owned by a role of the same name, so a
# service cannot even connect to another service's database. The password is
# <NAME>_DB_PASSWORD (for example AD_DB_PASSWORD) and defaults to the name.
# Runs only when the data volume is empty.
set -euo pipefail

for db in ad matching deal reputation communication; do
  password_var="${db^^}_DB_PASSWORD"
  password="${!password_var:-$db}"

  psql -v ON_ERROR_STOP=1 -v password="$password" --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    CREATE ROLE $db LOGIN PASSWORD :'password';
    CREATE DATABASE $db OWNER $db;
    REVOKE CONNECT ON DATABASE $db FROM PUBLIC;
EOSQL
done
