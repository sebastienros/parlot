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
BenchmarkDotNet v0.14.0, Debian GNU/Linux 12 (bookworm) (container)
Intel Xeon E-2336 CPU 2.90GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.303
  [Host]   : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                         | Mean             | Error          | StdDev        | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|--------------------------------|------------------|----------------|---------------|-------|---------|----------|----------|-----------|-------------|
| ParlotRawSmall                 | 245.526 ns       | 9.8448 ns      | 0.5396 ns     | 0.45  | 0.00    | 0.0482   | -        | 304 B     | 0.44        |
| ParlotCompiledSmall            | 541.858 ns       | 12.6762 ns     | 0.6948 ns     | 1.00  | 0.00    | 0.1097   | -        | 688 B     | 1.00        |
| ParlotFluentSmall              | 631.942 ns       | 16.6197 ns     | 0.9110 ns     | 1.17  | 0.00    | 0.1097   | -        | 688 B     | 1.00        |
| PidginSmall                    | 6,232.913 ns     | 455.9577 ns    | 24.9926 ns    | 11.50 | 0.04    | 0.1297   | -        | 832 B     | 1.21        |
|                                |                  |                |               |       |         |          |          |           |             |
| ParlotRawBig                   | 1,253.877 ns     | 96.9166 ns     | 5.3123 ns     | 0.41  | 0.00    | 0.1907   | -        | 1200 B    | 0.39        |
| ParlotCompiledBig              | 3,084.008 ns     | 94.1393 ns     | 5.1601 ns     | 1.00  | 0.00    | 0.4883   | -        | 3080 B    | 1.00        |
| ParlotFluentBig                | 3,447.370 ns     | 93.6097 ns     | 5.1311 ns     | 1.12  | 0.00    | 0.4883   | -        | 3080 B    | 1.00        |
| PidginBig                      | 31,896.523 ns    | 1,898.4107 ns  | 104.0583 ns   | 10.34 | 0.03    | 0.6104   | -        | 4152 B    | 1.35        |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Sprache, Superpower and Parlot are compared. The programming models are all based on parser combinator.
For reference, Newtonsoft.Json is also added to show the differences with a dedicated parser.
The results show that Sprache and Superpower are the slowest and most allocating ones. Parlot provides the best performance in all scenarios, being 2 times faster than the second fastest. The allocations of Parlot are also better or equivalent to the ones of Pidgin. This simple implementation is also faster than Newtonsoft, though it is far from being as rigorous. The best JSON parser is by far System.Text.Json, don't build your own!

```
BenchmarkDotNet v0.14.0, Debian GNU/Linux 12 (bookworm) (container)
Intel Xeon E-2336 CPU 2.90GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.303
  [Host]   : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method                         | Mean             | Error          | StdDev        | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|--------------------------------|------------------|----------------|---------------|-------|---------|----------|----------|-----------|-------------|
| BigJson_ParlotCompiled         | 99,206.961 ns    | 425.9311 ns    | 23.3467 ns    | 1.00  | 0.00    | 14.8926  | 2.3193   | 93936 B   | 1.00        |
| BigJson_Parlot                 | 97,746.352 ns    | 1,682.2353 ns  | 92.2090 ns    | 0.99  | 0.00    | 14.8926  | 2.3193   | 93936 B   | 1.00        |
| BigJson_Pidgin                 | 176,281.977 ns   | 43,607.4055 ns | 2,390.2690 ns | 1.78  | 0.02    | 14.8926  | 2.1973   | 93904 B   | 1.00        |
| BigJson_Newtonsoft             | 118,091.588 ns   | 2,407.3099 ns  | 131.9528 ns   | 1.19  | 0.00    | 33.0811  | 10.7422  | 207976 B  | 2.21        |
| BigJson_SystemTextJson         | 22,621.221 ns    | 145.1274 ns    | 7.9549 ns     | 0.23  | 0.00    | 3.9063   | 0.3052   | 24696 B   | 0.26        |
| BigJson_Sprache                | 1,572,933.219 ns | 51,532.2187 ns | 2,824.6548 ns | 15.86 | 0.02    | 859.3750 | 171.8750 | 5398324 B | 57.47       |
| BigJson_Superpower             | 1,016,317.802 ns | 30,384.2385 ns | 1,665.4626 ns | 10.24 | 0.01    | 146.4844 | 23.4375  | 927676 B  | 9.88        |
|                                |                  |                |               |       |         |          |          |           |             |
| DeepJson_ParlotCompiled        | 55,788.357 ns    | 807.2790 ns    | 44.2497 ns    | 1.00  | 0.00    | 15.9912  | 1.7090   | 100656 B  | 1.00        |
| DeepJson_Parlot                | 56,326.295 ns    | 541.5577 ns    | 29.6846 ns    | 1.01  | 0.00    | 15.9912  | 1.7090   | 100656 B  | 1.00        |
| DeepJson_Pidgin                | 235,759.393 ns   | 17,787.4510 ns | 974.9902 ns   | 4.23  | 0.02    | 21.7285  | 5.6152   | 137000 B  | 1.36        |
| DeepJson_Newtonsoft            | 69,953.638 ns    | 1,579.8252 ns  | 86.5955 ns    | 1.25  | 0.00    | 29.1748  | 8.6670   | 183432 B  | 1.82        |
| DeepJson_SystemTextJson        | NA               | NA             | NA            | ?     | ?       | NA       | NA       | NA        | ?           |
| DeepJson_Sprache               | 1,344,336.604 ns | 36,171.7543 ns | 1,982.6959 ns | 24.10 | 0.03    | 474.6094 | 173.8281 | 2984396 B | 29.65       |
|                                |                  |                |               |       |         |          |          |           |             |
| LongJson_ParlotCompiled        | 78,447.519 ns    | 3,279.8496 ns  | 179.7796 ns   | 1.00  | 0.00    | 19.2871  | 4.0283   | 121152 B  | 1.00        |
| LongJson_Parlot                | 73,648.078 ns    | 788.6781 ns    | 43.2301 ns    | 0.94  | 0.00    | 19.2871  | 4.0283   | 121152 B  | 1.00        |
| LongJson_Pidgin                | 172,058.354 ns   | 7,881.7898 ns  | 432.0275 ns   | 2.19  | 0.01    | 19.5313  | 3.9063   | 123136 B  | 1.02        |
| LongJson_Newtonsoft            | 94,847.667 ns    | 4,123.7818 ns  | 226.0384 ns   | 1.21  | 0.00    | 33.0811  | 9.5215   | 207544 B  | 1.71        |
| LongJson_SystemTextJson        | 17,431.022 ns    | 460.8791 ns    | 25.2623 ns    | 0.22  | 0.00    | 3.9063   | 0.3052   | 24696 B   | 0.20        |
| LongJson_Sprache               | 1,313,562.949 ns | 65,279.5707 ns | 3,578.1936 ns | 16.74 | 0.05    | 695.3125 | 150.3906 | 4363588 B | 36.02       |
| LongJson_Superpower            | 837,530.596 ns   | 16,634.4617 ns | 911.7910 ns   | 10.68 | 0.02    | 118.1641 | 23.4375  | 744234 B  | 6.14        |
|                                |                  |                |               |       |         |          |          |           |             |
| WideJson_ParlotCompiled        | 42,290.622 ns    | 565.5451 ns    | 30.9994 ns    | 1.00  | 0.00    | 6.5918   | 0.5493   | 41504 B   | 1.00        |
| WideJson_Parlot                | 40,642.455 ns    | 2,317.0918 ns  | 127.0076 ns   | 0.96  | 0.00    | 6.5918   | 0.5493   | 41504 B   | 1.00        |
| WideJson_Pidgin                | 69,621.323 ns    | 1,316.3023 ns  | 72.1510 ns    | 1.65  | 0.00    | 6.5918   | 0.4883   | 41448 B   | 1.00        |
| WideJson_Newtonsoft            | 58,908.398 ns    | 1,693.4597 ns  | 92.8242 ns    | 1.39  | 0.00    | 17.3950  | 3.4790   | 109280 B  | 2.63        |
| WideJson_Sprache               | 715,086.114 ns   | 39,534.8179 ns | 2,167.0368 ns | 16.91 | 0.05    | 451.1719 | 57.6172  | 2833274 B | 68.27       |
| WideJson_Superpower            | 493,485.514 ns   | 28,264.4806 ns | 1,549.2716 ns | 11.67 | 0.03    | 73.7305  | 6.8359   | 462657 B  | 11.15       |
```

### Regular Expressions

Regular expressions can also be replaced by more formal parser definitions. The following benchmarks show how Parlot compares to them when checking if a string matches
an email with the pattern `[\w\.+-]+@[\w-]+\.[\w\.-]+`. Note that in the case of pattern matching Parlot can use the pattern matching mode and do fewer allocations.

```
BenchmarkDotNet v0.14.0, Debian GNU/Linux 12 (bookworm) (container)
Intel Xeon E-2336 CPU 2.90GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.303
  [Host]   : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1 | Allocated | Alloc Ratio |
|---------------------|------------|-----------|-----------|-------|---------|--------|------|-----------|-------------|
| RegexEmailCompiled  | 85.561 ns  | 1.8927 ns | 0.1037 ns | 1.00  | 0.00    | 0.0331 | -    | 208 B     | 1.00        |
| RegexEmail          | 183.975 ns | 7.1863 ns | 0.3939 ns | 2.15  | 0.00    | 0.0331 | -    | 208 B     | 1.00        |
| ParlotEmailCompiled | 145.651 ns | 1.9186 ns | 0.1052 ns | 1.70  | 0.00    | 0.0355 | -    | 224 B     | 1.08        |
| ParlotEmail         | 253.400 ns | 5.2169 ns | 0.2860 ns | 2.96  | 0.00    | 0.0505 | -    | 320 B     | 1.54        |
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
