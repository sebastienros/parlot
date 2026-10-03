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
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev    | Gen0   | Allocated |
|--------------------- |------------:|------------:|----------:|-------:|----------:|
| ParlotRawSmall       |    138.4 ns |    55.61 ns |   3.05 ns | 0.0362 |     304 B |
| ParlotFluentSmall    |    266.6 ns |   110.47 ns |   6.06 ns | 0.0668 |     560 B |
| ParlotGeneratedSmall |    207.8 ns |    36.11 ns |   1.98 ns | 0.0496 |     416 B |
| PidginSmall          |  3,994.6 ns | 1,064.23 ns |  58.33 ns | 0.0992 |     832 B |
| FarkleSmall          |    458.5 ns |    27.57 ns |   1.51 ns | 0.0267 |     224 B |
|                      |             |             |           |        |           |
| ParlotRawBig         |    687.3 ns |    83.89 ns |   4.60 ns | 0.1431 |    1200 B |
| ParlotFluentBig      |  1,472.6 ns |   558.48 ns |  30.61 ns | 0.1736 |    1456 B |
| ParlotGeneratedBig   |  1,171.2 ns |   400.43 ns |  21.95 ns | 0.1564 |    1312 B |
| PidginBig            | 17,323.0 ns | 1,966.10 ns | 107.77 ns | 0.4883 |    4152 B |
| FarkleBig            |  2,261.7 ns |    28.11 ns |   1.54 ns | 0.1335 |    1120 B |
|                      |             |             |           |        |           |
| ParlotFluentUnary    |    317.5 ns |    34.05 ns |   1.87 ns | 0.0782 |     656 B |
| ParlotGeneratedUnary |    269.1 ns |    73.87 ns |   4.05 ns | 0.0610 |     512 B |
| FarkleUnary          |    697.9 ns |   125.15 ns |   6.86 ns | 0.0381 |     320 B |
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

| Method                   | Mean      | Error      | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------------- |----------:|-----------:|----------:|---------:|---------:|-----------:|
| BigJson_Parlot           |  39.47 us |  37.518 us |  2.056 us |  10.7422 |   1.8311 |   88.16 KB |
| BigJson_ParlotGenerated  |  35.37 us |  12.636 us |  0.693 us |  11.6577 |   2.1362 |   95.52 KB |
| BigJson_Pidgin           |  88.95 us |  27.215 us |  1.492 us |  11.1084 |   1.7090 |    91.7 KB |
| BigJson_Farkle           |  91.37 us |  14.225 us |  0.780 us |   8.9111 |   1.3428 |   72.86 KB |
| BigJson_Newtonsoft       |  58.93 us |  24.566 us |  1.347 us |  24.8413 |   8.2397 |   203.1 KB |
| BigJson_SystemTextJson   |  17.19 us |   5.495 us |  0.301 us |   2.9297 |   0.3052 |   24.12 KB |
| BigJson_Sprache          | 929.96 us |  17.024 us |  0.933 us | 628.9063 | 128.9063 | 5141.74 KB |
| BigJson_Superpower       | 404.71 us | 249.793 us | 13.692 us | 103.5156 |  18.0664 |  845.93 KB |
|                          |           |            |           |          |          |            |
| DeepJson_Parlot          |  33.62 us |   2.377 us |  0.130 us |  12.7563 |   1.4038 |  104.57 KB |
| DeepJson_ParlotGenerated |  27.03 us |   1.500 us |  0.082 us |  12.7563 |   1.3733 |  104.34 KB |
| DeepJson_Pidgin          | 111.01 us |  71.918 us |  3.942 us |  14.1602 |   3.5400 |  116.29 KB |
| DeepJson_Farkle          |  42.77 us |   2.944 us |  0.161 us |   8.3008 |   1.0376 |   68.17 KB |
| DeepJson_Newtonsoft      |  35.87 us |   8.218 us |  0.450 us |  21.9116 |   8.7280 |  179.13 KB |
| DeepJson_SystemTextJson  |  65.61 us |  26.508 us |  1.453 us |   2.4414 |   0.1221 |   20.24 KB |
| DeepJson_Sprache         | 712.99 us | 261.026 us | 14.308 us | 346.6797 | 141.6016 | 2834.27 KB |
|                          |           |            |           |          |          |            |
| LongJson_Parlot          |  34.16 us |   3.810 us |  0.209 us |  13.7329 |   2.7466 |  112.52 KB |
| LongJson_ParlotGenerated |  29.84 us |   6.279 us |  0.344 us |  15.1978 |   3.7231 |  124.38 KB |
| LongJson_Pidgin          |  81.29 us |   7.422 us |  0.407 us |  14.6484 |   3.0518 |  120.25 KB |
| LongJson_Farkle          |  71.96 us |  29.272 us |  1.605 us |  10.7422 |   2.1973 |   88.19 KB |
| LongJson_Newtonsoft      |  44.43 us |   7.706 us |  0.422 us |  24.7803 |   9.6436 |  202.68 KB |
| LongJson_SystemTextJson  |  11.02 us |   2.154 us |  0.118 us |   2.9297 |   0.3204 |   24.12 KB |
| LongJson_Sprache         | 774.45 us | 183.814 us | 10.075 us | 513.6719 | 131.8359 |  4197.2 KB |
| LongJson_Superpower      | 337.19 us |  53.184 us |  2.915 us |  83.0078 |  20.0195 |  678.79 KB |
|                          |           |            |           |          |          |            |
| WideJson_Parlot          |  18.20 us |   4.637 us |  0.254 us |   4.9744 |   0.4883 |   40.72 KB |
| WideJson_ParlotGenerated |  13.56 us |   4.098 us |  0.225 us |   4.9591 |   0.5493 |   40.58 KB |
| WideJson_Pidgin          |  32.08 us |   1.495 us |  0.082 us |   4.9438 |   0.4883 |   40.48 KB |
| WideJson_Farkle          |  43.51 us |   0.632 us |  0.035 us |   5.6152 |   0.5493 |   45.92 KB |
| WideJson_Newtonsoft      |  24.78 us |   2.257 us |  0.124 us |  13.0615 |   3.2349 |  106.72 KB |
| WideJson_Sprache         | 369.76 us |  28.221 us |  1.547 us | 324.7070 |  44.4336 | 2654.69 KB |
| WideJson_Superpower      | 177.94 us |   4.054 us |  0.222 us |  49.3164 |   4.6387 |  403.81 KB |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers, Pidgin, and Farkle. Pidgin discards character results and Farkle uses a syntax-only DFA; both return the validated email without building an AST.

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
| RegexEmailCompiled   |  39.99 ns |  1.476 ns | 0.081 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  93.35 ns |  7.820 ns | 0.429 ns |  2.33 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  39.36 ns |  1.362 ns | 0.075 ns |  0.98 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 142.58 ns | 21.224 ns | 1.163 ns |  3.57 | 0.0372 |     312 B |        1.50 |
| ParlotEmailGenerated |  73.58 ns | 22.321 ns | 1.224 ns |  1.84 | 0.0229 |     192 B |        0.92 |
| PidginEmail          |  97.80 ns |  6.875 ns | 0.377 ns |  2.45 | 0.0048 |      40 B |        0.19 |
| FarkleEmail          | 106.33 ns | 12.799 ns | 0.702 ns |  2.66 |      - |         - |        0.00 |
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
| Document, `ReadToEnd` + `Parse(string)` | 1,000 | 310.4 us | 1.00 | 422.44 KB | 1.00 |
| Document, `Parse(TextReader)` | 1,000 | 364.3 us | 1.17 | 212.55 KB | 0.50 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 1,000 | 283.1 us | 1.00 | 422.44 KB | 1.00 |
| Document, generated `TryParse(TextReader)` | 1,000 | 322.6 us | 1.14 | 212.55 KB | 0.50 |
| Document, `ReadToEnd` + `Parse(string)` | 10,000 | 3.18 ms | 1.00 | 4.09 MB | 1.00 |
| Document, `Parse(TextReader)` | 10,000 | 3.73 ms | 1.17 | 2.15 MB | 0.53 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 10,000 | 3.07 ms | 1.00 | 4.09 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 10,000 | 3.24 ms | 1.05 | 2.15 MB | 0.53 |
| Document, `ReadToEnd` + `Parse(string)` | 100,000 | 33.7 ms | 1.00 | 41.04 MB | 1.00 |
| Document, `Parse(TextReader)` | 100,000 | 36.6 ms | 1.08 | 21.49 MB | 0.52 |
| Document, `ReadToEnd` + generated `TryParse(string)` | 100,000 | 30.8 ms | 1.00 | 41.04 MB | 1.00 |
| Document, generated `TryParse(TextReader)` | 100,000 | 32.8 ms | 1.06 | 21.49 MB | 0.52 |
| Lines, `ReadLine` + `Parse(string)` | 100,000 | 33.1 ms | 1.00 | 39.20 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100,000 | 31.7 ms | 0.96 | 20.61 MB | 0.53 |

Streaming a single value costs 5 to 17% more time and allocates half as much: `ReadToEnd` builds the text in a
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
