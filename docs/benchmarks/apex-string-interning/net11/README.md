# Apex-style bounded string interning investigation

This isolated candidate routes Parlot-controlled string materialization through a shared, bounded UTF-16 cache. It adapts the two-hit admission cache in [Apex PgValueCaches.cs](https://github.com/sebastienros/apex/blob/2527e58d1656d54a1ccc84516b68afbdd69753a8/src/Apex.PgClient/Internal/PgValueCaches.cs#L6). The MIT notice is retained in `StringCache.cs` and therefore in the embedded generated support source.

## Implementation and scope

The shared cache has 4,096 direct-mapped slots, rounds configured capacities to a power of two, and admits strings of at most 256 UTF-16 code units. The first miss remembers a candidate hash; the second matching miss publishes an immutable hash/string entry; later full-content matches return that instance. Collisions overwrite a slot only after two candidate misses. This is best-effort reuse, not `string.Intern`: reference identity is not guaranteed under eviction or races.

Apex hashes UTF-8 with XxHash3 and stores both bytes and a decoded string. Parlot already has UTF-16: it hashes the span's bytes directly with XxHash3 on net8+, compares against the retained string, and stores no duplicate byte/char array. Modern targets add `System.IO.Hashing` 10.0.12; the downlevel path uses allocation-free 64-bit FNV-1a over UTF-16 code units. Hash equality alone never returns an entry. Volatile publication keeps hash and value consistent under concurrent access; racing misses may allocate duplicate entries.

Covered allocation sites:

- `TextSpan.ToString()`: substrings used by identifiers, captures, quoted strings, and sample grammar callbacks.
- `Character.DecodeStringInternal()`: after decoding into stack/rented scratch storage and before allocating the output string. Equivalent escaped and unescaped values can share entries; decoding work itself still happens on every parse.
- Runtime and generated `TextLiteral` with matched-text output: now uses the same `TextSpan` materialization path. Generated quoted strings use the embedded decoder and cache.
- Numeric span polyfills on older TFMs: their unavoidable temporary input strings now use the cache. Modern numeric parsing still consumes spans directly.

Canonical text literals, whole-buffer `TextSpan` values, unescaped whole-string decoding, and parsers returning spans continue reusing their existing values. Parser construction, diagnostics, generated C# text, the unused `CharToStringTable`, and arbitrary application callbacks that allocate strings directly are outside the materialization cache.

The runtime assembly shares one cache across documents and threads. Each generated consumer assembly has its own embedded cache; runtime and generated caches do not share references. No public API is added. Modern generated consumers acquire a hashing package dependency; libraries publishing generated parsers must reference it non-privately, as documented in the [source generation guide](../../../source-generation.md). This experimental branch enables the cache by default; its internal `Disable` method is tested but is not a public configuration mechanism.

## Memory and behavior

Only copied token strings are retained, never a `TextSpan` or input document. A full cache with maximum-length strings retains approximately 2.3 MiB per runtime/generated assembly on a 64-bit runtime (entries, strings, and arrays, excluding runtime overhead variation). Entries may survive until eviction or assembly unload. Allocation counters measure newly allocated bytes per operation, not retained heap size. First use also allocates the table and two arrays; warmed benchmarks exclude that cost.

String contents and parsing/failure behavior are preserved. Reference identity can now be shared across parses where previous calls created separate strings. Strings above the limit bypass hashing; an escaped benchmark with Length=256 decodes to 257 characters and therefore exercises this bypass. The cache bounds memory but cannot eliminate hashing overhead or cache churn.

## Measurement

Baseline and candidate use identical benchmark sources and the common Sep benchmark infrastructure, without any other optimization candidate. Only net11.0 was benchmarked. Full reports and compact machine-readable measurements are under `baseline/` and `candidate/`; [all before/after deltas](comparison.md) include 99.9% confidence-interval half-widths and bytes allocated per operation.

- Baseline source: `91ffb8ee0b5a002e2e1c84338d8bddbdd3e38329` (benchmark-only commit).
- Candidate source: `3f81b9fdeef6421fedd78d27f789ccc4c8f00115`.
- Host: Apple M4 Pro ARM64, macOS 15.7.9, .NET 11 RC1 (`11.0.0-rc.1.26413.103`), SDK `11.0.100-rc.1.26413.103`, BenchmarkDotNet `0.16.0-preview.1`.
- One launch, three warmups, five 100 ms measured iterations. Runs were serial, with no task builds/tests during measurement. BenchmarkDotNet could not set high process priority. These are screening results on one architecture; small differences or overlapping intervals are inconclusive.
- 28 baseline + 28 XxHash3 candidate + 28 preliminary BCL-hash cases. Materialization batches 256 strings and reports per-string costs. Distinct=1 measures warm repeated values; Distinct=8192 cycles persistently through more values than cache slots. Length=16/256/1024 probes short, boundary, and bypass cases, both plain and escaped.
- The existing 16 SQL/JSON grammar cases compare runtime and generated parsers on fixed documents, so their caches warm across parse calls. They do not measure one-shot processes, concurrent throughput, or a stream of unique documents; the materialization churn cases expose the unique-value overhead separately.

Reproduce in this worktree with the installed .NET 11 SDK:

```sh
export DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet'
scripts/benchmark-latest.sh --filter '*ApexStringCacheBenchmarks*' '*SepGrammarBenchmarks*' \
  --warmupCount 3 --iterationCount 5 --iterationTime 100 --exporters json \
  --artifacts BenchmarkDotNet.Artifacts/apex-candidate
```

For the baseline, use the same command at the baseline source revision in a separate checkout. Default builds continue targeting the repository's regular frameworks; only the benchmark script opts in to net11.

## Validation

Full Release multi-target build: passed with zero warnings/errors, including netstandard2.0 generated consumer compilation.

- Runtime tests: 873 passed on net10.0 and 835 on net8.0.
- Source-generator tests: 244 passed, including package dependency propagation, central package management, and escaped-string cache reuse in a published Native AOT consumer.
- Standalone generated-consumer tests: 31 passed on each of net8.0 and net10.0 (62 total), with no Parlot runtime reference or unsafe compilation.
- New checks cover two-hit admission, slot replacement, zero/disabled capacities, disable, length boundaries, concurrency, exact Unicode/NUL contents, escaped/plain sharing, matched-text casing, canonical/full-buffer reuse, and failure cursor restoration. Downlevel targets are compile-verified on this host; they are not benchmarked.

## Findings and recommendation

XxHash3 is substantially better than the preliminary BCL hash, but this screen does **not support an unconditional throughput optimization**. Keep it as an allocation-focused candidate for applications with repeated values and an acceptable cache lifetime; do not merge the default-on policy solely on these timings.

- Repeated 16-character plain strings: 5.67 → 5.37 ns (-5.2%), 56 → 0 B. Repeated escaped strings also eliminate their 56 B string allocation; their +5.5% time difference is inconclusive given the intervals.
- Repeated 256-character plain strings: 24.83 → 29.97 ns (+20.7%), 536 → 0 B. Removing allocations is not automatically faster in this workload.
- Rotating 8,192 plain values: 5.86 → 10.96 ns (+87.0%) at length 16; 25.52 → 46.84 ns (+83.5%) at length 256. Allocations fall only about 13–14% because few values stay cached. Escaped short-value churn is +26.8%.
- Long plain JSON: runtime 33,744 → 13,264 B (-60.7%); generated 35,136 → 14,656 B (-58.3%). Time changes are +1.0% and +4.0%, with overlapping confidence intervals. Short JSON saves roughly 24–28% allocation; long escaped JSON saves about 6.5% because decoded values above 256 characters bypass the cache.
- SQL allocation reductions range from about 9.8% to 25.5%. Grammar time changes overall range from -3.3% to +5.3%, with overlapping confidence intervals; no clear throughput improvement is established.
- Bypass-case allocation is unchanged. Timing movement on bypass paths (including the -7.2% repeated long escaped case) must not be credited to cache hits: those values are never admitted.

The draft deliberately retains the requested default-on behavior so it can be reviewed and benchmarked. A shipping decision should weigh allocation pressure against miss overhead, the bounded retained heap, and the new hashing dependency. Capacity/length tuning, concurrent throughput, cold-start cost, and additional architectures remain unmeasured. The preliminary BCL-hash variant should be rejected for this policy.

The preliminary dependency-free variant used the BCL's ordinal span hash. Its [separate comparison](bcl-comparison.md) and `bcl-hash/` reports are retained at source `1f0acf606210b592b057bc3c8239e6939061a8fe`. A repeated plain 256-character string took 165.43 ns versus 24.83 ns without caching (+566%); long plain JSON slowed by 31–40%. This motivated testing Apex's XxHash3 algorithm itself rather than treating the BCL hash as interchangeable.
