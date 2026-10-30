# Builds Rotation Trainer on Windows.
#
#   .\build.ps1                 Debug build
#   .\build.ps1 -Release        Release build
#   .\build.ps1 -Test           Debug build, then run the tests
#   .\build.ps1 -Release -Version 1.2.3   Build as that version instead of the one in the project
#
# Needs the .NET 10 SDK and Dalamud's dev files (installed by XIVLauncher with Dalamud enabled). DALAMUD_HOME
# defaults to %APPDATA%\XIVLauncher\addon\Hooks\dev; set it yourself to override. The game-data tests also need
# FFXIV_SQPACK (the game's sqpack folder) and skip without it.
param(
    [switch]$Release,
    [switch]$Test,
    [string]$Version
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$config = if ($Release) { 'Release' } else { 'Debug' }

if (-not $env:DALAMUD_HOME) {
    $candidate = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'
    if (Test-Path (Join-Path $candidate 'Dalamud.dll')) { $env:DALAMUD_HOME = $candidate }
}
if (-not $env:DALAMUD_HOME) {
    Write-Error 'Dalamud dev files not found. Set DALAMUD_HOME to the folder containing Dalamud.dll.'
}
Write-Host "Using DALAMUD_HOME=$env:DALAMUD_HOME"

$props = @()
if ($Version) { $props += "/p:Version=$Version" }
dotnet build RotationTrainer.sln -c $config @props
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Test) {
    if (-not $env:FFXIV_SQPACK) { Write-Host 'FFXIV_SQPACK not set: game-data tests will skip.' }
    dotnet test RotationTrainer.sln -c $config --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ''
Write-Host "Plugin: $PSScriptRoot\src\RotationTrainer\bin\x64\$config\RotationTrainer.dll"
