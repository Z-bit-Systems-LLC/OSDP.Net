#!/usr/bin/env bash
# Publishes one build of the OSDP.Net API docs on the web server. Run by the Azure DevOps Release over SSH
# as the deploy user:
#
#   deploy-api-docs.sh <html-dir> <docs-dir> <version>
#   deploy-api-docs.sh ~/incoming/osdp-net-api-17/html /var/www/osdp-net-api 5.2.0
#
# Each publish goes into <docs-dir>/releases/<version>-<timestamp>, then <docs-dir>/current is switched to
# it. Caddy serves <docs-dir>/current directly, so nothing needs restarting.
set -euo pipefail

if [[ $# -ne 3 ]]; then
    echo "usage: $0 <html-dir> <docs-dir> <version>" >&2
    exit 2
fi

src=$1 docs=$2 version=$3
keep=3

[[ -f "$src/index.html" ]] || { echo "No index.html in $src" >&2; exit 1; }
[[ -d "$docs/releases" ]] || { echo "$docs/releases missing; run the site's setup-vps.sh first" >&2; exit 1; }

release="$docs/releases/$version-$(date -u +%Y%m%d-%H%M%S)"
echo "Copying docs to $release"
mkdir "$release"
cp -R "$src/." "$release/"
# Public docs: Caddy (a different user) must be able to read everything.
chmod -R u+rwX,go+rX,go-w "$release"

ln -sfn "$release" "$docs/current.new"
mv -T "$docs/current.new" "$docs/current"
echo "Now serving $version from $release"

# Keep the newest $keep releases. Sort by modification time, not name: names start with the version, so
# 5.10.0 sorts before 5.9.0 and name order would prune the release just published.
mapfile -t old < <(ls -1dt "$docs"/releases/*/ | tail -n +$((keep + 1)))
if (( ${#old[@]} > 0 )); then
    rm -rf "${old[@]}"
fi
