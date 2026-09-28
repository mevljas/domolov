#!/usr/bin/env bash
set -euo pipefail

if [ "${DOMOLOV_BROWSER_HEADLESS:-true}" = "true" ]; then
  exec dotnet Domolov.Api.dll "$@"
fi

if ! command -v Xvfb >/dev/null 2>&1; then
  echo "DOMOLOV_BROWSER_HEADLESS=false: headed mode needs Dockerfile.dev; this image is headless-only" >&2
  exit 1
fi

# Do not use xvfb-run as PID 1: it waits for SIGUSR1 from Xvfb, and that
# signal is not delivered to PID 1 in Docker, so the app never starts.
DISPLAY_NUM="${XVFB_DISPLAY_NUM:-99}"
export DISPLAY=":${DISPLAY_NUM}"

# Stale locks from a previous crash block Xvfb on restart.
rm -f "/tmp/.X${DISPLAY_NUM}-lock" "/tmp/.X11-unix/X${DISPLAY_NUM}"

Xvfb "${DISPLAY}" -screen 0 1365x900x24 -nolisten tcp -ac &
XVFB_PID=$!
trap 'kill "${XVFB_PID}" 2>/dev/null || true' EXIT

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

# Bash is PID 1 here (it must outlive the app to stop Xvfb), so forward stop
# signals; otherwise the app never sees SIGTERM and gets killed mid-scan.
dotnet Domolov.Api.dll "$@" &
APP_PID=$!
trap 'kill -TERM "${APP_PID}" 2>/dev/null || true' INT TERM

set +e
wait "${APP_PID}"
status=$?
# A trapped signal interrupts `wait` early; wait again for the real exit code.
if kill -0 "${APP_PID}" 2>/dev/null; then
  wait "${APP_PID}"
  status=$?
fi
exit "${status}"
