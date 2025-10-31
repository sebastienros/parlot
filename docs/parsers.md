# List of parser combinators

> **Important:** To use the parsers documented below, you must add the following import statements to your file:
> ```c#
> using Parlot.Fluent;
> using static Parlot.Fluent.Parsers;
> ```
> The `using static` statement makes `Terms`, `Literals`, and other parser combinators (like `ZeroOrOne`, `Between`, `Deferred`, etc.) directly accessible.
>
> If your project has `ImplicitUsings` (Global Usings) enabled, the static import is included automatically.

> Note: when samples use a local `input` variable representing the input text to parse, and a `parser` variable, the result is usually the outcome of calling `var result = parser.Parse(input)` or `var success = parser.TryParse(input, out var result)`.

## Terms and Literals

These are lowest level elements of a grammar, like a `'.'` (dot), predefined strings like `"hello"`, numbers, and more.
In Parlot they usually are accessed from the `Literals` or `Terms` classes, with the difference that `Terms` will return a parser that
accepts blank spaces before the element.

Terms and Literals are accessed using the `Terms` and `Literals` properties from the `Parsers` static class (imported via `using static Parlot.Fluent.Parsers;`).

### WhiteSpace

Matches blank spaces, optionally including new lines. Returns a `TextSpan` with the matched spaces. This parser is not available in the `Terms` static class.

```c#
Parser<TextSpan> WhiteSpace(bool includeNewLines = false)
```

Usage</summary>

```c#
var input = "   \thello world  ";
var parser = Literals.WhiteSpace();
```

Result:

```
"   \t"
```

### NonWhiteSpace

Matches any non-blank spaces, optionally including new lines. Returns a `TextSpan` with the matched characters.  

### Select

Selects the parser to execute at runtime. Use it when the next parser depends on mutable state or a custom `ParseContext` implementation.

```c#
Parser<T> Select<T>(Func<ParseContext, Parser<T>> selector)
Parser<T> Select<C, T>(Func<C, Parser<T>> selector) where C : ParseContext
```

Usage:

```c#
var parser = Select<CustomContext, string>(context =>
{
    return context.PreferYes ? Literals.Text("yes") : Literals.Text("no");
});

var result = parser.Parse(new CustomContext(new Scanner("yes")) { PreferYes = true });
```
`CustomContext` is an application-defined type that derives from `ParseContext` and exposes additional configuration.


If the selector returns `null`, the `Select` parser fails without consuming any input. Capture additional state through closures or custom `ParseContext` properties when needed.

```c#
Parser<TextSpan> NonWhiteSpace(bool includeNewLines = false)
```

Usage:

```c#
var input = "hello world";
var parser = Terms.NonWhiteSpace();
```

Result:

```
"hello"
```

### Text

Matches a given string, optionally in a case insensitive way.

```c#
Parser<string> Text(string text, bool caseInsensitive = false)
```

Usage:

```c#
var input = "hello world";
var parser = Terms.Text("hello");
```

Result:

```
"hello"
```

### Char

Matches a given character.

```c#
Parser<char> Char(char c)
```

Usage:

```c#
var input = "hello world";
var parser = Terms.Char('h');
```

Result:

```
'h'
```

### Integer

Matches an integral numeric value and an optional leading sign.

```c#
Parser<long> Integer()
```

Usage:

```c#
var input = "-1234";
```

Result:

```
-1234
```

### Decimal

Matches a numeric value with optional digits and leading sign. The exponent is supported.

```c#
Parser<decimal> Decimal()
```

NB: This is equivalent to `Number<decimal>()` and only exists for backward compatibility purposes.

Usage:

```c#
var input = "-1234.56";
var parser = Terms.Decimal(NumberOptions.AllowSign);
```

Result:

```
-1234.56
```

### Number

Matches a numeric value of any .NET type. The `NumberOptions` enumeration enables to customize how the number is parsed.
The return type can be any numeric .NET type that is compatible with the selected options.

```c#
Parser<T> Number() where T : INumber<T>
```

Usage:

```c#
var input = "-1,234.56e1";
var parser = Terms.Number<double>(NumberOptions.Float | NumberOptions.AllowGroupSeparators);
```

Result:

```
-12345.6
```

### String

Matches a quoted string literal with escape sequences. Use this parser to parse strings from a programming language.

```c#
Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble)
```

Usage:

```c#
var input = "'hello\\nworld'";
var parser = Terms.String();
```

Result:

```
'hello\\nworld'
```

### Identifier

Matches an identifier, optionally with extra allowed characters.
Default start chars are `[$_a-zA-Z]`. Other chars also include digits.

```c#
Parser<TextSpan> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null)
```

Usage:

```c#
var input = "slice_text();";
var parser = Terms.Identifier();
```

Result:

```
slice_text
```

### Pattern

Matches a consecutive characters with a specific predicate, optionally defining a minimum and maximum size.

```c#
Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
```

Usage:

```c#
var input = "ababcad";
var parser = Terms.Pattern(c => c == 'a' || c == 'b');
```

Result:

```
abab
```

### AnyOf

Matches any chars from a list of chars.

```c#
Parser<TextSpan> AnyOf(string values, int minSize = 1, int maxSize = 0)
```

The following overloads are available when targeting .NET 8 or later and use vectorized parsing for better performance.

```c#
Parser<TextSpan> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0)
Parser<TextSpan> AnyOf(SearchValue<char> searchValues, int minSize = 1, int maxSize = 0)
```

Usage:

```c#
var input = "ababcad";
var parser = Terms.AnyOf("ab");
```

Result:

```
abab
```

### NoneOf

Matches any other chars than the ones specified.

```c#
Parser<TextSpan> NoneOf(string values, int minSize = 1, int maxSize = 0)
```

The following overloads are available when targeting .NET 8 or later and use vectorized parsing for better performance.

```c#
Parser<TextSpan> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0)
Parser<TextSpan> NoneOf(SearchValue<char> searchValues, int minSize = 1, int maxSize = 0)
```

Usage:

```c#
var input = "ababcad";
var parser = Terms.NoneOf("cd");
```

Result:

```
abab
```

## Combining parsers

### Or

Matches any of two parsers.

```c#
Parser<T> Or<T>(this Parser<T> parser, Parser<T> or)
```

Another overload accepts to return a common base class from the two parsers

```c#
Parser<T> Or<A, B, T>(this Parser<A> parser, Parser<B> or) 
```

Usage:

```c#
var parser = Terms.Text("one").Or(Terms.Text("1"));
parser.Parse("1");
parser.Parse("one");
parser.Parse("hello")
```

Result:

```
"1"
"one"
null
```

Multiple `Or()` calls can be used in a row, e.g., `a.Or(b).Or(c).Or(d)`

### And

Matches two consecutive parsers. The result is a strongly typed tuple containing the two individual results.

```c#
Parser<ValueTuple<T1, T2>> And<T1, T2>(this Parser<T1> parser, Parser<T2> and)
```

Usage:

```c#
var parser = Terms.Text("hello").And(Terms.Text("world"));
parser.Parse("hello world").Item1;
parser.Parse("hello world").Item2;
parser.Parse("hello");
```

Result:

```
"hello"
"world"
null
```

Multiple `And()` calls can be used in a row, e.g. to match a variable assignment like `age = 12` 

```c#
var input = "age = 12";
var parser = Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer());
var result = parser.Parse(input);

Assert.Equal("age", result.Item1);
Assert.Equal('=', result.Item2);
Assert.Equal(12, result.Item3);
```

### AndSkip

Behaves like [And](#And) but skips the later one's result.

```c#
Parser<T1> AndSkip<T1, T2>(this Parser<T1> parser, Parser<T2> and)```
```

Usage:

```c#
var parser = Terms.Text("hello").AndSkip(Terms.Text("world"));
parser.Parse("hello world");
parser.Parse("hello");
```

Result:

```
"hello"
null
```

This is useful to expect successive terms but to ignore some of them and make the result as lean as possible.

```c#
var input = "age = 12";
var parser = Terms.Identifier().AndSkip(Terms.Char('=')).And(Terms.Integer());
var result = parser.Parse(input);

Assert.Equal("age", result.Item1);
Assert.Equal(12, result.Item2);
```

### SkipAnd

Behaves like [And](#And) but skips the former one's result.

```c#
Parser<T2> SkipAnd<T1, T2>(this Parser<T1> parser, Parser<T2> and)
```

Usage:

```c#
var parser = Terms.Text("hello").SkipAnd(Terms.Text("world"));
parser.Parse("hello world");
parser.Parse("hello");
```

Result:

```
"world"
null
```

This is useful to expect successive terms but to ignore some of them and make the result as lean as possible.

```c#
var input = "age = 12";
var parser = Terms.Identifier().And(Terms.Char('=')).SkipAnd(Terms.Integer());
var result = parser.Parse(input);

Assert.Equal("age", result.Item1);
Assert.Equal(12, result.Item2);
```

## Cardinality Parsers

### ZeroOrOne

Makes an existing parser optional. The method can also be be post-fixed.

```c#
Parser<T> ZeroOrOne<T>(Parser<T> parser)
```

Usage:

```c#
var parser = ZeroOrOne(Terms.Text("hello"));
// or Terms.Text("hello").ZeroOrOne()
parser.Parse("hello");
parser.Parse(""); // returns null but with a successful state
```

Result:

```
"hello"
null
```

### Optional

Makes an existing parser optional by always returning an `Option<T>` result. It is then easy to know if the parser was successful or not by using the `HasValue` property.

```c#
static Parser<Option<T>> Optional<T>(this Parser<T> parser)
```

Usage:

```c#
var parser = Terms.Text("hello").Optional();
parser.Parse("hello"); // HasValue -> true
parser.Parse(""); // HasValue -> false
```

Use the `OrSome<T>()` method to provide a default value if the `Option<T>` instance has no value.

### ZeroOrMany

Executes a parser as long as it's successful. The result is a list of all individual results. The method can also be post-fixed.

```c#
Parser<IReadOnlyList<T> ZeroOrMany<T>(Parser<T> parser)
```

Usage:

```c#
var parser = ZeroOrMany(Terms.Text("hello"));
// or Terms.Text("hello").ZeroOrMany()
parser.Parse("hello hello");
parser.Parse("");
```

Result:

```
[ "hello", "hello" ]
[]
```

### OneOrMany

Executes a parser as long as it's successful, and is successful if at least one occurrence is found. The result is a list of all individual results. The method can also be post-fixed.

```c#
Parser<IReadOnlyList<T> OneOrMany<T>(Parser<T> parser)
```

Usage:

```c#
var parser = OneOrMany(Terms.Text("hello"));
// or Terms.Text("hello").OneOrMany()
parser.Parse("hello hello");
parser.Parse("");
```

Result:

```
[ "hello", "hello" ]
null
```

### Not

Succeeds if the parser is not matching.

```c#
Parser<T> Not<T>(Parser<T> parser)
```

Usage:

```c#
var parser = Not(Terms.Text("hello"));
parser.Parse("hello");
parser.Parse("world");
```

Result:

```
hello // failure
null // success
```

## Coordination parsers

### Separated

Matches all occurrences of a parser that are separated by another one. If a separator is not followed by a value, it is not consumed.

```
Parser<IReadOnlyList<T> Separated<U, T>(Parser<U> separator, Parser<T> parser)
```

Usage:

```c#
var parser = Separated(Terms.Text(","), Terms.Integer());
parser.Parse("1, 2, 3");
parser.Parse("1,2;3");
```

Result:

```
[1, 2, 3]
[1, 2]
```

### Between

Matches a parser when between two other ones.

```c#
Parser<T> Between<A, T, B>(Parser<A> before, Parser<T> parser, Parser<B> after)
```

Usage:

```c#
var parser = Between(Terms.Char('['), Terms.Integer(), Terms.Char(']'));
parser.Parse("[ 1 ]");
parser.Parse("[ 1");
```

Result:

```
1
0 // failure
```

### SkipWhiteSpace

Matches a parser after any blank spaces. This parser respects the `Scanner` options related to multi-line grammars.


```c#
Parser<T> SkipWhiteSpace<T>(Parser<T> parser)
```

Usage:

```c#
var parser = SkipWhiteSpace(Literals.Text("abc"));
parser.Parse("abc");
parser.Parse("  abc");
```

Result:

```
"abc"
"abc"
```

> Note: This parser is used by all Terms (e.g., Terms.Text) to skip blank spaces before a Literal.

### WithWhiteSpaceParser

Temporarily sets a custom whitespace parser for the inner parser. The custom whitespace parser is used to skip whitespace within the scope of the wrapped parser, then the previous whitespace parser is restored.

This allows grammars to define custom whitespace handling for specific parts of the grammar.

```c#
Parser<T> WithWhiteSpaceParser<T>(this Parser<T> parser, Parser<TextSpan> whiteSpaceParser)
```

Usage:

```c#
var hello = Terms.Text("hello");
var world = Terms.Text("world");
var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

parser.Parse("..hello.world");  // Succeeds - dots are treated as whitespace
parser.Parse("hello world");     // Fails - regular spaces are not whitespace
```

Result:

```
("hello", "world")
null
```

This parser can be nested, with each level managing its own whitespace context:

```c#
var a = Terms.Text("a");
var b = Terms.Text("b");
var c = Terms.Text("c");

var inner = a.And(b).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));
var outer = inner.And(c).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('-'))));

outer.Parse("a.b-c");  // Inner uses '.', outer uses '-' as whitespace
```

> Note: The custom whitespace parser must return a `TextSpan`. Use `Capture()` to wrap parsers that don't return `TextSpan`.

### WithComments

Based on `WithWhiteSpaceParser`, this helper makes it easier to define custom comments syntax.

Usage:

```c#
var hello = Terms.Text("hello");
var world = Terms.Text("world");
var parser = hello.And(world)
    .WithComments(builder =>
    {
        builder.WithSingleLine("--");
        builder.WithSingleLine("#");
        builder.WithMultiLine("/*", "*/");
    });

parser.Parse("hello -- comment\n world");
parser.Parse("hello -- comment\r\n world");
parser.Parse("hello # comment\n world");
parser.Parse("hello /* multiline\n comment\n */ world");
```

### Deferred

Creates a parser that can be referenced before it is actually defined. This is used when there is a cyclic dependency between parsers.

```c#
Deferred<T> Deferred<T>()
```

Usage:

```c#
var parser = Deferred<string>();
var group = Between(Terms.Char('('), parser, Terms.Char(')'));
parser.Parser = Terms.Integer().Or(group);

parser.Parse("((1))");
parser.Parse("1");
```

Result:

```
1
1
```

### Recursive

Creates a parser that can reference itself.

```c#
Deferred<T> Recursive<T>(Func<Deferred<T>, Parser<T>> parser)
```

Usage:

```c#
var number = Terms.Decimal();
var minus = Terms.Char('-');

var parser = Recursive<decimal>((u) =>
    minus.And(u)
        .Then(static x => 0 - x.Item2)
    .Or(number)
    );

parser.Parse("--1");
```

Result:

```
1
```

### Capture

Ignores the individual result of a parser and returns the whole matched string instead.

This can be used for pattern matching when each part of a pattern isn't more useful that the whole result.

```c#
Parser<TextSpan> Capture<T>(Parser<T> parser)
```

Usage:

```c#
var parser = Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer());
var capture = Capture(parser);

capture.Parse("age = 12");
```

Result:

```
"age = 12"
```

## Flow parsers

### Then

Convert the result of a parser. This is usually used to create custom data structures when a parser succeeds, or to convert it to another type.

```c#
Parser<U> Then<U>(Func<T, U> conversion)
Parser<U> Then<U>(Func<ParseContext, T, U> conversion)
Parser<U> Then<U>(U value)
Parser<U?> Then<U>() // Converts the result to `U`
```

Usage:

```c#
var parser = 
    Terms.Integer()
    .AndSkip(Terms.Char(','))
    .And(Terms.Integer())
    .Then(x => new Point(x.Item1, y.Item2));

parser.Parse("1,2");
```

Result:

```
Point { x: 1, y: 2}
```

When the previous results or the `ParseContext` are not used then the version without delegates can be used:

```c#
var parser = OneOf(
    Terms.Text("not").Then(UnaryOperator.Not),
    Terms.Text("-").Then(UnaryOperator.Negate)
);
```

### Else

Returns a value if the previous parser failed.

Usage:

```c#
var parser = Terms.Integer().Else<string>(0).And(Terms.Text("years"));

capture.Parse("years");
capture.Parse("123 years");
```

Result:

```
(0, "years")
(123, "years")
```

NB: This is similar to using `Optional()` since the result is always successful, but `Else()` returns a value. Use `Optional()` if you need to know if the parser was successful or not, and `Else()` if you only care about having a value as a result.

### ThenElse

Converts the result of a parser, or returns a value if it didn't succeed. This parser always succeeds.

NB: It is implemented using `Then()` and `Else()` parsers.

```c#
Parser<U> ThenElse<U>(Func<T, U> conversion, U elseValue)
Parser<U> ThenElse<U>(Func<ParseContext, T, U> conversion, U elseValue)
Parser<U> ThenElse<U>(U value, U elseValue)
```

Usage:

```c#
var parser = 
    Terms.Integer().ThenElse<long?>(x => x, null)

parser.Parse("abc");
```

Result:

```
(long?)null
```

When the previous results or the `ParseContext` are not used then the version without delegates can be used:

```c#
var parser = OneOf(
    Terms.Text("not").Then(UnaryOperator.Not),
    Terms.Text("-").Then(UnaryOperator.Negate)
);
```

### ElseError

Fails parsing with a custom error message when the inner parser didn't match.

```c#
Parser<T> ElseError(string message)
```

Usage:

```c#
var parser = 
    Terms.Integer().ElseError("Expected an integer")
    .AndSkip(Terms.Char(',').ElseError("Expected a comma"))
    .And(Terms.Integer().ElseError("Expected an integer"))
    .Then(x => new Point(x.Item1, y.Item2));

parser.Parse("1,");
```

Result:

```
failure: "Expected an integer at (1:3)
```

### Error

Fails parsing with a custom error message when the inner parser matched.

```c#
Parser<T> Error(string message)
Parser<U> Error<U>(string message)
```

Usage:

```c#
var parser = 
    Terms.Char('a')
    .Or(Terms.Char('b')
    .Or(Terms.Char('c').Error("Unexpected char c")));

parser.Parse("c");
```

Result:

```
failure: "Unexpected char c"
```

### When

Adds some additional logic for a parser to succeed. The condition is executed when the previous parser succeeds. If the predicate returns `false`, the parser fails.

```c#
Parser<T> When(Func<ParseContext, T, bool> predicate)
```

To evaluate a condition before a parser is executed use the `If` parser instead.

### If (Deprecated)

NB: This parser can be rewritten using `Select` (and `Fail`) which is more flexible and simpler to understand.

Executes a parser only if a condition is true.

```c#
Parser<T> If<TContext, TState, T>(Func<ParseContext, TState, bool> predicate, TState state, Parser<T> parser)
```

To evaluate a condition before a parser is executed use the `If` parser instead.

### Switch

Returns a parser using custom logic based on previous results.

```c#
Parser<U> Switch<U>(Func<ParseContext, T, Parser<U>> action)
```

Usage:

```c#
var parser = Terms.Integer().Switch((context, i) =>
        {
            // Valid entries: "1 is odd", "2 is even"
            // Invalid: "7 is even"

            return i % 2 == 0
            ? Terms.Text("is odd")
            : Terms.Text("is even");
        });
```

For performance reasons it is recommended to return a singleton (or static) Parser instance. Otherwise each `Parse` execution will allocate a new Parser instance.

### Eof

Expects the end of the string.

```c#
Parser<T> Eof()
```

### Discard

> This parser has been obsoleted. Use `Then` instead as it provides the same behavior
and more options.

Discards the previous result and replaces it with the default value or a custom one.

```c#
Parser<U> Discard<U>()
Parser<U> Discard<U>(U value)
```

### Lookup

Builds a parser that lists all possible matches to improve performance. Most parsers implement `ISeekable` parsers in order to provide `OneOf` a way to build a lookup table and identify the potential next parsers in the chain. Some parsers don't implement `ISeekable` because they are built too late, like `Deferred`. The `Lookup` parser circumvents that lack.

```c#
Parser<T> Lookup<U>(params ReadOnlySpan<char> expectedChars)
Parser<T> Lookup(params ISeekable[] parsers)
```

## Other parsers

### AnyCharBefore

Returns any characters until the specified parser is matched.

```c#
Parser<TextSpan> AnyCharBefore<T>(Parser<T> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
```

### Always

Always returns successfully, with an optional return type or value.

```c#
Parser<T> Always<T>()
Parser<object> Always()
Parser<T> Always<T>(T value)
```

### Fail

A parser that returns a failed attempt. Used when a Parser needs to be returned but one that should depict a failure.

```c#
Parser<T> Fail<T>()
Parser<object> Fail()
```

### OneOf

Like [Or](#Or), with an unlimited list of parsers.

```c#
Parser<T> OneOf<T>(params Parser<T>[] parsers)
```

## Comments

Whitespaces are parsed automatically when using `Terms` helper methods. To use custom comments 