# Sep optimization study

The tables below record the initial .NET 10 study. New PR comparisons use only .NET 11 via `scripts/benchmark-latest.sh`; the shipping target matrix remains unchanged. The new grammar corpus also includes sparse escapes. See `docs/benchmarks/sep/net11/` for the new runs.

Sep source inspected at commit [`76b063ec151af051526d064e9f45ad9584e63384`](https://github.com/nietras/Sep/tree/76b063ec151af051526d064e9f45ad9584e63384), 2026-09-17.
This is an inventory of distinct implementation techniques in `src/Sep`, including writer and experimental variants, not a claim that every technique can improve a parser combinator. Paths in the tables are relative to Sep's `src/Sep`; links pin the source revision. No Sep implementation was copied into Parlot.

The study catalogs **37 techniques** and measures **95 benchmark cases**, with four cursor variants tested against runtime and generated SQL/JSON grammars. The best balanced cursor candidate improves long-token JSON by 36–40% and long-token SQL by 11–12%, but short runtime SQL measures 4.1% slower. No production hot-path change is retained; the patches and reports preserve the useful experiments.

Existing SearchValues beats the custom SIMD candidates on long token scans. String pooling and bulk unescaping help particular repetition/escape distributions but regress others. See [cursor results](#cursor-decisions-and-results) and [raw reports](#raw-results) for the measured decisions. Unbenchmarked options, including x86-specific kernels and architectural/API changes, are explicitly marked in the inventory.

## Inventory and applicability

### Scanning and SIMD

| Technique | Sep evidence | Parlot use and disposition |
|---|---|---|
| Runtime ISA selection, cached factory, override for experiments | [Internals/SepParserFactory.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepParserFactory.cs) | Parlot already delegates ISA selection to span APIs/SearchValues. Keep that until custom kernels demonstrate a win. Wider vectors are not automatically faster: Sep's ordering accounts for JIT mask-register code generation. |
| Portable Vector64/128/256/512 variants plus SSE2, AVX2, AVX-512, ARM AdvSimd | Same factory and `SepParser*` implementations | Benchmark portable Vector128 on this ARM64 host; x86-specific rankings require x86 hardware. Do not infer AVX throughput from ARM measurements. |
| Load UTF-16, saturate/clamp, narrow to bytes, compare twice as many characters | [Internals/SepParserVector128NrwCmpExtMsbTzcnt.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepParserVector128NrwCmpExtMsbTzcnt.cs) | `SepScanBenchmarks.NarrowedVectorMask`. Saturation is essential: U+0122 must not alias ASCII quote. Only appropriate for ASCII delimiter sets. |
| Combine equality masks, extract most significant bits | Same vector parser | `SepScanBenchmarks.VectorMask`; compare to BCL SearchValues for token-ending searches. |
| Visit set bits with trailing-zero count and `mask &= mask - 1` | [Internals/SepParseMask.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepParseMask.cs) | First-set-bit tested in scan kernels. Full mask enumeration could accelerate line tracking or a dedicated lexer, but would introduce extra structural state for general backtracking grammars. |
| ARM bulk movemask via masked pairwise sums; four-block loads | [Internals/SepParserAdvSimdNrwCmpOrBulkMoveMaskTzcnt.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepParserAdvSimdNrwCmpOrBulkMoveMaskTzcnt.cs), `SepParserAdvSimdLoad4xNrwCmpOrBulkMoveMaskTzcnt.cs` | Deferred: Parlot usually needs the first token terminator, not all delimiter positions in 64 characters. Would need a dedicated long-token workload to justify added ISA-specific maintenance. |
| No-special-character, separators-only, separators-and-newlines fast paths | Vector parsers, `SepParseMask.cs` | Apply the same common-case specialization to cursor advancement: skip a whole span with no CR/LF, preserving scalar handling otherwise. |
| Quote count/parity instead of a general state machine | `SepParseMask.cs` | CSV-specific. JSON uses backslash escapes and SQL has different quoting rules; cannot substitute this algorithm. |
| Padded buffers allow full vector loads and one-character lookahead at the end | Vector parsers and [SepReader.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepReader.cs) | Reject for existing string API: strings provide no readable padding contract. Experimental kernels use bounded full loads plus scalar tails. Copying input solely to obtain padding defeats zero-copy parsing. |
| BCL `IndexOfAny` fallback | [Internals/SepParserIndexOfAny.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepParserIndexOfAny.cs) | Already widely used in Scanner. Benchmark against hand-written SIMD before replacing it. |
| Parse row batches, store offsets, materialize columns later | Reader state and vector parsers | Parlot already returns `TextSpan`. A structural-index prepass changes memory and backtracking costs; deferred to an explicit lexer API, not a transparent combinator optimization. |

### C# and JIT shaping

| Technique | Sep evidence | Parlot use and disposition |
|---|---|---|
| Hoist object fields to locals, keep state in registers, write back once | Vector parsers and `SepParserIndexOfAny.cs` | Direct experiment in `Cursor.Advance(int)`; compare against original field-updating loop on full SQL/JSON grammars. |
| Precompute separator vectors and configuration outside loops | Vector parser constructors | Already standard for Parlot's `SearchValues`, seekability tables, and grammar construction. No new runtime allocation needed. |
| `Unsafe.Add`, `MemoryMarshal.GetArrayDataReference`, unaligned loads, native integer offsets | Vector parsers, `SepReaderState.cs`, `SepHash.cs` | Experimental vector loads have explicit bounds. Avoid a blanket unsafe rewrite: many Parlot loops already eliminate range checks, while malformed input must remain safe. |
| Static abstract interface strategies specialize column-end versus column-info layouts | [Internals/SepColInfo.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepColInfo.cs) | Source generation already specializes graphs. A generic runtime parser hierarchy would change the public API and increase native code size; deferred. |
| `AggressiveInlining` for selected inner helpers, `AggressiveOptimization` for large kernels | Parser implementations | Parlot already hints tiny helpers. Do not apply indiscriminately to generated combinators: graph expansion can cause code bloat and bypass useful tiered PGO. No blanket attribute change. |
| Cold paths split with `NoInlining` | [SepReaderState.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepReaderState.cs), `Internals/SepThrow.cs` | Already present through Parlot throw helpers and reset/whitespace slow paths. New splits need disassembly and measured call-site evidence. |
| `SkipLocalsInit`, stack scratch buffers | Reader joining methods, parser kernels | Parlot already uses bounded stackalloc for string decoding. `SepDecodeBenchmarks.BulkCopySkipInit` tests removing scratch zeroing; only the fully written prefix is read. |
| Unsigned bounds comparisons, power-of-two sizes, shifts for layout arithmetic | `SepStringHashPool.cs`, `SepColInfo.cs` | Already common BCL/JIT idioms; no mechanical replacement of division with shifts without evidence. |
| Type-specialized generic conversion without boxing | `SepReaderState.Parse<T>/TryParse<T>` | Parlot's modern numeric dispatch/source generation already uses static conversion. Preserve downlevel and AOT compatibility. |
| Optional csFastFloat with consumed-length checks and BCL fallback | `SepReaderState.cs` | Deferred, not measured: would add a dependency and requires numeric rounding/overflow/culture compatibility validation. JSON sample currently models string/object/array values, so it cannot assess numeric conversion. |
| Assertions/tracing removed from normal release hot paths | `Internals/SepAssert.cs`, `SepTrace.cs` | Existing release/debug discipline; no new candidate. |

### Allocation, ownership, and reuse

| Technique | Sep evidence | Parlot use and disposition |
|---|---|---|
| Ref-struct row/column/span views | [SepReader.Row.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepReader.Row.cs), `SepReader.Col.cs`, `SepReader.Cols.cs` | `TextSpan` already avoids token allocations while remaining storable. `KeepTextSpan` measures an application AST choice, not a drop-in string replacement. Retaining spans also retains input strings. |
| Reusable pooled input, metadata and row arrays, geometric growth | `SepReader.cs`, `SepReaderState.cs` | Parlot owns immutable input; no input pool necessary. Pooling parse stacks would require deterministic return on failure, exceptions and cancellation, and a new lifetime contract. Deferred. |
| Indexed typed scratch-array pool reset per row | [Internals/SepArrayPoolAccessIndexed.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepArrayPoolAccessIndexed.cs) | General combinator results escape parse scope. Cannot recycle returned collection storage; existing `HybridList<T>` avoids allocation for small lists without ownership changes. |
| Bounded string pools: shared, per-column, thread-safe, fixed-capacity | [Internals/SepStringHashPool.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepStringHashPool.cs), `SepToStringHashPool*` | Test a document-scoped dictionary keyed by TextSpan, including construction/misses. SQL identifiers/JSON keys can repeat; arbitrary string values may not. No global interning or shared mutable parser state. |
| Last-string cache, length/capacity caps, bounded collision-chain traversal | `SepStringHashPool.cs`, `SepStringHashPoolFixedCapacity.cs` | `LastStringCache` measures document-scoped last-value reuse with consecutive, cyclic, and unique tokens. A custom weak hash is not justified by a repeat-only benchmark. |
| Native-word hashing, unrolling, experimental SIMD hash variants | [Internals/SepHash.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepHash.cs) | Deferred: production pool uses equality checks and collision limits; a hash microbenchmark alone would miss collision attacks and retention costs. |
| Single-character string cache | [Internals/SepStringCache.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepStringCache.cs) | Modern string construction already has runtime optimizations; no permanent 256-entry cache added without a representative win. Canonical text parsers already reuse grammar literals. |
| Column-name access-order cache with reference equality | `SepReaderState.TryGetCachedColIndex` | CSV schema-specific. Parlot already caches whitespace at an offset and builds seekability tables. No equivalent repeated named-column lookup exists. |
| In-place unescape/trim with branch-light quote counting; lazy work on column access | [Internals/SepUnescape.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/Internals/SepUnescape.cs), reader state | Reject in-place mutation of Parlot input: immutable strings, retained spans, backtracking, and concurrent parses require stable contents. Existing decoder uses stack/pool scratch only when needed. |
| Exact-length `string.Create` and direct fill for joined output | Reader state's joining methods | `SepDecodeBenchmarks.ExactString` measures a two-pass decoder against stack/pool scratch; dense and sparse escapes expose the extra-pass cost. |
| `ISpanFormattable.TryFormat`, reusable writer buffers, span input and UTF-8 conversion | [SepWriter.Col.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepWriter.Col.cs) | Writer-only; no equivalent output formatting hot path in Parlot's parser core. |
| Custom interpolated-string handler reuses writer storage through UnsafeAccessor | Same writer file | Writer-only; no reason to depend on private BCL fields for parser diagnostics. |
| Span `params` overloads avoid temporary arrays | Reader/writer row and column APIs | Parser graphs are built once, so construction savings do not change parse throughput. C# 13 syntax is also beyond generated consumers' C# 12 minimum. |
| Synchronous completion fast path returning ValueTask; async slow method | [SepReader.IO.Async.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepReader.IO.Async.cs) | Not applicable to current synchronous in-memory parsing API. |
| Ordered parallel processing over batches with pooled state | [SepReaderExtensions.Enumeration.cs](https://github.com/nietras/Sep/blob/76b063ec151af051526d064e9f45ad9584e63384/src/Sep/SepReaderExtensions.Enumeration.cs) | Application-level independent-document parallelism is already possible. Arbitrary recursive/backtracking grammar boundaries cannot be partitioned as CSV rows. |

## Experimental method

`test/Parlot.Benchmarks/SepInspiredBenchmarks.cs` contains deterministic SQL and JSON grammar benchmarks, cursor advancement, four delimiter scan kernels, four token materialization strategies, and four escaped-string decoder variants. Every stateful cursor benchmark resets before each invocation. Cursor timing inputs use either plain text or LF every 16 characters; CR/CRLF are covered by correctness tests, not a distinct timing parameter. Setup validates scan results and grammar success. The existing whitespace benchmark had no reset; its historic table was invalid and has been removed while fixing reset and Unicode fallback offset handling.

BenchmarkDotNet runs Release/net10.0 with MemoryDiagnoser, five warmups and eight measurement iterations of 200 ms. Results include error and standard deviation; tiny timing changes are not evidence of a win. The installed SDK selected by the repository's `latestMajor` policy is 11.0.100-rc.1; the measured runtime is net10.0 (see raw reports). All comparisons use the same host/toolchain. No timing loops run concurrently intentionally; follow-up confirmation is required for noisy close results.

The SIMD microbenchmark compares the same four delimiters and the same return value. It is not a complete replacement for Scanner string parsing, which also handles escape validation and cursor semantics. `KeepTextSpan` deliberately changes the representation of application values; its allocation result is an upper bound on avoidable materialization, not a semantically interchangeable library patch. Document pooling includes cold pool construction and unique misses, avoiding a permanently warmed-cache comparison.

Each inventory item is either an experiment, already implemented, unsuitable for the existing API/grammar, or explicitly deferred. Deferred/architecture-specific options are **not benchmarked and not accepted**. This study does not establish a result for every possible Sep optimization or every CPU.

## Reproduction

Parlot base revision: `0c9e3ea89e76dad993dc6782e5fe77f91f5a3281`.

```sh
dotnet build -c Release --disable-build-servers -m:1
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release --no-build -- \
  --filter '*Sep*Benchmarks*' --warmupCount 5 --iterationCount 8 --iterationTime 200 \
  --artifacts /tmp/parlot-sep-results
```

For controlled comparisons use separate checkouts of the base revision with the same added benchmark files and the benchmark project's `AllowUnsafeBlocks` property (needed for the SkipLocalsInit experiment). The baseline uses the original Cursor. Apply one candidate patch to the base: [local state](benchmarks/sep/cursor-local.patch), [inline vector check at 16](benchmarks/sep/cursor-vector.patch), [separate helper at 64 plus local state](benchmarks/sep/cursor-refined.patch), or [separate helper at 64 with the original scalar loop](benchmarks/sep/cursor-long-only.patch). Do not stack these patches. Always rebuild **all target frameworks in Release** before measurement, so source-generated parsers execute the matching build-time library. Run measurements sequentially, without overlapping builds or tests. The initial screening run overlapped a build and was discarded; only subsequent isolated runs are used for acceptance.

`CursorBulkAdvanceTests` compares bulk advancement with individual steps for exhaustive short strings and longer spans around SIMD boundaries. It checks line, column, offset, current character and EOF, including CR/LF combinations, vertical tabs, Unicode and reset/reuse. This preserves the existing cursor semantics, including its special terminal step at EOF. Scan setup checks every delimiter location, absent matches, short tails, and Unicode values whose low byte could alias an ASCII delimiter. Decoder setup compares all variants with the current decoder across supported escape forms.

## Decoder findings

Bulk copying normal runs is a useful **workload-specific** experiment, not an unconditional replacement. At decoded length 1024 with one escape per 128 characters, bulk copying took 187 ns versus 944 ns for the current loop. With one escape per four characters it took 1,926 ns versus 933 ns. Both allocate the same 2,072-byte output string; the pool is warm in these steady-state measurements. Sparse decoding would need a measured density-aware strategy before adoption.

The exact-length two-pass implementation took 1,857 ns on the long sparse case and 4,335 ns on the long dense case. It saved no managed output allocations versus the existing pooled scratch buffer, and is rejected as implemented. SkipLocalsInit saved about 1 ns for the short sparse case but did not improve the pooled long case; no production initialization policy changed. These observations concern the implemented variants, not a proof that every possible two-pass decoder is slower.

## Search and allocation findings

On this ARM64 host, SearchValues took 34.0 ns to find the final delimiter after 1,024 ASCII characters, versus 100.3 ns for direct ushort Vector128 masks and 69.6 ns for saturated byte narrowing. Unicode results were similar. Direct masks did better on the eight-character case, but that isolated advantage is not enough to replace the BCL path. Keep the existing BCL search strategy; preserve the custom kernels as experiments, including their correctness checks.

For 256 cyclic tokens drawn from four identifiers, document pooling reduced allocations from 12,288 B to 656 B but increased time from 1,054 ns to 2,310 ns. On unique tokens it increased allocations to 40,768 B and took 7,250 ns instead of 1,131 ns. Reject automatic document pooling as implemented. This dictionary experiment measures the policy's costs; it is not a benchmark of Sep's custom hash table.

A last-string cache won for consecutive identical values: 388 ns and 48 B versus 1,124 ns and 12,288 B. It lost on cyclic/unique inputs with no allocation savings. Keep this as an application-specific option, not mutable state on shared grammar objects. Retaining TextSpan avoids all measured token-string allocations, but applications must accept the different AST representation and input lifetime. Neither option was silently applied to the SQL or JSON sample models.

## Whitespace findings

Corrected benchmarks reset the scanner on every invocation and now include 256-character runs. For one ASCII space, the current whitespace scanner took 1.02 ns versus 1.92 ns for a direct SearchValues scan. For 256 spaces the ranking reversed: 133.1 ns versus 10.1 ns. Splitting an ASCII search from a Unicode fallback took 19.4 ns on the long case, slower than the single full-set search. The whitespace-plus-newline variant still pays cursor advancement costs: 296.3 ns current versus 162.6 ns vectorized on the original cursor.

Keep the existing short-prefix strategy; do not replace all whitespace scans with SearchValues. A hybrid long-prefix implementation remains a separate follow-up candidate. These measurements use ASCII spaces only: Unicode fallback correctness was fixed in the benchmark, but Unicode throughput is not established by this table. Near-zero results on empty input are below BenchmarkDotNet's overhead resolution, not zero-cost operations.

## Cursor decisions and results

**No production cursor change is retained.** Local state batching alone improves the isolated loop but regresses short SQL. The inline vector check at 16 improves long strings but increases short-token cost. Moving the check into a helper and raising its threshold to 64 is the strongest balanced candidate, yet short runtime SQL still measures 4.1% slower. Preserving the original scalar loop does not remove the short-grammar regressions. Keep these as benchmarked patches for workload-specific adoption or further tuning; do not silently trade common short-token performance for long-token wins.

All grammar runs allocate the same bytes before and after. These are separate process/build comparisons on one ARM64 machine, not cross-platform guarantees; small differences require paired repeated runs before drawing architectural conclusions. The conservative decision is to leave runtime behavior and throughput policy unchanged.

Means in microseconds (lower is better):

| Grammar | Token chars | Original | Locals | Inline vector ≥16 | Helper ≥64 + locals | Helper ≥64, original loop |
|---|---:|---:|---:|---:|---:|---:|
| JsonRuntime | 8 | 6.624 | 6.787 | 7.075 | 6.677 | 6.994 |
| JsonGenerated | 8 | 6.151 | 6.489 | 6.631 | 5.982 | 6.382 |
| SqlRuntime | 8 | 2.601 | 2.861 | 2.829 | 2.707 | 2.705 |
| SqlGenerated | 8 | 2.417 | 2.672 | 2.643 | 2.457 | 2.546 |
| JsonRuntime | 256 | 13.358 | 12.429 | 8.813 | 8.556 | 8.370 |
| JsonGenerated | 256 | 12.707 | 11.524 | 8.198 | 7.618 | 7.940 |
| SqlRuntime | 256 | 3.316 | 3.384 | 3.053 | 2.918 | 2.958 |
| SqlGenerated | 256 | 3.708 | 3.759 | 3.396 | 3.299 | 3.168 |

The strongest candidate's changes relative to baseline:

| Grammar | Token chars | Time change | Allocated per parse |
|---|---:|---:|---:|
| JsonRuntime | 8 | +0.8% | 17.45 KB |
| JsonGenerated | 8 | -2.7% | 18.81 KB |
| SqlRuntime | 8 | +4.1% | 5.19 KB |
| SqlGenerated | 8 | +1.7% | 2.56 KB |
| JsonRuntime | 256 | -35.9% | 32.95 KB |
| JsonGenerated | 256 | -40.0% | 34.31 KB |
| SqlRuntime | 256 | -12.0% | 7.61 KB |
| SqlGenerated | 256 | -11.0% | 4.98 KB |

Isolated cursor means in nanoseconds, including reset (all allocate 0 B):

| Chars | LF every 16 chars | Original | Locals | Inline vector ≥16 | Helper ≥64 + locals | Helper ≥64, original loop |
|---:|---|---:|---:|---:|---:|---:|
| 1 | False | 1.15 | 1.07 | 1.73 | 1.25 | 1.45 |
| 1 | True | 1.21 | 1.09 | 1.79 | 1.28 | 1.44 |
| 8 | False | 5.99 | 3.02 | 3.71 | 3.01 | 6.13 |
| 8 | True | 7.96 | 3.73 | 4.47 | 3.44 | 8.01 |
| 32 | False | 21.02 | 11.54 | 20.84 | 10.75 | 21.13 |
| 32 | True | 28.33 | 12.69 | 13.98 | 11.49 | 30.43 |
| 256 | False | 167.75 | 117.56 | 19.39 | 11.68 | 11.75 |
| 256 | True | 283.93 | 96.28 | 97.95 | 101.07 | 302.41 |
| 4096 | False | 2613.50 | 1918.43 | 197.63 | 179.43 | 179.97 |
| 4096 | True | 5416.11 | 1810.77 | 1799.46 | 1735.43 | 5546.41 |

The helper-plus-locals candidate reduces newline-free advancement of 256 characters from 167.75 ns to 11.68 ns (14.4×), yet this is not a 14.4× grammar speedup. Its 32-character result also explains why the first threshold was rejected: scalar locals take 11.54 ns while the inline vector variant takes 20.84 ns.

## Retained deliverables

- The optimization inventory with source links and concrete Parlot mappings.
- 95 reproducible benchmark cases across six classes, including runtime/generated SQL and JSON.
- Four cursor patches, with matching results and correctness boundary coverage.
- The cursor differential regression test, reusable when promoting a future candidate.
- Corrected whitespace benchmark reset and Unicode fallback offset handling; removed the misleading historic table.
- No new runtime dependency, public API, cache, allocation policy, custom SIMD scanner, or production hot-path change.

## Raw results

Every directory contains BenchmarkDotNet Markdown and CSV reports with means, confidence errors, standard deviations, allocations and environment metadata:

- [Baseline grammars](benchmarks/sep/baseline/Parlot.Benchmarks.SepGrammarBenchmarks-report-github.md), [baseline cursor](benchmarks/sep/baseline/Parlot.Benchmarks.SepCursorBenchmarks-report-github.md), [corrected whitespace](benchmarks/sep/baseline/Parlot.Benchmarks.SkipWhiteSpaceBenchmarks-report-github.md).
- [Local-state grammars](benchmarks/sep/local/Parlot.Benchmarks.SepGrammarBenchmarks-report-github.md), [local-state cursor](benchmarks/sep/local/Parlot.Benchmarks.SepCursorBenchmarks-report-github.md).
- [Inline-vector grammars](benchmarks/sep/vector/Parlot.Benchmarks.SepGrammarBenchmarks-report-github.md), [inline-vector cursor](benchmarks/sep/vector/Parlot.Benchmarks.SepCursorBenchmarks-report-github.md).
- [Refined grammars](benchmarks/sep/refined/Parlot.Benchmarks.SepGrammarBenchmarks-report-github.md), [refined cursor](benchmarks/sep/refined/Parlot.Benchmarks.SepCursorBenchmarks-report-github.md).
- [Original-loop control grammars](benchmarks/sep/long-only/Parlot.Benchmarks.SepGrammarBenchmarks-report-github.md), [original-loop control cursor](benchmarks/sep/long-only/Parlot.Benchmarks.SepCursorBenchmarks-report-github.md).
- [Delimiter scans](benchmarks/sep/vector/Parlot.Benchmarks.SepScanBenchmarks-report-github.md), [string allocations](benchmarks/sep/vector/Parlot.Benchmarks.SepStringAllocationBenchmarks-report-github.md), [decoders](benchmarks/sep/vector/Parlot.Benchmarks.SepDecodeBenchmarks-report-github.md).

## Validation

Final retained tree, Release configuration:

| Check | Result |
|---|---|
| Full `dotnet build`, all TFMs, including standalone netstandard2.0 consumer | Passed, 0 warnings, 0 errors |
| Runtime tests, net10.0 | 862 passed / 864; two unchanged decimal expectation failures |
| Runtime tests, net8.0 | 825 passed / 827; the same two failures |
| Source generator tests, net10.0 | 243 passed / 243 |
| Standalone tests, net8.0 and net10.0 | 38 passed / 38 |
| Candidate cursor differential boundary tests | Passed on net10.0 for each candidate; initial inline-vector candidate also checked on net8.0 |
| Final baseline cursor differential test | Passed on both runtime targets as part of the full suites |
| Scan and decoder benchmark setup assertions | Passed for all measured cases |
| Diff whitespace and report artifact links | Passed |

The two runtime failures are `FluentTests.SwitchShouldProvidePreviousResult` (line 474) and `FluentTests.NumberParsesCustomDecimalSeparator` (line 1353). Both unchanged assertions use `(decimal)123.456`: on this compiler/toolchain the expected value is `123.45600000000000306954461848`, while parsing produces `123.456`. Runtime source and those tests are identical to the base revision in the final tree; this study does not change numeric parsing or repair these unrelated assertions. The full runtime suites are therefore **not green**.

net472 and netstandard2.0 are compile-verified on this macOS host. x86/AVX-specific performance, other CPUs, and cold-start/pool-retention behavior are not established by these measurements. The SDK chosen by the existing roll-forward setting is a preview SDK; repeat important acceptance measurements on the intended shipping toolchain and CPU before promoting a patch.
