#!/usr/bin/env bash
# Publishes a Rotation Trainer release to GitHub, for players who add this repo to Dalamud.
#
#   ./publish.sh              Next patch version (1.0.0 -> 1.0.1)
#   ./publish.sh minor        Next minor version (1.0.3 -> 1.1.0)
#   ./publish.sh major        Next major version (1.4.2 -> 2.0.0)
#   ./publish.sh --dry-run    Build, test and write repo.json only: no version change, commit, tag, push or release
#
# The first release publishes the version already in the project (1.0.0) without bumping; after that, each run
# bumps from the last released version. A release: sets the version in RotationTrainer.csproj, builds Release and
# runs the tests, writes repo.json (the Dalamud repo manifest), commits both, tags vX.Y.Z, pushes main and the
# tag, and creates a GitHub release with the plugin zip.
#
# Needs: a clean main branch that's up to date with origin/main, an "origin" remote on GitHub, the GitHub CLI (gh) logged in, python3, and what
# build.sh needs.
set -euo pipefail
cd "$(dirname "$0")"

die() { echo "publish: $*" >&2; exit 1; }

bump=patch
dry=false
for arg in "$@"; do
  case "${arg,,}" in
    patch|minor|major) bump="${arg,,}" ;;
    --dry-run|-n) dry=true ;;
    *) die "unknown option: $arg (use patch, minor, major or --dry-run)" ;;
  esac
done

csproj=src/RotationTrainer/RotationTrainer.csproj
package=src/RotationTrainer/bin/x64/Release/RotationTrainer
current=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$csproj")
[[ $current =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "expected <Version>X.Y.Z</Version> in $csproj, found '$current'"

remote=$(git remote get-url origin 2>/dev/null) \
  || die "no 'origin' remote. Create the GitHub repo, then: git remote add origin https://github.com/OWNER/REPO.git"
[[ $remote == *github.com* ]] || die "origin is not a GitHub remote: $remote"
slug=$(sed -E 's#^(https://github\.com/|git@github\.com:|ssh://git@github\.com/)##; s#\.git$##; s#/$##' <<<"$remote")
[[ $slug =~ ^[^/]+/[^/]+$ ]] || die "could not read OWNER/REPO from origin: $remote"

if ! $dry; then
  command -v gh >/dev/null || die "the GitHub CLI (gh) is not installed: https://cli.github.com"
  gh auth status >/dev/null 2>&1 || die "gh is not logged in: run 'gh auth login'"
  [[ $(git branch --show-current) == main ]] || die "switch to main first"
  [[ -z $(git status --porcelain) ]] || die "commit or stash your changes first"
  git fetch --tags origin 2>/dev/null || die "could not fetch from origin"
  # Releasing from a main that's missing merged work would publish the old code (and the push would fail after the
  # tag went up).
  behind=$(git rev-list --count HEAD..origin/main)
  [[ $behind == 0 ]] || die "main is $behind commit(s) behind origin/main: run 'git pull' first"
fi

if git rev-parse -q --verify "refs/tags/v$current" >/dev/null; then
  IFS=. read -r major minor patch <<<"$current"
  case $bump in
    major) version="$((major + 1)).0.0" ;;
    minor) version="$major.$((minor + 1)).0" ;;
    patch) version="$major.$minor.$((patch + 1))" ;;
  esac
else
  version=$current # not released yet: publish it as is
fi
tag="v$version"
git rev-parse -q --verify "refs/tags/$tag" >/dev/null && die "tag $tag already exists"
echo "Publishing $tag to $slug$($dry && echo ' (dry run)')"

restore() { $dry || git checkout -q -- "$csproj" 2>/dev/null || true; }
trap restore ERR
$dry || sed -i "s|<Version>$current</Version>|<Version>$version</Version>|" "$csproj"

rm -rf "$package"
./build.sh release --test "--version=$version"
[[ -f $package/latest.zip ]] || die "build produced no $package/latest.zip"

python3 - "$package/RotationTrainer.json" "$slug" "$tag" <<'PY'
import json, sys, time
manifest_path, slug, tag = sys.argv[1:]
with open(manifest_path, encoding="utf-8-sig") as f:
    entry = json.load(f)
download = f"https://github.com/{slug}/releases/download/{tag}/latest.zip"
entry.update({
    "RepoUrl": f"https://github.com/{slug}",
    "DownloadLinkInstall": download,
    "DownloadLinkUpdate": download,
    "DownloadLinkTesting": download,
    "IsHide": False,
    "IsTestingExclusive": False,
    "LastUpdate": int(time.time()),
})
with open("repo.json", "w", encoding="utf-8", newline="\n") as f:
    json.dump([entry], f, indent=2)
    f.write("\n")
PY
echo "Wrote repo.json"

if $dry; then
  echo "Dry run done: $package/latest.zip would be released as $tag."
  exit 0
fi

git add "$csproj" repo.json
git commit -q -m "release: $tag"
git tag -a "$tag" -m "Rotation Trainer $version"
trap - ERR
git push origin main "$tag"
gh release create "$tag" "$package/latest.zip" --repo "$slug" --title "Rotation Trainer $version" --generate-notes

echo
echo "Released $tag. Players add this repo in Dalamud (/xlsettings > Experimental > Custom Plugin Repositories):"
echo "  https://raw.githubusercontent.com/$slug/main/repo.json"
