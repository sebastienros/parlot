# Source Generation Guide

Parlot source generation compiles a build-only Fluent grammar into a direct partial parsing method.
The generated parser and its support code are emitted into the consuming assembly, so application code
does not need a runtime reference to `Parlot`.

Build with .NET SDK 10.0.400 or later, which provides the analyzer's required Roslyn version.
Generated consumers can target .NET 8 or later and C# 12 or later.

## Getting started

Add the analyzer package to the project that owns the grammar:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <LangVersion>12</LangVersion>
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

Each `[GenerateParser(nameof(...))]` factory must have exactly one matching partial method declaration.

The factory:

- is in a `.parlot.cs` file supplied as an `AdditionalFile` and excluded from `Compile`;
- is static and non-generic;
- is contained by the same top-level, non-generic partial class as the entry point;
- returns `Parlot.Fluent.Parser<T>`;
- uses only ordinary by-value configuration parameters.

The application-facing method:

- is a static, non-generic partial method returning `bool`;
- takes the input `string` first;
- takes the factory's configuration parameters next, in the same type and order;
- takes `out T value` last, where `T` is the factory parser's result type.

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

The generated method creates its per-parse execution context and executes the emitted parser directly.
Configuration is stored in the existing per-call execution context, without a separate parser or closure
allocation. The scanner, cursor, and context use the shared runtime implementations.

- A successful parse assigns `value` and returns `true`.
- An ordinary mismatch returns `false` and assigns `default` to `value`.
- `Error` and `ElseError` throw `ParseException` internally; the direct entry point converts that exception
  to `false` and a default result.
- Exceptions thrown by application callbacks are not swallowed and propagate to the caller.
- A null input is rejected.
- End-of-input matching is explicit. Add `.Eof()` when trailing input must fail.
- `Terms` parsers skip configured whitespace and comments; `Literals` parsers do not.

### Deliberate standalone boundary

Standalone entry points intentionally expose only the input string, application configuration, and the
parsed value. They do not expose the internal execution context:

- No consumed offset is returned. Without `.Eof()`, a parser may successfully match a prefix, but the
  caller cannot retrieve the position where parsing stopped. Use `.Eof()` for whole-input parsing.
- No `CancellationToken` is accepted and parsing cannot be cancelled through the entry point.
- No custom `ParseContext`, recursion-depth setting, or other execution-context option can be supplied.
  Factory parameters configure generated callbacks and branches; they do not configure the parsing engine.
- The input is a `string`; `ReadOnlySpan<char>` entry points are not supported.

Use the normal Parlot runtime API when consumed positions, cancellation, or custom parse contexts are
required.

Generated collection parsers preserve the runtime collection behavior: small results use inline storage,
larger results grow into a list, and an empty `ZeroOrMany` result uses an empty array. Public results should
normally use `IReadOnlyList<T>`.

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

Usings in the `.parlot.cs` file compile the build-time factory. `[IncludeUsings]` applies to the generated
output, so add it when copied callback expressions require extension methods or unqualified application
types that are not otherwise emitted with fully qualified names.

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

Assembly names must match analyzer references available to the consuming project. Multiple names may be
specified, and class-level and method-level values are combined.

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
`Parlot.SourceGenerator` package includes the repository `LICENSE`. Applications do not acquire a runtime
package dependency, but binary distributions containing the generated support should retain the Parlot BSD
notice in their third-party notices or equivalent distribution materials.

The support layer is an implementation detail:

- it is not a public parser-combinator API;
- it is not interchangeable with types from the runtime `Parlot` assembly;
- internal support emitted into separate assemblies is not shared across those assemblies;
- unsupported runtime references fail generation instead of falling back to runtime Parlot execution.

Custom parser types used by a build-only grammar must implement `ISourceable` and emit self-contained code
that resolves only to BCL types, application code, or the generated internal support layer. A custom parser
that requires the runtime Parlot assembly cannot be used by a dependency-free entry point.

## IDE and design-time behavior

IDE/design-time analysis does not execute grammar factories. The generator emits throwing stubs so partial
method declarations remain available to IntelliSense and other compiler features. A real command-line or
explicit IDE build executes the factory and emits the parser implementation.

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
map back to the original lambda or method-group location.

## Diagnostics

Standalone-specific diagnostics are:

| Diagnostic | Meaning |
|---|---|
| `PARLOT023` | The named direct entry point is missing, ambiguous, or has an invalid signature |
| `PARLOT024` | Generated code requires unsupported runtime code |
| `PARLOT025` | An annotated factory was compiled into the application instead of supplied build-only |
| `PARLOT026` | The project targets an unsupported framework or C# language version |

Other factory diagnostics still apply. Common cases include:

- `PARLOT009`: unsupported generic, by-reference, ref-like, pointer, or function-pointer factory signature;
- `PARLOT015`: callback captures a local or a parameter from another method;
- `PARLOT016`-`PARLOT020`: invalid, unsafe, unreadable, or excessive `[IncludeFiles]` input;
- `PARLOT021`: configuration was read eagerly while constructing the graph;
- `PARLOT022`: a deferred callback capturing configuration was invoked while constructing the graph.

There is no runtime fallback for unsupported standalone code. Treat generator errors as compatibility issues
in the grammar or callback and move runtime dependencies into supported application-owned helpers.

## Performance characteristics

Direct generated parsers avoid runtime graph construction, parser-instance caching, delegate invocation for
generated callbacks, and an application dependency on the Parlot runtime assembly. Configuration is supplied
per call without allocating a bound parser object.

Generated parser helpers and callbacks use normal JIT inlining heuristics. Forcing inlining based only
on a helper's local statement count can duplicate large parser chains, increasing native code size and
stack initialization costs. A bounded hint is retained only for the entry core, which can inline into
the public wrapper without forcing internal helper chains. The shared scanner and runtime support code
retain their own inlining hints.

Measure representative grammars. Result models and callbacks can still allocate, and conversions such as
`TextSpan.ToString()` intentionally create application-owned strings when the public result requires them.

Custom string delimiters use the scanner's single-character overload without allocating a delimiter array
per token. When `Capture` discards a string parser's decoded value, the generated parser still validates
escape sequences but skips decoding and its string allocation. Callbacks and predicates that consume the
value still receive decoded text and execute normally, even when their result is captured or discarded.
Helpers are specialized by result mode so the same parser can be used both for capture and for its value.
