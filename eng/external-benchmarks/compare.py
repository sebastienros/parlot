"""Compares BenchmarkDotNet JSON reports of several variants, each run in one or more rounds.

Usage: compare.py <results dir> <baseline variant> [variant order...]

<results dir>/<variant>/round<N>/**/*-report-full.json are read. For each benchmark, the median of every round is
compared with the baseline's median of the same round, so that a drift of the machine between rounds cancels out.
The table shows the mean of those per-round ratios and their range.
"""

import json
import pathlib
import statistics
import sys

root = pathlib.Path(sys.argv[1])
baseline = sys.argv[2]
order = sys.argv[3:]

# results[variant][benchmark][round] = (median ns, allocated bytes)
results = {}

for report in root.glob("*/round*/**/*-report-full*.json"):
    variant = report.relative_to(root).parts[0]
    round_ = report.relative_to(root).parts[1]
    data = json.loads(report.read_text(encoding="utf-8-sig"))

    for benchmark in data["Benchmarks"]:
        stats = benchmark.get("Statistics")
        if not stats:
            continue
        name = f'{benchmark["Type"]}.{benchmark["Method"]}'
        if benchmark.get("Parameters"):
            name += f'({benchmark["Parameters"]})'
        allocated = (benchmark.get("Memory") or {}).get("BytesAllocatedPerOperation")
        results.setdefault(variant, {}).setdefault(name, {})[round_] = (stats["Median"], allocated)

if baseline not in results:
    sys.exit(f"No results for the baseline {baseline} in {root}")

variants = [v for v in order if v in results] or sorted(v for v in results if v != baseline)
variants = [v for v in variants if v != baseline]


def fmt_time(ns):
    for unit, scale in (("s", 1e9), ("ms", 1e6), ("us", 1e3)):
        if ns >= scale:
            return f"{ns / scale:,.2f} {unit}"
    return f"{ns:,.1f} ns"


def fmt_bytes(b):
    if b is None:
        return "?"
    return f"{b / 1024:,.1f} KB" if b >= 10240 else f"{b:,} B"


header = ["Benchmark", f"{baseline} median", f"{baseline} alloc"]
for v in variants:
    header += [f"{v} time", f"{v} alloc"]

print("| " + " | ".join(header) + " |")
print("|" + "|".join(["---"] + ["---:"] * (len(header) - 1)) + "|")

for name in sorted(results[baseline]):
    base_rounds = results[baseline][name]
    base_median = statistics.median(m for m, _ in base_rounds.values())
    base_alloc = next(iter(base_rounds.values()))[1]
    row = [name, fmt_time(base_median), fmt_bytes(base_alloc)]

    for v in variants:
        rounds = results.get(v, {}).get(name, {})
        ratios = [rounds[r][0] / base_rounds[r][0] for r in rounds if r in base_rounds]
        if not ratios:
            row += ["-", "-"]
            continue
        mean = statistics.mean(ratios)
        spread = f" ({min(ratios):.2f}-{max(ratios):.2f})" if len(ratios) > 1 else ""
        alloc = next(iter(rounds.values()))[1]
        alloc_text = fmt_bytes(alloc)
        if alloc is not None and base_alloc:
            alloc_text += f" ({alloc / base_alloc:.2f})"
        row += [f"{mean:.2f}{spread}", alloc_text]

    print("| " + " | ".join(row) + " |")

print()
print("Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.")
print("Alloc: bytes allocated per operation, with the ratio to the baseline.")
