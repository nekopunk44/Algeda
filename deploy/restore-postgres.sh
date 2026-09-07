#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

if [[ $# -ne 1 ]]; then
    echo "Usage: RESTORE_DATABASE=algeda_restore_YYYYMMDD bash deploy/restore-postgres.sh /path/to/backup.dump" >&2
    exit 2
fi

backup_archive="$1"
if [[ ! -f "$backup_archive" || ! -s "$backup_archive" ]]; then
    echo "Backup archive does not exist or is empty: ${backup_archive}" >&2
    exit 1
fi

backup_directory="$(cd -- "$(dirname -- "$backup_archive")" && pwd -P)"
backup_archive="$backup_directory/$(basename -- "$backup_archive")"
checksum_file="${backup_archive}.sha256"

if [[ ! -f "$checksum_file" ]]; then
    echo "Checksum file not found: ${checksum_file}" >&2
    echo "Restore is refused because archive integrity cannot be verified." >&2
    exit 1
fi

if command -v sha256sum >/dev/null 2>&1; then
    (
        cd -- "$backup_directory"
        sha256sum --check --status "$(basename -- "$checksum_file")"
    )
elif command -v shasum >/dev/null 2>&1; then
    (
        cd -- "$backup_directory"
        shasum -a 256 --check "$(basename -- "$checksum_file")" >/dev/null
    )
else
    echo "Neither sha256sum nor shasum is available; restore is refused." >&2
    exit 1
fi

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repository_root="$(cd -- "$script_directory/.." && pwd -P)"
compose_file="$repository_root/compose.production.yml"
environment_file="${ENV_FILE:-$repository_root/.env.production}"
restore_database="${RESTORE_DATABASE:-algeda_restore_$(date -u +'%Y%m%d_%H%M%S')}"

if [[ ! "$restore_database" =~ ^[A-Za-z_][A-Za-z0-9_]{0,62}$ ]]; then
    echo "RESTORE_DATABASE must be a valid PostgreSQL identifier with at most 63 characters." >&2
    exit 2
fi

if [[ ! -f "$environment_file" ]]; then
    echo "Production environment file not found: ${environment_file}" >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required." >&2
    exit 1
fi

compose=(
    docker compose
    --project-directory "$repository_root"
    --env-file "$environment_file"
    --file "$compose_file"
)

if ! "${compose[@]}" ps --status running --services | grep -Fxq postgres; then
    echo "The production postgres service is not running." >&2
    exit 1
fi

source_database="$("${compose[@]}" exec -T postgres sh -ceu 'printf %s "$POSTGRES_DB"')"
source_database="${source_database%$'\r'}"
if [[ "$restore_database" == "$source_database" ]]; then
    echo "In-place restore of the active production database is intentionally prohibited." >&2
    echo "Choose a new RESTORE_DATABASE and perform an explicit connection-string cutover after validation." >&2
    exit 1
fi

"${compose[@]}" exec -T postgres pg_restore --list <"$backup_archive" >/dev/null

database_exists="$("${compose[@]}" exec -T postgres sh -ceu '
    psql \
        --username "$POSTGRES_USER" \
        --dbname postgres \
        --tuples-only \
        --no-align \
        --command "SELECT 1 FROM pg_database WHERE datname = '\''$1'\''"
' sh "$restore_database")"
database_exists="${database_exists//[[:space:]]/}"
if [[ "$database_exists" == "1" ]]; then
    echo "Target database already exists; nothing was changed: ${restore_database}" >&2
    exit 1
fi

echo "Creating isolated restore target: ${restore_database}"
"${compose[@]}" exec -T postgres sh -ceu '
    exec createdb \
        --username "$POSTGRES_USER" \
        --owner "$ALGEDA_MIGRATION_DB_USER" \
        --template template0 \
        "$1"
' sh "$restore_database"

"${compose[@]}" exec -T postgres sh -ceu '
    exec psql \
        --set=ON_ERROR_STOP=1 \
        --username "$POSTGRES_USER" \
        --dbname "$1" \
        --command "CREATE EXTENSION IF NOT EXISTS postgis"
' sh "$restore_database"

echo "Restoring the archive in a single transaction..."
if ! "${compose[@]}" exec -T postgres sh -ceu '
    export PGPASSWORD="$(<"$ALGEDA_MIGRATION_DB_PASSWORD_FILE")"
    exec pg_restore \
        --host 127.0.0.1 \
        --username "$ALGEDA_MIGRATION_DB_USER" \
        --dbname "$1" \
        --exit-on-error \
        --single-transaction \
        --no-owner \
        --no-privileges \
        --no-comments
' sh "$restore_database" <"$backup_archive"; then
    echo "Restore failed. The isolated database was left in place for investigation: ${restore_database}" >&2
    echo "The active production database was not modified." >&2
    exit 1
fi

"${compose[@]}" exec -T postgres sh -ceu '
    exec psql \
        --set=ON_ERROR_STOP=1 \
        --username "$POSTGRES_USER" \
        --dbname "$1" \
        --set=app_user="$ALGEDA_APP_DB_USER" \
        --set=migration_user="$ALGEDA_MIGRATION_DB_USER"
' sh "$restore_database" <<'EOSQL'
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'app_user')
\gexec
SELECT format('GRANT USAGE, CREATE ON SCHEMA public TO %I', :'migration_user')
\gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I', :'app_user')
\gexec
SELECT format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO %I', :'app_user')
\gexec
SELECT format('GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO %I', :'app_user')
\gexec

SELECT count(*) AS applied_migrations FROM "__EFMigrationsHistory";
EOSQL

echo "Restore completed in isolated database: ${restore_database}"
echo "The active database and application connection strings were not changed."
echo "Validate the restored database before performing the documented cutover."
