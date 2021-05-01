# Parlot

[![NuGet](https://img.shields.io/nuget/v/Parlot.svg)](https://nuget.org/packages/Parlot)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE)

Parlot is a __fast__, __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Fluent API

The Fluent API provides simple parser combinators that are assembled to express more complex expressions.
The main goal of this API is to provide and easy-to-read grammar. Another advantage is that grammars are built at runtime, and they can be extended dynamically.

The following example is a complete parser that create a mathematical expression tree (AST).
The source is available [here](./test/Parlot.Tests/Calc/FluentParser.cs).

```c#
public static readonly Parser<Expression> Expression;

static FluentParser()
{
    /*
     * Grammar:
     * expression     => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
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

    // The Recursive helper allows to create parsers that depend on themselves.
    // ( "-" ) unary | primary;
    var unary = Recursive<Expression>((u) => 
        minus.And(u)
            .Then<Expression>(static x => new NegateExpression(x.Item2))
            .Or(primary));

    // factor => unary ( ( "/" | "*" ) unary )* ;
    var factor = unary.And(ZeroOrMany(divided.Or(times).And(unary)))
        .Then(static x =>
        {
            // unary
            var result = x.Item1;

            // (("/" | "*") unary ) *
            foreach (var op in x.Item2)
            {
                result = op.Item1 switch
                {
                    '/' => new Division(result, op.Item2),
                    '*' => new Multiplication(result, op.Item2),
                    _ => null
                };
            }

            return result;
        });

    // expression => factor ( ( "-" | "+" ) factor )* ;
    expression.Parser = factor.And(ZeroOrMany(plus.Or(minus).And(factor)))
        .Then(static x =>
        {
            // factor
            var result = x.Item1;

            // (("-" | "+") factor ) *
            foreach (var op in x.Item2)
            {
                result = op.Item1 switch
                {
                    '+' => new Addition(result, op.Item2),
                    '-' => new Subtraction(result, op.Item2),
                    _ => null
                };
            }

            return result;
        });            

    Expression = expression;
}
```

## Documentation

1- [Existing parsers and usage examples](docs/parsers.md)
2- [Best practices for custom parsers](docs/writing.md)

## Compilation

Grammar trees built using the Fluent API can optionally be compiled with the `Compile()` method. At that point instead of evaluating recursively all the parsers in the grammar tree, these 
are converted to a more linear and optimized and equivalent compiled IL. This can improve the performance by 20% (see benchmarks results).

## Performance

Parlot is faster and allocates less than all other known parser combinators for .NET.

It was originally made to provide a more efficient alternative to projects like
- [Superpower](https://github.com/nblumhardt/superpower)
- [Sprache](https://github.com/sprache/Sprache)
- [Irony](https://github.com/IronyProject/Irony)

Finally, even though [Pidgin](https://github.com/benjamin-hodgson/Pidgin) showed some very good performance, Parlot is still faster.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises two expressions:
- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`

Only Pidgin and Parlot are benchmarked here. These benchmarks don't evaluate the expressions but only parse them to create the same AST.

In this benchmark Parlot Fluent is more than 10 times faster than Pidgin, and Parlot Raw gives another 2 times boost. Allocations are also smaller with Parlot.
When compiled the Parlot grammar shows even better results, without losing its simplicity.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=6.0.100-preview.4.21216.15
  [Host]   : .NET Core 5.0.5 (CoreCLR 5.0.521.16609, CoreFX 5.0.521.16609), X64 RyuJIT
  ShortRun : .NET Core 5.0.5 (CoreCLR 5.0.521.16609, CoreFX 5.0.521.16609), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |        Mean |       Error |      StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------- |------------:|------------:|------------:|------:|--------:|-------:|------:|------:|----------:|
| ParlotCompiledSmall |    769.6 ns |    709.4 ns |    38.89 ns |  1.70 |    0.08 | 0.1564 |     - |     - |     656 B |
|   ParlotFluentSmall |    931.7 ns |    622.3 ns |    34.11 ns |  2.06 |    0.10 | 0.1564 |     - |     - |     656 B |
|         PidginSmall | 11,668.0 ns | 10,591.9 ns |   580.58 ns | 25.77 |    1.64 | 0.1831 |     - |     - |     816 B |
|                     |             |             |             |       |         |        |       |       |           |
|   ParlotCompiledBig |  4,357.3 ns |  2,429.2 ns |   133.15 ns |  1.94 |    0.09 | 0.6866 |     - |     - |    2888 B |
|     ParlotFluentBig |  5,312.8 ns |  3,441.7 ns |   188.65 ns |  2.36 |    0.06 | 0.6866 |     - |     - |    2888 B |
|           PidginBig | 60,793.9 ns | 24,192.2 ns | 1,326.06 ns | 27.02 |    0.11 | 0.8545 |     - |     - |    4072 B |

```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.

The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides the best performance in all scenarios, being at least 2 times faster than the second fastest. The allocations of Parlot are also better or equivalent to the ones of Pidgin.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=6.0.100-preview.4.21216.15
  [Host]   : .NET Core 5.0.5 (CoreCLR 5.0.521.16609, CoreFX 5.0.521.16609), X64 RyuJIT
  ShortRun : .NET Core 5.0.5 (CoreCLR 5.0.521.16609, CoreFX 5.0.521.16609), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|                  Method |       Mean |       Error |   StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|------------------------ |-----------:|------------:|---------:|------:|--------:|----------:|---------:|------:|-----------:|
|  BigJson_ParlotCompiled |   203.8 us |    34.68 us |  1.90 us |  0.85 |    0.02 |   24.9023 |   1.2207 |     - |  101.79 KB |
|          BigJson_Parlot |   239.2 us |    47.22 us |  2.59 us |  1.00 |    0.00 |   24.9023 |   7.0801 |     - |  101.79 KB |
|          BigJson_Pidgin |   470.1 us |    79.16 us |  4.34 us |  1.97 |    0.00 |   24.9023 |   7.3242 |     - |   101.7 KB |
|         BigJson_Sprache | 3,348.1 us | 1,216.50 us | 66.68 us | 14.00 |    0.29 | 1308.5938 |   3.9063 |     - | 5349.63 KB |
|      BigJson_Superpower | 2,059.3 us | 1,215.90 us | 66.65 us |  8.61 |    0.23 |  222.6563 |  66.4063 |     - |  913.43 KB |
|                         |            |             |          |       |         |           |          |       |            |
| LongJson_ParlotCompiled |   150.6 us |    57.08 us |  3.13 us |  0.89 |    0.02 |   25.3906 |   6.3477 |     - |  104.34 KB |
|         LongJson_Parlot |   168.4 us |    27.75 us |  1.52 us |  1.00 |    0.00 |   25.3906 |   6.3477 |     - |  104.34 KB |
|         LongJson_Pidgin |   391.9 us |    71.57 us |  3.92 us |  2.33 |    0.00 |   25.3906 |   6.3477 |     - |  104.25 KB |
|        LongJson_Sprache | 2,443.5 us |   662.89 us | 36.34 us | 14.51 |    0.12 | 1054.6875 |   3.9063 |     - | 4311.36 KB |
|     LongJson_Superpower | 1,646.8 us |   279.91 us | 15.34 us |  9.78 |    0.13 |  171.8750 |  42.9688 |     - |  706.79 KB |
|                         |            |             |          |       |         |           |          |       |            |
| DeepJson_ParlotCompiled |   109.3 us |     3.72 us |  0.20 us |  0.70 |    0.01 |   20.1416 |   0.6104 |     - |   82.33 KB |
|         DeepJson_Parlot |   155.9 us |    26.57 us |  1.46 us |  1.00 |    0.00 |   20.0195 |   0.2441 |     - |   82.33 KB |
|         DeepJson_Pidgin |   491.7 us |    87.57 us |  4.80 us |  3.15 |    0.01 |   49.8047 |   1.9531 |     - |  205.29 KB |
|        DeepJson_Sprache | 2,809.1 us |   412.98 us | 22.64 us | 18.02 |    0.13 |  550.7813 | 222.6563 |     - | 2946.57 KB |
|                         |            |             |          |       |         |           |          |       |            |
| WideJson_ParlotCompiled |   139.6 us |    76.79 us |  4.21 us |  0.93 |    0.01 |   11.7188 |   2.1973 |     - |   48.51 KB |
|         WideJson_Parlot |   150.3 us |    58.06 us |  3.18 us |  1.00 |    0.00 |   11.7188 |   2.1973 |     - |   48.51 KB |
|         WideJson_Pidgin |   248.5 us |    56.74 us |  3.11 us |  1.65 |    0.04 |   11.7188 |   1.9531 |     - |   48.42 KB |
|        WideJson_Sprache | 1,559.7 us |   841.64 us | 46.13 us | 10.38 |    0.37 |  683.5938 |   3.9063 |     - | 2797.28 KB |
|     WideJson_Superpower | 1,020.0 us |   275.34 us | 15.09 us |  6.79 |    0.04 |  111.3281 |   7.8125 |     - |  459.74 KB |
```

### Usages

Parlot is already used in these projects:
- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
