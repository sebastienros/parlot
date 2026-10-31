#!/usr/bin/env bash
# Runs Parlot's own benchmarks (test/Parlot.Benchmarks) of several checkouts, ROUNDS times, reversing the order of
# the checkouts every other round, then compares them with compare.py.
#
# Usage: run-internal.sh name=/path/to/parlot [name=/path/to/parlot ...] [-- BenchmarkDotNet filters and arguments]
# Example: run-internal.sh main=/src/parlot lazy=/src/parlot-lazy -- --filter '*ExprBench.Parlot*'
#
# Environment: ROUNDS (default 2), BENCH_OUT (default ./artifacts/internal), BENCH_WARMUP, BENCH_ITERATIONS.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="${BENCH_OUT:-$here/artifacts/internal}"
rounds="${ROUNDS:-2}"

variants=()
bdn=()
while [ $# -gt 0 ]; do
    if [ "$1" = "--" ]; then shift; bdn=("$@"); break; fi
    variants+=("$1"); shift
done

if [ ${#variants[@]} -eq 0 ]; then
    sed -n '2,8p' "$0"
    exit 1
fi
[ ${#bdn[@]} -eq 0 ] && bdn=(--filter '*ExprBench.Parlot*' '*JsonBench.*_Parlot*' '*RegexBenchmarks.Parlot*' '*SqlParserBenchmarks*')

mkdir -p "$out"

for variant in "${variants[@]}"; do
    root="$(cd "${variant#*=}" && pwd)"
    echo "== Building ${variant%%=*} ($root @ $(git -C "$root" rev-parse --short HEAD))"
    # The benchmarks load the netstandard2.0 Parlot assembly as an analyzer: build everything in Release first
    dotnet build "$root/Parlot.sln" -c Release -nologo -v q
done

for round in $(seq 1 "$rounds"); do
    order=("${variants[@]}")
    if [ $((round % 2)) -eq 0 ]; then
        order=()
        for ((i = ${#variants[@]} - 1; i >= 0; i--)); do order+=("${variants[$i]}"); done
    fi

    for variant in "${order[@]}"; do
        name="${variant%%=*}"
        root="$(cd "${variant#*=}" && pwd)"
        echo "== Round $round: $name"
        rm -rf "$out/$name/round$round"
        dotnet run --project "$root/test/Parlot.Benchmarks/Parlot.Benchmarks.csproj" -c Release --no-build -- \
            --warmupCount "${BENCH_WARMUP:-6}" --iterationCount "${BENCH_ITERATIONS:-20}" --launchCount 1 \
            --memory --exporters json --artifacts "$out/$name/round$round" "${bdn[@]}" \
            > "$out-$name-round$round.log" 2>&1 || echo "!! $name failed, see $out-$name-round$round.log"
    done
done

python3 "$here/compare.py" "$out" "${variants[0]%%=*}"
