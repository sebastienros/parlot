# Parlot

[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![GitHub Discussions](https://img.shields.io/github/discussions/sebastienros/parlot)](https://github.com/sebastienros/parlot/discussions)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Branches and NuGet feeds

| Branch | Purpose | Package feed |
| --- | --- | --- |
| [`release/1.x`](https://github.com/sebastienros/parlot/tree/release/1.x) | Maintenance of stable Parlot 1.x releases. | [![NuGet.org stable version](https://img.shields.io/nuget/v/Parlot.svg?label=nuget.org)](https://www.nuget.org/packages/Parlot) |
| [`main`](https://github.com/sebastienros/parlot/tree/main) | Development of Parlot 2.0, including source-generated parsers. | [![Feedz preview version](https://img.shields.io/endpoint?url=https%3A%2F%2Ff.feedz.io%2Fsebastienros%2Fparlot%2Fshield%2FParlot%2Flatest)](https://feedz.io/org/sebastienros/repository/parlot/packages/Parlot) |

### Preview packages

Succefful build on `main`, publishes preview packages to the
[Parlot feed on feedz.io](https://f.feedz.io/sebastienros/parlot/nuget/index.json).
Versions follow `2.0.0-preview.<run number>`, using the GitHub Actions build run number.
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

The same table can be declared at once with `primary.Operators()`, which parses it with a single precedence climbing
loop that looks for the operator following an operand once whatever the number of levels, and also supports postfix
operators, see [Operator tables](docs/parsers.md#operator-tables).

## Documentation

- [Existing parsers and usage examples](docs/parsers.md)
- [Best practices for custom parsers](docs/writing.md)
- [Source generation guide](docs/source-generation.md)
- [Parser Explorer](docs/explorer.md) — local generated-parser diagnostics and replay
- [Grammar rules](docs/grammar-rules.md) — the patterns Parlot reports as slow or wrong, and how to fix them
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

- Use a compiler host with Roslyn 5.0 or later and C# 10 or later. Generated code supports `net472`, `netstandard2.0`, `net8.0`, and `net10.0`; older targets use compatibility packages such as `System.Memory`, not Parlot.
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

| Method               | Mean        | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------:|----------:|---------:|------:|-------:|----------:|------------:|
| ParlotRawSmall       |    120.9 ns |   2.71 ns |  0.15 ns |  0.55 | 0.0353 |     296 B |        0.54 |
| ParlotFluentSmall    |    219.3 ns |  17.95 ns |  0.98 ns |  1.00 | 0.0648 |     544 B |        1.00 |
| ParlotGeneratedSmall |    160.2 ns |   3.38 ns |  0.19 ns |  0.73 | 0.0486 |     408 B |        0.75 |
| PidginSmall          |  2,978.3 ns | 121.07 ns |  6.64 ns | 13.58 | 0.0992 |     832 B |        1.53 |
| FarkleSmall          |    417.2 ns |  75.69 ns |  4.15 ns |  1.90 | 0.0267 |     224 B |        0.41 |
|                      |             |           |          |       |        |           |             |
| ParlotRawBig         |    661.1 ns |  38.45 ns |  2.11 ns |  0.60 | 0.1421 |    1192 B |        0.83 |
| ParlotFluentBig      |  1,098.1 ns |  90.80 ns |  4.98 ns |  1.00 | 0.1717 |    1440 B |        1.00 |
| ParlotGeneratedBig   |    924.4 ns | 232.71 ns | 12.76 ns |  0.84 | 0.1554 |    1304 B |        0.91 |
| PidginBig            | 16,058.3 ns | 629.86 ns | 34.52 ns | 14.62 | 0.4883 |    4152 B |        2.88 |
| FarkleBig            |  2,156.7 ns | 235.07 ns | 12.88 ns |  1.96 | 0.1335 |    1120 B |        0.78 |
|                      |             |           |          |       |        |           |             |
| ParlotFluentUnary    |    275.1 ns |   2.57 ns |  0.14 ns |  1.00 | 0.0763 |     640 B |        1.00 |
| ParlotGeneratedUnary |    220.7 ns |  15.76 ns |  0.86 ns |  0.80 | 0.0601 |     504 B |        0.79 |
| FarkleUnary          |    568.0 ns |   5.46 ns |  0.30 ns |  2.07 | 0.0381 |     320 B |        0.50 |
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

| Method                   | Mean       | Error      | StdDev    | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |-----------:|-----------:|----------:|------:|---------:|---------:|-----------:|------------:|
| BigJson_Parlot           |  32.224 us |  2.7577 us | 0.1512 us |  1.00 |  10.7422 |   1.7700 |   88.15 KB |        1.00 |
| BigJson_ParlotGenerated  |  29.374 us |  1.0358 us | 0.0568 us |  0.91 |  11.6882 |   2.1057 |   95.51 KB |        1.08 |
| BigJson_Pidgin           |  85.517 us | 11.9370 us | 0.6543 us |  2.65 |  11.1084 |   1.7090 |    91.7 KB |        1.04 |
| BigJson_Farkle           |  76.748 us |  7.4357 us | 0.4076 us |  2.38 |   8.9111 |   1.3428 |   72.86 KB |        0.83 |
| BigJson_Newtonsoft       |  48.449 us |  7.6430 us | 0.4189 us |  1.50 |  24.8413 |   8.2397 |   203.1 KB |        2.30 |
| BigJson_SystemTextJson   |  14.482 us |  0.3366 us | 0.0184 us |  0.45 |   2.9297 |   0.3204 |   24.12 KB |        0.27 |
| BigJson_Sprache          | 756.560 us | 51.9793 us | 2.8492 us | 23.48 | 613.2813 | 125.0000 | 5011.43 KB |       56.85 |
| BigJson_Superpower       | 342.335 us | 20.6510 us | 1.1320 us | 10.62 | 103.5156 |  18.0664 |  845.93 KB |        9.60 |
|                          |            |            |           |       |          |          |            |             |
| DeepJson_Parlot          |  26.284 us |  0.1531 us | 0.0084 us |  1.00 |  12.7869 |   1.5869 |  104.55 KB |        1.00 |
| DeepJson_ParlotGenerated |  20.775 us |  1.5505 us | 0.0850 us |  0.79 |  12.7563 |   1.3123 |  104.32 KB |        1.00 |
| DeepJson_Pidgin          |  95.705 us |  8.1459 us | 0.4465 us |  3.64 |  14.1602 |   3.5400 |  116.29 KB |        1.11 |
| DeepJson_Farkle          |  35.854 us |  2.0163 us | 0.1105 us |  1.36 |   8.3008 |   1.0376 |   68.17 KB |        0.65 |
| DeepJson_Newtonsoft      |  30.857 us |  1.9070 us | 0.1045 us |  1.17 |  21.9116 |   8.7280 |  179.13 KB |        1.71 |
| DeepJson_SystemTextJson  |  55.390 us |  2.0405 us | 0.1118 us |  2.11 |   2.4414 |   0.1831 |   20.24 KB |        0.19 |
| DeepJson_Sprache         | 605.412 us | 25.3428 us | 1.3891 us | 23.03 | 340.8203 | 145.5078 |  2786.2 KB |       26.65 |
|                          |            |            |           |       |          |          |            |             |
| LongJson_Parlot          |  27.776 us |  1.5196 us | 0.0833 us |  1.00 |  13.7634 |   2.7466 |  112.51 KB |        1.00 |
| LongJson_ParlotGenerated |  24.853 us |  2.8926 us | 0.1586 us |  0.89 |  15.1978 |   3.7537 |  124.37 KB |        1.11 |
| LongJson_Pidgin          |  74.068 us |  2.9960 us | 0.1642 us |  2.67 |  14.6484 |   3.0518 |  120.25 KB |        1.07 |
| LongJson_Farkle          |  61.998 us |  2.9736 us | 0.1630 us |  2.23 |  10.7422 |   2.1973 |   88.19 KB |        0.78 |
| LongJson_Newtonsoft      |  36.517 us |  0.9236 us | 0.0506 us |  1.31 |  24.7803 |   9.7656 |  202.68 KB |        1.80 |
| LongJson_SystemTextJson  |   9.281 us |  0.1690 us | 0.0093 us |  0.33 |   2.9297 |   0.3204 |   24.12 KB |        0.21 |
| LongJson_Sprache         | 634.640 us | 20.6063 us | 1.1295 us | 22.85 | 511.7188 | 130.8594 |  4181.2 KB |       37.16 |
| LongJson_Superpower      | 270.539 us | 64.6839 us | 3.5455 us |  9.74 |  83.0078 |  20.0195 |  678.79 KB |        6.03 |
|                          |            |            |           |       |          |          |            |             |
| WideJson_Parlot          |  15.321 us |  2.4136 us | 0.1323 us |  1.00 |   4.9744 |   0.4578 |    40.7 KB |        1.00 |
| WideJson_ParlotGenerated |  12.633 us |  0.1673 us | 0.0092 us |  0.82 |   4.9591 |   0.4883 |   40.56 KB |        1.00 |
| WideJson_Pidgin          |  31.404 us |  1.3129 us | 0.0720 us |  2.05 |   4.9438 |   0.4883 |   40.48 KB |        0.99 |
| WideJson_Farkle          |  42.888 us |  1.9269 us | 0.1056 us |  2.80 |   5.6152 |   0.5493 |   45.92 KB |        1.13 |
| WideJson_Newtonsoft      |  24.113 us |  1.2730 us | 0.0698 us |  1.57 |  13.0615 |   3.2349 |  106.72 KB |        2.62 |
| WideJson_Sprache         | 347.730 us | 22.6952 us | 1.2440 us | 22.70 | 322.7539 |  45.4102 | 2638.81 KB |       64.83 |
| WideJson_Superpower      | 167.898 us | 40.1261 us | 2.1994 us | 10.96 |  47.3633 |   4.3945 |  387.63 KB |        9.52 |
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
| RegexEmailCompiled   |  38.52 ns |  1.669 ns | 0.091 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  91.22 ns |  1.105 ns | 0.061 ns |  2.37 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  53.10 ns |  5.016 ns | 0.275 ns |  1.38 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 136.45 ns |  8.782 ns | 0.481 ns |  3.54 | 0.0353 |     296 B |        1.42 |
| ParlotEmailGenerated |  56.44 ns |  2.693 ns | 0.148 ns |  1.47 | 0.0210 |     176 B |        0.85 |
| PidginEmail          |  81.00 ns |  0.989 ns | 0.054 ns |  2.10 | 0.0048 |      40 B |        0.19 |
| FarkleEmail          | 107.08 ns | 16.246 ns | 0.890 ns |  2.78 |      - |         - |        0.00 |
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
| Document, `ReadToEnd` + `Parse(string)` | 1,000 | 234.3 us | 1.00 | 422.46 KB | 1.00 |
| Document, `Parse(TextReader)` | 1,000 | 280.3 us | 1.20 | 212.55 KB | 0.50 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 1,000 | 198.9 us | 1.00 | 418.04 KB | 1.00 |
| Document, generated `TryParse(TextReader)` | 1,000 | 241.2 us | 1.21 | 208.13 KB | 0.50 |
| Document, `ReadToEnd` + `Parse(string)` | 10,000 | 2.40 ms | 1.00 | 4.09 MB | 1.00 |
| Document, `Parse(TextReader)` | 10,000 | 2.78 ms | 1.16 | 2.15 MB | 0.53 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 10,000 | 2.01 ms | 1.00 | 4.05 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 10,000 | 2.41 ms | 1.20 | 2.11 MB | 0.52 |
| Document, `ReadToEnd` + `Parse(string)` | 100,000 | 25.4 ms | 1.00 | 41.04 MB | 1.00 |
| Document, `Parse(TextReader)` | 100,000 | 27.8 ms | 1.09 | 21.49 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 100,000 | 21.7 ms | 1.00 | 40.61 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 100,000 | 24.1 ms | 1.11 | 21.06 MB | 0.52 |
| Lines, `ReadLine` + `Parse(string)` | 100,000 | 26.0 ms | 1.00 | 39.20 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100,000 | 23.4 ms | 0.90 | 20.61 MB | 0.53 |

Streaming a single value costs 9 to 21% more time and allocates about half as much. The non-streaming methods hold the
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
