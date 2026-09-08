# Parlot.SourceGenerator

`Parlot.SourceGenerator` compiles build-only Parlot Fluent grammars into dependency-free direct parsing
methods. The consuming application calls a generated partial
`bool TryParse(string input, ..., out T value)` method and does not need a runtime reference to `Parlot`.

Use an SDK or IDE with Roslyn 5.9 or later and C# 12 or later. Generated code supports .NET Framework 4.7.2,
.NET Standard 2.0, .NET 8, and .NET 10. Older targets use compatibility packages such as `System.Memory`,
not the Parlot runtime package.

## Installation

Reference only the dedicated analyzer package:

```xml
<ItemGroup>
  <PackageReference Include="Parlot.SourceGenerator"
                    Version="2.0.0-preview"
                    PrivateAssets="all" />
</ItemGroup>
```

The runtime `Parlot` package does not include or activate the analyzer. A dependency-free generated
consumer should not reference the runtime package.

Downlevel applications restore `System.Memory` automatically. When publishing a library targeting
`net472` or `netstandard2.0`, add an explicit, non-private `System.Memory` reference so your package
propagates that runtime dependency to downstream consumers:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net472' or '$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Memory" Version="4.6.3" />
</ItemGroup>
```

Packing reports an error if this reference is missing. Modern targets need no compatibility packages,
and the generated support code does not require PolySharp.

## Usage

Declare the direct entry point in normal application source:

```csharp
namespace MyApp;

public static partial class NumberParser
{
    public static partial bool TryParse(string input, out int value);
}
```

Put the factory in a build-only `NumberParser.parlot.cs` file:

```csharp
using Parlot.Fluent;
using Parlot.SourceGenerator;

namespace MyApp;

public static partial class NumberParser
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<int> Build() =>
        Parsers.Terms.Number<int>(NumberOptions.Integer).Eof();
}
```

The package's build props and targets remove `**/*.parlot.cs` from `Compile` and add those files as
`AdditionalFiles`. The factory executes only during a real build; it and its `Parser<T>` graph are not
emitted into the application.

```csharp
if (NumberParser.TryParse("42", out var value))
{
    Console.WriteLine(value);
}
```

The entry point must be a static partial `bool` method in the same top-level, non-generic partial class as
the factory. Its first parameter is the input string, its final parameter is `out T`, and any parameters
between them match the factory's by-value configuration parameters in type and order. One additional
`System.Threading.CancellationToken` may follow configuration, immediately before `out T`.

Public entry signatures may use only BCL or application-owned types. Convert Parlot values such as
`TextSpan` and `Option<T>` inside the grammar rather than returning them.

## Configuration

Factory parameters are supplied on every parse call. Read them only in supported inline parse-time
callbacks such as `If`, `Select`, `Then`, `ThenElse`, `When`, `Switch`, and `Else`:

```csharp
public static partial bool TryParse(
    string input,
    ParserOptions options,
    out string value);

[GenerateParser(nameof(TryParse))]
private static Parser<string> Build(ParserOptions options) =>
    If(
        () => options.Formal,
        Literals.Text("Hello"),
        Literals.Text("Hi"))
    .Then(text => options.Prefix + text)
    .Eof();
```

Configuration cannot select or construct the graph eagerly. Captured locals, another method's parameters,
parameter reassignment, and by-reference use are unsupported. Application `ParseContext` subclasses are
also incompatible with the dependency-free runtime; pass application-owned state as configuration instead.

## Cancellation

Enable engine cancellation by adding a token to the entry point, without changing the factory:

```csharp
public static partial bool TryParse(
    string input,
    System.Threading.CancellationToken cancellationToken,
    out int value);
```

Cancellation throws `OperationCanceledException`, including for an already cancelled token. Checks are
cooperative and throttled at parser boundaries; a single scanner operation or callback is not interrupted.
Use `CancellationToken.None` or a token-free entry point when cancellation is unnecessary.
Tokens included in factory configuration remain ordinary callback state and do not implicitly configure
engine cancellation.

## Parse behavior

- Success assigns the result and returns `true`.
- Mismatch and `ParseException` return `false` with a default result.
- Exceptions thrown by application callbacks propagate.
- Engine cancellation propagates as `OperationCanceledException`, not a mismatch.
- End-of-input matching remains explicit through `.Eof()`.
- No parser object or combinator graph is allocated per call.

Application models and runtime callback helpers belong in normal `.cs` files. Code that exists only in a
`.parlot.cs` file is unavailable at runtime.

## Helper attributes

`[IncludeUsings]` adds imports required by emitted callback source:

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeUsings("System.Collections.Generic", "MyApp.Models")]
private static Parser<Expression> Build() => ...;
```

`[IncludeFiles]` adds build-time grammar helpers to the private factory compilation. Paths are relative to
the `.parlot.cs` file, must remain inside the MSBuild project root, and may use `*`, `**`, and `?` globs.
Included files are not emitted into the application, so do not define runtime models or callbacks only
there.

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeFiles("GrammarHelpers.cs", "Tokens/**/*.cs")]
private static Parser<Expression> Build() => ...;
```

`[IncludeGenerators]` runs named analyzer assemblies before compiling the factory when build-time grammar
code depends on another generator's output:

```csharp
[GenerateParser(nameof(TryParse))]
[IncludeGenerators("PolySharp")]
private static Parser<Expression> Build() => ...;
```

All helper attributes may also be applied to the containing partial class.

## Security

Grammar factories execute as trusted code inside the compiler host. Included generators are executable
build dependencies, and included files become part of the private executable factory compilation. Build
only trusted factories and generators, and keep file globs narrow. See the repository's
[source-generation security guidance](../../docs/security.md#source-generation-and-build-time-code-execution).

## Generated output

The parser implementation is emitted into your partial class; shared internal support is emitted under
`Parlot.Generated` in the consuming assembly. Unsupported runtime references produce a generation error;
there is no fallback to runtime Parlot execution.

Generated shared-support files retain Parlot's BSD-3-Clause license notice, and the analyzer package
includes `LICENSE`. Binary distributions containing the generated support should retain the Parlot BSD
notice in their third-party notices or equivalent distribution materials even though they have no Parlot
runtime package dependency.

To inspect generated files:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>
    obj/$(Configuration)/$(TargetFramework)/GeneratedFiles
  </CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

For the complete contract, capture restrictions, diagnostics, and `IncludeFiles` containment limits, see
the [Source Generation Guide](../../docs/source-generation.md).
