# Evaluation of fast parsing techniques for Parlot

Seven techniques were implemented on separate branches from `main` (8a5f6b1) and measured with Parlot's own
benchmarks and with two libraries built from source against each branch: Fluid (runtime grammar) and NCalc
(source-generated parser, plus the same grammar on the runtime). A UTF-8 input mode was not attempted, see the end.

Six of them were then merged on `ccr-2d630000-0z83na`, see [Merged branch](#merged-branch).

## Summary

Time is the ratio to `main` (lower is faster), the mean of two rounds. Allocations are per parse.

| Branch | Technique | Fluid | NCalc runtime | NCalc generated | Parlot benchmarks | Verdict |
|---|---|---|---|---|---|---|
| [`perf/lazy-line-column`](https://github.com/sebastienros/parlot/tree/perf/lazy-line-column) | Cursor tracks only its offset; line and column computed when read | **0.93–0.97** | **0.94–0.96** | **0.91–0.93** | **0.88–0.97** (SQL runtime 1.01) | Adopt, with the behavior changes below |
| [`perf/generated-local-position`](https://github.com/sebastienros/parlot/tree/perf/generated-local-position) (on lazy) | Generated "span code": text-only subgraphs inlined over a span with a local position | = lazy | = lazy | **0.89–0.91** (lazy 0.91–0.93) | Email generated **0.56** (lazy 0.94); others within ±5% of lazy | Promising; resolve the small regressions first |
| [`perf/static-loop-detection`](https://github.com/sebastienros/parlot/tree/perf/static-loop-detection) | Prove at build time that a `Deferred` can't recurse at the same offset, skip the runtime check | **0.95–0.97**, −144 B | 0.99 (detection already off) | 1.00 (no loop detection in generated code) | Runtime **0.85–0.98** on recursive grammars, −144 B | Adopt |
| [`perf/per-parse-allocations`](https://github.com/sebastienros/parlot/tree/perf/per-parse-allocations) | Smaller context; generated scanner is its own cursor | 1.00–1.02 | 0.98–1.00 | 0.97–0.98, −56 B | Small inputs **0.90–0.94** (one case 1.03), −56 to −152 B | Adopt after rebasing on static loop detection |
| [`perf/pratt-expressions`](https://github.com/sebastienros/parlot/tree/perf/pratt-expressions) | `Operators()` precedence table; automatic flattening of nested operator levels | 1.00 | flattening **0.92–0.95**; NCalc ported to `Operators()` **0.66–0.82**, −27 to −43% alloc on short expressions | flattening **0.91–0.93**; ported **0.68–0.87**, −27 to −42% alloc | Calc runtime **1.02–1.09 (slower)**, Calc generated 0.93–1.00, SQL 0.91–0.95 | Adopt the API; restrict or drop the automatic flattening |
| [`perf/pooled-lists`](https://github.com/sebastienros/parlot/tree/perf/pooled-lists) | Per-thread scratch stack, exact-size arrays for list results | 0.97–1.00, **−15% alloc** | 0.97–1.01, **−21 to −31% alloc** | 0.96–1.00, **−21 to −28% alloc** | Alloc −4 to −38%; Deep JSON generated 0.85, Big JSON runtime **1.07 (slower)** | Adopt after fixing the Big JSON regression; result type changes |
| [`perf/memoize`](https://github.com/sebastienros/parlot/tree/perf/memoize) | Opt-in `Memoize()` | 1.00–1.02 unused; **1.03–1.09 slower** applied to Fluid's repeats | 1.00 | 1.00 | 0.99–1.03 | Don't merge |

## Method

- **Machine:** x64 4-core cloud VM (Intel Xeon, AVX-512), .NET 10.0.14, BenchmarkDotNet 0.15.8. No other load during
  the timed runs.
- **Harness:** `eng/external-benchmarks` builds Fluid (a8d6d36) and NCalc (8c3a6ff) from source against each branch,
  checks that every variant produces the same Fluid renders, NCalc expression trees and error messages (with line and
  column) as `main`, then runs each variant twice, in reverse order the second time. Ratios are computed per round and
  averaged; the ranges in `eng/external-benchmarks/results/*.md` show the two rounds.
- **Libraries:** Fluid parses a 216 B and a 1.6 KB template from its benchmarks, the latter repeated 20 times (32 KB), and
  ten short templates. NCalc parses four expressions (two from its benchmarks, one using every precedence level, one long
  rule-engine style expression) with its shipping generated parser and with the same grammar on the Parlot runtime.
- **All variants passed validation**: identical outputs to `main`, including the NCalc grammar ported to `Operators()`.
- **Noise:** rounds agree within about ±2% for Fluid and Parlot's benchmarks. NCalc's generated parser is more
  sensitive to code layout: variants that run identical NCalc code (`main`/`main-offset`, `lazy`/`lazy-offset`, `memo`/`memo-fluid`)
  agree within 1%.
- **Tests:** each branch was re-built (all target frameworks) and tested independently of the agent that wrote it:
  runtime tests on net10.0 and net8.0, generator, standalone, netstandard consumer and Release builds all pass.
- **Harness issues found and fixed during the evaluation** (results above use the fixed runs): BenchmarkDotNet rebuilt the
  benchmark project it found from the current directory, so a first internal run measured `main` for every branch; and
  MSBuild skipped recompiling NCalc against a generator older than its last output, so a first external run measured
  another variant's generated NCalc parser. Both are fixed in the scripts, which now verify the assemblies of each variant.

## Techniques

### Lazy line and column (`perf/lazy-line-column`)

`Cursor` tracks only its offset. `Position` counts new lines from the last computed position (vectorized on net8+), and
every runtime parser and generator emitter saves and restores offsets (`Cursor.ResetPosition(int offset)`, new public
API). Tokens no longer count new lines; compacting refills count the lines of the text they discard.

- **Results:** the broadest win: Parlot's expression, JSON, email and generated SQL benchmarks 3–12% faster, Fluid 3–7%,
  NCalc 4–6% on the runtime and 7–9% generated. `Cursor` grows by 16 bytes (+8 B per parse measured).
- **Behavior changes to review:**
  - `ResetPosition(default)` no longer compiles (ambiguous with the new overload).
  - `ResetPosition(TextPosition)` uses only the offset: a hand-made position whose line or column doesn't match the
    text now reports the counted ones.
  - In compacting mode, a position below the backtrack floor can't be counted anymore. `ElseError` now captures its
    position while the text is buffered; other code reporting an error there gets an approximate line and column
    (documented in `docs/streaming.md`).
- **Third-party parsers** that read `Position` on hot paths still work but pay a line count. Fluid's
  `StringLiteralParser` does; switching it to `Offset` (`patches/fluid-string-literal-offset.patch`) gains about 1% more
  on Fluid with this branch and nothing on `main`, so it isn't required.

### Generated span code (`perf/generated-local-position`, stacked on lazy)

When a subgraph only reads text (chars, ordinal text, patterns, identifiers, decimal numbers, and the sequences,
choices of fewer than 8 alternatives, loops, optionals and `Then` conversions built from them), the generator emits it
as one block that reads a span with a local position and moves the cursor once on success, as .NET's Regex source
generator does. `Scanner.ReadDecimal` also counts on a span and moves once, which helps the runtime number parsers.

- **Results** against `lazy`: email generated 0.60 (103 ns → 58 ns on `main`'s scale), generated expression (big) 0.95,
  runtime expression 0.94–0.99 (number parsing), but generated expression (small) and generated SQL 3–4% slower.
  On NCalc's generated parser it adds 2% to lazy's 7–9% (0.89–0.91 against `main`).
- **JIT code size:** email parser 3,688 B in 18 methods → 2,235 B in 2 methods; NCalc-like grammar 96 → 65 methods
  (−10% bytes).
- **Risks:** 1,464 added lines in the generator; inlining duplicates shared parsers within a subgraph (capped); gotos.
- **Next steps:** find the source of the small regressions (likely duplicated white space handling or lost
  JIT inlining at subgraph boundaries), and extend span code to quoted strings.

### Static loop detection (`perf/static-loop-detection`)

Every built-in parser reports which children it can invoke before reading anything and whether it can succeed without
reading. A lazy, once-per-grammar analysis (least fixed point plus Tarjan's cycles) proves which `Deferred` parsers
can't be re-entered at the same offset; those skip the active-parser stack and its `try/finally`, and the two arrays
it allocates per parse.

- **Results:** every `Deferred` in Calc, Json, Sql and Fluid is proven. Fluid 3–5% faster, Parlot's recursive runtime
  grammars 2–15% (Deep JSON 0.85, small expression 0.90), 144 B less per parse. Generated parsers had no loop detection and are unchanged.
- **Risks:** custom parser types are treated as leaves when their fields hold only plain data (reflection, disabled
  under native AOT, where Fluid's `Primary` and `FilterExpression` would keep detection). The proof assumes callbacks
  don't invoke parsers. Setting `Deferred.Parser` takes a global lock. A public "calls no parsers" marker for custom
  parsers would remove the reflection heuristic.

### Per-parse allocations (`perf/per-parse-allocations`)

Packs `ParseContext` (104 → 96 B), keeps the first loop-detection entry in fields, and in generated code makes the
scanner its own cursor and moves loop state to a lazily allocated cold state.

- **Results:** tiny inputs gain 6–10% (expressions, email), 56–152 B less per parse; the runtime unary expression is
  3% slower. No effect on Fluid, whose
  nested recursion allocates the loop arrays anyway, or on NCalc's runtime grammar (loop detection disabled there).
  NCalc's generated parser allocates 56 B less and is 2–3% faster.
- **Overlap:** with static loop detection, most grammars never allocate the loop arrays, which makes the inline first
  entry redundant. Merge static loop detection first, then keep the context packing and the generated scanner.

### Precedence table (`perf/pratt-expressions`)

`primary.Operators()` returns a precedence-climbing parser (`.Unary`, `.Postfix`, `.LeftAssociative`,
`.RightAssociative` levels from the tightest) that reads each operator once through one choice, whatever the number of
levels, with generator support. Directly nested `LeftAssociative`/`RightAssociative`/`Unary` chains are also flattened
into one table automatically (a differential test over about 8,000 random expressions covers it).

- **Results:** NCalc ported to `Operators()` (12 levels, `patches/ncalc-pratt.patch`): runtime 30–34% faster on short
  expressions and 18% on the long one, generated parser 29–32% and 13%, allocations −27 to −43%. The automatic flattening helps
  NCalc's 4 nested levels by 5–8% on the runtime and 7–9% generated, and generated Calc by up to 7%, but makes Calc's
  2-level runtime chain 2–9% slower.
- **Decide:** the flattened parser reads the next operator before calling the factories of the levels that end there;
  only factories with side effects can observe it (`ParseContext` factories are never flattened).
- **Recommendation:** ship `Operators()`; flatten only chains of three levels or more, or not at all, after looking at the
  2-level overhead.

### Pooled list results (`perf/pooled-lists`)

List parsers accumulate on a per-thread scratch stack (nested lists share it) and allocate one exact-size array.
A parse is synchronous on one thread in every mode, including the streaming ones, which makes the thread-static
stack safe; every builder is released in a `finally`.

- **Results:** allocations −15% on Fluid, −21 to −31% on NCalc's runtime grammar, −12 to −25% on generated JSON, −38% on
  runtime SQL; generated Deep JSON 15% faster, runtime SQL 5%, but runtime Big JSON 7% slower. It also explains why
  generated JSON allocated more than the runtime: `new Dictionary(members)` enumerated `HybridList` through an iterator.
- **Compatibility:** list results are now `T[]` instead of `HybridList<T>`/`List<T>`; code casting to `List<T>` breaks.
- **Next step:** find the Big JSON runtime regression (the per-list `try/finally` and thread-static access are the
  suspects) before merging.

### Memoization (`perf/memoize`)

Counting parsers that run again where they already ran bounded the opportunity first: 7.7% of Fluid's parser entries
(cheap `{%`/`{{` tokens shared by alternatives) and 1.0% of NCalc's. No automatic mechanism is both exact and cheaper than
the token it would skip. The branch adds an opt-in `Memoize()` (per-context direct-mapped tables, failures memoized,
guards for streaming, white space and loop detection; generated code emits the inner parser).

- **Results:** unused, it costs nothing measurable. Applied to Fluid's repeated tokens, repeats drop from 7.7% to 2.0% of
  entries but parsing is 3–9% slower and allocates 25–35% more on small templates.
- **Recommendation:** don't merge; selective memoization only pays for grammars with expensive repeated subtrees, which
  neither library has.

### Not attempted: UTF-8 input

Fluid and NCalc take `string`, so they would have to transcode first, and Parlot's own measurements
(`docs/keyword-lookups.md`) show byte keyword recognition no faster than char recognition (2.85 vs 2.17–2.82 ns per
token). It would only pay for byte-oriented inputs (HTTP bodies, files) with long runs, through a generated
`ReadOnlySpan<byte>` entry point; a spike on the JSON grammar would be the way to size it.

## Merged branch

`ccr-2d630000-0z83na` merges, in this order and with the follow-ups the evaluation called for:

1. `perf/static-loop-detection`, unchanged.
2. `perf/lazy-line-column`, unchanged.
3. `perf/per-parse-allocations` without its inline first loop-detection entry, which static loop detection makes
   redundant, nor the packing of two options into the context's flags, whose bits the loop analysis uses: the byte
   cancellation counter, the generated scanner that is its own cursor, and the generated loop state in the cold state.
4. `perf/pratt-expressions` **without the automatic flattening**: `Operators()` is merged, existing chains of operator
   levels parse level by level as before (it made the calculator's runtime chain slower and called factories in a
   different order). The precedence parser saves offsets, and reports its start to the loop analysis.
5. `perf/pooled-lists`, after keeping the first four items inline in the builder (a014020): short lists no longer touch
   the thread static stack, which turned runtime Big JSON from 7% slower into 1% faster.
6. `perf/generated-local-position`, after fixing a bug of span code found while merging it (9427828): a loop in an
   enclosing loop kept the count and the items of its previous run, so `OneOrMany` could match nothing and the items
   of an outer list shared one inner list. Span code loops accumulate with the list builder.

`perf/memoize` isn't merged. Each step was built for every target framework and tested; the final state passes 4,321
runtime tests (net10.0 and net8.0), 796 generator tests and the standalone tests.

Against `main`, with the same harness (`eng/external-benchmarks/results/merged-*.md`):

| | Time | Allocations |
|---|---|---|
| Fluid | **0.85–0.89** | **−15 to −22%** |
| NCalc runtime grammar | **0.94–0.98** | 0 to −30% |
| NCalc runtime grammar ported to `Operators()` | **0.65–0.79** | 0 to −42% |
| NCalc generated parser | **0.85–0.92**, long expression **1.07** (see below) | 0 to −30% |
| NCalc generated parser ported to `Operators()` | **0.61–0.71**, long expression 0.98 | 0 to −46% |
| Parlot expressions (runtime and generated) | **0.81–0.86** | −4 to −25% |
| Parlot JSON (runtime and generated) | **0.77–0.86** | −4 to −25% |
| Parlot email (runtime / generated) | **0.91 / 0.48** | −5 / −27% |
| Parlot SQL (runtime / generated) | **0.89 / 0.94** | −38 / −20% |

**The long NCalc expression:** each branch alone was neutral or faster on it, and the slowdown appears with the merge of
pooled lists over the previous merges (0.93 before, 1.11 after, 1.07 with span code). It isn't the code the generator
emits being slower: with dynamic PGO disabled, the merged branch is faster than `main` on every NCalc expression, the
long one included (0.89), and 0.97 with tiered compilation disabled. `main`'s generated parser gains much more from
dynamic PGO on that expression (62 µs with it, 92 µs without), and the list loops, whose helpers hold a builder and a
`try`/`finally` even when they stay empty, as NCalc's operator levels do for each operand, reduce that gain. Removing the
`try`/`finally` alone recovers 4 of the 19 points. A follow-up: emit list loops whose empty path has no builder and no
exception handling, and measure with PGO on.

## Reproducing

```bash
git worktree add ../parlot-main main
git worktree add ../parlot-lazy perf/lazy-line-column
eng/external-benchmarks/run.sh main=../parlot-main lazy=../parlot-lazy
eng/external-benchmarks/run-internal.sh main=../parlot-main lazy=../parlot-lazy
```

Raw tables: `eng/external-benchmarks/results/`.
