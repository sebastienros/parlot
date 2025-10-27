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

## Compilation

Grammar trees built using the Fluent API can optionally be compiled with the `Compile()` method. At that point, instead of evaluating recursively all the parsers in the grammar tree, these 
are converted to a more linear and optimized but equivalent compiled IL. This can improve the performance by 20% (see benchmarks results).

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

Only Pidgin and Parlot are benchmarked here. These benchmarks don't evaluate the expressions but only parse them to create the same AST.

In this benchmark, Parlot Fluent is more than 10 times faster than Pidgin, and Parlot Raw gives another 2 times boost. Allocations are also smaller with Parlot.
When compiled, the Parlot grammar shows even better results, without losing its simplicity.

```
BenchmarkDotNet v0.15.0, Windows 11 (10.0.26100.4770/24H2/2024Update/HudsonValley)
12th Gen Intel Core i7-1260P 2.10GHz, 1 CPU, 16 logical and 12 physical cores
.NET SDK 10.0.100-preview.6.25358.103
  [Host]   : .NET 9.0.7 (9.0.725.31616), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.7 (9.0.725.31616), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| ParlotRawSmall      |    234.0 ns |   133.27 ns |   7.31 ns |  0.49 |    0.01 | 0.0322 |     304 B |        0.43 |
| ParlotCompiledSmall |    481.6 ns |    89.48 ns |   4.90 ns |  1.00 |    0.01 | 0.0753 |     712 B |        1.00 |
| ParlotFluentSmall   |    486.7 ns |   219.33 ns |  12.02 ns |  1.01 |    0.02 | 0.0753 |     712 B |        1.00 |
| PidginSmall         |  5,301.9 ns | 1,864.01 ns | 102.17 ns | 11.01 |    0.21 | 0.0839 |     832 B |        1.17 |
|                     |             |             |           |       |         |        |           |             |
| ParlotRawBig        |  1,074.4 ns |   115.56 ns |   6.33 ns |  0.45 |    0.01 | 0.1259 |    1200 B |        0.39 |
| ParlotCompiledBig   |  2,403.9 ns |   777.57 ns |  42.62 ns |  1.00 |    0.02 | 0.3281 |    3104 B |        1.00 |
| ParlotFluentBig     |  2,443.7 ns |   204.37 ns |  11.20 ns |  1.02 |    0.02 | 0.3281 |    3104 B |        1.00 |
| PidginBig           | 26,210.1 ns | 3,920.26 ns | 214.88 ns | 10.91 |    0.19 | 0.4272 |    4152 B |        1.34 |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The results show that Sprache and Superpower are the slowest and most allocating ones. This simple implementation is also faster than Newtonsoft, though it is far from being as rigorous. The best JSON parser is by far System.Text.Json, don't build your own!

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3476)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 10.0.100-preview.2.25164.34
  [Host]   : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                  | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |------------:|-----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| BigJson_ParlotCompiled  |   103.55 us |  97.726 us |  5.357 us |  1.00 |    0.06 |   9.8877 |   1.7090 |   91.76 KB |        1.00 |
| BigJson_Parlot          |   109.54 us |  10.337 us |  0.567 us |  1.06 |    0.05 |   9.8877 |   1.3428 |   91.76 KB |        1.00 |
| BigJson_Pidgin          |   113.67 us |  16.122 us |  0.884 us |  1.10 |    0.05 |   9.8877 |   1.7090 |    91.7 KB |        1.00 |
| BigJson_Newtonsoft      |    88.96 us |  39.806 us |  2.182 us |  0.86 |    0.04 |  22.0947 |  10.7422 |   203.1 KB |        2.21 |
| BigJson_SystemTextJson  |    16.58 us |   5.167 us |  0.283 us |  0.16 |    0.01 |   2.5940 |   0.1526 |   24.12 KB |        0.26 |
| BigJson_Sprache         | 1,198.82 us | 367.849 us | 20.163 us | 11.60 |    0.55 | 572.2656 | 113.2813 | 5271.74 KB |       57.45 |
| BigJson_Superpower      |   917.17 us | 105.805 us |  5.800 us |  8.87 |    0.41 |  97.6563 |  13.6719 |  905.93 KB |        9.87 |
|                         |             |            |           |       |         |          |          |            |             |
| DeepJson_ParlotCompiled |    37.58 us |  10.674 us |  0.585 us |  1.00 |    0.02 |  10.6812 |   1.2817 |   98.32 KB |        1.00 |
| DeepJson_Parlot         |    42.41 us |  19.550 us |  1.072 us |  1.13 |    0.03 |  10.6812 |   1.2817 |   98.32 KB |        1.00 |
| DeepJson_Pidgin         |   191.21 us |  26.727 us |  1.465 us |  5.09 |    0.08 |  10.7422 |   2.1973 |   98.79 KB |        1.00 |
| DeepJson_Newtonsoft     |    49.74 us |  11.019 us |  0.604 us |  1.32 |    0.02 |  19.4702 |   5.7373 |  179.13 KB |        1.82 |
| DeepJson_SystemTextJson |    58.23 us |   3.305 us |  0.181 us |  1.55 |    0.02 |   2.1973 |   0.1221 |   20.24 KB |        0.21 |
| DeepJson_Sprache        |   888.98 us |  95.883 us |  5.256 us | 23.66 |    0.34 | 316.4063 | 110.3516 | 2914.39 KB |       29.64 |
|                         |             |            |           |       |         |          |          |            |             |
| LongJson_ParlotCompiled |    64.75 us |  79.715 us |  4.369 us |  1.00 |    0.08 |  12.8174 |   3.1738 |  118.34 KB |        1.00 |
| LongJson_Parlot         |    65.13 us |  15.860 us |  0.869 us |  1.01 |    0.06 |  12.8174 |   3.1738 |  118.34 KB |        1.00 |
| LongJson_Pidgin         |   106.46 us |  33.390 us |  1.830 us |  1.65 |    0.10 |  13.0615 |   2.5635 |  120.25 KB |        1.02 |
| LongJson_Newtonsoft     |    63.70 us |  30.461 us |  1.670 us |  0.99 |    0.06 |  21.9727 |   8.0566 |  202.68 KB |        1.71 |
| LongJson_SystemTextJson |    12.23 us |   2.406 us |  0.132 us |  0.19 |    0.01 |   2.6093 |   0.1526 |   24.12 KB |        0.20 |
| LongJson_Sprache        |   981.93 us | 281.616 us | 15.436 us | 15.21 |    0.88 | 462.8906 |  97.6563 | 4261.26 KB |       36.01 |
| LongJson_Superpower     |   558.36 us | 103.129 us |  5.653 us |  8.65 |    0.49 |  78.1250 |  15.6250 |  726.79 KB |        6.14 |
|                         |             |            |           |       |         |          |          |            |             |
| WideJson_ParlotCompiled |    42.51 us |  12.363 us |  0.678 us |  1.00 |    0.02 |   4.3945 |   0.4272 |   40.55 KB |        1.00 |
| WideJson_Parlot         |    48.11 us |  13.988 us |  0.767 us |  1.13 |    0.02 |   4.3945 |   0.4272 |   40.55 KB |        1.00 |
| WideJson_Pidgin         |    41.59 us |  14.049 us |  0.770 us |  0.98 |    0.02 |   4.3945 |   0.3662 |   40.48 KB |        1.00 |
| WideJson_Newtonsoft     |    37.86 us |  10.418 us |  0.571 us |  0.89 |    0.02 |  11.5967 |   3.1738 |  106.72 KB |        2.63 |
| WideJson_Sprache        |   534.46 us |  57.222 us |  3.137 us | 12.57 |    0.18 | 300.7813 |  38.0859 | 2766.81 KB |       68.22 |
| WideJson_Superpower     |   386.10 us |  59.677 us |  3.271 us |  9.08 |    0.14 |  48.8281 |   4.3945 |  451.81 KB |       11.14 |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3476)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 10.0.100-preview.2.25164.34
  [Host]   : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| RegexEmailCompiled  |  67.15 ns |  12.98 ns | 0.711 ns |  1.00 |    0.01 | 0.0221 |     208 B |        1.00 |
| RegexEmail          | 135.23 ns |  99.35 ns | 5.446 ns |  2.01 |    0.07 | 0.0219 |     208 B |        1.00 |
| RegexEmailGenerated |  55.02 ns |  17.55 ns | 0.962 ns |  0.82 |    0.01 | 0.0221 |     208 B |        1.00 |
| ParlotEmailCompiled | 133.83 ns |  24.35 ns | 1.335 ns |  1.99 |    0.03 | 0.0160 |     152 B |        0.73 |
| ParlotEmail         | 190.46 ns | 116.02 ns | 6.360 ns |  2.84 |    0.09 | 0.0365 |     344 B |        1.65 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot 1.3.5
- Pidgin 3.4.0
- Sprache 3.0.0-develop-00049
- Superpower 3.0.0
- Newtonsoft.Json 13.0.3

### Usages

Parlot is already used in these projects:

- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
- [YesSql](https://github.com/sebastienros/yessql)
- [NCalc](https://github.com/ncalc/ncalc)
- [hyperbee.xs] https://github.com/Stillpoint-Software/hyperbee.xs
