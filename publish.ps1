# Publishes a Rotation Trainer release to GitHub, for players who add this repo to Dalamud.
#
#   .\publish.ps1                 Next patch version (1.0.0 -> 1.0.1)
#   .\publish.ps1 -Bump minor     Next minor version (1.0.3 -> 1.1.0)
#   .\publish.ps1 -Bump major     Next major version (1.4.2 -> 2.0.0)
#   .\publish.ps1 -DryRun         Build, test and write repo.json only: no version change, commit, tag, push or release
#
# The first release publishes the version already in the project (1.0.0) without bumping; after that, each run
# bumps from the last released version. A release: sets the version in RotationTrainer.csproj, builds Release and
# runs the tests, writes repo.json (the Dalamud repo manifest), commits both, tags vX.Y.Z, pushes main and the
# tag, and creates a GitHub release with the plugin zip.
#
# Needs: a clean main branch that's up to date with origin/main, an "origin" remote on GitHub, the GitHub CLI (gh) logged in, and what build.ps1 needs.
param(
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump = 'patch',
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Fail([string]$message) { Write-Host "publish: $message" -ForegroundColor Red; exit 1 }
function Invoke-Git { & git @args; if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed" } }
# Runs a native command with stderr discarded; check $LASTEXITCODE after. (Windows PowerShell turns redirected
# stderr into a terminating error under 'Stop', so it is relaxed here.)
function Quiet { $ErrorActionPreference = 'Continue'; $exe, $rest = $args; & $exe @rest 2>$null }

$csproj = 'src\RotationTrainer\RotationTrainer.csproj'
$package = 'src\RotationTrainer\bin\x64\Release\RotationTrainer'
$projectText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $csproj))
if ($projectText -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') { Fail "expected <Version>X.Y.Z</Version> in $csproj" }
$current = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
$major, $minor, $patch = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]

$remote = Quiet git remote get-url origin
if ($LASTEXITCODE -ne 0 -or -not $remote) {
    Fail "no 'origin' remote. Create the GitHub repo, then: git remote add origin https://github.com/OWNER/REPO.git"
}
if ($remote -notmatch 'github\.com[:/]([^/]+/[^/]+?)(\.git)?/?$') { Fail "could not read OWNER/REPO from origin: $remote" }
$slug = $Matches[1]

if (-not $DryRun) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail 'the GitHub CLI (gh) is not installed: https://cli.github.com' }
    Quiet gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "gh is not logged in: run 'gh auth login'" }
    if ((git branch --show-current) -ne 'main') { Fail 'switch to main first' }
    if (git status --porcelain) { Fail 'commit or stash your changes first' }
    Quiet git fetch --tags origin | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'could not fetch from origin' }
    # Releasing from a main that's missing merged work would publish the old code (and the push would fail after the
    # tag went up).
    $behind = [int](git rev-list --count HEAD..origin/main)
    if ($behind -ne 0) { Fail "main is $behind commit(s) behind origin/main: run 'git pull' first" }
}

function TagExists([string]$name) { Quiet git rev-parse -q --verify "refs/tags/$name" | Out-Null; return $LASTEXITCODE -eq 0 }

if (TagExists "v$current") {
    $version = switch ($Bump) {
        'major' { "$($major + 1).0.0" }
        'minor' { "$major.$($minor + 1).0" }
        'patch' { "$major.$minor.$($patch + 1)" }
    }
} else {
    $version = $current # not released yet: publish it as is
}
$tag = "v$version"
if (TagExists $tag) { Fail "tag $tag already exists" }
Write-Host "Publishing $tag to $slug$(if ($DryRun) { ' (dry run)' })"

try {
    if (-not $DryRun) {
        $updated = $projectText -replace "<Version>$([regex]::Escape($current))</Version>", "<Version>$version</Version>"
        [IO.File]::WriteAllText((Join-Path $PSScriptRoot $csproj), $updated)
    }

    if (Test-Path $package) { Remove-Item -Recurse -Force $package }
    & .\build.ps1 -Release -Test -Version $version
    if ($LASTEXITCODE -ne 0) { throw 'build or tests failed' }
    $zip = Join-Path $package 'latest.zip'
    if (-not (Test-Path $zip)) { throw "build produced no $zip" }

    $entry = Get-Content (Join-Path $package 'RotationTrainer.json') -Raw | ConvertFrom-Json
    $download = "https://github.com/$slug/releases/download/$tag/latest.zip"
    $extra = [ordered]@{
        RepoUrl             = "https://github.com/$slug"
        DownloadLinkInstall = $download
        DownloadLinkUpdate  = $download
        DownloadLinkTesting = $download
        IsHide              = $false
        IsTestingExclusive  = $false
        LastUpdate          = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    }
    foreach ($key in $extra.Keys) { $entry | Add-Member -NotePropertyName $key -NotePropertyValue $extra[$key] -Force }
    $json = ConvertTo-Json -InputObject @($entry) -Depth 5
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'repo.json'), ($json -replace "`r`n", "`n") + "`n")
    Write-Host 'Wrote repo.json'
} catch {
    if (-not $DryRun) { Quiet git checkout -q -- $csproj }
    Fail $_
}

if ($DryRun) {
    Write-Host "Dry run done: $package\latest.zip would be released as $tag."
    exit 0
}

Invoke-Git add $csproj repo.json
Invoke-Git commit -q -m "release: $tag"
Invoke-Git tag -a $tag -m "Rotation Trainer $version"
Invoke-Git push origin main $tag
gh release create $tag $zip --repo $slug --title "Rotation Trainer $version" --generate-notes
if ($LASTEXITCODE -ne 0) { Fail "gh release create failed; the tag $tag is pushed, so rerun: gh release create $tag $zip --repo $slug --generate-notes" }

Write-Host ''
Write-Host 'Released' $tag'. Players add this repo in Dalamud (/xlsettings > Experimental > Custom Plugin Repositories):'
Write-Host "  https://raw.githubusercontent.com/$slug/main/repo.json"
