# AGENTS.md

Guidance for AI agents working in this repository. This is the single source of truth: `CLAUDE.md` and
`.github/copilot-instructions.md` only point here. Record new guidance in this file.

Parlot is a parser combinator library whose reason to exist is speed. Every change is judged on allocations
and throughput first, ergonomics second. Assume any code you touch under `src/Parlot` is on a hot path
until a benchmark says otherwise.

## Build, test, benchmark

```bash
dotnet build                       # all TFMs: net472, netstandard2.0, net8.0, net10.0 (~3s incremental)
dotnet test test/Parlot.Tests/Parlot.Tests.csproj -f net10.0
dotnet test test/Parlot.SourceGenerator.Tests/Parlot.SourceGenerator.Tests.csproj   # net10.0 only
dotnet test test/Parlot.Standalone.Tests/Parlot.Standalone.Tests.csproj              # net8.0/net10.0; net472 on Windows, no Parlot reference
dotnet build test/Parlot.Standalone.NetStandard/Parlot.Standalone.NetStandard.csproj # netstandard2.0 generated consumer
```

Develop and validate against `net10.0` first; only widen to the other TFMs once the behaviour is right.

**`-f net10.0` does not work solution-wide.** `dotnet build -f net10.0` and `dotnet test -f net10.0` from
the root fail with `NETSDK1005`, because `Parlot.SourceGenerator` targets `netstandard2.0` only. Pass `-f`
to an individual project, or build everything without `-f`.

`Parlot.Explorer` stays out of `Parlot.sln` (its native shells need one runner per platform) and is built by
`explorer.yml`. Keep every NuGet push in `publish.yml`: the trusted-publishing policy names that file.

Tests run on Microsoft.Testing.Platform, which has no VSTest `--filter`. Use xunit v3's filters after `--`, or run
the test host directly (it is an `Exe`) to skip the MSBuild pass:

```bash
dotnet test test/Parlot.Tests/Parlot.Tests.csproj -f net10.0 -- --filter-method "*.ShouldReturnElse*"
test/Parlot.Tests/bin/Debug/net10.0/Parlot.Tests.exe --filter-method "*.ShouldReturnElse*"   # after a build; drop .exe on Unix
```

Benchmarks (`--project` needs the full `.csproj` path):

```bash
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- --filter "*Json*"
```

### Two things that will waste your time

- `TreatWarningsAsErrors` is on repo-wide with `AnalysisLevel=latest-Recommended` for `src`. An unused variable
  fails the build.
- `Parlot.Benchmarks` and `Parlot.SourceGenerator.Tests` load `src/Parlot/bin/$(Configuration)/netstandard2.0/Parlot.dll`
  as an `<Analyzer>`, because the generator *executes* your parser code at compile time. After changing
  `src/Parlot`, run a plain `dotnet build` in the **same configuration** before trusting generated output —
  a stale `netstandard2.0` assembly means the generator emits code from the old parser logic, and a missing
  one breaks generation outright.

## Architecture

`Character.Generated.cs` (the byte char table behind `Character` below net8.0) is **generated** — don't hand-edit it;
rerun `CanGenerateMasks` in `test/Parlot.Tests/CharMaskGeneratorTest.cs` with `PARLOT_CHARACTER_MASK_OUTPUT` set to
the absolute path of `src/Parlot/Character.Generated.cs`.

`Parser<T>.Parse(ParseContext, ref ParseResult<T>)` is the hot method and carries a contract
(see `docs/writing.md`, read it before writing a parser):

- bracket the body with `context.EnterParser(this)` / `context.ExitParser(this)`;
- **on failure the cursor must be back where it started** — `Cursor.ResetPosition(start)` when this parser
  advanced it, but not when a sub-parser failed (that one already reset itself);
- write a test that asserts the cursor position is restored on failure.

**Streaming** (`docs/streaming.md`) reuses that same `Parse`, for `Parse(TextReader)`/`ParseAsync` (a compacting
buffer) and `ParseManyAsync` (a window driver). Both rely on `Cursor.HitEnd`: a parser (or `ISourceable` emitter)
that fails or stops by *looking* at the end of `Cursor.Span`/`Buffer` without advancing must call `MarkHitEnd()`,
on cold end-of-buffer paths only. In compacting mode, new leaf parsers that read the cursor start with
`if (context.IsCompacting) return context.ParseToken(this, ref result);`, and parsers that reset the cursor *and read
again* (choices, optionals, loops per element, lookaheads, `Capture`) `Pin()`; sequences that reset only to report a
failure don't. `Offset`/`Position` are absolute there: index `Buffer` with `offset - BufferStart` (or
`GetSpan`/`CreateSpan`), never with `Offset`. When touching scanning or backtracking, add grammars to the differential
harness `test/Parlot.Tests/StreamingTests.cs` and to `CompactingStreamTests.cs` (pins, memory bounds).

### A new parser type

Most of Parlot's speed comes from parsers advertising capabilities rather than from the `Parse` bodies. A new parser
type implements each of these that applies:

- `ISeekable` (`Parlot.Rewriting`): the first chars that can match, so `OneOf` builds a char lookup table, skips
  branches and hoists the whitespace skip. A parser that matches a fixed ordinal text also implements the internal
  `ISeekablePrefixes`; one that copies the hints of the parser it reads first forwards them with
  `SeekableHints.Prefixes` (`docs/keyword-lookups.md`).
- A parser that takes its hints from the parsers it starts with reads them when they are asked for, through the
  internal `ForwardedHints`, never in its constructor: those can be `Deferred` parsers set later, or choices whose table
  is only complete once the grammar is. A parser has no hints when it can succeed without reading, or when skipping it
  changes the outcome, like `ElseError`.
- `ISourceable` (`Parlot.SourceGeneration`): emits C# for the source generator. Generated parsers must be
  self-contained; unsupported runtime code is rejected rather than falling back to Parlot execution.
- `ILoopAnalyzable` (internal): which parsers it invokes before reading anything and whether it can succeed without
  reading. `LoopAnalysis` uses it to skip loop detection for `Deferred` parsers it proves can't left-recurse; a type
  without it is treated as invoking any parser. Never report fewer parsers or return `false` when unsure.
- `ParseWithoutValue` / `CanParseWithoutValue` (internal): override them when the value allocates (lists) or returns
  or combines such values (sequences, choices, wrappers); same positions and parsers entered as `Parse`, no value. A
  parser that ignores the value of a parser it invokes (`Capture`, `AndSkip`'s skipped parser, `Between`'s delimiters,
  a constant `Then`) reads `CanParseWithoutValue` once, in its constructor. Add each override to
  `ParseWithoutValueTests`.
- A wrapper, a parser that never fails, or one that starts with a fixed text goes in the sets at the top of
  `GrammarAnalysis`.
- A parser that recurses (`Deferred`) calls the internal `EnsureSufficientStack()` on each entry and counts a level with
  `EnterRecursion` / `ExitRecursion` when `MaxRecursionDepth` is set; its emitter does the same, counting only when
  `SourceGenerationContext.MaxRecursionDepth` is set. A parser that would repeat itself reads in a loop instead and only
  counts the levels, like `Unary` and the prefix operators, which keep their operators in the internal `OperatorList`.

White space is the context's `WhiteSpaceSet`, a bit of `Character`'s char table: test it with
`context.MaySkipWhiteSpace(ch)` or the context's masks, never the one-argument `Character.IsWhiteSpace*`, which only
know the default set. The default set is exactly Unicode `White_Space` (asserted by `CharMaskGeneratorTest`); another
set is a new `WhiteSpaceSet` bit pair, not an edit of the default one, and the byte table is full: one more set needs a
wider table. Runtime grammars that need external state can subclass `ParseContext`; generated entry points accept
application-owned configuration parameters instead.

### Grammar analysis

`Parlot.Analysis.GrammarAnalyzer` checks grammars against `docs/grammar-rules.md`; the source generator reports
findings as `Info` diagnostics `PARLOT1xx`, and Parser Explorer adds trace rules `PARLOT2xx` (`replay.js`). A new rule
gets an id in `GrammarRule`, a section in `docs/grammar-rules.md`, positive and negative tests, and, for performance,
a pattern/fix pair in `GrammarRulesBenchmarks`. Report only what Parlot's types prove, and don't add a rule for a
rewrite that preserves what a grammar parses: do the rewrite instead.

### Source generation

Full reference: `docs/source-generation.md`. Inspect output under `obj/.../GeneratedFiles`
(`EmitCompilerGeneratedFiles` is set in the test and benchmark projects).

- The embedded runtime files and the code emitters write must compile with C# 10: no collection expressions
  (`default` for an empty span, `Array.Empty<T>()`, `new[] { ... }`), raw strings, `required`, `file` types or other
  later syntax. The generic math helpers of `Numbers.cs` need C# 11: they are compiled out under
  `PARLOT_NO_GENERIC_MATH`, and emitters check `SourceGenerationContext.SupportsStaticAbstractMembers` before
  calling them. `Parlot.Standalone.Tests` and `Parlot.Standalone.NetStandard` build with `LangVersion` 10 to catch this.
- `TextReader` entry points set `SourceGenerationContext.IsCompacting` (token loops, `CreateSpan`, pins). Emitters gate
  that code on `IsCompacting` so `string` output is unchanged.
- Subgraphs of parsers which only read the text are generated as *span code* (`SourceGeneration/SpanSource.cs`): one
  method reads a span local through a position local and moves the cursor once, when it matches. Composite emitters
  call `SpanSource.TryGenerate` first. Span code leaves the position anywhere when it jumps to a failure label, whose
  owner moves it back; it marks `HitEnd` when a check looks at the end of the span and before a callback invoked there.
  A new literal whose code only reads the text implements the internal `ISpanSourceable`.
- The furthest failure (`out int` / `out (int Offset, int Line, int Column)` overloads) is kept by the cursor
  (`Cursor.FurthestOffset`, compared in `ResetPositionNotInlined` only); span code calls `Cursor.RecordFailure` at its
  failure label. Keep any new `RecordFailure` on failure paths only.
- Leave generated helper and callback inlining to the JIT. Locally small helpers can transitively expand
  large parser graphs; do not force `AggressiveInlining` along these chains. Only the entry core retains
  a bounded hint for inlining into the public wrapper. Shared runtime helpers keep their existing hints.
- `StandaloneRuntimeSources` embeds the shared runtime files into generated consumers as internal `Parlot.Generated`
  types, compiled with `#define PARLOT_STANDALONE`. Do not fork algorithms into separately maintained copies.
- `Numbers.Reflection.cs` stays in the runtime library, not the embedded support; do not reintroduce reflection-only
  helpers into generated consumers. The package tests publish and execute a net10.0 Native AOT consumer, which needs
  the platform's native compiler toolchain.

## Performance rules

- Parsers are built once and run many times: do the expensive work (lookup tables, arrays, type checks) in
  the constructor, never in `Parse`. Never construct parsers from a lambda per parse — use
  `Parsers.Select(selector, a, b)` with an index into a fixed list (`docs/writing.md`, "Parser factories").
- No allocations in `Parse`. No LINQ, no closures, no `params` arrays on hot paths; mark lambdas `static`.
- Prefer `ReadOnlySpan<char>` / `TextSpan` over `string`; `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
  on tiny hot helpers, as in `Character`, `Scanner` and `ParseContext`.
- Accumulate list results with the internal `ListBuilder<T>`, not `List<T>` or `HybridList<T>`. Declare it as a
  local, never copy it, and call `Release()` in a `finally` block (`ListBuilderSource.ReleaseInFinally` in emitters)
  so that an exception can't leave items on its per-thread stack. Read its items through the builder after a
  callback, which can parse and grow the stack.
- No loop in a `finally` block: the JIT then compiles the method fully optimized at once, without tiering it up with
  PGO (`ExitRecursion(count)` leaves several levels at once).
- Strings of the input that are kept and repeat come from the internal `StringPool`, one per parser
  (`ToPooledString()`; emitters get its field with `SourceGenerationContext.GetStringPoolField`). The few cases a
  case-insensitive text returns go in the internal `CaseCache` instead (`GetCaseCacheField`).
- Modern APIs (`SearchValues`, vectorization) go behind `#if NET8_0_OR_GREATER` with a downlevel path;
  PolySharp and `src/Parlot/Polyfills` cover the rest.
- Measure. Add or extend a benchmark in `test/Parlot.Benchmarks` for any hot-path change and put the
  before/after table in the PR; the tables in `README.md` are the current baseline.

## Grammar API notes

- `Terms.*` skips whitespace and comments; `Literals.*` does not. Never wrap a `Terms` parser in
  `SkipWhiteSpace()`.
- `And()` builds **flat** tuples: `a.And(b).And(c)` yields `(char, char, char)`, not nested pairs.
- `Text("hello", caseInsensitive: true)` returns the canonical text to avoid an allocation; pass
  `returnMatchedText: true` if you need the input's casing.
- Samples worth reading before writing a grammar: `src/Samples/Calc`, `src/Samples/Json`, `src/Samples/Sql`.

## Conventions

- Tests execute on net8.0/net10.0, and net472 on Windows only. On other hosts, downlevel targets are
  compile-verified only — be deliberate about `#if` branches.
- Generated files, never hand-edited: `Character.Generated.cs` (see above) and `ParserOperatorExtensions.cs`
  (T4 output of `ParserOperatorExtensions.tt` — edit the template).
- Follow `.editorconfig`: `var` everywhere, file-scoped namespaces, `_camelCase` private fields, no primary
  constructors (not all of it fails the build).
- The public API ships on NuGet: mark members `[Obsolete]` rather than removing them, and add XML docs to
  new public API.
- Tests cover the failure path, not just the match. `test/Parlot.Tests/BenchmarksTests.cs` re-runs the benchmark
  grammars as correctness tests (net10.0 only), so benchmark code must stay valid.

## Pull requests

Branch as `feature/…`, `fix/…` or `perf/…`, keep logical changes in separate commits, and before opening:
a full `dotnet build` (all TFMs) plus the test projects above must be green, benchmarks included for
performance-sensitive work, and `docs/` updated when behaviour or the public API changes.
