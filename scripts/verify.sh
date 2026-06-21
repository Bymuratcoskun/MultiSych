#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION="$ROOT_DIR/MultiSych.slnx"
DESKTOP_PROJECT="$ROOT_DIR/MultiSych.Desktop/MultiSych.Desktop.csproj"

echo "[verify] restore"
dotnet restore "$SOLUTION"

echo "[verify] build"
dotnet build "$DESKTOP_PROJECT"

echo "[verify] test"
dotnet test "$ROOT_DIR/MultiSych.Tests/MultiSych.Tests.csproj"

echo "[verify] publish"
dotnet publish "$DESKTOP_PROJECT"

echo "[verify] done"
