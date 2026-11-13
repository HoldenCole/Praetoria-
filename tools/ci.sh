#!/usr/bin/env bash
# Runs exactly what .github/workflows/ci.yml runs, in the same order. Run it before every push: the solution alone
# does not build the Godot layer or the web client, and a change in Core can break either.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore Praetoria.sln
dotnet build Praetoria.sln --no-restore -c Release
dotnet run --project src/Tools -c Release --no-build -- validate
python3 validate_praetoria.py
dotnet test Praetoria.sln --no-build -c Release
dotnet build src/Game/Praetoria.Game.csproj -c Release
dotnet publish src/Web/Praetoria.Web.csproj -c Release -o "${TMPDIR:-/tmp}/praetoria-web-ci"
echo "CI steps passed."
