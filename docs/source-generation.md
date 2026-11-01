# Source Generation Guide

Parlot source generation compiles a build-only Fluent grammar into a direct partial parsing method.
The generated parser and its support code are emitted into the consuming assembly, so application code
does not need a runtime reference to `Parlot`.

Use an SDK or IDE with a Roslyn 5.0 or later compiler host, independently of the application's runtime
target. The minimum supported SDK is .NET SDK 10.0.100. The package compatibility test pins that SDK
with roll-forward disabled in `test/Parlot.SourceGenerator.Tests/MinimumSdk/global.json`; CI installs it
explicitly on both platforms. The test treats CS9057 as an error and compiles and runs a generated parser.
When raising the minimum compiler requirement deliberately, update that manifest and this requirement
together rather than only upgrading the Roslyn package.
Generated consumers support the same target frameworks as Parlot: .NET Framework 4.7.2,
.NET Standard 2.0, .NET 8, and .NET 10. C# 10 or later is required even when targeting an older runtime, so a
project still on the language version of the .NET 6 SDK can use them; the compiler host itself must still be
Roslyn 5.0 or later. With C# 10, a .NET 8+ target parses numbers like a downlevel one, see below.
Downlevel targets use compatibility packages such as `System.Memory`, but never require the Parlot
runtime package or assembly.

## Downlevel package dependencies

The analyzer package depends on `System.Memory` 4.5.5 or later for downlevel applications, so a project
without a reference restores 4.5.5, and one that references a later version, such as 4.6.3, keeps it.
Generated parsers build against either. A project that pins the 4.5.x compatibility packages (`System.Memory`
4.5.5, `System.Buffers` 4.5.1, `System.Numerics.Vectors` 4.5.0) or `System.Runtime.CompilerServices.Unsafe`
6.0.0 gets no NU1605 downgrade. The grammar factory is compiled against the analyzer's Parlot assembly,
which is built against `System.Memory` 4.6.3: the generator lowers that assembly's references to the versions
the project references, so a project on 4.5.5 does not fail with CS1705. Shared polyfills are emitted as
C# 10-compatible internal helpers, so consuming projects do not need PolySharp. Modern targets do not acquire
these compatibility dependencies.

When publishing a library that contains generated parsers for downlevel targets, explicitly reference
`System.Memory` so it is included in your library's package dependencies:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net472' or '$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Memory" Version="4.6.3" />
</ItemGroup>
```

Any version from 4.5.5 works; reference the one your library already ships with.

Do not mark that reference `PrivateAssets="all"`. The analyzer itself should remain private, but its
private transitive dependencies are not propagated to consumers of your library. Packing reports an
error if the explicit compatibility reference is missing. With central package management, put the
version in `Directory.Packages.props` and omit `Version` on the `PackageReference`.

The factory itself is compiled against the consuming project's references. A grammar shared with a
.NET Framework or .NET Standard 2.0 target therefore cannot rely on conversions those frameworks lack,
such as the implicit `string` to `ReadOnlySpan<char>` conversion: `AnyOf` and `NoneOf` provide `string`
overloads so `Literals.AnyOf("0123456789")` builds on every target.

## Getting started

Add the analyzer package to the project that owns the grammar:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <!-- 10 or later -->
  <LangVersion>10</LangVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Parlot.SourceGenerator"
                    Version="2.0.0-preview"
                    PrivateAssets="all" />
</ItemGroup>
```

`Parlot.SourceGenerator` is a dedicated analyzer-only package. Installing the runtime `Parlot` package
does not enable source generation, and a dependency-free generated consumer should not reference it.

The package's build props and targets find `**/*.parlot.cs`, remove those files from `Compile`, and add
them as `AdditionalFiles`. Repository projects that reference the generator project directly instead of
using the package must import the same files explicitly:

```xml
<Import Project="../Parlot.SourceGenerator/build/Parlot.SourceGenerator.props" />
<Import Project="../Parlot.SourceGenerator/build/Parlot.SourceGenerator.targets" />
```

No interceptor namespace or other call-site rewriting configuration is required.

Declare the application-facing entry point in a normal `.cs` file:

```csharp
namespace MyApp;

public static partial class GreetingParser
{
    public static partial bool TryParse(
        string input,
        GreetingOptions options,
        out string value);
}

public sealed class GreetingOptions
{
    public bool Formal { get; init; }
    public string Prefix { get; init; } = "";
}
```

Define the grammar factory in a paired build-only file such as `GreetingParser.parlot.cs`:

```csharp
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace MyApp;

public static partial class GreetingParser
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<string> Build(GreetingOptions options) =>
        If(
            () => options.Formal,
            Terms.Text("Hello"),
            Terms.Text("Hi"))
        .Then(text => options.Prefix + text)
        .Eof();
}
```

Application code calls the generated method directly:

```csharp
var options = new GreetingOptions { Formal = true, Prefix = "Greeting: " };

if (GreetingParser.TryParse("Hello", options, out var greeting))
{
    Console.WriteLine(greeting);
}
```

The factory exists only in the generator's private build compilation. It is not emitted into the
application and no `Parser<T>` object or combinator graph is created when `TryParse` runs.

## Entry point contract

Each `[GenerateParser(nameof(...))]` factory must have one matching partial method declaration whose input is a
`string`, one whose input is a `TextReader`, or both.

The factory:

- is in a `.parlot.cs` file supplied as an `AdditionalFile` and excluded from `Compile`;
- is static and non-generic;
- is contained by the same top-level, non-generic partial class as the entry point;
- returns `Parlot.Fluent.Parser<T>`;
- uses only ordinary by-value configuration parameters.

The application-facing method:

- is a static, non-generic partial method returning `bool`;
- takes the input first, as a `string` or a `System.IO.TextReader` (see [TextReader entry points](#textreader-entry-points));
- takes the factory's configuration parameters next, in the same type and order;
- may take one additional by-value `CancellationToken` after configuration and before the result;
- takes `out T value` last, where `T` is the factory parser's result type, unless the
  opt-in diagnostics overload adds its diagnostics-list output afterward (see below).

Parameter names do not need to match. The entry point may be public, internal, or private as appropriate.
Results and configuration values must be BCL or application-owned types. Do not expose source-generation
implementation types such as `TextSpan`, `Option<T>`, `ParseContext`, or `ParseResult<T>` in the entry
point signature. Convert them inside the grammar:

```csharp
[GenerateParser(nameof(TryParseIdentifier))]
private static Parser<string> BuildIdentifier() =>
    Terms.Identifier()
        .Then(static span => span.ToString())
        .Eof();
```

## Parse behavior

Compatible keyword-only `OneOf`/`Or` choices can use generated length/discriminator
lookup instead of first-character dispatch. The specialization is limited to bounded
sets of ordinal ASCII-letter keywords and preserves keyword boundaries and whitespace
behavior; small, mixed, case-insensitive, and other unsupported choices keep the existing
path. See [Keyword lookup investigation](keyword-lookups.md) for eligibility, benchmarks,
and why this does not change choices into general longest-token matching.

Compatible ordinal `Text`-only choices can also use packed prefix lookup. Unlike keyword
recognition, this matches prefixes and retains the **first** successful alternative, not
the longest text. Unicode, punctuation, duplicate spellings, and newlines are supported.
Both specializations accept 8-256 nonempty literals of at most 64 characters each, with
at most 4096 characters in the vocabulary, and require consistent whitespace behavior.
Callbacks, mixed parser kinds, other comparisons, and choices outside those bounds retain
the existing dispatch. Larger keyword recognizers separate length buckets into helpers
to keep the length dispatcher small; helper inlining remains a JIT decision.

Other choices dispatch on the first char, then again on the rest of the input when at least
three of the alternatives for a char tell longer prefixes: ordinal `Text` and `Keyword`
parsers, and the parsers which start with one, like `Then`, `When`, `Error`, `Capture`,
sequences and nested choices. Only the alternatives whose prefix the input starts with are
invoked, in their order, and each one still matches its own text. Choices read from a
`TextReader` keep the first-char dispatch. See
[Keyword lookup investigation](keyword-lookups.md#prefix-dispatch-for-wrapped-and-mixed-choices).

An `Operators()` table (see [Operator tables](parsers.md#operator-tables)) is emitted as a single precedence climbing
loop: recursive local functions which look for the operator following an operand once, through the
generated choice of all the operators, instead of one helper per level that tries its own operators after each operand.
The lookup stops at a level whose operators have a value (valued `Postfix`, `LeftAssociative`, `RightAssociative` and
`Ternary`), and the case of the loop for that level parses them and passes their values to the factories.

The generated method creates its per-parse execution context and executes the emitted parser directly.
Configuration is stored in the existing per-call execution context, without a separate parser or closure
allocation. The scanner, cursor, and context use the shared runtime implementations.

- A successful parse assigns `value` and returns `true`.
- An ordinary mismatch returns `false` and assigns `default` to `value`. An overload can also return where the
  parse failed, see [Failure position](#failure-position).
- `Error` and `ElseError` throw `ParseException` internally; the direct entry point converts that exception
  to `false` and a default result.
- An explicitly declared diagnostics overload instead collects grammar errors and warnings and returns
  them, including when a failed token was absorbed by an optional or repetition parser.
- `Warning` only reports anything through the diagnostics overload.
- Exceptions thrown by application callbacks are not swallowed and propagate to the caller.
- Cancellation throws `OperationCanceledException`; it is not reported as a parse mismatch.
- A null input is rejected.
- End-of-input matching is explicit. Add `.Eof()` when trailing input must fail.
- `Terms` parsers skip configured whitespace and comments; `Literals` parsers do not. The execution context can't be
  configured, so select another white space set, such as `WithWhiteSpaceSet(WhiteSpaceSet.Ascii)` or `WhiteSpaceSet.SpaceTabCrLf`, in the grammar.
- A `Deferred` parser that is entered again at the position where it is already active (left recursion) fails
  at that position instead of overflowing the stack, as the runtime `Deferred` does. The check costs one
  comparison per entry and allocates nothing.
- Recursion that consumes input is bounded like at runtime (see
  [Limiting parser recursion](parsers.md#limiting-parser-recursion)). `Deferred`, `Recursive` and `Unary` parsers and
  the prefix operators of `Operators()` check the stack of the thread on each entry, and input nested too deeply for it
  makes the entry point return `false` instead of overflowing the stack and ending the process. The diagnostics overload
  returns the error "The input is nested too deeply to be parsed on the remaining stack." at the position where it was
  reached. `[GenerateParser(nameof(TryParse), MaxRecursionDepth = 128)]` sets a limit that doesn't depend on the stack,
  counted like `ParseContext.MaxRecursionDepth`: the entry point returns `false` when a parse exceeds it, and the
  diagnostics overload returns "The maximum parser recursion depth of 128 was exceeded.". Both fail the whole parse, a
  choice doesn't try another alternative. The code counting the levels is only generated with a limit.
- `Number<T>`, `Hexadecimal<T>`, `Octal<T>` and `Binary<T>` accept every numeric type the consuming
  target supports: on .NET 8 or later this includes `Half`, `Int128`, `UInt128`, `nint` and `nuint`, which
  the generator host itself cannot name. A .NET Framework or .NET Standard 2.0 consumer is limited to the
  primitive types, `decimal` and `BigInteger`; other types are reported as a build error. So is a .NET 8+
  consumer compiled with C# 10, which can't compile the generic math helpers (static abstract interface members
  are C# 11): its numbers are parsed with the fixed overloads, and its embedded runtime defines
  `PARLOT_NO_GENERIC_MATH`. These overloads call the type's `TryParse`, without the generic helper's fast path
  for short runs of decimal digits.
- A `Then((context, start, end, value) => ...)` callback receives the offsets of the inner match. A `Terms`
  parser reports the start of its content, after the skipped white space, as at runtime. A composite parser
  that skips white space internally (a sequence or a choice of `Terms` parsers) reports the offset where the
  generated parser was entered, before that white space, where the runtime reports the start of its first
  element.

### Non-breaking diagnostics

Declare an additional partial overload (or only this overload) with an extra final
`out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics` parameter.
The source generator implements it from the same grammar as the ordinary entry point:

```csharp
public static partial bool TryParse(string input, out MyValue value);
public static partial bool TryParse(
    string input, out MyValue value,
    out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
```

The diagnostics overload enables collection in its per-call context; the ordinary overload
retains the exception-to-`false` behavior. The returned list is empty when no grammar errors or
warnings occur, and `IsWarning` distinguishes warnings reported by `Warning(...)` from errors. The
boolean still reflects whether the grammar matched, so a successful parse can contain diagnostics
from absorbed failures. Failed choices keep only the errors from the furthest-reaching failed
alternative, while a successful later alternative discards diagnostics from earlier failed
alternatives. Generated entry points do not expose `TreatWarningsAsErrors`; callers can reject
results containing warnings instead. A `CancellationToken`, if declared, remains immediately before
`out MyValue value`. The tuple result avoids exposing the generated parser's internal runtime
support types or adding a dependency on the Parlot runtime package.

A `TextReader` entry point can return the diagnostics the same way. Each overload is generated as a
parser of its own, like the `TextReader` ones: only the overloads returning diagnostics contain the
code collecting them, so the code generated for the other overloads is unchanged, and declaring both
generates the grammar twice. The [Parser Explorer](explorer.md) traces the overloads that don't
return diagnostics.

### Failure position

Declare an overload with an extra final `out (int Offset, int Line, int Column)` parameter, or `out int` for the offset
only, to learn where a failed parse went wrong. It needs no `Error` or `ElseError` in the grammar:

```csharp
public static partial bool TryParse(string input, out MyValue value);
public static partial bool TryParse(string input, out MyValue value, out (int Offset, int Line, int Column) failure);

if (!Sql.TryParse(query, out var value, out var failure))
{
    throw new FormatException($"Syntax error at line {failure.Line}, column {failure.Column}.");
}
```

When the parse fails, `failure` is the *furthest failure*: the position where the attempt which matched the most text
stopped. For `SELECT a, b FRM t` it is the offset of `FRM`, after the white space the expected `FROM` skipped. When the
grammar fails through an `Error` or `ElseError` parser, it is the position of that error, like the runtime
`TryParse(..., out ParseError)` reports. On success it is `default`. Lines and columns start at 1 and count lines
like `TextPosition`. The names of the tuple elements are free.

The overload doesn't generate the parser again: it calls the parser generated for the overload with the same input
and without the failure, which takes the same arguments, a `CancellationToken` included (declare one of them as a
normal forwarding method otherwise). When only this overload is declared, the parser is generated for it. A string and
a `TextReader` overload each have their own parser, as above, and can both return the failure. The overloads which
return diagnostics can't also return the failure, the diagnostics having positions already.

The parsers move the cursor back when they fail after reading, and the cursor keeps the furthest offset it was moved
back from: four instructions, without a branch, on that path only. Span code covering several parsers, which doesn't
move the cursor until it matches, records the position of its failing check on its failure path. The overloads which
don't return the failure run the same code, and the line and column of the failure are only computed when they are
returned. On the JSON benchmark document, returning the failure doesn't change the time of a parse, successful or not,
nor its allocations. The furthest failure also counts
the text lookaheads such as `WhenFollowedBy` read. A `TextReader` overload returns the same failure as the `string`
one: the buffer only drops text before positions the parse read later.

### Cancellation

To enable cooperative cancellation, add a `System.Threading.CancellationToken` parameter immediately
before `out T` in the entry point. Do not add this engine parameter to the grammar factory:

```csharp
// Normal application source; the Build(GreetingOptions options) factory is unchanged.
public static partial bool TryParse(
    string input,
    GreetingOptions options,
    System.Threading.CancellationToken cancellationToken,
    out string value);
```

The generated method initializes its internal execution context with the supplied token. An already
cancelled token throws before parsing starts, and parsing checks cancellation cooperatively at parser
boundaries, throttled to every 64 checks. This includes recursive parsers, repetition, and custom
whitespace grammars. A single long scanner operation or application callback is not interrupted in the
middle of its execution.

Pass `CancellationToken.None` when cancellation is not needed, or keep the original token-free signature.
There is no additional parser, closure, or token-source allocation. Parameter names are unrestricted:
the extra parameter's type and position identify it, not its name.

A token declared in the factory remains ordinary application configuration. It is not implicitly used
for engine cancellation; a separate extra token in the entry point controls the engine. If both
token-free and cancellable public overloads are desired, declare only the cancellable one as partial
and write a normal forwarding overload that supplies `CancellationToken.None`.

### Deliberate standalone boundary

Standalone entry points expose the input string, application configuration, optional cancellation, and
the parsed value. They do not expose the internal execution context:

- No consumed offset is returned. Without `.Eof()`, a parser may successfully match a prefix, but the
  caller cannot retrieve the position where parsing stopped. Use `.Eof()` for whole-input parsing.
- No custom `ParseContext` or other execution-context option can be supplied. Factory parameters configure
  generated callbacks and branches; they do not configure the parsing engine. The recursion limit is set on the
  attribute, `MaxRecursionDepth`, for every call.
- The input is a `string` or a `TextReader`; `ReadOnlySpan<char>` and `Stream` entry points are not supported.
  Wrap a `Stream` in a `StreamReader`.

Use the normal Parlot runtime API when consumed positions or custom parse contexts are
required.

### TextReader entry points

An entry point whose input is a `System.IO.TextReader` parses a large document without reading it into a string
first, using the [compacting buffer](streaming.md#compacting-buffer) of `Parser<T>.Parse(TextReader)`:

```csharp
public static partial bool TryParse(string input, out IJson value);
public static partial bool TryParse(TextReader reader, out IJson value);
public static partial bool TryParse(TextReader reader, CancellationToken cancellationToken, out IJson value);
```

Both overloads share the factory, and the configuration and cancellation rules above apply unchanged. A factory can
have one `string` overload, one `TextReader` overload, or both; declare a second `TextReader` overload as a normal
forwarding method. The generated reader method:

- reads the first 4096 characters; when the input ends within them it is parsed like a `string`;
- otherwise refills a pooled buffer synchronously with `TextReader.Read`, dropping the text no active parser can
  return to, so the memory is bounded by the largest pending value, not by the document;
- doesn't dispose the reader;
- returns the same values and failures as the `string` overload for the same text.

A `TextSpan` value references the buffer window it was read from. Windows are immutable strings, so the value stays
valid after later refills, but it keeps its window alive: convert it with `ToString()` in the grammar to keep only the
token. Choices which aren't exclusive
and unbounded lookaheads retain their text until they complete, see [Limitations](streaming.md#limitations).

The reader overload is generated as a separate parser with the token refill and backtrack pin code. The `string`
overload of the same factory keeps its own code, but an assembly which declares any `TextReader` entry point embeds a
streaming-capable `Cursor`, whose movable buffer base can cost some throughput to generated `string` parsers of
that assembly. The 2026-10-02 `ShortRun` comparison measured roughly 1-3% higher expression means and JSON
means ranging from 3% lower to 5% higher. Most confidence intervals overlap, so this run does not establish
a consistent overhead. Keep latency-critical `string` parsers and reader parsers in separate assemblies when that matters.
Assemblies without reader entry points compile the compacting code out.

The paired generated-parser means below use the same Apple M4 Pro, .NET 10.0.11,
SDK 11.0.100-rc.1.26425.128, and deterministic JSON inputs; only the reader-enabled build changes.
Allocations were unchanged between the two builds.

| Generated parser | String-only assembly | Reader-enabled assembly | Mean change |
|---|---:|---:|---:|
| Expression small | 178.3 ns | 181.1 ns | +1.6% |
| Expression big | 1,004.2 ns | 1,022.9 ns | +1.9% |
| Expression unary | 238.6 ns | 245.2 ns | +2.8% |
| JSON big | 32.070 us | 32.40 us | +1.0% |
| JSON deep | 23.901 us | 25.14 us | +5.2% |
| JSON long | 26.640 us | 26.93 us | +1.1% |
| JSON wide | 13.593 us | 13.21 us | -2.8% |

Generated collection parsers preserve the runtime collection behavior: the first four items are kept in a local and
longer lists are accumulated on a scratch stack of the thread, shared by nested lists, and each non-empty result is one array of its exact size; an empty result is
`Array.Empty<T>()`. Public results should normally use `IReadOnlyList<T>`.

## Configuration and captures

Factory parameters are configuration values supplied on every direct method call. The generator emits the
graph once and stores the current call's values in the execution context; it does not allocate a separate parser
or rebuild the graph per parse.

Parameters may be read by inline parse-time callbacks passed to:

- `If`
- `Select`
- `Then` and `ThenElse`
- `When`
- `Switch`
- `Else`

For example:

```csharp
[GenerateParser(nameof(TryParseKeyword))]
private static Parser<string> BuildKeyword(ParserOptions options) =>
    If(
        () => options.UseLongForm,
        Literals.Text("configuration"),
        Literals.Text("config"))
    .Then(text => options.Prefix + text);
```

Both branches are generated. The predicate runs once per parse and only the selected branch is attempted.
Reference-type configuration is not cloned or frozen, so mutations made before a call are visible during
that call. Prefer immutable options when calls may run concurrently.

### Unsupported eager parameter use

Configuration cannot determine the graph while the factory executes:

```csharp
// Wrong: one branch would be missing from generated code.
return options.Formal ? Literals.Text("Hello") : Literals.Text("Hi");

// Wrong: a runtime value cannot become a build-time literal.
return Literals.Text(options.Keyword);

// Wrong: normalization eagerly reads a value that exists only at parse time.
var prefix = options.Prefix.Trim();
return Literals.Text("Hello").Then(text => prefix + text);
```

Defer those reads:

```csharp
return If(
        () => options.Formal,
        Literals.Text("Hello"),
        Literals.Text("Hi"))
    .Then(text => options.Prefix.Trim() + text);
```

Parameters cannot be reassigned or passed by reference. A callback that captures configuration must be
written inline rather than stored in a local variable.

### Other capture restrictions

Generated callbacks may not capture arbitrary locals or parameters belonging to another helper method:

```csharp
// Wrong: prefix is a local captured by the callback.
var prefix = "hello";
return Terms.Identifier().Then(value => prefix + value.ToString());
```

Use one of these alternatives:

1. Pass state as a factory configuration parameter and read it in an inline callback.
2. Use static fields or properties when global shared state is intentional.
3. Use a static method group for a callback whose dependencies are available from its arguments or static
   state. A helper that mentions Parlot types must remain in build-only grammar source.

```csharp
// In the .parlot.cs file:
private static string Normalize(TextSpan value) => value.ToString().ToUpperInvariant();

[GenerateParser(nameof(TryParseIdentifier))]
private static Parser<string> BuildIdentifier() =>
    Terms.Identifier().Then(Normalize);
```

The `TextSpan` in this example is build-time grammar code only; the direct entry point still returns
`string`. A normal compiled application helper should instead accept and return application-owned types.

### Parse context subclasses

Application subclasses of Parlot's runtime `ParseContext` are not compatible with the dependency-free
entry point. The generated parser uses an internal context under `Parlot.Generated`, and that type is not
interchangeable with the runtime library type.

Pass application-owned state through configuration parameters instead. Context-aware callbacks may use the
built-in context for parser internals, but public entry points and application state must not depend on
Parlot context types.

## Application models and runtime helpers

Result models, callback helpers, and any code called while parsing belong in normal `.cs` files. The
generator's private compilation can see the consuming project's source and references, while generated code
must resolve against the final application compilation.

Do not place an application model or runtime helper only in `.parlot.cs`; build-only grammar files are not
compiled into the application. A grammar can construct and return application-owned types:

```csharp
// Expression.cs
namespace MyApp;

public sealed record Expression(int Value);

public static partial class ExpressionParser
{
    public static partial bool TryParse(string input, out Expression value);
}
```

```csharp
// ExpressionParser.parlot.cs
[GenerateParser(nameof(TryParse))]
private static Parser<Expression> Build() =>
    Terms.Integer()
        .Then(static value => new Expression(checked((int)value)))
        .Eof();
```

## Helper attributes

Helper attributes may be applied to an individual factory or to its containing partial class.
Method-level values are combined with class-level values.

### `[IncludeUsings]`

Adds namespace imports needed by emitted callback or helper source:

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeUsings("System.Collections.Generic", "MyApp.Models")]
private static Parser<Expression> Build() => ...;
```

Callbacks bind their names as they do in the `.parlot.cs` file. The generator copies them with the types,
namespaces and static members they name fully qualified (`global::App.Models.SqlParameter.Create(...)`), so
usings placed inside a `namespace` block, aliases and `using static` keep their meaning, and a type of the same
name in an enclosing namespace of the generated class can't take over. Extension methods, and the methods of
query and collection expressions, still bind by name: the generated file imports the namespaces of the
factory's usings, file-level and from its enclosing `namespace` blocks, that exist in the application.

`[IncludeUsings]` adds more namespaces to the generated output, for instance for the extension methods of a
custom emitter's source.

### `[IncludeFiles]`

Adds source files to the generator's private factory compilation:

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeFiles("GrammarHelpers.cs", "Tokens/*.cs")]
private static Parser<Expression> Build() => ...;
```

Use this for build-time grammar helpers that are not already part of the consuming compilation. Included
files are not copied into the application. Application models and runtime callback helpers must still be
normal compiled source.

Paths are resolved relative to the `.parlot.cs` file and must remain inside the MSBuild project root.
Absolute paths, symbolic-link traversal, and patterns not ending in `.cs` are rejected.

| Pattern | Meaning |
|---|---|
| `*` | Any characters except a path separator |
| `**` | Any characters including path separators |
| `?` | One character except a path separator |

Examples:

```csharp
[IncludeFiles("*.cs")]
[IncludeFiles("Grammar/*.cs")]
[IncludeFiles("Grammar/**/*.cs")]
[IncludeFiles("../SharedGrammar/**/*.cs")]
```

Parent paths are valid only while the resolved files remain inside the project root. A factory may include
at most 256 files, each no larger than 1 MiB and no larger than 8 MiB in total. Glob traversal is capped at
10,000 entries. Rejections are reported as `PARLOT016` through `PARLOT020` without exposing resolved
filesystem paths. Prefer exact files or narrow directory globs.

### `[IncludeGenerators]`

Runs selected analyzer assemblies before the private factory compilation is emitted. Use this only when
build-time grammar code depends on another generator's output:

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeGenerators("PolySharp")]
private static Parser<Expression> Build() => ...;
```

Each name is an analyzer assembly name (`PolySharp.SourceGenerators`) or the id of the package that brings it
(`PolySharp`). Multiple names may be specified, and class-level and method-level values are combined.

The generator is taken from the analyzer references MSBuild passes to the compiler, so the version the project
references is loaded wherever NuGet restored it (`NUGET_PACKAGES`, `--packages`, `RestorePackagesPath`). When the
build doesn't pass them, or the name isn't among them, the newest version under the project's `NuGetPackageRoot`,
then `NUGET_PACKAGES`, then `~/.nuget/packages` is used. A name that can't be found reports the warning
`PARLOT012` and the factory compiles without that generator.

## Build-time security

`[GenerateParser]` factories are executable build code, not declarative metadata. They run inside the
compiler host with the build process's permissions and can access the build environment.
`[IncludeGenerators]` additionally loads and executes the selected analyzer assemblies.

Only build trusted grammar factories and generators outside an isolated environment. Keep `[IncludeFiles]`
patterns narrow and review changes to build-only source with the same care as build scripts. See
[Source generation and build-time code execution](security.md#source-generation-and-build-time-code-execution)
and the [`IncludeFiles` containment guidance](security.md#includefiles-containment).

## Generated support code

The generator emits the parser implementation plus shared scanner, cursor, character, number, result, and
context support into the consuming assembly. These support types are internal and live under
`Parlot.Generated`.

Every generated shared-support source file retains Parlot's original BSD-3-Clause license notice, and the
`Parlot.SourceGenerator` package includes the repository `LICENSE`. Applications do not acquire a Parlot runtime
package dependency, but binary distributions containing the generated support should retain the Parlot BSD
notice in their third-party notices or equivalent distribution materials.

The support layer is an implementation detail:

- it is not a public parser-combinator API;
- it is not interchangeable with types from the runtime `Parlot` assembly;
- internal support emitted into separate assemblies is not shared across those assemblies;
- unsupported runtime references fail generation instead of falling back to runtime Parlot execution.

Custom parser types used by a build-only grammar must implement `ISourceable` and emit self-contained code
that resolves only to BCL types, application code, or the generated internal support layer. A custom parser
that requires the runtime Parlot assembly cannot be used by a dependency-free entry point. This includes
subclasses of `NumberLiteralBase<T>`: the generated code parses numbers with the built-in methods and cannot
call a `TryParseNumber` override, so the generator reports `PARLOT005` instead of emitting a parser that
would behave differently from the runtime one.

### Native AOT

Generated support can be used in libraries with `IsAotCompatible=true` and warnings treated as errors,
and in applications published with `PublishAot=true`. Numeric parsing uses statically resolved calls;
runtime grammar-construction reflection and delegate-discovery helpers are not emitted.

This applies to the generated support, not arbitrary application code. Callbacks, models, and custom
sourceable parsers must also be compatible with trimming and Native AOT. The analyzer and build-only
grammar still execute in the normal compiler host, not in the published application.

## IDE and design-time behavior

IDE/design-time analysis does not execute grammar factories. The generator emits throwing stubs so partial
method declarations remain available to IntelliSense and other compiler features. A real command-line or
explicit IDE build executes the factory and emits the parser implementation.

Grammar files are registered during project evaluation so the IDE can discover them before a build.
Design-time builds use a separate generated MSBuild editorconfig file: a real build must not replace the
IDE's design-time flags and cause grammar factories to run during editor analysis. Referenced application
assemblies are loaded from memory so compiler hosts do not lock their build outputs.

Do not call a generated entry point while constructing another grammar. Entry points are parse-time APIs,
not factory-composition APIs.

## Debugging generated code

Enable compiler-generated file output to inspect the direct parser and shared support:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>
    obj/$(Configuration)/$(TargetFramework)/GeneratedFiles
  </CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated callbacks include `#line` mappings to their `.parlot.cs` source, so breakpoints and stack traces
map back to the original lambda or method-group location. The `ParlotLineDirectives` MSBuild property
chooses the paths of these directives:

| Value | `#line` directives |
|---|---|
| `absolute` (default) | The full path of the `.parlot.cs` file, as the compiler has it. Breakpoints work. |
| `relative` | The path relative to the project directory, with `/` separators, so the output is the same in every checkout. |
| `none` | Not emitted. Callbacks are debugged in the generated code. |

```xml
<PropertyGroup>
  <ParlotLineDirectives>relative</ParlotLineDirectives>
</PropertyGroup>
```

Use `relative` or `none` to check generated files in or compare them between machines. The compiler resolves a
relative `#line` path from the directory of the generated file, which isn't the project directory, so the
debugger may not find the `.parlot.cs` file from it; keep the default for debugging. Another value is reported
as the warning `PARLOT027`, and the default is used.

## Diagnostics

Standalone-specific diagnostics are:

| Diagnostic | Meaning |
|---|---|
| `PARLOT023` | The named direct entry point is missing, ambiguous (for instance two `TextReader` overloads), or has an invalid signature |
| `PARLOT024` | Generated code requires unsupported runtime code |
| `PARLOT025` | An annotated factory was compiled into the application instead of supplied build-only |
| `PARLOT026` | The project targets an unsupported framework or C# language version |
| `PARLOT027` | `ParlotLineDirectives` is not `absolute`, `relative` or `none` (warning) |

Other factory diagnostics still apply. Common cases include:

- `PARLOT009`: unsupported generic, by-reference, ref-like, pointer, or function-pointer factory signature;
- `PARLOT015`: callback captures a local or a parameter from another method;
- `PARLOT016`-`PARLOT020`: invalid, unsafe, unreadable, or excessive `[IncludeFiles]` input;
- `PARLOT021`: configuration was read eagerly while constructing the graph;
- `PARLOT022`: a deferred callback capturing configuration was invoked while constructing the graph.

There is no runtime fallback for unsupported standalone code. Treat generator errors as compatibility issues
in the grammar or callback and move runtime dependencies into supported application-owned helpers.

The generator also analyzes each grammar it generates and reports the patterns of the
[grammar rules](grammar-rules.md) (`PARLOT101`-`PARLOT113`) as `Info` diagnostics on the code that builds the parsers
concerned, with the edit that [fixes](grammar-rules.md#fixes) it when there is a mechanical one. They suggest changes
and never fail a build unless the project asks for it, for instance with `WarningsAsErrors`.

## Performance characteristics

Direct generated parsers avoid runtime graph construction, parser-instance caching, delegate invocation for
generated callbacks, and an application dependency on the Parlot runtime assembly. Configuration is supplied
per call without allocating a bound parser object.

Besides what the grammar's callbacks and results allocate, a `string` entry point allocates two objects: the
internal context, which also holds the configuration and the state of the grammar's deferred parsers, and a
scanner that is its own cursor. The context's loop detection and recursion limit members, which generated
parsers don't use, only allocate their state when a callback calls them, or, for the recursion limit, when the attribute
sets `MaxRecursionDepth`.

Generated parser helpers and callbacks use normal JIT inlining heuristics. Forcing inlining based only
on a helper's local statement count can duplicate large parser chains, increasing native code size and
stack initialization costs. A bounded hint is retained only for the entry core, which can inline into
the public wrapper without forcing internal helper chains. The shared scanner and runtime support code
retain their own inlining hints.

Measure representative grammars. Result models and callbacks can still allocate, and conversions such as
`TextSpan.ToString()` intentionally create application-owned strings when the public result requires them.
`ToPooledString()` keeps its pool, and case-insensitive `Text`/`Keyword` parsers returning the matched text keep their
cases, in a static field of the generated class, shared by all the calls of the entry point, like the runtime parser
shares them between parses. A pooled string literal decodes its escape sequences into the same field.

Custom string delimiters use the scanner's single-character overload without allocating a delimiter array
per token. When `Capture` discards a string parser's decoded value, the generated parser still validates
escape sequences but skips decoding and its string allocation. Callbacks and predicates that consume the
value still receive decoded text and execute normally, even when their result is captured or discarded.
Helpers are specialized by result mode so the same parser can be used both for capture and for its value.

Generated parsers also use recognition-only helpers for fully skipped `AndSkip`/`SkipAnd` operands,
`Between` delimiters, separators, lookaheads, operator tokens whose values are unused, and `AnyCharBefore`
delimiters. Constant-result `Then(value)` parsers do not materialize their input value either. A skipped
`Text` token still checks its comparison mode and advances the cursor, but does not materialize matched
text, including when `returnMatchedText: true`. Retained tokens keep their normal casing and value
semantics. This optimization applies to generated string and `TextReader` entry points, not runtime
`Parse` methods.

Recognition-only helpers still validate numeric conversion and range: skipping a number does not
accept overflow or values its numeric type cannot represent. Skipping a callback's result does not
skip its execution, required input values, or exceptions.
Higher-arity `SkipAnd` keeps the preceding tuple value-producing when some of its members are returned;
individual dropped tuple members are not independently optimized.

### Span code

Parsers which only read the text are generated as *span code*, the way the .NET regular expression source
generator emits a pattern. The text after the cursor is read through a `ReadOnlySpan<char>` local indexed by a
position local, the parsers of the subgraph are emitted inline in a single method, with jumps to failure labels, and
the cursor is moved once, when the subgraph matches. A failure leaves the cursor where it was: a sequence doesn't
move it back, and choices, optional parsers and loops move the position local back rather than the cursor.

`Char`, ordinal `Text`, `Pattern`, `AnyOf`, `NoneOf`, `Identifier` and the decimal number parsers (`Number<T>`,
`Integer`, `Decimal`) are generated as span code inside the composite parsers whose parsers all are: `Terms` parsers
(`SkipWhiteSpace`), `Then` with a callback receiving the value or with a constant, `Capture`, `OneOf` with less than 8
alternatives (more texts or keywords use their lookups), `ZeroOrMany`, `OneOrMany`, `Optional`, `ZeroOrOne`,
`Between` and the sequences (`And`, `AndSkip`, `SkipAnd`). The outermost such parser becomes one method, of up to 64
parsers, a parser used twice counting twice and a number 8 times. The other parsers, like `Deferred`, `Separated`, lookaheads or callbacks
receiving the context, invoke the span code of their parts. A literal alone keeps its code.

```csharp
// One method, which moves the cursor once. The JIT inlines the 'w' callbacks and drops their TextSpan.
var wordChar = Literals.Pattern(char.IsLetterOrDigit);
var email = Capture(
    OneOrMany(OneOf(wordChar.Then(static _ => 'w'), Literals.Char('.'), Literals.Char('+'), Literals.Char('-')))
        .And(Literals.Char('@'))
        .And(OneOrMany(OneOf(wordChar.Then(static _ => 'w'), Literals.Char('-'))))
        .And(Literals.Char('.'))
        .And(OneOrMany(OneOf(wordChar.Then(static _ => 'w'), Literals.Char('.'), Literals.Char('-')))));
```

The white space before a `Terms` parser is skipped by the execution context, which can run a custom white space
parser. Span code checks the next char first, and only moves the cursor to skip white space when there is some, then
moves it to the end of the match, or back to where it was, from there.

The results are the ones of the other generated code, and of the runtime parsers: values, failures, positions, and
the `Cursor.HitEnd` marks a non-final buffer depends on. A check of the char at the end of the span marks it, so does
a callback invoked there, and moving the cursor to the end does. `TextReader` entry points don't use span code, their
literals are tokens which refill the buffer, except inside a token, like the delimiter of `AnyCharBefore`.

### Parser Explorer diagnostics

Set `Diagnostics = true` on a factory's `GenerateParser` attribute to emit diagnostic entry/exit and
cursor-reset events for the local [Parser Explorer](explorer.md), and the findings of the [grammar rules](grammar-rules.md). This mode is intended for debugging;
ordinary generated parsers contain no tracing calls. It doesn't generate span code, so that each parser that is invoked
and each move of the cursor is reported. The explorer supports private entry points,
Monaco input editing, replay, rebuild detection, and result inspection without uploading assemblies.
