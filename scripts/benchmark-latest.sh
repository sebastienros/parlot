#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
# Environment inheritance also carries this selection into BenchmarkDotNet's child builds.
export ParlotBenchmarkTargetFramework=net11.0
dotnet build "$repo_dir/src/Parlot/Parlot.csproj" -c Release --disable-build-servers -m:1
dotnet build "$repo_dir/src/Samples/Samples.csproj" -c Release --disable-build-servers -m:1
dotnet build "$repo_dir/test/Parlot.Benchmarks/Parlot.Benchmarks.csproj" -c Release --disable-build-servers -m:1
dotnet run --project "$repo_dir/test/Parlot.Benchmarks/Parlot.Benchmarks.csproj" -c Release -f net11.0 --no-build -- "$@"
