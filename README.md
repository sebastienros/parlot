# Parlot

[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![Join the chat at https://gitter.im/sebastienros/parlot](https://badges.gitter.im/sebastienros/parlot.svg)](https://gitter.im/sebastienros/parlot?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Branches and NuGet feeds

| Branch | Purpose | Package feed |
| --- | --- | --- |
| [`release/1.x`](https://github.com/sebastienros/parlot/tree/release/1.x) | Maintenance of stable Parlot 1.x releases. | [![NuGet.org stable version](https://img.shields.io/nuget/v/Parlot.svg?label=nuget.org)](https://www.nuget.org/packages/Parlot) |
| [`main`](https://github.com/sebastienros/parlot/tree/main) | Development of Parlot 2.0, including source-generated parsers. | [![Feedz preview version](https://img.shields.io/endpoint?url=https%3A%2F%2Ff.feedz.io%2Fsebastienros%2Fparlot%2Fshield%2FParlot%2Flatest)](https://f.feedz.io/sebastienros/parlot/nuget/index.json) |

### Stable packages

Tagged releases are published to [NuGet.org](https://www.nuget.org/packages/Parlot),
using the feed `https://api.nuget.org/v3/index.json`. To install the latest stable version:

```shell
dotnet add package Parlot
```

### Preview packages

After a successful Ubuntu build triggered by a push to `main`, the workflow publishes preview packages to the
[Parlot feed on feedz.io](https://f.feedz.io/sebastienros/parlot/nuget/index.json).
Versions follow `2.0.0-preview-<run number>`, using the GitHub Actions build run number.
These packages contain the latest development changes and are intended for testing before release.

Add the preview feed alongside NuGet.org, then install the latest prerelease version:

```shell
dotnet nuget add source https://f.feedz.io/sebastienros/parlot/nuget/index.json --name parlot-preview
dotnet add package Parlot --prerelease
```

Keep NuGet.org enabled so dependencies can be restored. If your `NuGet.config` uses package source
mapping, also map `Parlot` to the `parlot-preview` source.

## Fluent API

The Fluent API provides simple parser combinators that are assembled to express more complex expressions.
The main goal of this API is to provide an easy-to-read grammar. Another advantage is that grammars are built at runtime, and they can be extended dynamically.

### Getting Started

To use the Fluent API, you need to import the static `Parsers` class which provides access to `Terms`, `Literals`, and other parser combinators:

```c#
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;
```

> **Important:** The `using static Parlot.Fluent.Parsers;` statement is required to access `Terms`, `Literals`, `ZeroOrOne`, `Between`, and other parser combinators used in the examples below. 
>
> Alternatively, if your project has `ImplicitUsings` (also known as Global Usings) enabled, this import is included automatically.

The following example is a complete parser that creates a mathematical expression tree (AST).
The source is available [here](./src/Samples/Calc/FluentParser.cs).

```c#
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static readonly Parser<Expression> Expression;

static FluentParser()
{
    /*
      * Grammar:
      * The top declaration has a lower priority than the lower one.
      * 
      * additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
      * multiplicative => unary ( ( "/" | "*" ) unary )* ;
      * unary          => ( "-" ) unary
      *                   | primary ;
      * primary        => NUMBER
      *                   | "(" expression ")" ;
    */

    // The Deferred helper creates a parser that can be referenced by others before it is defined
    var expression = Deferred<Expression>();

    var number = Terms.Decimal()
        .Then<Expression>(static d => new Number(d))
        ;

    var divided = Terms.Char('/');
    var times = Terms.Char('*');
    var minus = Terms.Char('-');
    var plus = Terms.Char('+');
    var openParen = Terms.Char('(');
    var closeParen = Terms.Char(')');

    // "(" expression ")"
    var groupExpression = Between(openParen, expression, closeParen);

    // primary => NUMBER | "(" expression ")";
    var primary = number.Or(groupExpression);

    // ( "-" ) unary | primary;
    var unary = primary.Unary(
        (minus, x => new NegateExpression(x))
        );

    // multiplicative => unary ( ( "/" | "*" ) unary )* ;
    var multiplicative = unary.LeftAssociative(
        (divided, static (a, b) => new Division(a, b)),
        (times, static (a, b) => new Multiplication(a, b))
        );

    // additive => multiplicative(("-" | "+") multiplicative) * ;
    var additive = multiplicative.LeftAssociative(
        (plus, static (a, b) => new Addition(a, b)),
        (minus, static (a, b) => new Subtraction(a, b))
        );

    expression.Parser = additive;

    Expression = expression;
}
```

## Documentation

- [Existing parsers and usage examples](docs/parsers.md)
- [Best practices for custom parsers](docs/writing.md)
- [Source generation guide](docs/source-generation.md)
- [Security guidance](docs/security.md)

## Source-generated parsers

Parlot's analyzer generates **direct parsing methods** during the normal build. Generated code and
shared internal support sources compile into your assembly, with **no Parlot runtime dependency**.

### How it works

- Reference the `Parlot.SourceGenerator` package with `PrivateAssets="all"`.
- Declare a static partial `bool TryParse(string text, out T value)` method in a normal `.cs` file.
- Define its factory in a build-only `.parlot.cs` file with `[GenerateParser(nameof(TryParse))]`.
- The analyzer executes the factory at build time and emits the method implementation and support code.
- For variants, pass configuration arguments between the input and output parameters and capture them
  in `If`, `Select`, or other supported parse-time callbacks in the matching factory.

Application code, in `MyGrammar.cs`:

```csharp
public static partial class MyGrammar
{
    public static partial bool TryParse(string text, out string value);
}

// Usage: MyGrammar.TryParse("hello", out var value)
```

Build-only grammar, in `MyGrammar.parlot.cs`:

```csharp
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class MyGrammar
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<string> Build() => Terms.Text("hello").Eof();
}
```

### Requirements

- Use a compiler host with Roslyn 5.9 or later and C# 12 or later. Generated code supports `net472`, `netstandard2.0`, `net8.0`, and `net10.0`; older targets use compatibility packages such as `System.Memory`, not Parlot.
- Add an extra `CancellationToken` before the `out` result to enable cooperative cancellation, without adding it to the grammar factory.
- No interceptors configuration is needed.
- Methods must be static and non-generic. By-value parameters may only be read in supported parse-time callbacks, not during graph construction.
- The containing class must be `partial`.

### Advanced Configuration

Additional attributes can be combined with `[GenerateParser]`:

- `[IncludeFiles("*.cs")]` – Include source files (supports globs) for types used by your parser.
- `[IncludeUsings("Namespace")]` – Add extra using directives to generated code.
- `[IncludeGenerators("AssemblyName")]` – Run other source generators before parser generation.

For detailed documentation, see [Source Generation Guide](docs/source-generation.md).

> **Why use source generation?**
> - Inlined parsing without combinator dispatch
> - Faster startup (no runtime parser graph construction)
> - AOT-friendly, deterministic parser code
> - No Parlot assembly or package dependency for downstream consumers

## Performance

Parlot is faster and allocates less memory than all other known parser combinators for .NET.

It was originally created to provide a more efficient alternative to projects like:

- [Superpower](https://github.com/nblumhardt/superpower)
- [Sprache](https://github.com/sprache/Sprache)
- [Irony](https://github.com/IronyProject/Irony)

Finally, even though [Pidgin](https://github.com/benjamin-hodgson/Pidgin) showed some very good performance, Parlot is still faster.

The current suite also includes [Farkle](https://github.com/teo-tsirpanis/Farkle) 7.1.0, an LR parser,
for all three expression cases and all four JSON shapes. Its grammars are built once during setup,
outside the timed methods, and produce the same AST models as the other parser libraries.

Farkle uses native LR operator precedence and a typed regex number terminal that creates `Number`
nodes directly, without boxing intermediate decimals. Its built-in float terminals require digits on
both sides of the decimal point, so a composed terminal preserves the integer, `.5`, `1.`, and exponent
syntax supported by `Terms.Decimal()`. JSON uses the built-in string terminal and left-recursive
collection accumulation, without intermediate key/value-pair lists or array copies.

To reproduce:

```bash
dotnet build -c Release
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release --no-build -- --filter "*ExprBench*" "*JsonBench*" "*RegexBenchmarks*"
```

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises three expressions:

- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`
- Unary: `-(3 + 2) * -4 + --6`

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin and Farkle. It parses the expressions into the same AST without evaluating them.

In these results, Parlot Fluent is about 13-14 times faster than Pidgin and Parlot Raw is faster still.
The source-generated parser allocates 144 fewer bytes per parse than Fluent in all three expressions.
Generated helpers use normal JIT inlining heuristics to avoid expanding
large parser chains into oversized native methods.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev    | Gen0   | Allocated |
|--------------------- |------------:|------------:|----------:|-------:|----------:|
| ParlotRawSmall       |    127.6 ns |    15.35 ns |   0.84 ns | 0.0362 |     304 B |
| ParlotFluentSmall    |    229.5 ns |    20.44 ns |   1.12 ns | 0.0668 |     560 B |
| ParlotGeneratedSmall |    176.8 ns |    22.02 ns |   1.21 ns | 0.0496 |     416 B |
| PidginSmall          |  3,135.2 ns |   749.92 ns |  41.11 ns | 0.0992 |     832 B |
| FarkleSmall          |    382.6 ns |    32.51 ns |   1.78 ns | 0.0267 |     224 B |
|                      |             |             |           |        |           |
| ParlotRawBig         |    645.2 ns |   115.47 ns |   6.33 ns | 0.1431 |    1200 B |
| ParlotFluentBig      |  1,255.3 ns |   120.46 ns |   6.60 ns | 0.1736 |    1456 B |
| ParlotGeneratedBig   |  1,029.0 ns |   249.33 ns |  13.67 ns | 0.1564 |    1312 B |
| PidginBig            | 16,781.1 ns | 2,515.88 ns | 137.90 ns | 0.4883 |    4152 B |
| FarkleBig            |  2,121.2 ns |   330.01 ns |  18.09 ns | 0.1335 |    1120 B |
|                      |             |             |           |        |           |
| ParlotFluentUnary    |    305.1 ns |    12.42 ns |   0.68 ns | 0.0782 |     656 B |
| ParlotGeneratedUnary |    245.1 ns |    91.53 ns |   5.02 ns | 0.0610 |     512 B |
| FarkleUnary          |    580.6 ns |    34.12 ns |   1.87 ns | 0.0381 |     320 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot use parser combinators; Farkle uses an LR grammar. These grammars parse the same subset of JSON: strings, arrays, and objects.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Farkle, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error       | StdDev     | Gen0     | Gen1     | Allocated  |
|------------------------- |-----------:|------------:|-----------:|---------:|---------:|-----------:|
| BigJson_Parlot           |  36.051 us |   1.0080 us |  0.0552 us |  10.7422 |   1.8311 |   88.16 KB |
| BigJson_ParlotGenerated  |  32.259 us |   5.6759 us |  0.3111 us |  11.6577 |   2.1362 |   95.52 KB |
| BigJson_Pidgin           |  79.276 us |   0.7289 us |  0.0400 us |  11.1084 |   1.7090 |    91.7 KB |
| BigJson_Farkle           |  78.754 us |   2.7509 us |  0.1508 us |   8.9111 |   1.3428 |   72.86 KB |
| BigJson_Newtonsoft       |  50.276 us |   6.6282 us |  0.3633 us |  24.8413 |   8.2397 |   203.1 KB |
| BigJson_SystemTextJson   |  15.422 us |   2.0314 us |  0.1113 us |   2.9297 |   0.3052 |   24.12 KB |
| BigJson_Sprache          | 805.894 us |  56.1919 us |  3.0801 us | 621.0938 | 125.9766 | 5080.12 KB |
| BigJson_Superpower       | 384.725 us |  83.2759 us |  4.5646 us | 103.5156 |  18.0664 |  845.93 KB |
|                          |            |             |            |          |          |            |
| DeepJson_Parlot          |  29.969 us |   3.7643 us |  0.2063 us |  12.7869 |   1.4038 |  104.57 KB |
| DeepJson_ParlotGenerated |  23.851 us |   3.1044 us |  0.1702 us |  12.7563 |   1.3733 |  104.34 KB |
| DeepJson_Pidgin          | 100.118 us |  11.7819 us |  0.6458 us |  14.1602 |   3.5400 |  116.29 KB |
| DeepJson_Farkle          |  37.476 us |   3.2420 us |  0.1777 us |   8.3008 |   1.0376 |   68.17 KB |
| DeepJson_Newtonsoft      |  38.450 us | 125.0969 us |  6.8570 us |  21.9116 |   8.7280 |  179.13 KB |
| DeepJson_SystemTextJson  |  62.645 us |  52.6606 us |  2.8865 us |   2.4414 |   0.1221 |   20.24 KB |
| DeepJson_Sprache         | 644.544 us | 250.0158 us | 13.7042 us | 344.7266 | 139.6484 | 2818.33 KB |
|                          |            |             |            |          |          |            |
| LongJson_Parlot          |  30.137 us |   1.1665 us |  0.0639 us |  13.7634 |   2.8076 |  112.52 KB |
| LongJson_ParlotGenerated |  26.734 us |   3.1907 us |  0.1749 us |  15.1978 |   3.7231 |  124.38 KB |
| LongJson_Pidgin          |  69.126 us |   7.5713 us |  0.4150 us |  14.6484 |   3.0518 |  120.25 KB |
| LongJson_Farkle          |  65.131 us |   9.0928 us |  0.4984 us |  10.7422 |   2.1973 |   88.19 KB |
| LongJson_Newtonsoft      |  37.871 us |   8.4325 us |  0.4622 us |  24.7803 |   9.6436 |  202.68 KB |
| LongJson_SystemTextJson  |   9.746 us |   3.6040 us |  0.1975 us |   2.9297 |   0.3204 |   24.12 KB |
| LongJson_Sprache         | 669.792 us | 251.4698 us | 13.7839 us | 509.7656 | 129.8828 |  4165.2 KB |
| LongJson_Superpower      | 276.934 us |  32.1281 us |  1.7610 us |  81.0547 |  20.0195 |  662.79 KB |
|                          |            |             |            |          |          |            |
| WideJson_Parlot          |  17.316 us |   3.8642 us |  0.2118 us |   4.9744 |   0.4883 |   40.72 KB |
| WideJson_ParlotGenerated |  13.416 us |   5.6621 us |  0.3104 us |   4.9591 |   0.5493 |   40.58 KB |
| WideJson_Pidgin          |  32.552 us |   1.3023 us |  0.0714 us |   4.9438 |   0.4883 |   40.48 KB |
| WideJson_Farkle          |  43.951 us |   4.1137 us |  0.2255 us |   5.6152 |   0.5493 |   45.92 KB |
| WideJson_Newtonsoft      |  25.193 us |  22.9050 us |  1.2555 us |  13.0310 |   3.2349 |  106.72 KB |
| WideJson_Sprache         | 362.677 us |   6.6732 us |  0.3658 us | 318.8477 |  45.4102 | 2606.25 KB |
| WideJson_Superpower      | 173.535 us |  12.9893 us |  0.7120 us |  49.3164 |   4.6387 |  403.81 KB |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|----------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  42.36 ns |  6.251 ns | 0.343 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  94.71 ns |  4.773 ns | 0.262 ns |  2.24 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  40.81 ns |  0.901 ns | 0.049 ns |  0.96 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 148.20 ns | 34.056 ns | 1.867 ns |  3.50 | 0.0372 |     312 B |        1.50 |
| ParlotEmailGenerated |  76.67 ns | 29.581 ns | 1.621 ns |  1.81 | 0.0229 |     192 B |        0.92 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot (current source)
- Farkle 7.1.0
- Pidgin 3.5.1
- Sprache 3.0.0-develop-00049
- Superpower 3.2.2-dev-00214
- Newtonsoft.Json 13.0.5-beta1

### Operator Syntax

Parlot supports intuitive operators for parser composition:

- **`+` operator**: Combines parsers in sequence (alternative to `.And()`)
- **`|` operator**: Creates choice between parsers (alternative to `.Or()`)

```c#
// Using operators
var parser = Literals.Char('a') + Literals.Char('b') + Literals.Char('c');
var choice = Literals.Char('x') | Literals.Char('y') | Literals.Char('z');

// Equivalent to
var parser = Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c'));
var choice = Literals.Char('x').Or(Literals.Char('y')).Or(Literals.Char('z'));
```

### Usages

Parlot is already used in these projects:

- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
- [YesSql](https://github.com/sebastienros/yessql)
- [NCalc](https://github.com/ncalc/ncalc)
- [hyperbee.xs] https://github.com/Stillpoint-Software/hyperbee.xs
