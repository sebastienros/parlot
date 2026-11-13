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

    // The levels of operators, from the one which binds the tightest
    expression.Parser = primary.Operators()
        // unary => ( "-" ) unary | primary ;
        .Unary((minus, static x => new NegateExpression(x)))
        // multiplicative => unary ( ( "/" | "*" ) unary )* ;
        .LeftAssociative(
            (divided, static (a, b) => new Division(a, b)),
            (times, static (a, b) => new Multiplication(a, b)))
        // additive => multiplicative ( ( "-" | "+" ) multiplicative )* ;
        .LeftAssociative(
            (plus, static (a, b) => new Addition(a, b)),
            (minus, static (a, b) => new Subtraction(a, b)));

    Expression = expression;
}
```

`primary.Operators()` parses the table with a single precedence climbing loop that looks for the operator following an
operand once whatever the number of levels, and also supports postfix operators, see
[Operator tables](docs/parsers.md#operator-tables).

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

The tables below were refreshed on 2026-11-01 using the existing BenchmarkDotNet `ShortRun` jobs,
without command-line iteration or warmup overrides. Short runs are indicative; use the reported
errors when comparing close timings.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises three expressions:

- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`
- Unary: `-(3 + 2) * -4 + --6`

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin and Farkle. It parses the expressions into the same AST without evaluating them. The Fluent and source-generated parsers declare their operators in an `Operators()` table.

In these results, Parlot Fluent is about 18 times faster than Pidgin and Parlot Raw is faster still.
The source-generated parser allocates 56 fewer bytes per parse than Fluent in all three expressions.
Generated helpers use normal JIT inlining heuristics to avoid expanding
large parser chains into oversized native methods.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.3.26522.148
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------:|------------:|---------:|------:|--------:|-------:|----------:|------------:|
| ParlotRawSmall       |    108.0 ns |     1.03 ns |  0.06 ns |  0.64 |    0.00 | 0.0381 |     320 B |        0.77 |
| ParlotFluentSmall    |    169.5 ns |     5.63 ns |  0.31 ns |  1.00 |    0.00 | 0.0496 |     416 B |        1.00 |
| ParlotGeneratedSmall |    129.3 ns |     8.87 ns |  0.49 ns |  0.76 |    0.00 | 0.0429 |     360 B |        0.87 |
| PidginSmall          |  3,054.1 ns |   160.56 ns |  8.80 ns | 18.02 |    0.05 | 0.0992 |     832 B |        2.00 |
| FarkleSmall          |    417.6 ns |    11.37 ns |  0.62 ns |  2.46 |    0.01 | 0.0267 |     224 B |        0.54 |
|                      |             |             |          |       |         |        |           |             |
| ParlotRawBig         |    522.4 ns |    13.78 ns |  0.76 ns |  0.60 |    0.00 | 0.1450 |    1216 B |        0.93 |
| ParlotFluentBig      |    875.5 ns |     5.71 ns |  0.31 ns |  1.00 |    0.00 | 0.1564 |    1312 B |        1.00 |
| ParlotGeneratedBig   |    761.1 ns |    36.48 ns |  2.00 ns |  0.87 |    0.00 | 0.1497 |    1256 B |        0.96 |
| PidginBig            | 15,659.2 ns | 1,006.11 ns | 55.15 ns | 17.89 |    0.05 | 0.4883 |    4152 B |        3.16 |
| FarkleBig            |  2,097.2 ns |    58.78 ns |  3.22 ns |  2.40 |    0.00 | 0.1335 |    1120 B |        0.85 |
|                      |             |             |          |       |         |        |           |             |
| ParlotFluentUnary    |    254.6 ns |     4.05 ns |  0.22 ns |  1.00 |    0.00 | 0.0610 |     512 B |        1.00 |
| ParlotGeneratedUnary |    199.3 ns |    10.62 ns |  0.58 ns |  0.78 |    0.00 | 0.0544 |     456 B |        0.89 |
| FarkleUnary          |    589.3 ns |    73.24 ns |  4.01 ns |  2.31 |    0.01 | 0.0381 |     320 B |        0.62 |
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
.NET SDK 11.0.100-rc.3.26522.148
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------- |-----------:|-----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| BigJson_Parlot           |  31.171 us |  1.0625 us | 0.0582 us |  1.00 |    0.00 |  10.3149 |   1.1597 |   84.27 KB |        1.00 |
| BigJson_ParlotGenerated  |  28.062 us |  1.6161 us | 0.0886 us |  0.90 |    0.00 |  10.2844 |   1.2512 |   84.21 KB |        1.00 |
| BigJson_Pidgin           |  86.521 us |  7.8265 us | 0.4290 us |  2.78 |    0.01 |  11.1084 |   1.7090 |    91.7 KB |        1.09 |
| BigJson_Farkle           |  78.948 us |  3.7799 us | 0.2072 us |  2.53 |    0.01 |   8.9111 |   1.3428 |   72.86 KB |        0.86 |
| BigJson_Newtonsoft       |  50.904 us |  2.9816 us | 0.1634 us |  1.63 |    0.01 |  24.8413 |   8.2397 |   203.1 KB |        2.41 |
| BigJson_SystemTextJson   |  15.171 us |  0.9603 us | 0.0526 us |  0.49 |    0.00 |   2.9297 |   0.3204 |   24.12 KB |        0.29 |
| BigJson_Sprache          | 796.452 us | 57.1454 us | 3.1323 us | 25.55 |    0.10 | 618.1641 | 126.9531 | 5051.43 KB |       59.95 |
| BigJson_Superpower       | 392.005 us | 31.0295 us | 1.7008 us | 12.58 |    0.05 | 103.5156 |  18.0664 |  845.93 KB |       10.04 |
|                          |            |            |           |       |         |          |          |            |             |
| DeepJson_Parlot          |  22.800 us |  0.5232 us | 0.0287 us |  1.00 |    0.00 |   9.5825 |   1.0681 |    78.3 KB |        1.00 |
| DeepJson_ParlotGenerated |  20.232 us |  1.5778 us | 0.0865 us |  0.89 |    0.00 |   9.5520 |   1.0376 |   78.25 KB |        1.00 |
| DeepJson_Pidgin          | 101.043 us | 23.7923 us | 1.3041 us |  4.43 |    0.05 |  14.1602 |   3.5400 |  116.29 KB |        1.49 |
| DeepJson_Farkle          |  38.336 us |  2.8322 us | 0.1552 us |  1.68 |    0.01 |   8.3008 |   1.0376 |   68.17 KB |        0.87 |
| DeepJson_Newtonsoft      |  33.022 us |  3.6007 us | 0.1974 us |  1.45 |    0.01 |  21.9116 |   8.7280 |  179.13 KB |        2.29 |
| DeepJson_SystemTextJson  |  59.287 us |  2.6963 us | 0.1478 us |  2.60 |    0.01 |   2.4414 |   0.1831 |   20.24 KB |        0.26 |
| DeepJson_Sprache         | 654.161 us | 20.7175 us | 1.1356 us | 28.69 |    0.05 | 348.6328 | 145.5078 | 2850.33 KB |       36.40 |
|                          |            |            |           |       |         |          |          |            |             |
| LongJson_Parlot          |  26.126 us |  0.7757 us | 0.0425 us |  1.00 |    0.00 |  11.7798 |   1.4648 |   96.23 KB |        1.00 |
| LongJson_ParlotGenerated |  23.861 us |  1.3436 us | 0.0736 us |  0.91 |    0.00 |  11.7493 |   1.4648 |   96.18 KB |        1.00 |
| LongJson_Pidgin          |  75.840 us |  6.1959 us | 0.3396 us |  2.90 |    0.01 |  14.6484 |   3.0518 |  120.25 KB |        1.25 |
| LongJson_Farkle          |  63.730 us |  5.9786 us | 0.3277 us |  2.44 |    0.01 |  10.7422 |   2.1973 |   88.19 KB |        0.92 |
| LongJson_Newtonsoft      |  38.062 us |  2.0910 us | 0.1146 us |  1.46 |    0.00 |  24.7803 |   9.6436 |  202.68 KB |        2.11 |
| LongJson_SystemTextJson  |   9.571 us |  0.9407 us | 0.0516 us |  0.37 |    0.00 |   2.9297 |   0.3204 |   24.12 KB |        0.25 |
| LongJson_Sprache         | 669.181 us | 77.6534 us | 4.2564 us | 25.61 |    0.15 | 511.7188 | 130.8594 |  4181.2 KB |       43.45 |
| LongJson_Superpower      | 302.899 us | 19.2105 us | 1.0530 us | 11.59 |    0.04 |  83.0078 |  20.0195 |  678.79 KB |        7.05 |
|                          |            |            |           |       |         |          |          |            |             |
| WideJson_Parlot          |  14.903 us |  1.1840 us | 0.0649 us |  1.00 |    0.01 |   4.4556 |   0.2289 |   36.43 KB |        1.00 |
| WideJson_ParlotGenerated |  11.689 us |  0.2258 us | 0.0124 us |  0.78 |    0.00 |   4.4403 |   0.2594 |   36.38 KB |        1.00 |
| WideJson_Pidgin          |  34.859 us |  9.1015 us | 0.4989 us |  2.34 |    0.03 |   4.9438 |   0.4883 |   40.48 KB |        1.11 |
| WideJson_Farkle          |  43.153 us |  1.5055 us | 0.0825 us |  2.90 |    0.01 |   5.6152 |   0.5493 |   45.92 KB |        1.26 |
| WideJson_Newtonsoft      |  24.422 us |  2.2983 us | 0.1260 us |  1.64 |    0.01 |  13.0615 |   3.2349 |  106.72 KB |        2.93 |
| WideJson_Sprache         | 362.587 us | 14.0980 us | 0.7728 us | 24.33 |    0.10 | 318.8477 |  45.4102 |    2606 KB |       71.54 |
| WideJson_Superpower      | 184.924 us | 12.7243 us | 0.6975 us | 12.41 |    0.06 |  51.2695 |   5.1270 |  419.69 KB |       11.52 |
```

Ratios are relative to Parlot Fluent for the same document.

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers, Pidgin, and Farkle. Pidgin discards character results and Farkle uses a syntax-only DFA; both return the validated email without building an AST.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.3.26522.148
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  39.28 ns |  1.600 ns | 0.088 ns |  1.00 |    0.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  91.51 ns |  7.194 ns | 0.394 ns |  2.33 |    0.01 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  38.47 ns |  1.374 ns | 0.075 ns |  0.98 |    0.00 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 106.78 ns |  0.692 ns | 0.038 ns |  2.72 |    0.01 | 0.0229 |     192 B |        0.92 |
| ParlotEmailGenerated |  27.35 ns |  6.815 ns | 0.374 ns |  0.70 |    0.01 | 0.0153 |     128 B |        0.62 |
| PidginEmail          |  81.93 ns |  3.505 ns | 0.192 ns |  2.09 |    0.01 | 0.0048 |      40 B |        0.19 |
| FarkleEmail          | 111.98 ns | 64.564 ns | 3.539 ns |  2.85 |    0.08 |      - |         - |        0.00 |
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
| Document, `ReadToEnd` + `Parse(string)` | 1,000 | 198.8 us | 1.00 | 418.17 KB | 1.00 |
| Document, `Parse(TextReader)` | 1,000 | 254.8 us | 1.28 | 208.27 KB | 0.50 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 1,000 | 146.4 us | 1.00 | 413.69 KB | 1.00 |
| Document, generated `TryParse(TextReader)` | 1,000 | 193.7 us | 1.32 | 203.82 KB | 0.49 |
| Document, `ReadToEnd` + `Parse(string)` | 10,000 | 2.10 ms | 1.00 | 4.00 MB | 1.00 |
| Document, `Parse(TextReader)` | 10,000 | 2.55 ms | 1.21 | 2.06 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 10,000 | 1.55 ms | 1.00 | 3.96 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 10,000 | 1.93 ms | 1.24 | 2.02 MB | 0.51 |
| Document, `ReadToEnd` + `Parse(string)` | 100,000 | 22.3 ms | 1.00 | 40.42 MB | 1.00 |
| Document, `Parse(TextReader)` | 100,000 | 25.2 ms | 1.13 | 20.87 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 100,000 | 16.5 ms | 1.00 | 39.99 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 100,000 | 19.4 ms | 1.18 | 20.44 MB | 0.51 |
| Lines, `ReadLine` + `Parse(string)` | 100,000 | 21.5 ms | 1.00 | 40.72 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100,000 | 20.8 ms | 0.96 | 20.63 MB | 0.51 |

Streaming a single value costs 13 to 32% more time and allocates about half as much. The non-streaming methods hold the
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
- [hyperbee.xs](https://github.com/Stillpoint-Software/hyperbee.xs)
- [Spring.NET](https://github.com/spring-projects/spring-net)
