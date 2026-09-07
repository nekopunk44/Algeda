#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repository_root="$(cd -- "$script_directory/.." && pwd -P)"
compose_file="$repository_root/compose.production.yml"
environment_file="${ENV_FILE:-$repository_root/.env.production}"
backup_directory="${BACKUP_DIR:-$repository_root/db-backups}"

if [[ ! -f "$environment_file" ]]; then
    echo "Production environment file not found: ${environment_file}" >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required." >&2
    exit 1
fi

mkdir -p -- "$backup_directory"
backup_directory="$(cd -- "$backup_directory" && pwd -P)"

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

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
final_archive="$backup_directory/algeda-${timestamp}.dump"
temporary_archive="$(mktemp "$backup_directory/.algeda-${timestamp}.XXXXXX.dump")"

cleanup() {
    if [[ -n "${temporary_archive:-}" && -f "$temporary_archive" ]]; then
        rm -f -- "$temporary_archive"
    fi
}
trap cleanup EXIT

echo "Creating a PostgreSQL custom-format backup..."
"${compose[@]}" exec -T postgres sh -ceu '
    exec pg_dump \
        --username "$POSTGRES_USER" \
        --dbname "$POSTGRES_DB" \
        --format=custom \
        --compress=9 \
        --no-owner \
        --no-privileges
' >"$temporary_archive"

if [[ ! -s "$temporary_archive" ]]; then
    echo "Backup failed: the archive is empty." >&2
    exit 1
fi

"${compose[@]}" exec -T postgres pg_restore --list <"$temporary_archive" >/dev/null

chmod 600 "$temporary_archive"
mv -- "$temporary_archive" "$final_archive"
temporary_archive=""

checksum_file="${final_archive}.sha256"
if command -v sha256sum >/dev/null 2>&1; then
    (
        cd -- "$backup_directory"
        sha256sum "$(basename -- "$final_archive")" >"$(basename -- "$checksum_file")"
    )
elif command -v shasum >/dev/null 2>&1; then
    (
        cd -- "$backup_directory"
        shasum -a 256 "$(basename -- "$final_archive")" >"$(basename -- "$checksum_file")"
    )
else
    echo "Neither sha256sum nor shasum is available; refusing an unverifiable backup." >&2
    exit 1
fi
chmod 600 "$checksum_file"

echo "Backup verified and written to: ${final_archive}"
echo "Checksum written to: ${checksum_file}"
echo "Copy both files to encrypted storage outside this host."
