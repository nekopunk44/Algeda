#!/usr/bin/env bash
set -Eeuo pipefail

# This script is executed by the official PostgreSQL entrypoint only when a
# new, empty data volume is initialized. It intentionally gives schema changes
# to a dedicated migration role and only DML privileges to the API role.

required_variables=(
    POSTGRES_DB
    POSTGRES_USER
    ALGEDA_APP_DB_USER
    ALGEDA_APP_DB_PASSWORD_FILE
    ALGEDA_MIGRATION_DB_USER
    ALGEDA_MIGRATION_DB_PASSWORD_FILE
)

for variable_name in "${required_variables[@]}"; do
    if [[ -z "${!variable_name:-}" ]]; then
        echo "Required variable ${variable_name} is not set." >&2
        exit 1
    fi
done

identifier_pattern='^[A-Za-z_][A-Za-z0-9_]{0,62}$'
for role_name in "$POSTGRES_USER" "$ALGEDA_APP_DB_USER" "$ALGEDA_MIGRATION_DB_USER"; do
    if [[ ! "$role_name" =~ $identifier_pattern ]]; then
        echo "Invalid PostgreSQL role name: ${role_name}" >&2
        exit 1
    fi
done

if [[ "$ALGEDA_APP_DB_USER" == "$ALGEDA_MIGRATION_DB_USER" \
    || "$ALGEDA_APP_DB_USER" == "$POSTGRES_USER" \
    || "$ALGEDA_MIGRATION_DB_USER" == "$POSTGRES_USER" ]]; then
    echo "PostgreSQL admin, migration and application roles must be distinct." >&2
    exit 1
fi

read_secret() {
    local secret_file="$1"

    if [[ ! -f "$secret_file" ]]; then
        echo "Secret file does not exist: ${secret_file}" >&2
        return 1
    fi

    local secret_value
    secret_value="$(<"$secret_file")"
    if [[ -z "$secret_value" || "$secret_value" == *$'\n'* || "$secret_value" == *$'\r'* ]]; then
        echo "Secret file must contain one non-empty line: ${secret_file}" >&2
        return 1
    fi

    printf '%s' "$secret_value"
}

app_password="$(read_secret "$ALGEDA_APP_DB_PASSWORD_FILE")"
migration_password="$(read_secret "$ALGEDA_MIGRATION_DB_PASSWORD_FILE")"

psql --set=ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --set=database_name="$POSTGRES_DB" \
    --set=app_user="$ALGEDA_APP_DB_USER" \
    --set=app_password="$app_password" \
    --set=migration_user="$ALGEDA_MIGRATION_DB_USER" \
    --set=migration_password="$migration_password" <<'EOSQL'
SELECT format(
    'CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION',
    :'app_user',
    :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user')
\gexec

SELECT format(
    'CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION',
    :'migration_user',
    :'migration_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'migration_user')
\gexec

SELECT format(
    'ALTER DATABASE %I OWNER TO %I',
    :'database_name',
    :'migration_user')
\gexec

SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'database_name')
\gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'database_name', :'app_user')
\gexec
SELECT format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO %I', :'database_name', :'migration_user')
\gexec

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'app_user')
\gexec
SELECT format('GRANT USAGE, CREATE ON SCHEMA public TO %I', :'migration_user')
\gexec

SELECT format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
    :'migration_user',
    :'app_user')
\gexec
SELECT format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO %I',
    :'migration_user',
    :'app_user')
\gexec
SELECT format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO %I',
    :'migration_user',
    :'app_user')
\gexec
EOSQL

unset app_password migration_password
