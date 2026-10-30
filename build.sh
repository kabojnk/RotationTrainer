#!/usr/bin/env bash
# Builds Rotation Trainer on Linux or WSL.
#
#   ./build.sh            Debug build
#   ./build.sh release    Release build
#   ./build.sh --test     Debug build, then run the tests (add "release" for Release)
#   ./build.sh release --version=1.2.3   Build as that version instead of the one in the project
#
# Needs the .NET 10 SDK and Dalamud's dev files. DALAMUD_HOME is found automatically from XIVLauncher.Core
# (~/.xlcore) or, under WSL, a Windows XIVLauncher install; set it yourself to override. The game-data tests
# also need FFXIV_SQPACK (the game's sqpack folder) and skip without it.
set -euo pipefail
cd "$(dirname "$0")"

config=Debug
test=false
props=()
for arg in "$@"; do
  case "${arg,,}" in
    release) config=Release ;;
    debug) config=Debug ;;
    --test|-t) test=true ;;
    --version=*) props+=("-p:Version=${arg#*=}") ;;
    *) echo "Unknown option: $arg (use release, debug, --test or --version=X.Y.Z)" >&2; exit 2 ;;
  esac
done

if [[ -z "${DALAMUD_HOME:-}" ]]; then
  for candidate in "$HOME/.xlcore/dalamud/Hooks/dev" /mnt/c/Users/*/AppData/Roaming/XIVLauncher/addon/Hooks/dev; do
    if [[ -f "$candidate/Dalamud.dll" ]]; then export DALAMUD_HOME="$candidate"; break; fi
  done
fi
if [[ -z "${DALAMUD_HOME:-}" ]]; then
  echo "Dalamud dev files not found. Set DALAMUD_HOME to the folder containing Dalamud.dll." >&2
  exit 1
fi
echo "Using DALAMUD_HOME=$DALAMUD_HOME"

dotnet build RotationTrainer.sln -c "$config" ${props[@]+"${props[@]}"}
if $test; then
  [[ -z "${FFXIV_SQPACK:-}" ]] && echo "FFXIV_SQPACK not set: game-data tests will skip."
  dotnet test RotationTrainer.sln -c "$config" --no-build
fi

echo
echo "Plugin: $(pwd)/src/RotationTrainer/bin/x64/$config/RotationTrainer.dll"
