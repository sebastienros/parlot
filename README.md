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

In these results, Parlot Fluent is about 13-14 times faster than Pidgin, Parlot Raw is faster still, and source generation improves on Fluent parsing while allocating less.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.8 (24G824) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean        | Error     | StdDev   | Gen0   | Allocated |
|--------------------- |------------:|----------:|---------:|-------:|----------:|
| ParlotRawSmall       |    137.4 ns |  16.87 ns |  0.92 ns | 0.0362 |     304 B |
| ParlotFluentSmall    |    212.6 ns |  15.12 ns |  0.83 ns | 0.0668 |     560 B |
| ParlotGeneratedSmall |    173.4 ns |   9.90 ns |  0.54 ns | 0.0496 |     416 B |
| PidginSmall          |  3,004.7 ns | 649.75 ns | 35.61 ns | 0.0992 |     832 B |
|                      |             |           |          |        |           |
| ParlotRawBig         |    694.6 ns |   9.31 ns |  0.51 ns | 0.1431 |    1200 B |
| ParlotFluentBig      |  1,159.1 ns |  42.50 ns |  2.33 ns | 0.1736 |    1456 B |
| ParlotGeneratedBig   |    981.0 ns | 124.41 ns |  6.82 ns | 0.1564 |    1312 B |
| PidginBig            | 15,087.5 ns | 631.39 ns | 34.61 ns | 0.4883 |    4152 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinators.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The benchmark compares Fluent and source-generated Parlot parsers with Pidgin, Sprache, Superpower, Newtonsoft.Json, and System.Text.Json. For most documents, the best JSON parser is System.Text.Json; don't build your own!

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.8 (24G824) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                   | Mean       | Error      | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------------- |-----------:|-----------:|----------:|---------:|---------:|-----------:|
| BigJson_Parlot           |  37.543 us |  0.8230 us | 0.0451 us |  11.6577 |   2.0142 |   95.66 KB |
| BigJson_ParlotGenerated  |  31.460 us | 14.9022 us | 0.8168 us |  11.2305 |   1.8921 |    91.8 KB |
| BigJson_Pidgin           |  76.461 us |  3.7114 us | 0.2034 us |  11.1084 |   1.7090 |    91.7 KB |
| BigJson_Newtonsoft       |  51.177 us | 25.9966 us | 1.4250 us |  24.8413 |   8.2397 |   203.1 KB |
| BigJson_SystemTextJson   |  18.408 us | 97.0510 us | 5.3197 us |   2.9297 |   0.3204 |   24.12 KB |
| BigJson_Sprache          | 789.885 us | 61.7248 us | 3.3833 us | 609.3750 | 122.0703 | 4980.05 KB |
| BigJson_Superpower       | 373.309 us | 26.2033 us | 1.4363 us | 103.5156 |  18.0664 |  845.93 KB |
|                          |            |            |           |          |          |            |
| DeepJson_Parlot          |  30.047 us |  2.7066 us | 0.1484 us |  14.2517 |   1.6785 |  116.57 KB |
| DeepJson_ParlotGenerated |  22.902 us | 10.0453 us | 0.5506 us |  12.0239 |   1.2512 |   98.36 KB |
| DeepJson_Pidgin          | 107.987 us | 78.4544 us | 4.3003 us |  14.1602 |   3.5400 |  116.29 KB |
| DeepJson_Newtonsoft      |  37.605 us |  2.4738 us | 0.1356 us |  21.9116 |   8.7280 |  179.13 KB |
| DeepJson_SystemTextJson  |  59.026 us |  3.1666 us | 0.1736 us |   2.4414 |   0.1831 |   20.24 KB |
| DeepJson_Sprache         | 651.383 us | 10.3188 us | 0.5656 us | 338.8672 | 139.6484 |  2770.2 KB |
|                          |            |            |           |          |          |            |
| LongJson_Parlot          |  32.043 us |  1.6211 us | 0.0889 us |  15.1978 |   3.0518 |  124.52 KB |
| LongJson_ParlotGenerated |  25.221 us |  0.4153 us | 0.0228 us |  14.4653 |   3.5706 |  118.38 KB |
| LongJson_Pidgin          |  67.923 us |  1.6013 us | 0.0878 us |  14.6484 |   3.0518 |  120.25 KB |
| LongJson_Newtonsoft      |  38.784 us | 12.1090 us | 0.6637 us |  24.7803 |   9.6436 |  202.68 KB |
| LongJson_SystemTextJson  |   9.833 us |  3.0383 us | 0.1665 us |   2.9297 |   0.3204 |   24.12 KB |
| LongJson_Sprache         | 683.235 us | 18.4119 us | 1.0092 us | 511.7188 | 130.8594 |  4181.2 KB |
| LongJson_Superpower      | 302.714 us | 20.1201 us | 1.1029 us |  83.0078 |  20.0195 |  678.79 KB |
|                          |            |            |           |          |          |            |
| WideJson_Parlot          |  16.975 us |  0.7353 us | 0.0403 us |   4.9744 |   0.4883 |   40.72 KB |
| WideJson_ParlotGenerated |  14.156 us |  1.1152 us | 0.0611 us |   4.9591 |   0.4883 |   40.59 KB |
| WideJson_Pidgin          |  32.560 us |  3.7986 us | 0.2082 us |   4.9438 |   0.4883 |   40.48 KB |
| WideJson_Newtonsoft      |  25.610 us |  1.7123 us | 0.0939 us |  13.0615 |   3.2349 |  106.72 KB |
| WideJson_Sprache         | 365.637 us | 53.3936 us | 2.9267 us | 318.8477 |  45.4102 | 2606.25 KB |
| WideJson_Superpower      | 174.801 us |  7.9470 us | 0.4356 us |  51.2695 |   5.1270 |  419.75 KB |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.
The benchmark compares regular, compiled, and source-generated .NET regular expressions with Fluent and source-generated Parlot parsers.

```
BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.8 (24G824) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.9 (10.0.9, 10.0.926.27113), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method               | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| RegexEmailCompiled   |  40.35 ns | 2.688 ns | 0.147 ns |  1.00 | 0.0249 |     208 B |        1.00 |
| RegexEmail           |  92.52 ns | 2.671 ns | 0.146 ns |  2.29 | 0.0248 |     208 B |        1.00 |
| RegexEmailGenerated  |  39.52 ns | 2.756 ns | 0.151 ns |  0.98 | 0.0249 |     208 B |        1.00 |
| ParlotEmail          | 139.38 ns | 3.087 ns | 0.169 ns |  3.45 | 0.0372 |     312 B |        1.50 |
| ParlotEmailGenerated |  97.09 ns | 2.432 ns | 0.133 ns |  2.41 | 0.0229 |     192 B |        0.92 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot (current source)
- Pidgin 3.5.1
- Sprache 3.0.0-develop-00049
- Superpower 3.2.1
- Newtonsoft.Json 13.0.4

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
