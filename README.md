# Rotation Trainer

A Dalamud plugin for Final Fantasy XIV that drills openers and rotations into muscle memory. A trail of upcoming
abilities, with your own keybinds above each icon, pops as you use each one: practise against a striking dummy
until the opener is automatic.

## Install

1. In game, open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add `https://raw.githubusercontent.com/kabojnk/RotationTrainer/main/repo.json` (this repository's `repo.json`), tick
   **Enabled**, and save.
3. Install **Rotation Trainer** from `/xlplugins`, then open it with `/rtrain`.

Import a rotation (see `examples/`) from the Rotation library tab, pick it on the Practice tab, and start a
`/countdown` at a dummy.

## Build

Needs the .NET 10 SDK and Dalamud's dev files (installed with XIVLauncher).

| | Windows | Linux / WSL |
|---|---|---|
| Debug build | `.\build.ps1` | `./build.sh` |
| Release build | `.\build.ps1 -Release` | `./build.sh release` |
| Build and test | `.\build.ps1 -Test` | `./build.sh --test` |

The plugin is written to `src/RotationTrainer/bin/x64/<Debug|Release>/RotationTrainer.dll`; add that path under
Dev Plugin Locations to load a local build. The game-data tests also need `FFXIV_SQPACK` set to the game's
`sqpack` folder and skip without it.

## Release

The version lives in `src/RotationTrainer/RotationTrainer.csproj` (`<Version>`). To publish:

| | Windows | Linux / WSL |
|---|---|---|
| Patch (1.0.0 → 1.0.1) | `.\publish.ps1` | `./publish.sh` |
| Minor (1.0.1 → 1.1.0) | `.\publish.ps1 -Bump minor` | `./publish.sh minor` |
| Major (1.1.0 → 2.0.0) | `.\publish.ps1 -Bump major` | `./publish.sh major` |
| Try it without publishing | `.\publish.ps1 -DryRun` | `./publish.sh --dry-run` |

The first release publishes the project's version (1.0.0) as is. Each release bumps the version, builds and tests
it, updates `repo.json`, commits, tags `vX.Y.Z`, pushes `main` and the tag, and creates a GitHub release with the
plugin zip. It needs a clean `main`, an `origin` remote on GitHub, and the [GitHub CLI](https://cli.github.com)
logged in (`gh auth login`). The repository must be public for players' Dalamud to read `repo.json`.
