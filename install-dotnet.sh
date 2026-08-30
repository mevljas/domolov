#!/usr/bin/env bash
set -euo pipefail
INSTALL_DIR=".dotnet"
CHANNEL="10.0"
QUALITY="ga"
VERSION=""
ROLL_FORWARD="latestFeature"
ALLOW_PRERELEASE="false"
WORKLOADS=("${@}")
ERROR_MESSAGE="Required .NET SDK not found. Run ./install-dotnet.sh (or .ps1) to install it locally."
INSTALL_SCRIPT="$(mktemp "${TMPDIR:-/tmp}/dotnet-install.XXXXXX")"
GLOBAL_JSON_TMP=""
cleanup() {
    rm -f "$INSTALL_SCRIPT"
    [ -n "$GLOBAL_JSON_TMP" ] && rm -f "$GLOBAL_JSON_TMP"
}
trap cleanup EXIT
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
INSTALL_ARGS=(--install-dir "$INSTALL_DIR")
if [ -n "$VERSION" ]; then
    INSTALL_ARGS+=(--version "$VERSION")
    ROLL_FORWARD="disable"
else
    INSTALL_ARGS+=(--channel "$CHANNEL" --quality "$QUALITY")
fi
bash "$INSTALL_SCRIPT" "${INSTALL_ARGS[@]}"
SDK_VERSION=$("$INSTALL_DIR/dotnet" --version)
if [ -f global.json ]; then
    cp global.json global.json.bak
fi
cat > global.json <<EOF
{
  "sdk": {
    "version": "$SDK_VERSION",
    "allowPrerelease": $ALLOW_PRERELEASE,
    "rollForward": "$ROLL_FORWARD",
    "paths": [".dotnet", "\$host\$"],
    "errorMessage": "$ERROR_MESSAGE"
  }
}
EOF
grep -qxF '.dotnet/' .gitignore 2>/dev/null || printf '\n.dotnet/\n' >> .gitignore
[ ${#WORKLOADS[@]} -gt 0 ] && "$INSTALL_DIR/dotnet" workload install "${WORKLOADS[@]}"
echo "Done. SDK: $SDK_VERSION"
