# Parlot

[![NuGet](https://img.shields.io/nuget/v/Parlot.svg)](https://nuget.org/packages/Parlot)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE)

Parlot is a __fast__,  __lightweight__ and simple to use .NET parser combinator.

Parlot provides a fluent API based on parser combinators that provide a more readable grammar definition.
Parlot also provides some classes to build lower level parsers that require even more optimization, while maintaining some simple to use APIs.

## Fluent API

The Fluent API provides simple parser combinators that are assembled to express more complex expressions.
The main goal of this API is to provide and easy-to-read grammar. Another advantage is that grammars are built at runtime, and they can be extended dynamically.

The following example is a complete parser that create a mathematical expression tree (AST).
The source is available [here](./test/Parlot.Tests/Calc/FluentParser.cs).

```c#
public static readonly IParser<Expression> Expression;

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
    var factor = unary.And(Star(divided.Or(times).And(unary)))
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
    expression.Parser = factor.And(Star(plus.Or(minus).And(factor)))
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

## Standard API

The standard API provides direct access to the `Scanner` which is responsible for consuming chars from the text buffer and decide what tokens are found.
It is recommended to use it if you are looking for the ultimate performance and using a very readable grammar is not required.

Here is how the same mathematical expression parser coded using the standard API.
The full source is available [here](./test/Parlot.Tests/Calc/Parser.cs).

```c#
private Expression ParseExpression()
{
    var expression = ParseFactor();

    while (true)
    {
        _scanner.SkipWhiteSpace();

        if (_scanner.ReadChar('+'))
        {
            _scanner.SkipWhiteSpace();

            expression = new Addition(expression, ParseFactor());
        }
        else if (_scanner.ReadChar('-'))
        {
            _scanner.SkipWhiteSpace();

            expression = new Subtraction(expression, ParseFactor());
        }
        else
        {
            break;
        }
    }

    return expression;
}
```

Another example shows how to interpret the expression without creating an intermediate AST [here](./test/Parlot.Tests/Calc/Interpreter.cs).

## Performance

Parlot was developed in order to provide a more performant solution to the tools I was knew about.

I was mostly using the [Irony](https://github.com/IronyProject/Irony) project, and even though is was fast enough for my needs, it appeared to allocate too much. I then discovered [Sprache](https://github.com/sprache/Sprache) and [Superpower](https://github.com/datalust/superpower) but they didn't provide any advantage over Irony that would make me switch to them. 

Finally I found out about [Pidgin](https://github.com/benjamin-hodgson/Pidgin) only after I had started developing Parlot. This was interesting as after reading its benchmarks results I assumed it would be impossible to make something better, but I found out that even the fluent API of Parlot was better than Pidgin so I decided to continue the work and release it.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises two expressions:
- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`

Only Pidgin and Parlot are benchmarked here. It also shows the results of the two Parlot APIs. The __Fluent API__ is the one that corresponds to Pidgin. The __Raw__ variant is using the standard Parlot APIto show how to get the fastest possible parser. These benchmarks don't evaluate the expressions but only parse them to create the same AST. 

In this benchmark Parlot Fluent is 3 times faster than Pidgin, and Parlot Raw gives another 3 times boost. Allocations are also smaller with Parlot.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.200-preview.20601.7
  [Host]   : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  ShortRun : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|            Method |        Mean |       Error |      StdDev | Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------ |------------:|------------:|------------:|------:|--------:|-------:|------:|------:|----------:|
|    ParlotRawSmall |    571.6 ns |    483.7 ns |    26.51 ns |  1.00 |    0.00 | 0.1411 |     - |     - |     592 B |
| ParlotFluentSmall |  1,705.4 ns |    970.2 ns |    53.18 ns |  2.99 |    0.13 | 0.1774 |     - |     - |     744 B |
|       PidginSmall |  9,821.3 ns |    974.2 ns |    53.40 ns | 17.21 |    0.81 | 0.1831 |     - |     - |     816 B |
|                   |             |             |             |       |         |        |       |       |           |
|      ParlotRawBig |  2,480.5 ns |  1,748.1 ns |    95.82 ns |  1.00 |    0.00 | 0.6447 |     - |     - |    2712 B |
|   ParlotFluentBig |  9,073.4 ns | 34,062.0 ns | 1,867.05 ns |  3.68 |    0.88 | 0.7477 |     - |     - |    3136 B |
|         PidginBig | 50,252.0 ns | 16,204.6 ns |   888.23 ns | 20.28 |    1.04 | 0.9155 |     - |     - |    4072 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator, hence are easy to understand.

The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides again the best performance in all scenarios. The allocations of Parlot are also better or equivalent to the one of Pidgin.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.200-preview.20601.7
  [Host]   : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  ShortRun : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |       Mean |       Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|-------------------- |-----------:|------------:|----------:|------:|--------:|----------:|---------:|------:|-----------:|
|      BigJson_Parlot |   259.4 us |   152.04 us |   8.33 us |  1.00 |    0.00 |   24.9023 |   3.4180 |     - |  101.84 KB |
|      BigJson_Pidgin |   415.5 us |   206.23 us |  11.30 us |  1.60 |    0.05 |   24.9023 |   3.9063 |     - |   101.7 KB |
|     BigJson_Sprache | 2,608.9 us | 2,245.48 us | 123.08 us | 10.06 |    0.19 | 1308.5938 |  50.7813 |     - | 5349.63 KB |
|  BigJson_Superpower | 1,803.5 us | 1,210.12 us |  66.33 us |  6.95 |    0.05 |  222.6563 |   1.9531 |     - |  913.43 KB |
|                     |            |             |           |       |         |           |          |       |            |
|     LongJson_Parlot |   197.7 us |    81.63 us |   4.47 us |  1.00 |    0.00 |   25.3906 |   0.2441 |     - |  104.39 KB |
|     LongJson_Pidgin |   341.4 us |   103.14 us |   5.65 us |  1.73 |    0.06 |   25.3906 |   2.4414 |     - |  104.25 KB |
|    LongJson_Sprache | 1,956.1 us | 1,473.74 us |  80.78 us |  9.90 |    0.60 | 1054.6875 |   5.8594 |     - | 4311.36 KB |
| LongJson_Superpower | 1,430.1 us |   401.45 us |  22.01 us |  7.24 |    0.25 |  171.8750 |   3.9063 |     - |  706.79 KB |
|                     |            |             |           |       |         |           |          |       |            |
|     DeepJson_Parlot |   179.5 us |   138.06 us |   7.57 us |  1.00 |    0.00 |   20.0195 |   0.9766 |     - |   82.38 KB |
|     DeepJson_Pidgin |   422.9 us |    94.89 us |   5.20 us |  2.36 |    0.12 |   49.8047 |   0.4883 |     - |  205.29 KB |
|    DeepJson_Sprache | 2,344.1 us | 1,578.03 us |  86.50 us | 13.06 |    0.44 |  554.6875 | 222.6563 |     - | 2946.56 KB |
|                     |            |             |           |       |         |           |          |       |            |
|     WideJson_Parlot |   154.5 us |   149.50 us |   8.19 us |  1.00 |    0.00 |   11.7188 |        - |     - |   48.56 KB |
|     WideJson_Pidgin |   234.9 us |    79.61 us |   4.36 us |  1.52 |    0.07 |   11.7188 |   1.2207 |     - |   48.42 KB |
|    WideJson_Sprache | 1,230.9 us |   585.60 us |  32.10 us |  7.99 |    0.57 |  683.5938 |  11.7188 |     - | 2797.28 KB |
| WideJson_Superpower |   895.5 us |   314.60 us |  17.24 us |  5.81 |    0.41 |  112.3047 |   1.9531 |     - |  459.74 KB |
```

### Usages

Parlot is already used in these projects:
- [Shortcodes](https://github.com/sebastienros/shortcodes)
