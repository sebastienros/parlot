# Parlot

[![NuGet](https://img.shields.io/nuget/v/Parlot.svg)](https://nuget.org/packages/Parlot)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![Join the chat at https://gitter.im/sebastienros/parlot](https://badges.gitter.im/sebastienros/parlot.svg)](https://gitter.im/sebastienros/parlot?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

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

- [Existing parsers and usage examples](docs/parsers.md)
- [Best practices for custom parsers](docs/writing.md)

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
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |        Mean |       Error |    StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|-------------------- |------------:|------------:|----------:|------:|--------:|-------:|-------:|----------:|
| ParlotCompiledSmall |    565.9 ns |   105.51 ns |   5.78 ns |  1.00 |    0.00 | 0.1049 |      - |     664 B |
|   ParlotFluentSmall |    850.6 ns |   146.17 ns |   8.01 ns |  1.50 |    0.03 | 0.1049 |      - |     664 B |
|         PidginSmall | 10,082.3 ns |   554.19 ns |  30.38 ns | 17.82 |    0.18 | 0.1221 |      - |     832 B |
|                     |             |             |           |       |         |        |        |           |
|   ParlotCompiledBig |  3,103.0 ns |    67.98 ns |   3.73 ns |  1.00 |    0.00 | 0.4616 | 0.0038 |   2,896 B |
|     ParlotFluentBig |  4,464.1 ns |   237.26 ns |  13.01 ns |  1.44 |    0.00 | 0.4578 |      - |   2,896 B |
|           PidginBig | 48,469.4 ns | 2,248.38 ns | 123.24 ns | 15.62 |    0.05 | 0.6104 |      - |   4,152 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.
For reference Newtonsoft.Json is also added to show the differences with a dedicated parser.
The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides the best performance in all scenarios, being at least 2 times faster than the second fastest. The allocations of Parlot are also better or equivalent to the ones of Pidgin. This simple implementation is also faster than Newtonsoft, though it is far for being as rigourus.

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|                  Method |        Mean |      Error |    StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 | Allocated |
|------------------------ |------------:|-----------:|----------:|------:|--------:|---------:|---------:|----------:|
|  BigJson_ParlotCompiled |   116.14 us |   8.945 us |  0.490 us |  1.00 |    0.00 |  16.1133 |   4.8828 |     99 KB |
|          BigJson_Parlot |   140.77 us |  22.625 us |  1.240 us |  1.21 |    0.01 |  16.1133 |   4.1504 |     99 KB |
|          BigJson_Pidgin |   271.31 us |  44.236 us |  2.425 us |  2.34 |    0.03 |  16.1133 |   3.9063 |     99 KB |
|      BigJson_Newtonsoft |   194.95 us | 223.703 us | 12.262 us |  1.68 |    0.11 |  32.9590 |  13.9160 |    203 KB |
|         BigJson_Sprache | 1,987.39 us | 121.528 us |  6.661 us | 17.11 |    0.06 | 859.3750 | 214.8438 |  5,272 KB |
|      BigJson_Superpower | 1,447.51 us | 295.441 us | 16.194 us | 12.46 |    0.12 | 148.4375 |  39.0625 |    911 KB |
|                         |             |            |           |       |         |          |          |           |
| LongJson_ParlotCompiled |    95.59 us |  20.169 us |  1.106 us |  1.00 |    0.00 |  21.4844 |   7.0801 |    132 KB |
|         LongJson_Parlot |   114.71 us |  12.814 us |  0.702 us |  1.20 |    0.01 |  21.4844 |   7.0801 |    132 KB |
|         LongJson_Pidgin |   249.60 us |  48.135 us |  2.638 us |  2.61 |    0.00 |  21.4844 |   6.8359 |    132 KB |
|     LongJson_Newtonsoft |   143.27 us | 109.147 us |  5.983 us |  1.50 |    0.05 |  32.9590 |  14.6484 |    203 KB |
|        LongJson_Sprache | 1,719.26 us | 242.870 us | 13.313 us | 17.99 |    0.18 | 697.2656 | 197.2656 |  4,273 KB |
|     LongJson_Superpower | 1,226.01 us | 232.482 us | 12.743 us | 12.83 |    0.18 | 119.1406 |  39.0625 |    735 KB |
|                         |             |            |           |       |         |          |          |           |
| DeepJson_ParlotCompiled |    65.61 us |   7.158 us |  0.392 us |  1.00 |    0.00 |  17.9443 |   4.6387 |    110 KB |
|         DeepJson_Parlot |    88.90 us |  18.409 us |  1.009 us |  1.35 |    0.01 |  17.9443 |   4.2725 |    110 KB |
|         DeepJson_Pidgin |   362.94 us |  44.256 us |  2.426 us |  5.53 |    0.06 |  36.6211 |  12.2070 |    225 KB |
|     DeepJson_Newtonsoft |   110.57 us |  16.565 us |  0.908 us |  1.69 |    0.02 |  29.1748 |  11.5967 |    179 KB |
|        DeepJson_Sprache | 1,543.61 us | 168.665 us |  9.245 us | 23.53 |    0.07 | 476.5625 | 193.3594 |  2,926 KB |
|                         |             |            |           |       |         |          |          |           |
| WideJson_ParlotCompiled |    54.11 us |   1.390 us |  0.076 us |  1.00 |    0.00 |   6.5918 |   1.0986 |     41 KB |
|         WideJson_Parlot |    65.62 us |   3.511 us |  0.192 us |  1.21 |    0.00 |   6.5918 |   1.0986 |     41 KB |
|         WideJson_Pidgin |   123.58 us |   7.182 us |  0.394 us |  2.28 |    0.01 |   6.5918 |   0.9766 |     41 KB |
|     WideJson_Newtonsoft |    95.25 us |  19.982 us |  1.095 us |  1.76 |    0.02 |  17.3340 |   5.7373 |    107 KB |
|        WideJson_Sprache |   949.33 us | 199.824 us | 10.953 us | 17.54 |    0.22 | 451.1719 |  89.8438 |  2,767 KB |
|     WideJson_Superpower |   708.42 us | 123.153 us |  6.750 us | 13.09 |    0.11 |  73.2422 |  11.7188 |    452 KB |
```

### Regular Expressions

Regular expression can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do less allocations.

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |     Mean |     Error |  StdDev | Ratio | RatioSD |  Gen 0 | Allocated |
|-------------------- |---------:|----------:|--------:|------:|--------:|-------:|----------:|
|  RegexEmailCompiled | 130.7 ns |  17.64 ns | 0.97 ns |  1.00 |    0.00 | 0.0331 |     208 B |
|          RegexEmail | 269.5 ns | 131.09 ns | 7.19 ns |  2.06 |    0.07 | 0.0329 |     208 B |
| ParlotEmailCompiled | 160.5 ns |  18.72 ns | 1.03 ns |  1.23 |    0.02 | 0.0215 |     136 B |
|         ParlotEmail | 354.1 ns |  62.89 ns | 3.45 ns |  2.71 |    0.02 | 0.0520 |     328 B |
```

### Versions

The benchmarks were executed with the following versions

- Parlot 0.0.19
- Pidgin 3.0.0
- Sprache 2.3.1
- Superpower 3.0.0
- Newtonsoft.Json 13.0.1

### Usages

Parlot is already used in these projects:
- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
