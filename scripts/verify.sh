#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DESKTOP_PROJECT="$ROOT_DIR/MultiSych.Desktop/MultiSych.Desktop.csproj"

echo "[verify] .NET SDK kontrolü"
bash "$ROOT_DIR/scripts/check-dotnet-sdk.sh"

echo "[verify] restore"
dotnet restore "$DESKTOP_PROJECT"
dotnet restore "$ROOT_DIR/MultiSych.Tests/MultiSych.Tests.csproj"

echo "[verify] build"
dotnet build "$DESKTOP_PROJECT"

echo "[verify] test"
dotnet test "$ROOT_DIR/MultiSych.Tests/MultiSych.Tests.csproj"

echo "[verify] publish"
dotnet publish "$DESKTOP_PROJECT"

echo "[verify] done"
