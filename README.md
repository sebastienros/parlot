# Parlot

[![NuGet](https://img.shields.io/nuget/v/Parlot.svg)](https://nuget.org/packages/Parlot)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![Join the chat at https://gitter.im/sebastienros/parlot](https://badges.gitter.im/sebastienros/parlot.svg)](https://gitter.im/sebastienros/parlot?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Fluent API

The Fluent API provides simple parser combinators that are assembled to express more complex expressions.
The main goal of this API is to provide an easy-to-read grammar. Another advantage is that grammars are built at runtime, and they can be extended dynamically.

The following example is a complete parser that creates a mathematical expression tree (AST).
The source is available [here](./src/Samples/Calc/FluentParser.cs).

```c#
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
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2314)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 9.0.100
  [Host]   : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean        | Error       | StdDev    | Ratio | Allocated | Alloc Ratio |
|-------------------- |------------:|------------:|----------:|------:|----------:|------------:|
| ParlotRawSmall      |    201.4 ns |    53.24 ns |   2.92 ns |  0.49 |     304 B |        0.44 |
| ParlotCompiledSmall |    407.4 ns |   297.60 ns |  16.31 ns |  1.00 |     688 B |        1.00 |
| ParlotFluentSmall   |    460.2 ns |   206.97 ns |  11.34 ns |  1.13 |     688 B |        1.00 |
| PidginSmall         |  4,890.6 ns | 1,535.62 ns |  84.17 ns | 12.02 |     832 B |        1.21 |
|                     |             |             |           |       |           |             |
| ParlotRawBig        |    967.4 ns |   378.73 ns |  20.76 ns |  0.41 |    1200 B |        0.39 |
| ParlotCompiledBig   |  2,347.6 ns |   455.92 ns |  24.99 ns |  1.00 |    3080 B |        1.00 |
| ParlotFluentBig     |  2,405.9 ns |   207.45 ns |  11.37 ns |  1.02 |    3080 B |        1.00 |
| PidginBig           | 25,741.2 ns | 6,880.56 ns | 377.15 ns | 10.97 |    4152 B |        1.35 |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The results show that Sprache and Superpower are the slowest and most allocating ones. This simple implementation is also faster than Newtonsoft, though it is far from being as rigorous. The best JSON parser is by far System.Text.Json, don't build your own!

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2314)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 9.0.100
  [Host]   : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                  | Mean        | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|------------------------ |------------:|-----------:|----------:|------:|-----------:|------------:|
| BigJson_ParlotCompiled  |   112.50 us |   0.705 us |  0.039 us |  1.00 |   91.73 KB |        1.00 |
| BigJson_Parlot          |   105.23 us |  34.390 us |  1.885 us |  0.94 |   91.73 KB |        1.00 |
| BigJson_Pidgin          |   120.40 us |  15.741 us |  0.863 us |  1.07 |    91.7 KB |        1.00 |
| BigJson_Newtonsoft      |   104.83 us | 532.735 us | 29.201 us |  0.93 |   203.1 KB |        2.21 |
| BigJson_SystemTextJson  |    20.75 us |   2.610 us |  0.143 us |  0.18 |   24.12 KB |        0.26 |
| BigJson_Sprache         | 1,287.67 us | 287.775 us | 15.774 us | 11.45 | 5271.74 KB |       57.47 |
| BigJson_Superpower      | 1,000.47 us | 409.978 us | 22.472 us |  8.89 |  905.93 KB |        9.88 |
|                         |             |            |           |       |            |             |
| DeepJson_ParlotCompiled |    36.59 us |   3.128 us |  0.171 us |  1.00 |    98.3 KB |        1.00 |
| DeepJson_Parlot         |    38.19 us |   1.670 us |  0.092 us |  1.04 |    98.3 KB |        1.00 |
| DeepJson_Pidgin         |   203.80 us |  39.255 us |  2.152 us |  5.57 |   98.79 KB |        1.01 |
| DeepJson_Newtonsoft     |    48.77 us |  13.462 us |  0.738 us |  1.33 |  179.13 KB |        1.82 |
| DeepJson_SystemTextJson |    61.72 us |   9.800 us |  0.537 us |  1.69 |   20.24 KB |        0.21 |
| DeepJson_Sprache        |   975.09 us | 210.722 us | 11.550 us | 26.65 | 2914.39 KB |       29.65 |
|                         |             |            |           |       |            |             |
| LongJson_ParlotCompiled |    69.21 us |  73.190 us |  4.012 us |  1.00 |  118.31 KB |        1.00 |
| LongJson_Parlot         |    65.53 us |   7.898 us |  0.433 us |  0.95 |  118.31 KB |        1.00 |
| LongJson_Pidgin         |   118.28 us |  17.916 us |  0.982 us |  1.71 |  120.25 KB |        1.02 |
| LongJson_Newtonsoft     |    67.76 us |  13.253 us |  0.726 us |  0.98 |  202.68 KB |        1.71 |
| LongJson_SystemTextJson |    14.39 us |   5.253 us |  0.288 us |  0.21 |   24.12 KB |        0.20 |
| LongJson_Sprache        | 1,099.49 us | 215.514 us | 11.813 us | 15.92 | 4261.26 KB |       36.02 |
| LongJson_Superpower     |   625.06 us | 238.207 us | 13.057 us |  9.05 |  726.79 KB |        6.14 |
|                         |             |            |           |       |            |             |
| WideJson_ParlotCompiled |    44.60 us |  19.276 us |  1.057 us |  1.00 |   40.53 KB |        1.00 |
| WideJson_Parlot         |    46.80 us |  29.437 us |  1.614 us |  1.05 |   40.53 KB |        1.00 |
| WideJson_Pidgin         |    46.29 us |  39.789 us |  2.181 us |  1.04 |   40.48 KB |        1.00 |
| WideJson_Newtonsoft     |    43.19 us |  15.428 us |  0.846 us |  0.97 |  106.72 KB |        2.63 |
| WideJson_Sprache        |   570.05 us | 148.927 us |  8.163 us | 12.79 | 2766.81 KB |       68.26 |
| WideJson_Superpower     |   556.49 us |  36.618 us |  2.007 us | 12.48 |  451.81 KB |       11.15 |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2314)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 9.0.100
  [Host]   : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean      | Error     | StdDev   | Ratio | Allocated | Alloc Ratio |
|-------------------- |----------:|----------:|---------:|------:|----------:|------------:|
| RegexEmailCompiled  |  75.70 ns | 22.363 ns | 1.226 ns |  1.00 |     208 B |        1.00 |
| RegexEmail          | 136.64 ns | 88.083 ns | 4.828 ns |  1.81 |     208 B |        1.00 |
| RegexEmailGenerated |  60.20 ns |  8.496 ns | 0.466 ns |  0.80 |     208 B |        1.00 |
| ParlotEmailCompiled | 124.27 ns | 25.065 ns | 1.374 ns |  1.64 |     128 B |        0.62 |
| ParlotEmail         | 188.65 ns | 53.202 ns | 2.916 ns |  2.49 |     320 B |        1.54 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot 1.0.2
- Pidgin 3.3.0
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
