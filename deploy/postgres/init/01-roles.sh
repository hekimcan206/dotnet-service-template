#!/bin/sh
# Runs once, when the PostgreSQL data directory is first initialised (docker-entrypoint-initdb.d).
# Two roles implement least privilege:
#   service_migrator  owns the "service" schema and runs migrations (DDL);
#   service_app       is what the API connects as: DML on the schema's tables, nothing else.
set -eu

: "${MIGRATOR_PASSWORD:?MIGRATOR_PASSWORD must be set}"
: "${APP_PASSWORD:?APP_PASSWORD must be set}"

psql -v ON_ERROR_STOP=1 \
     -v migrator_password="$MIGRATOR_PASSWORD" \
     -v app_password="$APP_PASSWORD" \
     -v db="$POSTGRES_DB" \
     --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'SQL'
CREATE ROLE service_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE service_app LOGIN PASSWORD :'app_password';

REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO service_migrator, service_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE SCHEMA service AUTHORIZATION service_migrator;
GRANT USAGE ON SCHEMA service TO service_app;

-- Tables the migrator creates later automatically grant DML (not DDL) to the runtime role.
ALTER DEFAULT PRIVILEGES FOR ROLE service_migrator IN SCHEMA service
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO service_app;
ALTER DEFAULT PRIVILEGES FOR ROLE service_migrator IN SCHEMA service
    GRANT USAGE, SELECT ON SEQUENCES TO service_app;
SQL
