#!/usr/bin/env bash
set -Eeuo pipefail

# Creates deploy/secrets/ with every file referenced by .env.production.example.
# Random values are generated where possible; provider-issued values are written
# as CHANGE_ME placeholders and listed at the end. Existing files are never
# overwritten, so the script is safe to re-run.

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(dirname "$script_dir")"
secrets_dir="$script_dir/secrets"
env_file="$repo_root/.env.production"

read_env_default() {
    local name="$1" fallback="$2"
    if [[ -f "$env_file" ]]; then
        local line
        line="$(grep -E "^${name}=" "$env_file" | tail -n 1 || true)"
        if [[ -n "$line" ]]; then
            printf '%s' "${line#*=}"
            return
        fi
    fi
    printf '%s' "$fallback"
}

postgres_db="$(read_env_default POSTGRES_DB algeda)"
app_db_user="$(read_env_default ALGEDA_APP_DB_USER algeda_app)"
migration_db_user="$(read_env_default ALGEDA_MIGRATION_DB_USER algeda_migrator)"

umask 077
mkdir -p "$secrets_dir"

random_password() {
    # Alphanumeric only, so values are safe inside Npgsql connection strings.
    openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 32
}

created=()
skipped=()
placeholders=()

write_secret() {
    local file_name="$1" value="$2"
    local path="$secrets_dir/$file_name"
    if [[ -f "$path" ]]; then
        skipped+=("$file_name")
        return
    fi
    printf '%s\n' "$value" > "$path"
    created+=("$file_name")
}

write_placeholder() {
    local file_name="$1"
    if [[ -f "$secrets_dir/$file_name" ]]; then
        skipped+=("$file_name")
        return
    fi
    write_secret "$file_name" "CHANGE_ME"
    placeholders+=("$file_name")
}

app_db_password_file="$secrets_dir/app_db_password"
migration_db_password_file="$secrets_dir/migration_db_password"

write_secret postgres_password "$(random_password)"
write_secret app_db_password "$(random_password)"
write_secret migration_db_password "$(random_password)"
write_secret jwt_signing_key "$(openssl rand -base64 48)"
write_secret bootstrap_admin_password "$(random_password)"

app_db_password="$(<"$app_db_password_file")"
migration_db_password="$(<"$migration_db_password_file")"
write_secret api_connection_string \
    "Host=postgres;Port=5432;Database=${postgres_db};Username=${app_db_user};Password=${app_db_password}"
write_secret migration_connection_string \
    "Host=postgres;Port=5432;Database=${postgres_db};Username=${migration_db_user};Password=${migration_db_password}"

write_placeholder email_password
write_placeholder automapper_license_key

echo "Secrets directory: $secrets_dir"
if [[ ${#created[@]} -gt 0 ]]; then
    printf 'Created: %s\n' "${created[*]}"
fi
if [[ ${#skipped[@]} -gt 0 ]]; then
    printf 'Left untouched: %s\n' "${skipped[*]}"
fi
if [[ ${#placeholders[@]} -gt 0 ]]; then
    printf 'Fill in manually before deploying: %s\n' "${placeholders[*]}" >&2
fi
echo "The bootstrap admin password is stored in deploy/secrets/bootstrap_admin_password."
