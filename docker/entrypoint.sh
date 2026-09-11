#!/usr/bin/env bash
set -euo pipefail

if [ "${DOMOLOV_BROWSER_HEADLESS:-false}" = "true" ]; then
  exec dotnet Domolov.dll "$@"
fi

# Do not use xvfb-run as PID 1: it waits for SIGUSR1 from Xvfb, and that
# signal is not delivered to PID 1 in Docker, so the app never starts.
DISPLAY_NUM="${XVFB_DISPLAY_NUM:-99}"
export DISPLAY=":${DISPLAY_NUM}"

# Stale locks from a previous crash block Xvfb on restart.
rm -f "/tmp/.X${DISPLAY_NUM}-lock" "/tmp/.X11-unix/X${DISPLAY_NUM}"

Xvfb "${DISPLAY}" -screen 0 1365x900x24 -nolisten tcp -ac &
XVFB_PID=$!

cleanup() {
  kill "${XVFB_PID}" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

for _ in $(seq 1 100); do
  if [ -S "/tmp/.X11-unix/X${DISPLAY_NUM}" ]; then
    break
  fi
  if ! kill -0 "${XVFB_PID}" 2>/dev/null; then
    echo "Xvfb failed to start" >&2
    exit 1
  fi
  sleep 0.1
done

if [ ! -S "/tmp/.X11-unix/X${DISPLAY_NUM}" ]; then
  echo "Timed out waiting for Xvfb on ${DISPLAY}" >&2
  exit 1
fi

dotnet Domolov.dll "$@"
