# Sep optimization PRs: .NET 11

Nine independent optimization PRs use dedicated worktrees and branches. Each PR body contains every before/after timing delta, error interval and allocation result for its selected workloads. All nine are drafts: measured regressions, workload-specific benefits or explicit result-ownership choices prevent treating them as unconditional default improvements.

Merge/review the separate prerequisites in order: [#344](https://github.com/sebastienros/parlot/pull/344) (decimal test expectations), [#346](https://github.com/sebastienros/parlot/pull/346) (complete UTF-16 character table), then [#345](https://github.com/sebastienros/parlot/pull/345) (study and benchmark infrastructure). Every optimization PR targets the infrastructure branch and includes no other candidate.

| Candidate | PR | Measured cases | Decision |
|---|---|---:|---|
| Cursor state in locals | [#355](https://github.com/sebastienros/parlot/pull/355) | 26 | Long plain JSON improves about 16%; short runtime JSON regresses 7.1%. |
| Bulk cursor advancement | [#347](https://github.com/sebastienros/parlot/pull/347) | 26 | Long JSON improves 31–40%; tiny advances and short runtime SQL regress. |
| Initial quoted-string SIMD probe | [#348](https://github.com/sebastienros/parlot/pull/348) | 28 | No consistent grammar benefit; do not keep as default. |
| Bulk escape decoding | [#349](https://github.com/sebastienros/parlot/pull/349) | 20 | Long escaped JSON improves 30–32%; dense decoding regresses 9–15%. |
| Skip decoder buffer initialization | [#350](https://github.com/sebastienros/parlot/pull/350) | 20 | No convincing decoder improvement; do not keep. |
| Whitespace vector prefix | [#351](https://github.com/sebastienros/parlot/pull/351) | 26 | Long whitespace improves; common short prefixes regress. |
| Document string pool | [#352](https://github.com/sebastienros/parlot/pull/352) | 24 | Repeated inputs allocate less but run slower; unique inputs also allocate more. |
| Last short-string cache | [#353](https://github.com/sebastienros/parlot/pull/353) | 24 | Repeated plain array: 10.1% faster, 53% fewer allocated bytes; conditional only. |
| Retained JSON spans | [#354](https://github.com/sebastienros/parlot/pull/354) | 24 | Repeated plain array: 13.4% faster, 44% fewer allocated bytes; escaped arrays allocate more and results retain buffers. |

333 completed cases support these PRs, including the [115-case common baseline](baseline/README.md). All timings use **net11.0**, .NET 11 RC1, Apple M4 Pro / ARM64, and BenchmarkDotNet 0.16.0-preview.1. Normal shipping target frameworks are unchanged. Historical net10.0 study results are retained separately and are not used for these deltas.

Each worktree passes a full Release multi-target build, runtime tests on .NET 10, 243 source-generator tests, and 38 standalone tests across .NET 8/10. Compatibility tests are not performance measurements. Each candidate records its runtime-test count and measured source revision alongside the data.

These are serial screening runs: one launch, three warmups, five measurements and 100 ms iteration time. Small/noisy changes are inconclusive. Results do not establish performance on x86. The pool/cache/spans comparisons use Optimize=false/true in the same sample; they include per-document state creation, plain and escaped values, arrays and objects, and 1/4/256 distinct values. Retained input lifetime is not represented by allocated-byte counts.

## Reproduce

Use `scripts/benchmark-latest.sh --filter <patterns> --warmupCount 3 --iterationCount 5 --iterationTime 100 --exporters json` on the desired candidate branch. The opt-in script builds the library, samples and benchmark executable for net11.0 and propagates that choice to BenchmarkDotNet child builds.

| Branch | Filter patterns |
|---|---|
| `perf/cursor-local-state` | `"*SepCursorBenchmarks*" "*SepGrammarBenchmarks*"` |
| `perf/cursor-vector-advance` | `"*SepCursorBenchmarks*" "*SepGrammarBenchmarks*"` |
| `perf/quoted-string-simd` | `"*SepQuotedStringBenchmarks*" "*SepGrammarBenchmarks*"` |
| `perf/string-decode-bulk-copy` | `"*SepDecodeBenchmarks.Current*" "*SepGrammarBenchmarks*"` |
| `perf/string-decode-skip-init` | `"*SepDecodeBenchmarks.Current*" "*SepGrammarBenchmarks*"` |
| `perf/whitespace-vector-prefix` | `"*SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default*" "*SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default*" "*SepGrammarBenchmarks*"` |
| `perf/json-document-string-pool` | `"*JsonDocumentPoolParserBenchmarks*"` |
| `perf/json-last-string-cache` | `"*JsonLastStringParserBenchmarks*"` |
| `perf/json-retained-text-spans` | `"*JsonRetainedTextParserBenchmarks*"` |
