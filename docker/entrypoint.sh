#!/usr/bin/env bash
set -euo pipefail

if [ "${DOMOLOV_BROWSER_HEADLESS:-false}" = "true" ]; then
  exec dotnet Domolov.dll "$@"
fi

exec xvfb-run -a --server-args="-screen 0 1365x900x24" dotnet Domolov.dll "$@"
