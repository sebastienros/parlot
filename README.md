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

Parlot was developed in order to provide a more performant solution to the tools I knew about.

I was mostly using the [Irony](https://github.com/IronyProject/Irony) project, and even though is was fast enough for my needs, it appeared to allocate too much. I then discovered [Sprache](https://github.com/sprache/Sprache) and [Superpower](https://github.com/datalust/superpower) but they didn't provide any advantage over Irony that would make me switch to them. 

Finally I found out about [Pidgin](https://github.com/benjamin-hodgson/Pidgin) only after I had started developing Parlot. This was interesting as after reading its benchmarks results I assumed it would be impossible to make something better, but I found out that even the fluent API of Parlot was better than Pidgin so I decided to continue the work and release it.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises two expressions:
- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`

Only Pidgin and Parlot are benchmarked here. It also shows the results of the two Parlot APIs. The __Fluent API__ is the one that corresponds to Pidgin. The __Raw__ variant is using the standard Parlot API to show how to get the fastest possible parser. These benchmarks don't evaluate the expressions but only parse them to create the same AST. 

In this benchmark Parlot Fluent is 10 times faster than Pidgin, and Parlot Raw gives another 2 times boost. Allocations are also smaller with Parlot.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.200-preview.20601.7
  [Host]   : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  ShortRun : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|            Method |        Mean |      StdDev | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|------------------ |------------:|------------:|------:|-------:|------:|------:|----------:|
|    ParlotRawSmall |    512.7 ns |    11.80 ns |  1.00 | 0.1183 |     - |     - |     496 B |
| ParlotFluentSmall |    946.7 ns |    18.12 ns |  1.85 | 0.1602 |     - |     - |     672 B |
|       PidginSmall | 11,027.5 ns |   220.52 ns | 21.52 | 0.1831 |     - |     - |     816 B |
|                   |             |             |       |        |       |       |           |
|      ParlotRawBig |  2,569.0 ns |    30.88 ns |  1.00 | 0.5264 |     - |     - |    2208 B |
|   ParlotFluentBig |  5,338.5 ns |    52.82 ns |  2.08 | 0.6943 |     - |     - |    2904 B |
|         PidginBig | 52,311.8 ns | 1,124.48 ns | 20.36 | 0.9155 |     - |     - |    4072 B |

```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator, hence are easy to understand.

The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides again the best performance in all scenarios, being at least 2 times faster than the second fastest. The allocations of Parlot are also better or equivalent to the ones of Pidgin.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.200-preview.20601.7
  [Host]   : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  ShortRun : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |       Mean |   StdDev | Ratio |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|-------------------- |-----------:|---------:|------:|----------:|---------:|------:|-----------:|
|      BigJson_Parlot |   188.4 us |  0.77 us |  1.00 |   24.9023 |   7.3242 |     - |   101.8 KB |
|      BigJson_Pidgin |   397.2 us |  6.45 us |  2.11 |   24.9023 |   7.3242 |     - |   101.7 KB |
|     BigJson_Sprache | 2,742.9 us | 37.58 us | 14.56 | 1308.5938 |   3.9063 |     - | 5349.63 KB |
|  BigJson_Superpower | 1,789.2 us |  1.63 us |  9.50 |  222.6563 |  54.6875 |     - |  913.43 KB |
|                     |            |          |       |           |          |       |            |
|     LongJson_Parlot |   136.0 us |  1.47 us |  1.00 |   25.3906 |   4.3945 |     - |  104.35 KB |
|     LongJson_Pidgin |   329.2 us |  0.67 us |  2.42 |   25.3906 |   6.3477 |     - |  104.25 KB |
|    LongJson_Sprache | 2,308.5 us | 81.29 us | 16.98 | 1054.6875 |   3.9063 |     - | 4311.36 KB |
| LongJson_Superpower | 1,476.9 us | 54.96 us | 10.86 |  171.8750 |  42.9688 |     - |  706.79 KB |
|                     |            |          |       |           |          |       |            |
|     DeepJson_Parlot |   126.4 us |  3.36 us |  1.00 |   20.0195 |   0.4883 |     - |   82.34 KB |
|     DeepJson_Pidgin |   418.8 us |  0.97 us |  3.31 |   49.8047 |   1.9531 |     - |  205.29 KB |
|    DeepJson_Sprache | 2,430.5 us | 38.94 us | 19.23 |  550.7813 | 222.6563 |     - | 2946.57 KB |
|                     |            |          |       |           |          |       |            |
|     WideJson_Parlot |   117.1 us |  1.27 us |  1.00 |   11.8408 |   2.3193 |     - |   48.52 KB |
|     WideJson_Pidgin |   216.3 us |  2.48 us |  1.85 |   11.7188 |   2.1973 |     - |   48.42 KB |
|    WideJson_Sprache | 1,227.8 us |  7.11 us | 10.48 |  683.5938 |   3.9063 |     - | 2797.28 KB |
| WideJson_Superpower |   873.7 us |  5.05 us |  7.46 |  112.3047 |   3.9063 |     - |  459.74 KB |
```

### Usages

Parlot is already used in these projects:
- [Shortcodes](https://github.com/sebastienros/shortcodes)
