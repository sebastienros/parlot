#!/usr/bin/env bash
# Clones the libraries benchmarked against Parlot at pinned commits, and points their Parlot references
# at the checkout passed to the build as $(ParlotRoot) instead of the NuGet packages.
#
# Environment: EXTERNAL_REPOS (default ./.repos), APPLY_PATCHES (patch files to apply to the clones).
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

# Optional source changes to the libraries, e.g. APPLY_PATCHES="patches/fluid-string-literal-offset.patch":
# fluid-*.patch files apply to Fluid, ncalc-*.patch files to NCalc
for patch in ${APPLY_PATCHES:-}; do
    case "$(basename "$patch")" in
        fluid-*) target="$repos/fluid" ;;
        ncalc-*) target="$repos/ncalc" ;;
        *) echo "Unknown patch target: $patch"; exit 1 ;;
    esac
    git -C "$target" apply "$(cd "$(dirname "$patch")" && pwd)/$(basename "$patch")"
    echo "applied $patch"
done
