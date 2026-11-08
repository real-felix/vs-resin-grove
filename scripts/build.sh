#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 scripts/validate_release.py
"${DOTNET:-dotnet}" run --project tests/ResinGrove.Tests -c Release
"${DOTNET:-dotnet}" build src/ResinGrove -c Release -t:Package --nologo "$@"
python3 scripts/validate_release.py --package
