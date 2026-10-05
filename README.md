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

## Parser Explorer

[Parlot Explorer](tools/Parlot.Explorer/README.md) is a local .NET tool for inspecting source-generated parsers
in a standalone window on Windows, macOS, and Linux.
Edit the input, replay named parser calls and backtracking, inspect the buffer at each step, and view the
returned object as a tree or JSON—all on your computer.

![Parlot Explorer showing editable input, parser trace replay, and a structured result](docs/images/parser-explorer.jpg)

## Documentation

- [Existing parsers and usage examples](docs/parsers.md)
- [Best practices for custom parsers](docs/writing.md)
- [Source generation guide](docs/source-generation.md)
- [Parser Explorer](docs/explorer.md) — local generated-parser diagnostics and replay
- [Parsing streams](docs/streaming.md)
- [Security guidance](docs/security.md)

## Parsing streams

`Parse`, `TryParse`, `ParseAsync` and `TryParseAsync` parse a `TextReader` with the same parsers,
through a compacting buffer which only retains the text the parser can still read: a document of any size is parsed
in a single pass, with memory bounded by its largest token. `ParseManyAsync` parses successive values, such as NDJSON
records, with memory bounded by the largest value:

```csharp
using var document = File.OpenText("document.json");
var value = JsonParser.Json.Parse(document);

using var input = File.OpenText("records.ndjson");
await foreach (var item in record.ParseManyAsync(input, '\n'))
{
    Console.WriteLine(item);
}
```

See [Parsing streams](docs/streaming.md).

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
- Declare an overload taking a `TextReader` instead of the `string` to parse large inputs with the [compacting buffer](docs/streaming.md#generated-parsers).
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

To reproduce:

```bash
dotnet build -c Release
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release --no-build -- --filter "*ExprBench*" "*JsonBench*" "*RegexBenchmarks*"
```

The tables below were refreshed on 2026-10-02 using the existing BenchmarkDotNet `ShortRun` jobs,
without command-line iteration or warmup overrides. Short runs are indicative; use the reported
errors when comparing close timings.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises three expressions:

- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`
- Unary: `-(3 + 2) * -4 + --6`

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin and Farkle. It parses the expressions into the same AST without evaluating them.

In these results, Parlot Fluent is about 12-15 times faster than Pidgin and Parlot Raw is faster still.
The source-generated parser allocates 144 fewer bytes per parse than Fluent in all three expressions.
Generated helpers use normal JIT inlining heuristics to avoid expanding
large parser chains into oversized native methods.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------:|------------:|---------:|------:|-------:|----------:|------------:|
| ParlotRawSmall       |    128.3 ns |     8.18 ns |  0.45 ns |  0.58 | 0.0353 |     296 B |        0.54 |
| ParlotFluentSmall    |    222.8 ns |    14.18 ns |  0.78 ns |  1.00 | 0.0648 |     544 B |        1.00 |
| ParlotGeneratedSmall |    178.3 ns |    40.40 ns |  2.21 ns |  0.80 | 0.0477 |     400 B |        0.74 |
| PidginSmall          |  3,215.2 ns |   267.80 ns | 14.68 ns | 14.43 | 0.0992 |     832 B |        1.53 |
| FarkleSmall          |    405.8 ns |    65.94 ns |  3.61 ns |  1.82 | 0.0267 |     224 B |        0.41 |
|                      |             |             |          |       |        |           |             |
| ParlotRawBig         |    705.2 ns |   124.69 ns |  6.83 ns |  0.54 | 0.1421 |    1192 B |        0.83 |
| ParlotFluentBig      |  1,302.8 ns |   438.88 ns | 24.06 ns |  1.00 | 0.1717 |    1440 B |        1.00 |
| ParlotGeneratedBig   |  1,004.2 ns |    57.74 ns |  3.17 ns |  0.77 | 0.1545 |    1296 B |        0.90 |
| PidginBig            | 15,682.8 ns | 1,410.23 ns | 77.30 ns | 12.04 | 0.4883 |    4152 B |        2.88 |
| FarkleBig            |  2,188.4 ns |   479.76 ns | 26.30 ns |  1.68 | 0.1335 |    1120 B |        0.78 |
|                      |             |             |          |       |        |           |             |
| ParlotFluentUnary    |    296.4 ns |    93.76 ns |  5.14 ns |  1.00 | 0.0763 |     640 B |        1.00 |
| ParlotGeneratedUnary |    238.6 ns |    20.52 ns |  1.12 ns |  0.81 | 0.0591 |     496 B |        0.78 |
| FarkleUnary          |    590.3 ns |   256.37 ns | 14.05 ns |  1.99 | 0.0381 |     320 B |        0.50 |
```

Ratios are relative to Fluent for the same expression.

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot use parser combinators; Farkle uses an LR grammar. These grammars parse the same subset of JSON: strings, arrays, and objects.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Farkle, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!
The generated documents use a fixed random seed so every parser receives the same input in its benchmark process.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error       | StdDev     | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |-----------:|------------:|-----------:|------:|---------:|---------:|-----------:|------------:|
| BigJson_Parlot           |  34.885 us |   7.4518 us |  0.4085 us |  1.00 |  10.7422 |   1.7700 |   88.15 KB |        1.00 |
| BigJson_ParlotGenerated  |  32.070 us |   1.9522 us |  0.1070 us |  0.92 |  11.6577 |   2.0142 |   95.51 KB |        1.08 |
| BigJson_Pidgin           |  77.520 us |   5.0888 us |  0.2789 us |  2.22 |  11.1084 |   1.7090 |    91.7 KB |        1.04 |
| BigJson_Farkle           |  78.567 us |   4.5833 us |  0.2512 us |  2.25 |   8.9111 |   1.3428 |   72.86 KB |        0.83 |
| BigJson_Newtonsoft       |  49.532 us |   6.7218 us |  0.3684 us |  1.42 |  24.8413 |   8.2397 |   203.1 KB |        2.30 |
| BigJson_SystemTextJson   |  15.439 us |   0.7737 us |  0.0424 us |  0.44 |   2.9297 |   0.3052 |   24.12 KB |        0.27 |
| BigJson_Sprache          | 811.027 us |  49.5139 us |  2.7140 us | 23.25 | 623.0469 | 125.0000 | 5091.43 KB |       57.76 |
| BigJson_Superpower       | 375.518 us |  45.5568 us |  2.4971 us | 10.77 | 103.5156 |  18.0664 |  845.93 KB |        9.60 |
|                          |            |             |            |       |          |          |            |             |
| DeepJson_Parlot          |  28.128 us |   2.1797 us |  0.1195 us |  1.00 |  12.7869 |   1.5869 |  104.55 KB |        1.00 |
| DeepJson_ParlotGenerated |  23.901 us |   2.3720 us |  0.1300 us |  0.85 |  12.7563 |   1.3123 |  104.32 KB |        1.00 |
| DeepJson_Pidgin          | 104.012 us |   3.5108 us |  0.1924 us |  3.70 |  14.1602 |   3.5400 |  116.29 KB |        1.11 |
| DeepJson_Farkle          |  37.255 us |   2.0543 us |  0.1126 us |  1.32 |   8.3008 |   1.0376 |   68.17 KB |        0.65 |
| DeepJson_Newtonsoft      |  33.949 us |   5.5867 us |  0.3062 us |  1.21 |  21.9116 |   8.7280 |  179.13 KB |        1.71 |
| DeepJson_SystemTextJson  |  83.354 us |   1.2695 us |  0.0696 us |  2.96 |   2.4414 |   0.1221 |   20.24 KB |        0.19 |
| DeepJson_Sprache         | 637.307 us |  32.5709 us |  1.7853 us | 22.66 | 348.6328 | 145.5078 | 2850.33 KB |       27.26 |
|                          |            |             |            |       |          |          |            |             |
| LongJson_Parlot          |  30.259 us |   1.7757 us |  0.0973 us |  1.00 |  13.7634 |   2.7466 |  112.51 KB |        1.00 |
| LongJson_ParlotGenerated |  26.640 us |   2.2033 us |  0.1208 us |  0.88 |  15.1978 |   3.7537 |  124.37 KB |        1.11 |
| LongJson_Pidgin          |  80.427 us |   5.0746 us |  0.2782 us |  2.66 |  14.6484 |   3.0518 |  120.25 KB |        1.07 |
| LongJson_Farkle          |  63.803 us |   0.8305 us |  0.0455 us |  2.11 |  10.7422 |   2.1973 |   88.19 KB |        0.78 |
| LongJson_Newtonsoft      |  38.303 us |   0.6839 us |  0.0375 us |  1.27 |  24.7803 |   9.6436 |  202.68 KB |        1.80 |
| LongJson_SystemTextJson  |   9.625 us |   0.3114 us |  0.0171 us |  0.32 |   2.9297 |   0.3204 |   24.12 KB |        0.21 |
| LongJson_Sprache         | 691.664 us | 569.9542 us | 31.2411 us | 22.86 | 509.7656 | 129.8828 |  4165.2 KB |       37.02 |
| LongJson_Superpower      | 309.774 us |  17.6229 us |  0.9660 us | 10.24 |  83.0078 |  20.0195 |  678.79 KB |        6.03 |
|                          |            |             |            |       |          |          |            |             |
| WideJson_Parlot          |  16.840 us |   0.7818 us |  0.0429 us |  1.00 |   4.9744 |   0.4578 |    40.7 KB |        1.00 |
| WideJson_ParlotGenerated |  13.593 us |   0.2554 us |  0.0140 us |  0.81 |   4.9591 |   0.4883 |   40.56 KB |        1.00 |
| WideJson_Pidgin          |  33.476 us |   1.9246 us |  0.1055 us |  1.99 |   4.9438 |   0.4883 |   40.48 KB |        0.99 |
| WideJson_Farkle          |  44.814 us |   1.4430 us |  0.0791 us |  2.66 |   5.6152 |   0.5493 |   45.92 KB |        1.13 |
| WideJson_Newtonsoft      |  25.334 us |   2.7433 us |  0.1504 us |  1.50 |  13.0310 |   3.2349 |  106.72 KB |        2.62 |
| WideJson_Sprache         | 360.265 us |  10.3194 us |  0.5656 us | 21.39 | 316.8945 |  44.9219 | 2590.25 KB |       63.64 |
| WideJson_Superpower      | 178.036 us |   0.7385 us |  0.0405 us | 10.57 |  51.2695 |   5.1270 |  419.63 KB |       10.31 |
```

Ratios are relative to Parlot Fluent for the same document.

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers, Pidgin, and Farkle. Pidgin discards character results and Farkle uses a syntax-only DFA; both return the validated email without building an AST.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|----------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  40.63 ns |  9.302 ns | 0.510 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  95.85 ns | 50.626 ns | 2.775 ns |  2.36 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  39.57 ns | 13.281 ns | 0.728 ns |  0.97 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 149.88 ns |  7.895 ns | 0.433 ns |  3.69 | 0.0353 |     296 B |        1.42 |
| ParlotEmailGenerated |  75.41 ns | 10.186 ns | 0.558 ns |  1.86 | 0.0210 |     176 B |        0.85 |
| PidginEmail          |  99.69 ns |  2.514 ns | 0.138 ns |  2.45 | 0.0048 |      40 B |        0.19 |
| FarkleEmail          | 110.78 ns |  3.036 ns | 0.166 ns |  2.73 |      - |         - |        0.00 |
```

### Streaming Benchmarks

This benchmark counts the failed records of an access log with a grammar of patterns, keywords, numbers and quoted
strings (`src/Samples/AccessLog`). It doesn't allocate its results, so it measures the parsers rather than a model. The log
is read from a `TextReader`, compared to reading the whole text first and parsing the `string`. `Document` parses the
log of `Count` records as a single value, `Lines` parses one record per line. The reader isn't a `StringReader`, which
would be parsed as a string directly. Ratios are relative to the non-streaming method of the same group. The generated
`TextReader` overload is benchmarked in an assembly that also declares reader entry points.

| Method | Count | Mean | Ratio | Allocated | Alloc ratio |
|---|---:|---:|---:|---:|---:|
| Document, `ReadToEnd` + `Parse(string)` | 1,000 | 311.5 us | 1.00 | 422.44 KB | 1.00 |
| Document, `Parse(TextReader)` | 1,000 | 364.2 us | 1.17 | 212.55 KB | 0.50 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 1,000 | 272.3 us | 1.00 | 418.02 KB | 1.00 |
| Document, generated `TryParse(TextReader)` | 1,000 | 309.7 us | 1.14 | 208.13 KB | 0.50 |
| Document, `ReadToEnd` + `Parse(string)` | 10,000 | 3.20 ms | 1.00 | 4.09 MB | 1.00 |
| Document, `Parse(TextReader)` | 10,000 | 3.69 ms | 1.15 | 2.15 MB | 0.53 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 10,000 | 2.80 ms | 1.00 | 4.05 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 10,000 | 3.07 ms | 1.09 | 2.11 MB | 0.52 |
| Document, `ReadToEnd` + `Parse(string)` | 100,000 | 34.3 ms | 1.00 | 41.04 MB | 1.00 |
| Document, `Parse(TextReader)` | 100,000 | 36.7 ms | 1.07 | 21.49 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 100,000 | 29.7 ms | 1.00 | 40.61 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 100,000 | 30.8 ms | 1.04 | 21.06 MB | 0.52 |
| Lines, `ReadLine` + `Parse(string)` | 100,000 | 33.7 ms | 1.00 | 39.20 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100,000 | 31.4 ms | 0.93 | 20.61 MB | 0.53 |

Streaming a single value costs 4 to 17% more time and allocates about half as much: `ReadToEnd` builds the text in a
`StringBuilder` and copies it into a `string`, while streaming copies it once into small window strings, which never
survive to Gen2. The memory it retains doesn't grow with the document: the non-streaming methods hold the whole document
in memory, the streaming ones about 4,218 characters (the default 4,096-character buffer plus the record in progress):

| Count | Document characters | Peak buffered characters |
|---:|---:|---:|
| 1,000 | 100,854 | 4,165 |
| 10,000 | 1,021,903 | 4,214 |
| 100,000 | 10,352,412 | 4,218 |

See [Parsing streams](docs/streaming.md#performance) for the details and the command to run them.

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
