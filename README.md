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

- Build with .NET SDK 10.0.400 or later. Generated code can target .NET 8 or later and C# 12 or later.
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

The tables below were measured on September 7, 2026 using the current dependency-free generated parsers.
All 41 cases use BenchmarkDotNet's out-of-process `ShortRun` job, with three warmup and three measurement
iterations. These are short-run estimates; consider the reported error bounds when comparing timings.

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

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin. It parses the expressions into the same AST without evaluating them.

In these results, Parlot Fluent is about 12-13 times faster than Pidgin and Parlot Raw is faster still.
The source-generated parser takes about 20-26% less time than Fluent and allocates 144 fewer bytes per
parse in all three expressions. Generated helpers use normal JIT inlining heuristics to avoid expanding
large parser chains into oversized native methods.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev    | Gen0   | Allocated |
|--------------------- |------------:|------------:|----------:|-------:|----------:|
| ParlotRawSmall       |    122.8 ns |    12.99 ns |   0.71 ns | 0.0362 |     304 B |
| ParlotFluentSmall    |    230.5 ns |    58.15 ns |   3.19 ns | 0.0668 |     560 B |
| ParlotGeneratedSmall |    177.3 ns |    25.68 ns |   1.41 ns | 0.0496 |     416 B |
| PidginSmall          |  3,087.1 ns |   153.50 ns |   8.41 ns | 0.0992 |     832 B |
|                      |             |             |           |        |           |
| ParlotRawBig         |    655.1 ns |   100.91 ns |   5.53 ns | 0.1431 |    1200 B |
| ParlotFluentBig      |  1,319.8 ns |   432.50 ns |  23.71 ns | 0.1736 |    1456 B |
| ParlotGeneratedBig   |    981.2 ns |    88.60 ns |   4.86 ns | 0.1564 |    1312 B |
| PidginBig            | 16,051.9 ns | 3,345.67 ns | 183.39 ns | 0.4883 |    4152 B |
|                      |             |             |           |        |           |
| ParlotFluentUnary    |    307.4 ns |    65.74 ns |   3.60 ns | 0.0782 |     656 B |
| ParlotGeneratedUnary |    246.4 ns |    34.78 ns |   1.91 ns | 0.0610 |     512 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinators.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error       | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------------- |-----------:|------------:|----------:|---------:|---------:|-----------:|
| BigJson_Parlot           |  37.518 us |   9.4310 us | 0.5169 us |  10.7422 |   1.8311 |   88.16 KB |
| BigJson_ParlotGenerated  |  34.607 us |   1.3631 us | 0.0747 us |  11.6577 |   2.1362 |   95.52 KB |
| BigJson_Pidgin           |  80.389 us |  11.6536 us | 0.6388 us |  11.1084 |   1.7090 |    91.7 KB |
| BigJson_Newtonsoft       |  49.972 us |   6.8959 us | 0.3780 us |  24.8413 |   8.2397 |   203.1 KB |
| BigJson_SystemTextJson   |  15.034 us |   0.7112 us | 0.0390 us |   2.9297 |   0.3204 |   24.12 KB |
| BigJson_Sprache          | 806.730 us | 130.6465 us | 7.1612 us | 632.8125 | 132.8125 | 5171.49 KB |
| BigJson_Superpower       | 369.483 us |  13.5161 us | 0.7409 us | 103.5156 |  18.0664 |  845.93 KB |
|                          |            |             |           |          |          |            |
| DeepJson_Parlot          |  28.972 us |   5.3522 us | 0.2934 us |  12.7869 |   1.4038 |  104.57 KB |
| DeepJson_ParlotGenerated |  24.236 us |   0.3640 us | 0.0200 us |  12.7563 |   1.3733 |  104.34 KB |
| DeepJson_Pidgin          | 101.181 us |  14.7459 us | 0.8083 us |  14.1602 |   3.5400 |  116.29 KB |
| DeepJson_Newtonsoft      |  31.064 us |   2.5722 us | 0.1410 us |  21.9116 |   8.7280 |  179.13 KB |
| DeepJson_SystemTextJson  |  59.066 us |   2.9614 us | 0.1623 us |   2.4414 |   0.1831 |   20.24 KB |
| DeepJson_Sprache         | 644.871 us |  11.5540 us | 0.6333 us | 344.7266 | 139.6484 | 2818.33 KB |
|                          |            |             |           |          |          |            |
| LongJson_Parlot          |  29.986 us |   1.3879 us | 0.0761 us |  13.7634 |   2.8076 |  112.52 KB |
| LongJson_ParlotGenerated |  28.654 us |   0.4009 us | 0.0220 us |  15.1978 |   3.7231 |  124.38 KB |
| LongJson_Pidgin          |  68.719 us |   8.6527 us | 0.4743 us |  14.6484 |   3.0518 |  120.25 KB |
| LongJson_Newtonsoft      |  38.660 us |  15.3922 us | 0.8437 us |  24.7803 |   9.6436 |  202.68 KB |
| LongJson_SystemTextJson  |   9.512 us |   2.7074 us | 0.1484 us |   2.9297 |   0.3204 |   24.12 KB |
| LongJson_Sprache         | 658.102 us |  97.0088 us | 5.3174 us | 513.6719 | 131.8359 |  4197.2 KB |
| LongJson_Superpower      | 297.433 us |   7.9671 us | 0.4367 us |  83.0078 |  20.0195 |  678.79 KB |
|                          |            |             |           |          |          |            |
| WideJson_Parlot          |  17.801 us |   4.7442 us | 0.2600 us |   4.9744 |   0.4883 |   40.72 KB |
| WideJson_ParlotGenerated |  14.613 us |   0.4515 us | 0.0247 us |   4.9591 |   0.5493 |   40.58 KB |
| WideJson_Pidgin          |  32.517 us |   1.9075 us | 0.1046 us |   4.9438 |   0.4883 |   40.48 KB |
| WideJson_Newtonsoft      |  24.943 us |   3.6234 us | 0.1986 us |  13.0310 |   3.2349 |  106.72 KB |
| WideJson_Sprache         | 359.650 us |  40.9163 us | 2.2428 us | 324.7070 |  44.4336 | 2654.69 KB |
| WideJson_Superpower      | 180.493 us |  16.6559 us | 0.9130 us |  49.3164 |   4.6387 |  403.63 KB |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  40.09 ns | 3.324 ns | 0.182 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  93.01 ns | 7.898 ns | 0.433 ns |  2.32 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  40.25 ns | 0.454 ns | 0.025 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 142.07 ns | 1.729 ns | 0.095 ns |  3.54 | 0.0372 |     312 B |        1.50 |
| ParlotEmailGenerated |  70.55 ns | 4.013 ns | 0.220 ns |  1.76 | 0.0229 |     192 B |        0.92 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot (current source)
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
