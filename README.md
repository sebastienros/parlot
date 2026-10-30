# Parlot

[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![GitHub Discussions](https://img.shields.io/github/discussions/sebastienros/parlot)](https://github.com/sebastienros/parlot/discussions)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Branches and NuGet feeds

| Branch | Purpose | Package feed |
| --- | --- | --- |
| [`release/1.x`](https://github.com/sebastienros/parlot/tree/release/1.x) | Maintenance of stable Parlot 1.x releases. | [![NuGet.org stable version](https://img.shields.io/nuget/v/Parlot.svg?label=nuget.org)](https://www.nuget.org/packages/Parlot) |
| [`main`](https://github.com/sebastienros/parlot/tree/main) | Development of Parlot 2.0, including source-generated parsers. | [![Feedz preview version](https://img.shields.io/endpoint?url=https%3A%2F%2Ff.feedz.io%2Fsebastienros%2Fparlot%2Fshield%2FParlot%2Flatest)](https://f.feedz.io/sebastienros/parlot/nuget/index.json) |

### Preview packages

Succefful build on `main`, publishes preview packages to the
[Parlot feed on feedz.io](https://f.feedz.io/sebastienros/parlot/nuget/index.json).
Versions follow `2.0.0-preview-<run number>`, using the GitHub Actions build run number.
These packages contain the latest development changes and are intended for testing before release.

Add the preview feed alongside NuGet.org, then install the latest prerelease version:

```shell
dotnet nuget add source https://f.feedz.io/sebastienros/parlot/nuget/index.json --name parlot-preview
dotnet add package Parlot --prerelease
```

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
- [Parser Explorer](docs/explorer.md) — local generated-parser diagnostics and replay
- [Parsing streams](docs/streaming.md)
- [Security guidance](docs/security.md)

## Parser Explorer

[Parlot Explorer](tools/Parlot.Explorer/README.md) is a local .NET tool for inspecting source-generated parsers
in a standalone window on Windows, macOS, and Linux.
Edit the input, replay named parser calls and backtracking, inspect the buffer at each step, and view the
returned object as a tree or JSON—all on your computer.

![Parlot Explorer showing editable input, parser trace replay, and a structured result](docs/images/parser-explorer.jpg)

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

- Use a compiler host with Roslyn 5.0 or later and C# 12 or later. Generated code supports `net472`, `netstandard2.0`, `net8.0`, and `net10.0`; older targets use compatibility packages such as `System.Memory`, not Parlot.
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

Parlot is faster and allocates less memory than all other known (to me) parser combinators for .NET.

It was originally created to provide a more efficient alternative to projects like:

- [Superpower](https://github.com/nblumhardt/superpower)
- [Sprache](https://github.com/sprache/Sprache)
- [Irony](https://github.com/IronyProject/Irony)

Other popular ones are: 

- [Pidgin](https://github.com/benjamin-hodgson/Pidgin)
- [Farkle](https://github.com/teo-tsirpanis/Farkle)

To reproduce:

```bash
dotnet build -c Release
dotnet run --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release --no-build -- --filter "*ExprBench*" "*JsonBench*" "*RegexBenchmarks*"
```

The tables below were refreshed on 2026-10-30 using the existing BenchmarkDotNet `ShortRun` jobs,
without command-line iteration or warmup overrides. Short runs are indicative; use the reported
errors when comparing close timings.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises three expressions:

- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`
- Unary: `-(3 + 2) * -4 + --6`

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin and Farkle. It parses the expressions into the same AST without evaluating them.

In these results, Parlot Fluent is about 13-15 times faster than Pidgin and Parlot Raw is faster still.
The source-generated parser allocates 136 fewer bytes per parse than Fluent in all three expressions.
Generated helpers use normal JIT inlining heuristics to avoid expanding
large parser chains into oversized native methods.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------:|------------:|---------:|------:|-------:|----------:|------------:|
| ParlotRawSmall       |    139.1 ns |     7.60 ns |  0.42 ns |  0.64 | 0.0353 |     296 B |        0.54 |
| ParlotFluentSmall    |    218.3 ns |    17.04 ns |  0.93 ns |  1.00 | 0.0648 |     544 B |        1.00 |
| ParlotGeneratedSmall |    170.1 ns |    12.83 ns |  0.70 ns |  0.78 | 0.0486 |     408 B |        0.75 |
| PidginSmall          |  3,190.6 ns | 1,129.58 ns | 61.92 ns | 14.62 | 0.0992 |     832 B |        1.53 |
| FarkleSmall          |    400.4 ns |    15.08 ns |  0.83 ns |  1.83 | 0.0267 |     224 B |        0.41 |
|                      |             |             |          |       |        |           |             |
| ParlotRawBig         |    661.5 ns |    19.71 ns |  1.08 ns |  0.56 | 0.1421 |    1192 B |        0.83 |
| ParlotFluentBig      |  1,186.3 ns |    10.90 ns |  0.60 ns |  1.00 | 0.1717 |    1440 B |        1.00 |
| ParlotGeneratedBig   |    915.7 ns |    29.25 ns |  1.60 ns |  0.77 | 0.1554 |    1304 B |        0.91 |
| PidginBig            | 15,879.2 ns | 1,816.29 ns | 99.56 ns | 13.38 | 0.4883 |    4152 B |        2.88 |
| FarkleBig            |  2,164.0 ns |    19.83 ns |  1.09 ns |  1.82 | 0.1335 |    1120 B |        0.78 |
|                      |             |             |          |       |        |           |             |
| ParlotFluentUnary    |    279.4 ns |     5.63 ns |  0.31 ns |  1.00 | 0.0763 |     640 B |        1.00 |
| ParlotGeneratedUnary |    224.0 ns |    10.06 ns |  0.55 ns |  0.80 | 0.0601 |     504 B |        0.79 |
| FarkleUnary          |    574.6 ns |    38.59 ns |  2.12 ns |  2.06 | 0.0381 |     320 B |        0.50 |
```

Ratios are relative to Fluent for the same expression.

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot use parser combinators; Farkle uses an LR grammar. These grammars parse the same subset of JSON: strings, arrays, and objects.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Farkle, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!
The generated documents use a fixed random seed so every parser receives the same input in its benchmark process.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error       | StdDev    | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |-----------:|------------:|----------:|------:|---------:|---------:|-----------:|------------:|
| BigJson_Parlot           |  32.909 us |   2.9546 us | 0.1620 us |  1.00 |  10.7422 |   1.7700 |   88.15 KB |        1.00 |
| BigJson_ParlotGenerated  |  29.974 us |   1.1543 us | 0.0633 us |  0.91 |  11.6882 |   2.1057 |   95.51 KB |        1.08 |
| BigJson_Pidgin           |  86.078 us |  58.4185 us | 3.2021 us |  2.62 |  11.1084 |   1.7090 |    91.7 KB |        1.04 |
| BigJson_Farkle           |  78.333 us |   4.4311 us | 0.2429 us |  2.38 |   8.9111 |   1.3428 |   72.86 KB |        0.83 |
| BigJson_Newtonsoft       |  49.021 us |   4.5821 us | 0.2512 us |  1.49 |  24.8413 |   8.2397 |   203.1 KB |        2.30 |
| BigJson_SystemTextJson   |  15.331 us |   1.2569 us | 0.0689 us |  0.47 |   2.9297 |   0.3052 |   24.12 KB |        0.27 |
| BigJson_Sprache          | 830.444 us | 179.2073 us | 9.8230 us | 25.24 | 638.6719 | 138.6719 | 5221.74 KB |       59.24 |
| BigJson_Superpower       | 388.690 us |  20.2018 us | 1.1073 us | 11.81 | 103.5156 |  18.0664 |  845.93 KB |        9.60 |
|                          |            |             |           |       |          |          |            |             |
| DeepJson_Parlot          |  28.490 us |   2.9086 us | 0.1594 us |  1.00 |  12.7869 |   1.5869 |  104.55 KB |        1.00 |
| DeepJson_ParlotGenerated |  22.197 us |   8.1116 us | 0.4446 us |  0.78 |  12.7563 |   1.3123 |  104.32 KB |        1.00 |
| DeepJson_Pidgin          |  99.257 us |   5.6072 us | 0.3073 us |  3.48 |  14.1602 |   3.5400 |  116.29 KB |        1.11 |
| DeepJson_Farkle          |  36.519 us |   0.3359 us | 0.0184 us |  1.28 |   8.3008 |   1.0376 |   68.17 KB |        0.65 |
| DeepJson_Newtonsoft      |  31.110 us |   0.8113 us | 0.0445 us |  1.09 |  21.9116 |   8.7280 |  179.13 KB |        1.71 |
| DeepJson_SystemTextJson  |  58.630 us |   5.3010 us | 0.2906 us |  2.06 |   2.4414 |   0.1831 |   20.24 KB |        0.19 |
| DeepJson_Sprache         | 604.248 us |  53.0376 us | 2.9072 us | 21.21 | 340.8203 | 145.5078 | 2786.27 KB |       26.65 |
|                          |            |             |           |       |          |          |            |             |
| LongJson_Parlot          |  28.468 us |   0.0133 us | 0.0007 us |  1.00 |  13.7634 |   2.7466 |  112.51 KB |        1.00 |
| LongJson_ParlotGenerated |  25.518 us |   0.2279 us | 0.0125 us |  0.90 |  15.1978 |   3.7537 |  124.37 KB |        1.11 |
| LongJson_Pidgin          |  72.271 us |   4.5421 us | 0.2490 us |  2.54 |  14.6484 |   3.0518 |  120.25 KB |        1.07 |
| LongJson_Farkle          |  63.091 us |   0.2630 us | 0.0144 us |  2.22 |  10.7422 |   2.1973 |   88.19 KB |        0.78 |
| LongJson_Newtonsoft      |  36.738 us |   2.6486 us | 0.1452 us |  1.29 |  24.7803 |   9.6436 |  202.68 KB |        1.80 |
| LongJson_SystemTextJson  |   9.438 us |   0.0244 us | 0.0013 us |  0.33 |   2.9297 |   0.3204 |   24.12 KB |        0.21 |
| LongJson_Sprache         | 661.604 us |  76.8797 us | 4.2140 us | 23.24 | 507.8125 | 126.9531 |  4149.2 KB |       36.88 |
| LongJson_Superpower      | 295.365 us |   5.8045 us | 0.3182 us | 10.38 |  83.0078 |  20.0195 |  678.79 KB |        6.03 |
|                          |            |             |           |       |          |          |            |             |
| WideJson_Parlot          |  16.020 us |   3.7863 us | 0.2075 us |  1.00 |   4.9744 |   0.4578 |    40.7 KB |        1.00 |
| WideJson_ParlotGenerated |  12.908 us |   0.3187 us | 0.0175 us |  0.81 |   4.9591 |   0.4883 |   40.56 KB |        1.00 |
| WideJson_Pidgin          |  34.016 us |   0.6118 us | 0.0335 us |  2.12 |   4.9438 |   0.4883 |   40.48 KB |        0.99 |
| WideJson_Farkle          |  44.601 us |   8.6381 us | 0.4735 us |  2.78 |   5.6152 |   0.5493 |   45.92 KB |        1.13 |
| WideJson_Newtonsoft      |  24.366 us |   1.0882 us | 0.0596 us |  1.52 |  13.0310 |   3.2349 |  106.72 KB |        2.62 |
| WideJson_Sprache         | 372.785 us | 144.0816 us | 7.8976 us | 23.27 | 320.8008 |  45.4102 | 2622.69 KB |       64.43 |
| WideJson_Superpower      | 184.622 us |   9.2038 us | 0.5045 us | 11.53 |  51.2695 |   5.3711 |  419.69 KB |       10.31 |
```

Ratios are relative to Parlot Fluent for the same document.

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers, Pidgin, and Farkle. Pidgin discards character results and Farkle uses a syntax-only DFA; both return the validated email without building an AST.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|----------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  40.40 ns | 10.904 ns | 0.598 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  92.72 ns |  1.747 ns | 0.096 ns |  2.30 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  39.41 ns |  2.153 ns | 0.118 ns |  0.98 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 134.24 ns | 18.287 ns | 1.002 ns |  3.32 | 0.0353 |     296 B |        1.42 |
| ParlotEmailGenerated |  58.05 ns | 14.788 ns | 0.811 ns |  1.44 | 0.0210 |     176 B |        0.85 |
| PidginEmail          |  87.81 ns | 20.173 ns | 1.106 ns |  2.17 | 0.0048 |      40 B |        0.19 |
| FarkleEmail          | 113.52 ns |  2.349 ns | 0.129 ns |  2.81 |      - |         - |        0.00 |
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
| Document, `ReadToEnd` + `Parse(string)` | 1,000 | 239.9 us | 1.00 | 422.46 KB | 1.00 |
| Document, `Parse(TextReader)` | 1,000 | 289.7 us | 1.21 | 212.55 KB | 0.50 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 1,000 | 208.7 us | 1.00 | 418.04 KB | 1.00 |
| Document, generated `TryParse(TextReader)` | 1,000 | 249.8 us | 1.20 | 208.13 KB | 0.50 |
| Document, `ReadToEnd` + `Parse(string)` | 10,000 | 2.48 ms | 1.00 | 4.09 MB | 1.00 |
| Document, `Parse(TextReader)` | 10,000 | 2.90 ms | 1.17 | 2.15 MB | 0.53 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 10,000 | 2.09 ms | 1.00 | 4.05 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 10,000 | 2.61 ms | 1.25 | 2.11 MB | 0.52 |
| Document, `ReadToEnd` + `Parse(string)` | 100,000 | 26.4 ms | 1.00 | 41.04 MB | 1.00 |
| Document, `Parse(TextReader)` | 100,000 | 29.1 ms | 1.10 | 21.49 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 100,000 | 22.7 ms | 1.00 | 40.61 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 100,000 | 24.8 ms | 1.09 | 21.06 MB | 0.52 |
| Lines, `ReadLine` + `Parse(string)` | 100,000 | 26.6 ms | 1.00 | 39.20 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100,000 | 24.3 ms | 0.91 | 20.61 MB | 0.53 |

Streaming a single value costs 9 to 25% more time and allocates about half as much. The non-streaming methods hold the
whole document in memory, while the streaming ones hold about 4,218 characters regardless of document size:

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

Parlot overloads the C# operators on `Parser<T>` as shorthands for the fluent combinators. Each operator builds exactly
the parser its method would, so they can be mixed with method calls freely.

| Operator | Equivalent | Result |
|---|---|---|
| `a + b` | `a.And(b)` | `(A, B)`, flattened across a chain |
| `a \| b` | `a.Or(b)` | `A` |
| `a >> b` | `a.SkipAnd(b)` | `B`, the arrow points to the kept result |
| `a << b` | `a.AndSkip(b)` | `A`, the arrow points to the kept result |
| `a - b` | `Not(b).SkipAnd(a)` | `A`, unless `b` matches at the same position |
| `a % b` | `Separated(b, a)` | `IReadOnlyList<A>`, one or more `a` separated by `b` |
| `!a` | `Not(a)` | succeeds without consuming when `a` fails |
| `-a` | `a.Optional()` | `Option<A>` |
| `+a` | `a.OneOrMany()` | `IReadOnlyList<A>` |
| `~a` | `a.ZeroOrMany()` | `IReadOnlyList<A>`, possibly empty |

```c#
// Using operators
var parser = Literals.Char('a') + Literals.Char('b') + Literals.Char('c');
var choice = Literals.Char('x') | Literals.Char('y') | Literals.Char('z');
var list = Terms.Char('[') >> Terms.Integer() % Terms.Char(',') << Terms.Char(']');
var name = Terms.Identifier() - (Terms.Keyword("if") | Terms.Keyword("else"));
var signed = -Terms.Char('-') + Terms.Integer();

// Equivalent to
var parser = Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c'));
var choice = Literals.Char('x').Or(Literals.Char('y')).Or(Literals.Char('z'));
var list = Terms.Char('[').SkipAnd(Separated(Terms.Char(','), Terms.Integer())).AndSkip(Terms.Char(']'));
var name = Not(Terms.Keyword("if").Or(Terms.Keyword("else"))).SkipAnd(Terms.Identifier());
var signed = Terms.Char('-').Optional().And(Terms.Integer());
```

The operators keep their C# precedence: unary operators bind tightest, then `%`, then `+` and `-`, then `<<` and `>>`,
and `|` last. So `a + b >> c + d` is `(a + b) >> (c + d)`, and a choice between sequences needs no parentheses. Use
parentheses when a choice is an operand of a sequence, as in `(a | b) + c`.

### Usages

Parlot is already used in these projects:

- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
- [YesSql](https://github.com/sebastienros/yessql)
- [NCalc](https://github.com/ncalc/ncalc)
- [hyperbee.xs] https://github.com/Stillpoint-Software/hyperbee.xs
