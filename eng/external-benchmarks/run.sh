#!/usr/bin/env bash
# Builds the external benchmarks against each Parlot checkout, checks that every variant produces the same outputs
# as the first one, then runs the benchmarks ROUNDS times, reversing the order of the variants every other round
# so that a drift of the machine doesn't favor one variant. Compare the results with compare.py.
#
# Usage: run.sh name=/path/to/parlot[,/path/to/repos] [...] [-- BenchmarkDotNet arguments]
# Example: run.sh main=/src/parlot lazy=/src/parlot-lazy -- --filter '*Fluid*'
# A variant can use other clones of the libraries, e.g. patched ones made with EXTERNAL_REPOS=... setup.sh.
#
# Environment: ROUNDS (default 2), BENCH_OUT (default ./artifacts), BENCH_WARMUP, BENCH_ITERATIONS, SKIP_BUILD=1.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="${BENCH_OUT:-$here/artifacts}"
rounds="${ROUNDS:-2}"
project="$here/Parlot.ExternalBenchmarks/Parlot.ExternalBenchmarks.csproj"

variants=()
bdn=()
while [ $# -gt 0 ]; do
    if [ "$1" = "--" ]; then shift; bdn=("$@"); break; fi
    variants+=("$1"); shift
done

if [ ${#variants[@]} -eq 0 ]; then
    sed -n '2,10p' "$0"
    exit 1
fi
[ ${#bdn[@]} -eq 0 ] && bdn=(--filter '*')

[ -d "$here/.repos/fluid" ] || "$here/setup.sh"
mkdir -p "$out"

# The external repositories are shallow clones
export NBGV_GitEngine=Disabled

if [ "${SKIP_BUILD:-}" != "1" ]; then
    for variant in "${variants[@]}"; do
        name="${variant%%=*}"
        spec="${variant#*=}"
        root="$(cd "${spec%%,*}" && pwd)"
        repos="$here/.repos"
        [ "$spec" != "${spec#*,}" ] && repos="$(cd "${spec#*,}" && pwd)"
        commit="$(git -C "$root" rev-parse --short HEAD)"
        echo "== Building $name ($root @ $commit, libraries from $repos)"

        # The source generator executes the grammar factories with Parlot's netstandard2.0 assembly
        dotnet build "$root/src/Parlot/Parlot.csproj" -c Release -f netstandard2.0 -nologo -v q

        # No compiler server: the analyzer assemblies of the previous variant must not be reused
        dotnet build "$project" -c Release -nologo -v q -p:UseSharedCompilation=false \
            -p:ParlotRoot="$root" -p:ParlotCommit="$commit" -p:ParlotVariant="$name" -p:ExternalRepos="$repos"

        rm -rf "$out/bin/$name"
        mkdir -p "$out/bin/$name"
        cp -r "$here/Parlot.ExternalBenchmarks/bin/Release/net10.0/." "$out/bin/$name/"

        dotnet "$out/bin/$name/Parlot.ExternalBenchmarks.dll" --validate > "$out/validate-$name.txt"
    done
fi

first="${variants[0]%%=*}"
for variant in "${variants[@]:1}"; do
    name="${variant%%=*}"
    # The re-parse statistics after the blank line legitimately differ between variants
    if ! diff <(sed '/^$/q' "$out/validate-$first.txt" | tail -n +4) <(sed '/^$/q' "$out/validate-$name.txt" | tail -n +4) > "$out/validate-$name.diff"; then
        echo "!! $name produces different outputs than $first, see $out/validate-$name.diff"
    else
        echo "== $name produces the same outputs as $first"
    fi
done

for round in $(seq 1 "$rounds"); do
    order=("${variants[@]}")
    if [ $((round % 2)) -eq 0 ]; then
        order=()
        for ((i = ${#variants[@]} - 1; i >= 0; i--)); do order+=("${variants[$i]}"); done
    fi

    for variant in "${order[@]}"; do
        name="${variant%%=*}"
        echo "== Round $round: $name"
        rm -rf "$out/results/$name/round$round"
        dotnet "$out/bin/$name/Parlot.ExternalBenchmarks.dll" --artifacts "$out/results/$name/round$round" "${bdn[@]}" \
            > "$out/results-$name-round$round.log" 2>&1 || { echo "!! $name failed, see $out/results-$name-round$round.log"; }
    done
done

python3 "$here/compare.py" "$out/results" "$first"
