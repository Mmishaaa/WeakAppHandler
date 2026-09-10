#!/bin/sh
# Creates the read-only role the GraphQL gateway connects with.
#
# Runs once, on an empty data directory, as POSTGRES_USER — the same role the Data Processor
# migrations run as. ALTER DEFAULT PRIVILEGES therefore covers every table that role creates
# later, which matters because at this point the schema does not exist yet.
set -e

psql --variable ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE ROLE "$GATEWAY_DB_USER" LOGIN PASSWORD '$GATEWAY_DB_PASSWORD';

    GRANT CONNECT ON DATABASE "$POSTGRES_DB" TO "$GATEWAY_DB_USER";
    GRANT USAGE ON SCHEMA public TO "$GATEWAY_DB_USER";

    GRANT SELECT ON ALL TABLES IN SCHEMA public TO "$GATEWAY_DB_USER";
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO "$GATEWAY_DB_USER";
EOSQL
