#!/usr/bin/env bash
# HTTP probe for images without curl/wget: exits 0 when GET <path> on localhost:8080 returns 2xx.
# Usage: healthcheck [/health/ready]
set -euo pipefail

path="${1:-/health/ready}"
port="${HEALTHCHECK_PORT:-8080}"

exec 3<>"/dev/tcp/127.0.0.1/${port}"
printf 'GET %s HTTP/1.0\r\nHost: localhost\r\n\r\n' "${path}" >&3
read -r -t 5 _ status _ <&3
[[ "${status}" == 2* ]]
