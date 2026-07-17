#!/usr/bin/env bash

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

if [[ $# -gt 1 || ( $# -eq 1 && "${1:-}" != "--run" ) ]]; then
    echo "Usage: $0 [--run]" >&2
    exit 2
fi

arch="x64"
if [[ "$(uname -m)" == "arm64" ]]; then
    arch="arm64"
fi

configuration="${CONFIGURATION:-Debug}"
find "bin/$configuration" -type d -name '*.app' -prune -exec rm -rf {} + 2>/dev/null || true
dotnet restore -r "osx-$arch"
dotnet msbuild -t:BundleApp \
    -p:Configuration="$configuration" \
    -p:RuntimeIdentifier="osx-$arch" \
    -p:SelfContained=true

app_path="$(find "bin/$configuration" -type d -path '*/publish/Avalonia Control Gallery.app' -print -quit)"
if [[ -z "$app_path" ]]; then
    echo "The ControlCatalog application bundle was not created." >&2
    exit 1
fi

codesign --force --deep --sign - \
    --identifier net.avaloniaui.controlcatalog \
    "$app_path"

if [[ "${1:-}" == "--run" ]]; then
    open -n "$app_path"
fi
