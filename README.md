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
     * expression     => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
    */

    // The Deferred helper creates a parser that can be referenced by others before it is defined.
    var expression = Deferred<Expression>();

    var number = Terms.Decimal()
        .Then<Expression>(static d => new Number(d));

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
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3155/23H2/2023Update/SunValley3)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 8.0.200
  [Host]   : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| ParlotRawSmall      |    176.1 ns |    41.63 ns |   2.28 ns |  0.60 |    0.00 | 0.0322 |     304 B |        0.46 |
| ParlotCompiledSmall |    292.4 ns |    43.69 ns |   2.39 ns |  1.00 |    0.00 | 0.0696 |     656 B |        1.00 |
| ParlotFluentSmall   |    360.3 ns |    44.18 ns |   2.42 ns |  1.23 |    0.01 | 0.0696 |     656 B |        1.00 |
| PidginSmall         |  5,237.3 ns | 2,162.97 ns | 118.56 ns | 17.91 |    0.33 | 0.0839 |     832 B |        1.27 |
|                     |             |             |           |       |         |        |           |             |
| ParlotRawBig        |    846.2 ns |   106.47 ns |   5.84 ns |  0.55 |    0.00 | 0.1268 |    1200 B |        0.42 |
| ParlotCompiledBig   |  1,551.9 ns |   274.97 ns |  15.07 ns |  1.00 |    0.00 | 0.3052 |    2888 B |        1.00 |
| ParlotFluentBig     |  1,921.7 ns |   116.32 ns |   6.38 ns |  1.24 |    0.01 | 0.3052 |    2888 B |        1.00 |
| PidginBig           | 26,802.7 ns | 6,253.70 ns | 342.79 ns | 17.27 |    0.09 | 0.4272 |    4152 B |        1.44 |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides the best performance in all scenarios, being at least 2 times faster than the second fastest. The allocations of Parlot are also better or equivalent to the ones of Pidgin. This simple implementation is also faster than Newtonsoft, though it is far from being as rigorous.

```
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3155/23H2/2023Update/SunValley3)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 8.0.200
  [Host]   : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                  | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |------------:|-----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| BigJson_ParlotCompiled  |    49.79 us |  16.541 us |  0.907 us |  1.00 |    0.00 |   9.9487 |   1.7700 |   91.79 KB |        1.00 |
| BigJson_Parlot          |    52.81 us |   7.892 us |  0.433 us |  1.06 |    0.03 |   9.9487 |   1.3428 |   91.79 KB |        1.00 |
| BigJson_Pidgin          |   124.43 us |  15.320 us |  0.840 us |  2.50 |    0.03 |   9.7656 |   1.7090 |    91.7 KB |        1.00 |
| BigJson_Newtonsoft      |    82.05 us |  18.702 us |  1.025 us |  1.65 |    0.05 |  22.0947 |   5.8594 |   203.1 KB |        2.21 |
| BigJson_Sprache         | 1,137.37 us | 259.708 us | 14.235 us | 22.84 |    0.14 | 572.2656 | 113.2813 |  5271.8 KB |       57.43 |
| BigJson_Superpower      |   810.11 us | 185.500 us | 10.168 us | 16.27 |    0.16 |  97.6563 |  13.6719 |  905.93 KB |        9.87 |
|                         |             |            |           |       |         |          |          |            |             |
| DeepJson_ParlotCompiled |    29.51 us |   2.907 us |  0.159 us |  1.00 |    0.00 |  10.6812 |   2.0142 |   98.33 KB |        1.00 |
| DeepJson_Parlot         |    34.88 us |   8.163 us |  0.447 us |  1.18 |    0.02 |  10.6812 |   1.1597 |   98.33 KB |        1.00 |
| DeepJson_Pidgin         |   233.67 us | 614.276 us | 33.671 us |  7.92 |    1.15 |  10.7422 |   2.1973 |   98.79 KB |        1.00 |
| DeepJson_Newtonsoft     |    49.00 us |  22.889 us |  1.255 us |  1.66 |    0.03 |  19.4702 |   5.7373 |  179.13 KB |        1.82 |
| DeepJson_Sprache        |   978.92 us | 667.766 us | 36.603 us | 33.17 |    1.07 | 316.4063 | 110.3516 | 2914.45 KB |       29.64 |
|                         |             |            |           |       |         |          |          |            |             |
| LongJson_ParlotCompiled |    43.54 us |  75.614 us |  4.145 us |  1.00 |    0.00 |  13.0615 |   2.6245 |  120.34 KB |        1.00 |
| LongJson_Parlot         |    51.76 us |  27.313 us |  1.497 us |  1.19 |    0.08 |  13.0615 |   2.6245 |  120.34 KB |        1.00 |
| LongJson_Pidgin         |   135.62 us | 263.370 us | 14.436 us |  3.15 |    0.60 |  13.0615 |   2.5635 |  120.25 KB |        1.00 |
| LongJson_Newtonsoft     |    77.94 us |  48.599 us |  2.664 us |  1.80 |    0.13 |  21.9727 |   7.2021 |  202.68 KB |        1.68 |
| LongJson_Sprache        | 1,215.53 us | 427.688 us | 23.443 us | 28.06 |    2.31 | 462.8906 |  97.6563 | 4261.31 KB |       35.41 |
| LongJson_Superpower     |   681.67 us | 996.045 us | 54.597 us | 15.80 |    2.52 |  78.1250 |  15.6250 |  726.79 KB |        6.04 |
|                         |             |            |           |       |         |          |          |            |             |
| WideJson_ParlotCompiled |    27.79 us |  21.605 us |  1.184 us |  1.00 |    0.00 |   4.3945 |   0.4883 |   40.56 KB |        1.00 |
| WideJson_Parlot         |    27.09 us |  19.498 us |  1.069 us |  0.98 |    0.02 |   4.3945 |   0.3967 |   40.56 KB |        1.00 |
| WideJson_Pidgin         |    59.53 us |  31.875 us |  1.747 us |  2.14 |    0.05 |   4.3945 |   0.3662 |   40.48 KB |        1.00 |
| WideJson_Newtonsoft     |    53.96 us |   5.562 us |  0.305 us |  1.94 |    0.09 |  11.5967 |   2.5635 |  106.72 KB |        2.63 |
| WideJson_Sprache        |   588.36 us | 467.479 us | 25.624 us | 21.21 |    1.45 | 300.7813 |  38.0859 | 2766.87 KB |       68.21 |
| WideJson_Superpower     |   456.39 us | 219.195 us | 12.015 us | 16.44 |    0.74 |  48.8281 |   4.3945 |  451.81 KB |       11.14 |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.

```
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3155/23H2/2023Update/SunValley3)
12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
.NET SDK 8.0.200
  [Host]   : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.2 (8.0.224.6711), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| RegexEmailCompiled  |  75.72 ns | 138.05 ns |  7.567 ns |  1.00 |    0.00 | 0.0221 |     208 B |        1.00 |
| RegexEmail          | 143.11 ns | 203.91 ns | 11.177 ns |  1.90 |    0.17 | 0.0219 |     208 B |        1.00 |
| ParlotEmailCompiled | 125.19 ns |  79.34 ns |  4.349 ns |  1.66 |    0.15 | 0.0136 |     128 B |        0.62 |
| ParlotEmail         | 176.86 ns |  38.78 ns |  2.126 ns |  2.35 |    0.28 | 0.0339 |     320 B |        1.54 |
```

### Versions

The benchmarks were executed with the following versions:

- Parlot 0.0.19
- Pidgin 3.2.2
- Sprache 2.3.1
- Superpower 3.0.0
- Newtonsoft.Json 13.0.3

### Usages

Parlot is already used in these projects:

- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
- [YesSql](https://github.com/sebastienros/yessql)
- [NCalc](https://github.com/ncalc/ncalc)
