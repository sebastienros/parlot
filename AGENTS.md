# AGENTS.md

Guidance for AI agents working in this repository. This is the single source of truth: `CLAUDE.md` and
`.github/copilot-instructions.md` only point here. Record new guidance in this file.

Parlot is a parser combinator library whose reason to exist is speed. Every change is judged on allocations
and throughput first, ergonomics second. Assume any code you touch under `src/Parlot` is on a hot path
until a benchmark says otherwise.

## Build, test, benchmark

The SDK is pinned by `global.json` (10.0.100, `rollForward: latestMajor`). Tests run on
Microsoft.Testing.Platform (also configured in `global.json`) with xunit v3.

```bash
dotnet build                       # all TFMs: net472, netstandard2.0, net8.0, net10.0 (~3s incremental)
dotnet test test/Parlot.Tests/Parlot.Tests.csproj -f net10.0
dotnet test test/Parlot.SourceGenerator.Tests/Parlot.SourceGenerator.Tests.csproj   # net10.0 only
```

Develop and validate against `net10.0` first; only widen to the other TFMs once the behaviour is right.

**`-f net10.0` does not work solution-wide.** `dotnet build -f net10.0` and `dotnet test -f net10.0` from
the root fail with `NETSDK1005`, because `Parlot.SourceGenerator` targets `netstandard2.0` only. Pass `-f`
to an individual project, or build everything without `-f`.

### Running a single test

Microsoft.Testing.Platform has no VSTest `--filter`. Use xunit v3's filters, after `--`:

```bash
dotnet test test/Parlot.Tests/Parlot.Tests.csproj -f net10.0 -- --filter-method "*.ShouldReturnElse*"
```

Faster inner loop — run the test host directly (it is an `Exe`), no MSBuild pass:

```bash
dotnet build test/Parlot.Tests/Parlot.Tests.csproj -f net10.0
test/Parlot.Tests/bin/Debug/net10.0/Parlot.Tests.exe --filter-method "*.ShouldReturnElse*"   # drop .exe on Unix
```

Also available: `--filter-class`, `--filter-namespace`, `--filter-trait`, `--filter-query`,
`--filter-not-*`, `--list-tests`. Simple filters and query filters cannot be mixed.

### Benchmarks

```bash
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- --list flat
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- --filter "*Json*"
```

`-p` no longer resolves a project directory; pass `--project` with the full `.csproj` path.

### Two things that will waste your time

- `TreatWarningsAsErrors` is on repo-wide (`Directory.Build.props`) with `AnalysisLevel=latest-Recommended`
  for `src`. An unused variable fails the build.
- `Parlot.Benchmarks` and `Parlot.SourceGenerator.Tests` load `src/Parlot/bin/$(Configuration)/netstandard2.0/Parlot.dll`
  as an `<Analyzer>`, because the generator *executes* your parser code at compile time. After changing
  `src/Parlot`, run a plain `dotnet build` in the **same configuration** before trusting generated output —
  a stale `netstandard2.0` assembly means the generator emits code from the old parser logic, and a missing
  one breaks generation outright.

## Architecture

Two layers, plus a compile-time path that mirrors the runtime one.

**Scanning layer** (`src/Parlot`): `Scanner` owns the input `string` and a `Cursor`; `TextSpan` carries
buffer + offset + length so no substring is ever allocated. `Character` is a partial class split by
technique: `Character.SearchValues.cs` for net8.0+, `Character.Mask.cs` plus the byte table in
`Character.Generated.cs` for everything below. That table is **generated** — don't hand-edit it; rerun
`CanGenerateMasks` in `test/Parlot.Tests/CharMaskGeneratorTest.cs` and take the string it builds from the
debugger.

**Combinator layer** (`src/Parlot/Fluent`): `Parser<T>` is the abstract base; the whole library is
instances of it composed into a graph. `Parsers` is the static entry point exposing the `Literals` and
`Terms` builder structs; the combinators are spread over `Parsers.*.cs` / `ParserExtensions.*.cs` by
concern. `Deferred<T>` closes recursive grammars.

`Parser<T>.Parse(ParseContext, ref ParseResult<T>)` is the hot method and carries a contract
(see `docs/writing.md`, read it before writing a parser):

- bracket the body with `context.EnterParser(this)` / `context.ExitParser(this)`;
- **on failure the cursor must be back where it started** — `Cursor.ResetPosition(start)` when this parser
  advanced it, but not when a sub-parser failed (that one already reset itself);
- write a test that asserts the cursor position is restored on failure.

### The optimization surface — three opt-in interfaces

Most of Parlot's speed comes from parsers advertising capabilities rather than from the `Parse` bodies.
A new parser type should implement each one that applies:

| Interface | Namespace | Effect |
|---|---|---|
| `ISeekable` | `Parlot.Rewriting` | Declares the first chars that can match, so `OneOf` builds a char lookup table, skips branches that cannot match, and hoists the whitespace skip. About two thirds of the parser types implement it. |
| `IRewritable<T>` | `Parlot.Rewriting` | Lets a parser replace itself with a faster equivalent when the graph is built. |
| `ISourceable` | `Parlot.SourceGeneration` | Emits C# for the source generator. Nearly every parser type implements it. **Without it, a parser silently falls back to runtime execution inside otherwise-generated parsers**, quietly losing the win. |

`ParseContext` is where per-parse state and several optimizations live: memoized whitespace skipping
(`_cacheOffset`), the loop-detection stack (`PushParserAtPosition` / `PopParserAtPosition`, a plain stack
scanned with a vectorized `IndexOf` rather than a hash set), cancellation checks throttled to every 64
parser entries, and the `OnEnterParser` / `OnExitParser` hooks. Grammars that need external state subclass
it — that is also the supported way to pass state into source-generated parsers.

### Source generation

`src/Parlot.SourceGenerator` (netstandard2.0, Roslyn 4.11) makes `[GenerateParser]`-annotated static
parameterless methods free at runtime: it loads `Parlot.dll`, executes the method at compile time to build
the graph, walks it calling `ISourceable.GenerateSource`, and emits C# interceptors that replace the call
sites. Consumers must set `<InterceptorsNamespaces>`.

- `ParserSourceGenerator.cs` (~2.3k lines) drives it; `LambdaRewriter.cs` lifts lambdas into static methods
  with `#line` mappings so breakpoints still land in the original source.
- Registries in `src/Parlot/SourceGeneration` (`LambdaRegistry`, `DeferredRegistry`, `ParserHelperRegistry`,
  `TargetFrameworkInfo`, `SourceGenerationContext`, `SourceResult`) are the emission API.
- Diagnostics are `PARLOT000`–`PARLOT015`; `PARLOT015` is the common one — **lambdas may not capture**
  (no closures). Use `static` lambdas, static fields, method groups, or a custom `ParseContext`.
- Inspect output via `EmitCompilerGeneratedFiles` (both test/benchmark projects already set it; look under
  `obj/.../GeneratedFiles`).
- The generator ships inside the Parlot NuGet package under `analyzers/dotnet/cs`, so `Parlot.csproj`
  packs it from `../Parlot.SourceGenerator/bin/$(Configuration)/netstandard2.0/`.

Full reference: `docs/source-generation.md`.

## Performance rules

- Parsers are built once and run many times: do the expensive work (lookup tables, arrays, type checks) in
  the constructor, never in `Parse`. Never construct parsers from a lambda per parse — use
  `Parsers.Select(selector, a, b)` with an index into a fixed list (`docs/writing.md`, "Parser factories").
- No allocations in `Parse`. No LINQ, no closures, no `params` arrays on hot paths; mark lambdas `static`.
- Prefer `ReadOnlySpan<char>` / `TextSpan` over `string`; `HybridList<T>` (4 items inline) over `List<T>`
  for result lists; `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on tiny hot helpers, as in
  `Character`, `Scanner` and `ParseContext`.
- Modern APIs (`SearchValues`, vectorization) go behind `#if NET8_0_OR_GREATER` with a downlevel path;
  PolySharp and `src/Parlot/Polyfill.cs` cover the rest.
- Measure. Add or extend a benchmark in `test/Parlot.Benchmarks` for any hot-path change and put the
  before/after table in the PR; the tables in `README.md` are the current baseline.

## Grammar API notes

- `Terms.*` skips whitespace and comments; `Literals.*` does not. Never wrap a `Terms` parser in
  `SkipWhiteSpace()`.
- `And()` builds **flat** tuples: `a.And(b).And(c)` yields `(char, char, char)`, not nested pairs.
  `+` is `And`, `|` is `Or`.
- Drop keywords from the AST with `SkipAnd` / `AndSkip`:
  `ifKeyword.SkipAnd(expression).AndSkip(thenKeyword).And(expression)` → `(Expression, Expression)`.
- `Optional()` always yields `Option<T>`; use `HasValue` / `TryGetValue(out …)` / `OrSome(default)`.
- `Text("hello", caseInsensitive: true)` returns the canonical text to avoid an allocation; pass
  `returnMatchedText: true` if you need the input's casing.
- `LeftAssociative` / `Unary` express operator precedence; `Named()` improves error messages.
- Samples worth reading before writing a grammar: `src/Samples/Calc`, `src/Samples/Json`, `src/Samples/Sql`.

## Conventions

- Multi-target: `net472;netstandard2.0;net8.0;net10.0`. Tests only execute on net8.0/net10.0 (source
  generator tests on net10.0), so downlevel targets are compile-verified only — be deliberate about
  `#if` branches.
- `Nullable` is enabled and `AllowUnsafeBlocks` is on for `src/Parlot`; the assembly is strong-named
  (`Parlot.snk`), test projects are not signed.
- Generated files, never hand-edited: `Character.Generated.cs` (see above) and `ParserOperatorExtensions.cs`
  (T4 output of `ParserOperatorExtensions.tt` — edit the template).
- Style is enforced by `.editorconfig`: 4-space C#, `var` everywhere, file-scoped namespaces, `_camelCase`
  private fields, no primary constructors.
- The public API ships on NuGet: mark members `[Obsolete]` rather than removing them, and add XML docs to
  new public API (`GenerateDocumentationFile` is on).
- Tests mirror the source layout and use xunit v3 `[Fact]`/`[Theory]`; cover the failure path, not just the
  match. `test/Parlot.Tests/BenchmarksTests.cs` re-runs the benchmark grammars as correctness tests
  (net10.0 only), so benchmark code must stay valid.

## Pull requests

Branch as `feature/…`, `fix/…` or `perf/…`, keep logical changes in separate commits, and before opening:
a full `dotnet build` (all TFMs) plus both test projects must be green, benchmarks included for
performance-sensitive work, and `docs/` updated when behaviour or the public API changes.
