#!/usr/bin/env bash
set -Eeuo pipefail

health_path="${1:-/health/live}"
health_host="${HEALTHCHECK_HOST:-127.0.0.1}"
health_port="${HEALTHCHECK_PORT:-8080}"

if [[ ! "$health_path" =~ ^/[A-Za-z0-9._~!$\&\'()*+,;=:@%/-]*$ ]]; then
    echo "Invalid health-check path." >&2
    exit 2
fi

exec 3<>"/dev/tcp/${health_host}/${health_port}"
printf 'GET %s HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' "$health_path" >&3

IFS= read -r status_line <&3
status_line="${status_line%$'\r'}"

case "$status_line" in
    HTTP/*" 200 "*) exit 0 ;;
    *)
        echo "Unhealthy response: ${status_line:-no response}" >&2
        exit 1
        ;;
esac
