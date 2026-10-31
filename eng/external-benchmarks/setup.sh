#!/usr/bin/env bash
# Clones the libraries benchmarked against Parlot at pinned commits, and points their Parlot references
# at the checkout passed to the build as $(ParlotRoot) instead of the NuGet packages.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repos="${EXTERNAL_REPOS:-$here/.repos}"

FLUID_SHA="${FLUID_SHA:-a8d6d36d6ab49468104e2a3c89b56702fd797ef5}"
NCALC_SHA="${NCALC_SHA:-8c3a6ff48b34a896416321810a6a76ef074cfe4a}"

clone() {
    local name=$1 url=$2 sha=$3 dir="$repos/$1"

    if [ ! -d "$dir/.git" ]; then
        git init -q "$dir"
        git -C "$dir" remote add origin "$url"
    fi

    git -C "$dir" fetch -q --depth 1 origin "$sha"
    git -C "$dir" checkout -q --force FETCH_HEAD
    echo "$name: $(git -C "$dir" rev-parse HEAD)"
}

mkdir -p "$repos"
clone fluid https://github.com/sebastienros/fluid "$FLUID_SHA"
clone ncalc https://github.com/ncalc/ncalc "$NCALC_SHA"

python3 "$here/patch.py" "$repos"
