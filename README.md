# Parlot

[![NuGet](https://img.shields.io/nuget/v/Parlot.svg)](https://nuget.org/packages/Parlot)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![Join the chat at https://gitter.im/sebastienros/parlot](https://badges.gitter.im/sebastienros/parlot.svg)](https://gitter.im/sebastienros/parlot?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

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

Parlot can generate parsers at **compile time** using C# interceptors, avoiding runtime graph construction and yielding **~20% faster parsing** with **faster startup**.

### How it works

- Annotate static, **parameterless** methods returning `Parlot.Fluent.Parser<T>` with `[GenerateParser]`.
- The source generator executes the method at compile time to build the parser graph.
- Uses C# interceptors to replace calls to the method with the generated, optimized code.
- For parser variants (e.g., different keywords), create separate methods instead of using parameters.

```csharp
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class MyGrammar
{
    // Simple parser
    [GenerateParser]
    public static Parser<string> HelloParser() => Terms.Text("hello");

    // For variants, create separate methods
    [GenerateParser]
    public static Parser<string> FooParser() => Terms.Text("foo");

    [GenerateParser]
    public static Parser<string> BarParser() => Terms.Text("bar");
}

// Usage - calls are automatically intercepted
var hello = MyGrammar.HelloParser();  // Uses generated code
var foo = MyGrammar.FooParser();      // Uses generated code
```

### Requirements

- Add `<InterceptorsNamespaces>$(InterceptorsNamespaces);YourNamespace</InterceptorsNamespaces>` to your project file.
- Methods must be static and parameterless.
- The containing class should be `partial` (optional but recommended).

### Advanced Configuration

Additional attributes can be combined with `[GenerateParser]`:

- `[IncludeFiles("*.cs")]` – Include source files (supports globs) for types used by your parser.
- `[IncludeUsings("Namespace")]` – Add extra using directives to generated code.
- `[IncludeGenerators("AssemblyName")]` – Run other source generators before parser generation.

For detailed documentation, see [Source Generation Guide](docs/source-generation.md).

> **Why use source generation?**
> - Faster parsing than equivalent Fluent parser graphs (see benchmarks)
> - Faster startup (no runtime parser graph construction)
> - AOT-friendly, deterministic parser code
> - Zero runtime overhead from method interception

## Performance

Parlot is faster and allocates less memory than all other known parser combinators for .NET.

It was originally created to provide a more efficient alternative to projects like:

- [Superpower](https://github.com/nblumhardt/superpower)
- [Sprache](https://github.com/sprache/Sprache)
- [Irony](https://github.com/IronyProject/Irony)

Finally, even though [Pidgin](https://github.com/benjamin-hodgson/Pidgin) showed some very good performance, Parlot is still faster.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises two expressions:

- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`

The benchmark compares Raw, Fluent, and source-generated Parlot parsers with Pidgin. It parses the expressions into the same AST without evaluating them.

In these results, Parlot Fluent is about 12-14 times faster than Pidgin and Parlot Raw is faster still. The source-generated parser allocates less than the Fluent parser; this short run measured it 17% slower for the small expression and 2% slower for the big expression.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error       | StdDev    | Gen0   | Allocated |
|--------------------- |------------:|------------:|----------:|-------:|----------:|
| ParlotRawSmall       |    123.4 ns |     7.04 ns |   0.39 ns | 0.0362 |     304 B |
| ParlotFluentSmall    |    219.1 ns |     1.51 ns |   0.08 ns | 0.0668 |     560 B |
| ParlotGeneratedSmall |    256.8 ns |    19.05 ns |   1.04 ns | 0.0496 |     416 B |
| PidginSmall          |  3,105.8 ns |   686.70 ns |  37.64 ns | 0.0992 |     832 B |
|                      |             |             |           |        |           |
| ParlotRawBig         |    671.1 ns |    15.22 ns |   0.83 ns | 0.1431 |    1200 B |
| ParlotFluentBig      |  1,300.6 ns |   260.54 ns |  14.28 ns | 0.1736 |    1456 B |
| ParlotGeneratedBig   |  1,324.8 ns |    35.25 ns |   1.93 ns | 0.1564 |    1312 B |
| PidginBig            | 15,521.2 ns | 1,261.73 ns |  69.16 ns | 0.4883 |    4152 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinators.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error       | StdDev     | Gen0     | Gen1     | Allocated  |
|------------------------- |-----------:|------------:|-----------:|---------:|---------:|-----------:|
| BigJson_Parlot           |  36.797 us |   0.5195 us |  0.0285 us |  10.7422 |   1.8311 |   88.16 KB |
| BigJson_ParlotGenerated  |  31.659 us |   0.3483 us |  0.0191 us |  11.2305 |   1.8921 |    91.8 KB |
| BigJson_Pidgin           |  76.997 us |   0.8952 us |  0.0491 us |  11.1084 |   1.7090 |    91.7 KB |
| BigJson_Newtonsoft       |  48.892 us |   2.3675 us |  0.1298 us |  24.8413 |   8.2397 |   203.1 KB |
| BigJson_SystemTextJson   |  14.912 us |   0.7656 us |  0.0420 us |   2.9297 |   0.3204 |   24.12 KB |
| BigJson_Sprache          | 807.783 us | 129.9628 us |  7.1237 us | 631.8359 | 130.8594 | 5161.74 KB |
| BigJson_Superpower       | 356.457 us |  26.1486 us |  1.4333 us | 103.5156 |  18.0664 |  845.93 KB |
|                          |            |             |            |          |          |            |
| DeepJson_Parlot          |  28.795 us |   5.6271 us |  0.3084 us |  12.7869 |   1.4038 |  104.57 KB |
| DeepJson_ParlotGenerated |  24.167 us |   0.5363 us |  0.0294 us |  12.0239 |   1.2512 |   98.36 KB |
| DeepJson_Pidgin          | 100.953 us |  16.4148 us |  0.8998 us |  14.1602 |   3.5400 |  116.29 KB |
| DeepJson_Newtonsoft      |  41.074 us |   6.1893 us |  0.3393 us |  21.9116 |   8.7280 |  179.13 KB |
| DeepJson_SystemTextJson  |  58.011 us |   1.7527 us |  0.0961 us |   2.4414 |   0.1831 |   20.24 KB |
| DeepJson_Sprache         | 628.468 us | 353.3203 us | 19.3667 us | 342.7734 | 141.6016 | 2802.33 KB |
|                          |            |             |            |          |          |            |
| LongJson_Parlot          |  29.772 us |   1.0465 us |  0.0574 us |  13.7634 |   2.8076 |  112.52 KB |
| LongJson_ParlotGenerated |  26.750 us |   1.5866 us |  0.0870 us |  14.4653 |   3.5706 |  118.38 KB |
| LongJson_Pidgin          |  70.988 us |   9.4521 us |  0.5181 us |  14.6484 |   3.0518 |  120.25 KB |
| LongJson_Newtonsoft      |  37.257 us |   4.3815 us |  0.2402 us |  24.7803 |   9.6436 |  202.68 KB |
| LongJson_SystemTextJson  |   9.422 us |   0.2364 us |  0.0130 us |   2.9297 |   0.3204 |   24.12 KB |
| LongJson_Sprache         | 671.931 us | 127.6828 us |  6.9987 us | 509.7656 | 129.8828 |  4165.2 KB |
| LongJson_Superpower      | 295.297 us |  12.4582 us |  0.6829 us |  83.0078 |  20.0195 |  678.79 KB |
|                          |            |             |            |          |          |            |
| WideJson_Parlot          |  17.604 us |   1.3398 us |  0.0734 us |   4.9744 |   0.4883 |   40.72 KB |
| WideJson_ParlotGenerated |  14.807 us |   0.8907 us |  0.0488 us |   4.9591 |   0.4883 |   40.59 KB |
| WideJson_Pidgin          |  32.984 us |   4.0433 us |  0.2216 us |   4.9438 |   0.4883 |   40.48 KB |
| WideJson_Newtonsoft      |  24.726 us |   1.2357 us |  0.0677 us |  13.0615 |   3.2349 |  106.72 KB |
| WideJson_Sprache         | 354.187 us |  10.4665 us |  0.5737 us | 320.8008 |  45.4102 | 2622.69 KB |
| WideJson_Superpower      | 173.612 us |  10.3190 us |  0.5656 us |  51.2695 |   4.8828 |  419.81 KB |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|----------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  39.98 ns |  8.539 ns | 0.468 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  91.41 ns |  3.353 ns | 0.184 ns |  2.29 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  40.36 ns |  4.797 ns | 0.263 ns |  1.01 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 147.08 ns | 27.495 ns | 1.507 ns |  3.68 | 0.0372 |     312 B |        1.50 |
| ParlotEmailGenerated |  62.36 ns |  8.718 ns | 0.478 ns |  1.56 | 0.0229 |     192 B |        0.92 |
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
