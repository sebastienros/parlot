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

## Limiting parser recursion

Recursive grammars can limit the nesting depth accepted from untrusted input by setting
`maxRecursionDepth` on the `ParseContext`. The default value is `0`, which applies no limit.
A source-generated parser takes the limit from its attribute instead,
`[GenerateParser(nameof(TryParse), MaxRecursionDepth = 128)]` (see [Source generation](source-generation.md)).

```c#
var context = new ParseContext(new Scanner(input), maxRecursionDepth: 128);

if (!parser.TryParse(context, out var result, out var error))
{
    // error reports when the configured recursion limit was exceeded
}
```

Each entry into a `Deferred` or `Recursive` parser counts as one level, and so does each invocation of a `Unary` parser
and each prefix operator applied by an [operator table](#operator-tables). Exceeding the limit throws a `ParseException`,
which fails the whole parse: a choice doesn't try its next alternative, `TryParse` returns `false` with the error and
`Parse` throws.

Without a limit, the `Deferred` and `Recursive` parsers check the stack of the thread on each entry, with
`RuntimeHelpers.TryEnsureSufficientExecutionStack()` (`EnsureSufficientExecutionStack()` below .NET Standard 2.1), and fail
the whole parse the same way, with the error "The input is nested too deeply to be parsed on the remaining stack.", rather
than overflowing the stack, which would end the process. How deep a grammar can go then depends on the stack of the thread
and on the stack each level of the grammar takes; a limit makes it independent of both. Each entry increments a counter
and the stack is checked on every 16th one, so a grammar recurses at most 16 levels past a check, which the runtime's
margin (64 KB or more) leaves room for. `Unary` and the prefix operators of an operator table read their operators in a
loop, so a chain of operators doesn't use the stack, whatever its length; the levels they count are only for the limit.

A `Deferred` parser that is invoked again at the position where it is already active (left recursion) fails at that position
instead of overflowing the stack. The parsers which can't be invoked again where they are active skip this check, see
[Left recursion](writing.md#left-recursion). `disableLoopDetection: true` on the `ParseContext` disables it for every parser.

## Reporting where a parse failed

A parse that fails without an error leaves `TryParse` with a `null` error and the cursor where it started. The most
useful position is then the *furthest failure*: where the attempt which matched the most text stopped. The cursor keeps
it in `Cursor.FurthestOffset`, and `Cursor.FurthestPosition` adds its line and column, computed when it is read:

```csharp
var context = new ParseContext(new Scanner(text));

if (!program.TryParse(context, out var value, out var error))
{
    // An Error or ElseError reports its own position, the furthest failure is where the longest attempt stopped
    var position = context.Scanner.Cursor.FurthestPosition;
    throw new FormatException($"{error?.Message ?? "Syntax error"} at {position}.");
}
```

For a program parsed as `ZeroOrMany(statement).AndSkip(Eof().ElseError("Expected a statement"))`, the error is where the
statements stopped matching, at the start of the statement that failed, while the furthest failure is where that
statement went wrong. For `SELECT a, b FRM t` and a `SELECT` grammar, it is the offset of `FRM`, after the white space the
expected `FROM` skipped. It is the position that [source-generated parsers](source-generation.md#failure-position)
return, with the same rules for what a parser reads and then rejects (`When`, `Not`, `WhenFollowedBy`,
`WhenNotFollowedBy`).

The `Parse`, `TryParse` and `TryMatch` methods taking a `ParseContext` reset it before they parse, so a context which is
reused for successive parses reports the failure of the last one. A parse which drives `Parse(context, ref result)`
itself calls `Cursor.ResetFurthestOffset()` first. After a successful parse, `FurthestOffset` is at least where the parse
ended. For a `TextReader`, `StreamParseOptions.ContextFactory` gives access to the context, whose cursor reports the same
furthest failure as the one of the `string`.

The parsers move the cursor back when they fail after reading, and the cursor keeps the furthest offset it was moved back
from on that path only: it doesn't change the time of a successful parse.

## Collecting diagnostics without stopping

By default, `Error(...)` and `ElseError(...)` throw `ParseException`; `TryParse(..., out error)`
converts it to a single `ParseError`. `Warning(...)` does nothing. To collect grammar errors
and warnings instead, enable `CollectDiagnostics` on the per-parse `ParseContext` and inspect
its `Diagnostics` list:

```csharp
var parser = Literals.Char('a').ElseError("Expected a").Optional()
    .And(Literals.Char('b').ElseError("Expected b").Optional());
var context = new ParseContext(new Scanner(input)) { CollectDiagnostics = true };
var success = parser.TryParse(context, out var value, out var fatalError);
foreach (var diagnostic in context.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Position}: {diagnostic.Message}");
}
```

Each `ParseDiagnostic` has a `Message`, a `TextPosition`, and a `Severity` (`Error` or `Warning`),
listed in the order they were reported.

In this mode an error records its diagnostic, then returns `false` from the parser that
reported it with the cursor restored to its starting position. The enclosing grammar can try
another alternative or absorb the failed token with an optional/repetition parser. No input is
skipped and no replacement value is synthesized. A warning records its diagnostic and keeps the
successful result. Ordinary mismatches do not produce diagnostics; recursion limits and cancellation
retain their existing handling and are not collected. `TryParse` can therefore return `true` even
when `context.Diagnostics` contains errors. Use a fresh context for each parse if diagnostics
should not accumulate across calls.

In ordered choices, diagnostics from a failed alternative are discarded if a later
alternative succeeds. When every alternative fails, only diagnostics from the branch whose
error reaches the greatest input offset are kept; ties favor the earlier branch, and a branch
that only reported warnings is not kept. Diagnostics absorbed by optional or repetition parsers
remain in the list when their surrounding branch is retained. Put `Warning` on the whole
construct it describes, for instance `(a + b).Warning(...)` rather than `a.Warning(...) + b`,
so that it is only reported once that construct matched.

Set `TreatWarningsAsErrors` to report warnings as errors. A promoted warning fails its parser like
`Error(...)`: it throws when `CollectDiagnostics` is disabled, and is otherwise recorded with the `Error`
severity so that the grammar can try another alternative.

To collect the diagnostics of a `TextReader`, create the context in `StreamParseOptions.ContextFactory`:

```csharp
ParseContext? context = null;
var options = new StreamParseOptions
{
    ContextFactory = (scanner, cancellationToken) =>
        context = new ParseContext(scanner, cancellationToken) { CollectDiagnostics = true },
};
var success = parser.TryParse(reader, out var value, out var fatalError, options);
// context.Diagnostics
```

## Matching without a value

`IsMatch` returns whether a parser matches the start of a text, and `TryMatch` also returns the text it matched, as a
`TextSpan`. Neither builds the value: the lists, tuples and options of the parsers are skipped, which `TryParse` would
allocate and discard.

```c#
var assignment = Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer());

assignment.IsMatch("age = 12; next"); // true
assignment.TryMatch("age = 12; next", out var match); // match is "age = 12"
```

- Like `TryParse`, the parser doesn't have to reach the end of the text. Add `.Eof()` to match the whole text.
- The text is the one [`Capture`](#capture) returns, from the cursor to where the parser ended: it includes the white space
  that a `Terms` parser skips before its first token.
- Callbacks that read a value, like those of `Then` or `When`, still receive it, so the parsers below them build their
  values.
- A parse error, like the ones of `ElseError`, or a cancellation fails the match. `TryMatch(..., out error)` returns it.
- The `ParseContext` overloads match at the cursor of the context and move it after the match, so a context can be reused
  without allocating.

## Associative operators

`LeftAssociative` groups from the left (`10 - 4 - 2` is `(10 - 4) - 2`); `RightAssociative` groups from the right (`10 - 4 - 2` is `10 - (4 - 2)`). Both accept an ordered array of operator parsers returning a common value and a shared factory that receives `(left, right, operation)`:

```csharp
var add = Terms.Char('+').Then(static _ => 1);
var subtract = Terms.Char('-').Then(static _ => -1);
var number = Terms.Decimal();

var expression = number.LeftAssociative(
    [add, subtract],
    static (left, right, operation) => operation == 1 ? left + right : left - right);

expression.Parse("10 - 4 - 2"); // 4
```

Replace `LeftAssociative` with `RightAssociative` to obtain `8` for the same input. The factory can also receive `ParseContext` as its first argument: `(context, left, right, operation)`. Existing overloads accepting `(operatorParser, factory)` tuples remain available when each operator needs a distinct factory. Both forms try the operators through a `OneOf`, so only the operators that can start with the next char are tried, in order, and white space is skipped once when they all skip it. If an operator matches but the next operand does not, the operator is rolled back and the expression parsed so far is returned.

## Operator tables

`Operators()` builds a single parser for a whole table of operators, from the level which binds the tightest. Each level
is added with the same tuples as the combinators above: `Unary` for prefix operators, `Postfix`, `LeftAssociative` and
`RightAssociative`:

```csharp
var expression = Deferred<Expression>();
var primary = number.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

expression.Parser = primary.Operators()
    .Postfix((Terms.Char('!'), static x => new Factorial(x)))
    .Unary((Terms.Char('-'), static x => new Negate(x)), (Terms.Text("not"), static x => new Not(x)))
    .RightAssociative((Terms.Text("**"), static (a, b) => new Power(a, b)))
    .LeftAssociative((Terms.Char('*'), static (a, b) => new Multiply(a, b)), (Terms.Char('/'), static (a, b) => new Divide(a, b)))
    .LeftAssociative((Terms.Char('+'), static (a, b) => new Add(a, b)), (Terms.Char('-'), static (a, b) => new Subtract(a, b)))
    .LeftAssociative((Terms.Text("<<"), static (a, b) => new ShiftLeft(a, b)))
    .LeftAssociative((Terms.Text("<="), static (a, b) => new LessOrEqual(a, b)), (Terms.Char('<'), static (a, b) => new Less(a, b)));

expression.Parse("-2 ** 3! * 4 << 1 < 5"); // ((((-2) ** (3!)) * 4) << 1) < 5
```

The table parses exactly what the chain of `Unary`, `LeftAssociative` and `RightAssociative` parsers with the same
levels parses, each one reading its operands with the previous one, with the same results, spans and cursor positions.
The chain tries the operators of each of its levels after each operand, and every operand goes through every level. The
table instead looks for the operator following an operand once, through a `OneOf` of all its operators, so that only the
operators that can start with the next char are tried, and climbs the levels from there (precedence climbing). The cost
of an operand doesn't depend on the number of levels.

- Within a level the first operator that matches applies, even when a later one would match more text, as in the chain.
- An operator that is not followed by an operand is not read, and the looser levels try their operators at its position:
  with `<` in a tighter level and `<=` in a looser one, `a <= b` reads the `<` of the tighter level, which isn't followed
  by an operand, then the `<=` of the looser one.
- A prefix operator that is not followed by an operand fails the parser, like `Unary`. The first prefix operator which
  reads some text applies, and its operand can start with another prefix operator of the same level.
- A postfix level applies its operators to the value of the tighter levels as long as one of them reads some text. After
  a postfix operator only the operators of its level and of the looser ones are tried: `a! ** b`, with `**` tighter than
  `!`, is `a!`.
- An operator and an operand that both read nothing stop the level, as in the chain.
- Chains of operators don't recurse, whatever their length: the prefix operators are read in a loop, like the binary ones.
  With a `maxRecursionDepth`, each prefix operator applied still counts as one level of recursion.
- The parser is seekable from its operand and its prefix operators, so it can be an alternative of a lookup table.
- Each call of `Unary`, `Postfix`, `LeftAssociative`, `RightAssociative` or `Ternary` returns a new parser, the table is not modified.
  `Named()` returns a `Parser<T>`, call it last.

A chain of `Unary`, `LeftAssociative` and `RightAssociative` parsers still parses level by level: build it with
`Operators()` to parse it with a single precedence parser.

### Operators with a value

The factories of a level can also receive the value of the operator, so that member accesses, indexers, calls and
conditionals are levels of the table rather than parsers around it:

```csharp
var expression = Deferred<Expression>();
var primary = identifier.Or(number).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

expression.Parser = primary.Operators()
    .Postfix(
        (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => new Member(x, name.ToString())),
        (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => new Index(x, index)))
    .Unary((Terms.Char('-'), static x => new Negate(x)))
    .LeftAssociative([Terms.Text("*"), Terms.Text("/")], static (a, b, op) => new Binary(op, a, b))
    .LeftAssociative((Terms.Char('+'), static (a, b) => new Add(a, b)))
    .LeftAssociative((Terms.Text("=="), static (a, b) => new Equal(a, b)))
    .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => new Conditional(c, a, b), rightAssociative: false)
    .LeftAssociative((Terms.Text("??"), static (a, b) => new Coalesce(a, b)));

expression.Parse("-a.b[i] * 2 == c ? d : e ?? f"); // ((((-(a.b[i])) * 2) == c) ? d : e) ?? f
```

- `Postfix((op, (x, value) => ...))` passes the value of the operator to its factory. Each call takes operators of one
  type; `Postfix(first, second)` takes two operators of different types in the same level, like a member access and an
  indexer, which apply in any order: `a.b[c].d`.
- `LeftAssociative(operators, (a, b, op) => ...)` and `RightAssociative(operators, (a, b, op) => ...)` are the levels of
  the `LeftAssociative(Parser<TOperator>[], Func<T, T, TOperator, T>)` combinators, whose single factory receives the
  value of the operator.
- `Ternary(question, middle, colon, (condition, whenTrue, whenFalse) => ...)` is a level of conditionals. The condition
  is a value of the tighter levels, and `middle`, usually the whole expression, is parsed between `question` and `colon`.
  With `rightAssociative: true`, the default, the last operand can be another conditional: `a ? b : c ? d : e` is
  `a ? b : (c ? d : e)`, as in C. With `rightAssociative: false` it is a value of the tighter levels only:
  `a ? b : c ? d : e` is `(a ? b : c) ? d : e`. The looser levels apply to the whole conditional, so with `??` looser,
  `a ? b : c ?? d` is `(a ? b : c) ?? d` and the last operand can't contain a `??`, while `a ?? b ? c : d` is
  `a ?? (b ? c : d)`. A last operand that is a whole expression, as in C# where it can be an assignment, can't be a level.
- These levels parse what the equivalent chain parses: a level whose operator is `question.SkipAnd(middle).AndSkip(colon)`
  for a conditional, or `primary.And(ZeroOrMany(postfix))` folded for the postfix operators of the tightest level, with
  the same results, spans and cursor positions. When `question`, `middle`, `colon` or the last operand doesn't match,
  nothing is read and the looser levels are tried at the position of `question`: a `?` doesn't need to exclude the `??`
  of a looser level.
- The table looks up the operator following an operand through a single choice of all its operators, in which each level
  of valued operators is a check of the first chars of these: when the next char can start one of them, the level that
  applies them parses them and passes their values to the factories right away, otherwise the looser levels are tried in
  the same choice. An operator is parsed only once at a position, even when it parses an expression, like an index or the
  middle of a conditional, and the values are never kept for a looser level. A table of valued levels, like a level per
  precedence of binary operators with a single factory receiving the operator, costs a single choice after an operand,
  like the same table with a factory per operator. When the next char can start an operator of a valued level that
  doesn't match, another choice follows, from the next level: with a valued `**` level tighter than a `*` level,
  `a * b` probes the `**` level, which doesn't match, then looks up the `*` from the next level.

## Terms and Literals

These are lowest level elements of a grammar, like a `'.'` (dot), predefined strings like `"hello"`, numbers, and more.
In Parlot they usually are accessed from the `Literals` or `Terms` classes, with the difference that `Terms` will return a parser that
accepts blank spaces before the element.

Terms and Literals are accessed using the `Terms` and `Literals` properties from the `Parsers` static class (imported via `using static Parlot.Fluent.Parsers;`).

### WhiteSpace

#### Literals.WhiteSpace()

Matches blank spaces, optionally including new lines. Returns a `TextSpan` with the matched spaces. The blank spaces are the
chars of the context's white space set, see [WithWhiteSpaceSet](#withwhitespaceset).

```c#
Parser<TextSpan> WhiteSpace(bool includeNewLines = false)
```

Usage:

```c#
var input = "   \thello world  ";
var parser = Literals.WhiteSpace();
```
Result:

```
"   \t"
```

#### Terms.WhiteSpace()

Matches the `WhiteSpaceParser` configured in the current context.

```c#
Parser<TextSpan> WhiteSpace()
```

When used from `Terms` it parses whitespace (or comments) as defined in the `ParseContext` or from `WithWhiteSpaceParser(parser)`.

When used from `Literals` it parses standard white spaces.

Usage:

```c#
var parser = Literals.Text("hello").And(Terms.WhiteSpace()).And(Literals.Text("world"));
parser.Parse("hello   world"); // success
parser.Parse("helloworld"); // failure
```

### NonWhiteSpace

Matches any non-blank spaces, optionally including new lines. Returns a `TextSpan` with the matched characters. The blank
spaces are the chars of the context's white space set, see [WithWhiteSpaceSet](#withwhitespaceset).


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

### Select

Selects the parser to execute at runtime. Use it when the next parser depends on mutable state or a custom `ParseContext` implementation.

```c#
Parser<T> Select<T>(Func<int> selector, params Parser<T>[] parsers)
Parser<T> Select<T>(Func<ParseContext, int> selector, params Parser<T>[] parsers)
Parser<T> Select<C, T>(Func<C, int> selector, params Parser<T>[] parsers) where C : ParseContext
```

Usage:

```c#
var yes = Literals.Text("yes");
var no = Literals.Text("no");

var parser = Select<CustomContext, string>(context => context.PreferYes ? 0 : 1, yes, no);

var result = parser.Parse(new CustomContext(new Scanner("yes")) { PreferYes = true });
```
`CustomContext` is an application-defined type that derives from `ParseContext` and exposes additional configuration.

The selector is evaluated once per parse, before any branch skips whitespace. Only the selected parser runs; its failure does not try another branch. If the selector returns an out-of-range index, the `Select` parser fails without consuming any input. Capture additional state through closures or custom `ParseContext` properties when needed.

### Text

Matches a given string, optionally in a case insensitive way.

```c#
Parser<string> Text(string text, bool caseInsensitive = false, bool returnMatchedText = false)
```

When `caseInsensitive` is `true`, the default behavior is to return the **requested** text (the canonical literal you passed in), not the input slice. This avoids allocating a new string for case-insensitive matches.

The case-insensitive comparison is ordinal. It doesn't depend on the current culture, neither when the parser is built nor when it parses: `Text("id", caseInsensitive: true)` matches `ID` under a Turkish culture too.

If you need to preserve the original casing from the input, set `returnMatchedText: true`.
An exact-case match returns the requested text without allocating; if the matched text
has different casing but covers the entire input, the input string itself is returned.
A differently cased substring is taken from the cases the parser keeps: the first four
casings it matches, usually the ones the input uses most, are allocated once and shared by
all its parses, and other casings are allocated each time. `Keyword` does the same.

Usage:

```c#
var input = "hello world";
var parser = Terms.Text("hello");
```

Result:

```
"hello"

Case-insensitive example:

```c#
var parser = Terms.Text("hello", caseInsensitive: true);
var result = parser.Parse(" HELLO world");
// result == "hello" (no allocation)
```

Opt-in to return the matched input text:

```c#
var parser = Terms.Text("hello", caseInsensitive: true, returnMatchedText: true);
var result = parser.Parse(" HELLO world");
// result == "HELLO"
```
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

### Keyword

Matches a keyword string, ensuring the following character is not a letter. This prevents partial matches of identifiers that start with the keyword text.

```c#
Parser<string> Keyword(string text, bool caseInsensitive = false, bool returnMatchedText = false)
Parser<string> Keyword(string text, ReadOnlySpan<char> identifierPart, bool caseInsensitive = false, bool returnMatchedText = false)
Parser<string> Keyword(string text, string identifierPart, bool caseInsensitive = false, bool returnMatchedText = false)
```

Like `Text`, when `caseInsensitive` is `true` the default behavior is to return the canonical keyword text you requested (e.g. passing "if" returns "if" even if the input is "IF"). Set `returnMatchedText: true` to preserve the matched input casing; exact-case matches reuse the keyword, and whole-input matches can reuse the input string.

This is useful when parsing programming language constructs like `if`, `while`, `return`, etc., where you want to match the exact keyword but not as part of a longer identifier.

Usage:

```c#
var input = "if(x > 5)";
var parser = Terms.Keyword("if");
var result = parser.Parse(input);
```

Result:

```
"if"
```

However, parsing `"ifoo"` would fail:

```c#
var input = "ifoo";
var parser = Terms.Keyword("if");
var result = parser.Parse(input); // Returns null - failure
```

Any non-letter is considered valid after a keyword (non-letters): `(`, `)`, `;`, `,`, whitespace, digits, `_`, `$`, etc.

When the identifiers of a language contain other chars, pass them as `identifierPart`: the keyword then fails when it is followed by one of these chars, which replace the default ASCII letters. For instance, to also reject a keyword followed by a digit, `_` or `-`:

```c#
const string identifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";
var parser = Terms.Keyword("in", identifierPart);

parser.Parse("in (1..3)"); // "in"
parser.Parse("in1");       // null - failure
parser.Parse("in_x");      // null - failure
parser.Parse("in-x");      // null - failure
```

A large set is a list too, built once with the grammar. For instance, for words that also take every char above ASCII:

```c#
static readonly string WordPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"
    + new string(Enumerable.Range(0x80, 0xFFFE - 0x80 + 1).Select(static i => (char)i).ToArray());

var from = Terms.Keyword("from", WordPart, caseInsensitive: true);

from.Parse("from x"); // "from"
from.Parse("from1");  // null - failure
from.Parse("fromé");  // null - failure
```

The set is checked with a bitset for ASCII chars, and the other chars are kept as ranges of consecutive chars, compared one after the other: the chars above ASCII of this example are a single range. It has the cost of the default check on ASCII input. Keywords given the same `string` share it. The `string` overload lets factories compiled for .NET Framework or .NET Standard 2.0 pass a string literal. A large source-generated choice of keywords uses the [keyword lookup](keyword-lookups.md) when they share their identifier chars, their case sensitivity, and contain only ASCII letters which are identifier chars themselves; a case-insensitive one must return the canonical text.

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

`NumberOptions.AllowUnderscore` accepts underscores between digits to make long numbers easier to read, like C#, Java or
Python do. Underscores can be repeated but must be between two digits, so `1_`, `1_.5` or `1e_5` only read `1`. They
are ignored when the value is computed. The option isn't part of `NumberOptions.Any`.

```c#
var parser = Terms.Number<long>(NumberOptions.Integer | NumberOptions.AllowUnderscore);
parser.Parse("1_000_000"); // 1000000
```

### Hexadecimal, Octal and Binary

Match integer digits in a specific radix and return any integer .NET type. These parsers don't consume
a radix prefix or a leading sign.

```c#
Parser<T> Hexadecimal<T>() where T : IBinaryInteger<T>
Parser<T> Octal<T>() where T : IBinaryInteger<T>
Parser<T> Binary<T>() where T : IBinaryInteger<T>
```

Prefixes can be composed independently:

```c#
var hexadecimal = Terms.Text("0x").SkipAnd(Literals.Hexadecimal<int>());
var octal = Terms.Text("0o").SkipAnd(Literals.Octal<int>());
var binary = Terms.Text("0b").SkipAnd(Literals.Binary<int>());
```

### String

Matches a quoted string literal with escape sequences. Use this parser to parse strings from a programming language.

```c#
Parser<TextSpan> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble)
Parser<TextSpan> String(StringLiteralQuotes quotes, StringEscapes escapes)
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

The value is a `TextSpan` of the input when the string has no escape sequence, and of a new string otherwise. To get
strings that repeat allocated once, use `ToPooledString()`, which also decodes the escape sequences into its pool.

#### Escape sequences

By default (`StringEscapes.Default`), a string accepts `\0 \\ \a \b \f \n \r \t \v \' \"`, a backslash before its own
quote, `\u` followed by four hex digits and `\x` followed by one to four hex digits. Any other escape sequence fails the
string. Raw new lines are allowed.

`StringEscapes` changes this for another language. These presets come with it:

| Preset | Escape sequences |
| --- | --- |
| `Default` | Described above. |
| `CSharp` | `Default`, plus `\e` and `\U` followed by eight hex digits. Raw new lines fail. |
| `JavaScript` | `\' \" \\ \b \f \n \r \t \v \0`, `\uXXXX`, `\u{...}`, `\xXX`, legacy octal (`\101`), line continuations (a backslash before a new line is removed), and any other char after a backslash stands for itself (`\$` is `$`). Raw `\r` and `\n` fail. |
| `Json` | `\" \\ \/ \b \f \n \r \t` and `\uXXXX`. Raw control chars (U+0000 to U+001F) fail. |
| `Python` | `\' \" \\ \a \b \f \n \r \t \v`, octal, `\xXX`, `\uXXXX`, `\UXXXXXXXX` and line continuations. Other escape sequences are kept as is, backslash included (`\d` is `\d`). Raw `\r` and `\n` fail. |
| `Java` | `\' \" \\ \b \f \n \r \t \s`, octal and `\uXXXX`. Raw `\r` and `\n` fail. |
| `Sql` | A doubled quote is a quote (`'it''s'`), and a backslash is an ordinary char. |

```c#
var javaScript = Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript);
javaScript.Parse(@"'\u{1F600} costs \$5'"); // "😀 costs $5"

var sql = Terms.String(StringLiteralQuotes.Single, StringEscapes.Sql);
sql.Parse("'it''s'"); // "it's"
```

For another language, combine the flags of its escape sequences. Whenever one of them adds escape sequences that start
with a backslash, `\\` is a backslash too; with none of them, as in `StringEscapes.None` or `Sql`, a backslash is an
ordinary char.

| Flag | Effect |
| --- | --- |
| `EscapedQuote` | A backslash before the string's own quote, whichever it is, is that quote. |
| `Quotes` | `\'` and `\"`, whichever quote delimits the string. |
| `NewLineAndTab` | `\n`, `\r` and `\t`. |
| `BackspaceAndFormFeed` | `\b` and `\f`. |
| `VerticalTab`, `Alert`, `Null`, `Escape` | `\v`, `\a`, `\0` and `\e` (U+001B). |
| `Slash`, `QuestionMark`, `Space` | `\/`, `\?` and `\s` (a space). |
| `Unicode`, `LongUnicode` | `\u` followed by four hex digits, `\U` followed by eight. |
| `BracedUnicode` | `\u{` hex digits `}`, a code point up to U+10FFFF. |
| `Hex`, `TwoDigitHex` | `\x` followed by one to four hex digits, or by exactly two (`\x41B` is `AB`). |
| `Octal` | `\0` to `\377`, as in JavaScript and Java. It takes precedence over `Null`. |
| `AnyChar` | A backslash before any other char is that char. |
| `KeepUnknown` | A backslash before any other char is kept, with the char. It takes precedence over `AnyChar`. |
| `LineContinuation` | A backslash before `\n`, `\r\n`, `\r`, U+2028 or U+2029 is removed with it. |
| `DoubledQuote` | Two quotes that delimit the string are one quote. |
| `SingleLine` | A raw `\r` or `\n` fails the string. |
| `NoControlChars` | A raw char from U+0000 to U+001F fails the string. |

Code points above U+FFFF decode to surrogate pairs. A sequence a flag claims but that isn't complete, like `\u12` with
`Unicode`, fails the string even with `AnyChar` or `KeepUnknown`. For example, Rust strings are
`StringEscapes.EscapedQuote | StringEscapes.Quotes | StringEscapes.NewLineAndTab | StringEscapes.Null | StringEscapes.TwoDigitHex | StringEscapes.BracedUnicode | StringEscapes.LineContinuation`
(except that Rust also skips the white space after a line continuation), and a JavaScript template literal, which can span
lines, is `StringEscapes.JavaScript & ~StringEscapes.SingleLine` with `StringLiteralQuotes.Backtick` (without its `${}`
substitutions). The escape char is always a backslash: a language with another one, like PowerShell's backtick, needs its
own parser. To get the raw text of a literal as well, as a JavaScript AST does, read its offsets in a
`Then(static (context, start, end, value) => ...)` callback, or wrap the string parser in `Capture()` for the raw text only.

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

#### Code points, escape sequences and surrogate pairs

The overload taking code point predicates matches identifiers of any Unicode chars, like those of JavaScript, Java,
C# or Python. The predicates define the whole sets, and `IdentifierOptions` tells what else is a char:

```c#
Parser<TextSpan> Identifier(Func<int, bool> isStart, Func<int, bool> isPart, IdentifierOptions options)
```

| Option | Accepts |
|---|---|
| `UnicodeEscapes` | `\uXXXX`, four hexadecimal digits (JavaScript, Java, C#) |
| `CodePointEscapes` | `\u{X...}`, up to `10FFFF` (JavaScript) |
| `SurrogatePairs` | chars outside the Basic Multilingual Plane, like `𝒜` (U+1D49C); a lone surrogate is never a char |
| `JavaScript` | all of the above |

An escape sequence or a surrogate pair is accepted when the predicate accepts the code point it stands for: `1`
(`1`) can follow the start of an identifier but can't start one. An escape sequence which isn't valid, or whose code
point isn't accepted, ends the identifier, like any char it doesn't accept. Without `SurrogatePairs`, each surrogate is
given to the predicates as a code point.

The value is the text of the identifier, escape sequences included. `Character.DecodeIdentifier()` returns the name,
without allocating when there are no escape sequences:

```c#
// Mathematical letters stand for the letters outside the BMP here
static bool IsStart(int c) => c is '$' or '_' || (c < 0x10000 ? char.IsLetter((char)c) : c is >= 0x1D400 and <= 0x1D7FF);
static bool IsPart(int c) => IsStart(c) || c is 0x200C or 0x200D || (c < 0x10000 && char.IsDigit((char)c));

var identifier = Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript);
var name = identifier.Then(static x => Character.DecodeIdentifier(x).ToString());

name.Parse(@"ab\u{63}"); // "abc"
```

The predicates are invoked for each ASCII char when the parser is built, which is then tested with bit masks: they must
give the same answer for a code point each time, and can't accept a new line. They are only invoked when parsing for
the other chars, escape sequences and surrogate pairs.

Since the chars the start predicate accepts can't be listed, the identifier has no hints for `OneOf`'s lookup table, like
`Identifier()` with an `extraStart` predicate. `Lookup()` can give it the chars it starts with, when they are known.

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

### Scan

Matches a token whose rules are written by hand, as a function that receives the remaining text and returns the
length of the token at its start. Use it instead of a custom `Parser<TextSpan>` when a token can't be expressed
quickly with other parsers.

```c#
delegate int SpanScanner(ReadOnlySpan<char> span)

Parser<TextSpan> Scan(SpanScanner scanner, ReadOnlySpan<char> startChars = default)
```

The scanner returns:

- the number of chars of the token, between `1` and `span.Length`;
- `0` when there is no token;
- a negative value when there is no token because `span` ended, such that more text could make one.

`startChars` lists the chars a token can start with, such that `OneOf` can skip the parser. Leave it empty when they
are not known.

Parlot handles the rest of the parser contract: the cursor doesn't move on failure, lines and columns are tracked when
the token contains new lines, and the token is scanned again with more text when parsing a stream. When parsing a
stream, `span` can end before the input does: a token reaching the end of `span` is scanned again, so a scanner that
looks at the chars following its token must include them when they are missing, or return a negative value.

Usage, the identifiers of Liquid templates, which can contain dashes but not end with one that starts a `-}}` or `-%}`
delimiter, and need a char other than a digit:

```c#
static int LiquidIdentifier(ReadOnlySpan<char> span)
{
    var size = 0;
    var nonDigits = 0;

    while (size < span.Length)
    {
        var c = span[size];

        if (char.IsAsciiLetter(c) || c == '_' || (c == '-' && size > 0))
        {
            nonDigits++;
        }
        else if (!char.IsAsciiDigit(c))
        {
            break;
        }

        size++;
    }

    // At the end of the span the dash is kept, then scanned again with more text
    if (size > 1 && size < span.Length && span[size - 1] == '-' && span[size] is '%' or '}')
    {
        size--;
        nonDigits--;
    }

    if (nonDigits == 0)
    {
        // Only digits, more text could add a letter
        return size == span.Length ? -1 : 0;
    }

    return size;
}

var input = "page-title-}}";
var parser = Terms.Scan(LiquidIdentifier, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");
```

Result:

```
page-title
```

Source generated parsers call the scanner directly. It must be a method group or a lambda that captures nothing,
not even the factory's parameters (`PARLOT015`, `PARLOT021`). At runtime a delegate to a static method goes through a thunk; a `static` lambda calling
it is slightly faster.

#### Text before the token

A `TextScanner` also gets the text before its token, to match only at the start of the input or of a line, or after
some char:

```c#
delegate int TextScanner(ReadOnlySpan<char> text, int position)

Parser<TextSpan> Scan(TextScanner scanner, ReadOnlySpan<char> startChars = default)
```

The token starts at `text[position]`, and the return value is the one of a `SpanScanner` with `span` being
`text.Slice(position)`. When parsing a `string`, `text` is the whole input and `position` the offset of the cursor. When
parsing a stream, the text before the token may be partial: `text` starts with the char before `position`, or earlier,
and `position` is `0` only at the start of the input. `ParseManyAsync` parses each value as its own input, so `position`
is `0` at the start of each value. With `Terms.Scan`, the white space skipped before the token is part of the text before it.

Usage, the HTML-like close comment of JavaScript (Annex B), a comment only at the start of the input or of a line:
`x-->y` is `x-- > y`.

```c#
static int HtmlCloseComment(ReadOnlySpan<char> text, int position)
{
    var span = text.Slice(position);

    if (!span.StartsWith("-->"))
    {
        // More text could complete a "-->"
        return span.Length < 3 && "-->".AsSpan().StartsWith(span) ? -1 : 0;
    }

    if (position > 0 && text[position - 1] is not ('\n' or '\r'))
    {
        return 0;
    }

    // Up to the end of the line
    var end = span.Slice(3).IndexOfAny('\n', '\r');
    return end < 0 ? span.Length : 3 + end;
}

var comment = Literals.Scan(HtmlCloseComment, "-");
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

## Operators

The combinators below are also available as C# operators on `Parser<T>`. Each one builds the same parser as the method
it stands for, so both forms can be mixed. See the [README](../README.md#operator-syntax) for the precedence rules.

| Operator | Equivalent |
|---|---|
| `a + b` | [And](#and) |
| `a \| b` | [Or](#or) |
| `a >> b` | [SkipAnd](#skipand) |
| `a << b` | [AndSkip](#andskip) |
| `a - b` | `Not(b).SkipAnd(a)`, see [Not](#not) |
| `a % b` | [Separated](#separated)`(b, a)` |
| `!a` | [Not](#not) |
| `-a` | [Optional](#optional) |
| `+a` | [OneOrMany](#oneormany) |
| `~a` | [ZeroOrMany](#zeroormany) |

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

A sequence, including the ones of `AndSkip` and `SkipAnd`, starts where the first of its parsers which matched something
does. A parser which matches nothing, like an `Optional`, a `ZeroOrMany` without elements or `Always`, is at the cursor,
before the white space the next `Terms` parser skips, so the sequence starts after that white space. When none of its
parsers matched something, the sequence starts where the first one does. Only the start changes, not the cursor:

```c#
var decorators = ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier()));
var cls = decorators.And(Terms.Text("class")).Then((context, start, end, value) => start);

cls.Parse("   class");    // 3, the start of "class"
cls.Parse("   @a class"); // 3, the start of "@a"
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

`ZeroOrMany`, `OneOrMany` and `Separated` return an array of the exact number of items, or `Array.Empty<T>()` when
there is none, typed as `IReadOnlyList<T>`. The first four items are kept by the parser and longer lists are
accumulated on a scratch stack of the thread, shared by nested lists, so a list only allocates its result. Rely on `IReadOnlyList<T>` rather than on the concrete type:
earlier versions returned a `HybridList<T>` or a `List<T>`.

### ZeroOrOne

Matches zero or one occurrence and returns an empty list or a single-item list. The method can also be post-fixed.

```c#
Parser<IReadOnlyList<T>> ZeroOrOne<T>(Parser<T> parser)
```

Usage:

```c#
var parser = ZeroOrOne(Terms.Text("hello"));
// or Terms.Text("hello").ZeroOrOne()
parser.Parse("hello"); // ["hello"]
parser.Parse(""); // [] with a successful state
```

Result:

```
["hello"]
[]
```

`ZeroOrOne(parser, defaultValue)` is no longer available. For a scalar value with a fallback, use
`Optional()` and project its `Option<T>` result:

```c#
var scalar = Terms.Text("hello").Optional().Then(static option => option.OrSome("world"));
```

For any number of matches, use `ZeroOrMany(parser)`.

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

When the parser tells the chars it can start with, like a sequence starting with a text, it isn't invoked when the next
char (after the white space it skips) can't start it. `ZeroOrOne`, `ZeroOrMany`, `OneOrMany` and the separator of
`Separated` do the same, see [Lookup tables](writing.md#lookup-tables).

### ZeroOrMany

Executes a parser as long as it's successful. The result is a list of all individual results. The method can also be post-fixed. An optional `max` limits the number of matches; `0` (the default) means unlimited.

```c#
Parser<IReadOnlyList<T>> ZeroOrMany<T>(Parser<T> parser)
Parser<IReadOnlyList<T>> ZeroOrMany<T>(Parser<T> parser, int max)
```

Usage:

```c#
var parser = ZeroOrMany(Terms.Text("hello"));
// or Terms.Text("hello").ZeroOrMany()
parser.Parse("hello hello");
parser.Parse("");
ZeroOrMany(Literals.Char('x'), max: 2).Parse("xxx"); // Returns two items; leaves the last x unconsumed
```

Result:

```
[ "hello", "hello" ]
[]
```

### OneOrMany

Executes a parser as long as it's successful, and is successful if at least one occurrence is found. The result is a list of all individual results. The method can also be post-fixed. An optional `max` limits the number of matches; `0` (the default) means unlimited.

```c#
Parser<IReadOnlyList<T>> OneOrMany<T>(Parser<T> parser)
Parser<IReadOnlyList<T>> OneOrMany<T>(Parser<T> parser, int max)
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

Succeeds if the parser is not matching. It never consumes any input: on success its value is `default` and its
result is empty, starting and ending where `Not` was invoked, before any white space the parser would skip. A sequence
ending with `Not`, like `keyword.AndSkip(Not(identifierPart))`, then ends where the previous parser did.

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

### Peek

Succeeds with the value of a parser when it matches, without consuming any input. This is the positive lookahead
(PEG `&p`), the counterpart of [Not](#not). Unlike [WhenFollowedBy](#whenfollowedby), it doesn't need a parser before it,
so it can start an alternative.

```c#
Parser<T> Peek<T>(Parser<T> parser)
```

Usage:

```c#
// An automatic semicolon: a ';', or nothing before a '}'
var semicolon = OneOf(
    Terms.Char(';').Then(static _ => true),
    Peek(Terms.Char('}')).Then(static _ => false));

var parser = Peek(Terms.Integer()).And(Terms.Integer());
parser.Parse("42");
parser.Parse("x");
```

Result:

```
(42, 42)
failure
```

The result starts and ends where the parser was invoked. When the parser fails, `Peek` fails and the cursor is where it
was. `Peek` tells the chars its parser can start with, so a choice only tries it for these chars. In a choice whose
alternatives all skip white space, like `semicolon` above, the choice skips the white space once before trying them, so
the cursor is after the white space when `Peek` succeeds there.

## Coordination parsers

### Separated

Matches one or more values separated by another parser. By default, a separator
not followed by a value is not consumed. An explicit `min: 0` accepts an empty
list, and `max: 0` means unlimited. At the maximum, the next separator and
value remain unconsumed.

```c#
Parser<IReadOnlyList<T>> Separated<U, T>(Parser<U> separator, Parser<T> parser, int min = 1, int max = 0)
Parser<IReadOnlyList<T>> Separated<U, T>(
    Parser<U> separator,
    Parser<T> parser,
    bool removeEmptyEntries = false,
    bool allowLeadingSeparator = false,
    bool allowTrailingSeparator = false)
Parser<IReadOnlyList<T>> Separated<U, T>(
    Parser<U> separator, Parser<T> parser, int min, int max,
    bool removeEmptyEntries, bool allowLeadingSeparator, bool allowTrailingSeparator)
```

Usage:

```c#
var parser = Separated(Terms.Text(","), Terms.Integer());
parser.Parse("1, 2, 3");
parser.Parse("1,2;3");
Separated(Literals.Char(','), Literals.Char('x'), min: 0, max: 2).Parse("x,x,x"); // Returns two items; leaves ",x"
```

Result:

```
[1, 2, 3]
[1, 2]
```

Set `removeEmptyEntries: true` to skip successive separators **between**
values, so `1,,2` returns `[1, 2]`. It does not permit a leading or trailing
separator on its own. Enable `allowLeadingSeparator` to accept `,1,2`, or
`allowTrailingSeparator` to consume `1,2,`. Each boundary accepts only one
separator unless `removeEmptyEntries` is also set, in which case it accepts
a run:

```c#
var parser = Separated(Terms.Char(','), Terms.Integer(),
    removeEmptyEntries: true,
    allowLeadingSeparator: true,
    allowTrailingSeparator: true);

parser.Eof().Parse(",,1,,2,,"); // [1, 2]
```

At least one value must match by default: empty input and input containing only
separators fail. With explicit `min: 0`, an empty list succeeds without consuming
leading separators if no value matches. Without `allowTrailingSeparator`, a run
of separators not followed by another value is left untouched for the next parser.
Negative bounds or a positive `max` less than `min` throw `ArgumentOutOfRangeException`
when the parser is built. Attach `.Eof()` to reject any extra input.

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

The value is the one of `parser`, but the result's `Start` and `End`, which `Then((context, start, end, value) => ...)`
receives, span the delimiters, as they do for `before.SkipAnd(parser).AndSkip(after)` and in generated parsers:

```c#
var key = Terms.Identifier();
Between(Terms.Char('['), key, Terms.Char(']')).Then(static (c, s, e, v) => (s, e)).Parse(" [x] "); // (1, 4)
```

For the span of `parser` alone, put the callback on it: `Between(before, parser.Then(...), after)`.

> [!NOTE]
> Before Parlot 2.0, the result of a runtime `Between` had the `Start` and `End` of `parser`, without the delimiters,
> while generated parsers already included them.

### SkipWhiteSpace

Matches a parser after any blank spaces. This parser respects the `Scanner.WhiteSpaceParser` option.


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

The parsers that skip white space invoke the custom parser only before the chars it can start with, which it tells with its
`ISeekable` hints, and at the end of the text, where a stream tells whether more text follows. Before a `.`, a `(` or an
identifier, a parser of white space and comments isn't invoked at all. A parser that can succeed without reading anything has no
hints, but `Capture`, `ZeroOrMany`, `ZeroOrOne` and `Optional` are looked through: `Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(),
Literals.Comments("//"))))`, the parser `WithComments` builds, is invoked only before white space and `/`. A parser that can start
with any char (one of the alternatives of its choice doesn't tell its chars, like `Literals.Pattern()`), or skips white space
itself, is invoked before every token. The same applies to `ParseContext.WhiteSpaceParser`.

Skipping white space ignores the value of the parser, so not invoking it where it would read nothing doesn't change what a
grammar parses. Only `ParseContext.OnEnterParser` sees fewer invocations. A [source-generated](source-generation.md) parser still
invokes it before each token.

### WithWhiteSpaceSet

Selects the chars that are white space for the inner parser, then restores the previous set. It applies to every `Terms` parser,
to `SkipWhiteSpace`, to `Literals.WhiteSpace()` and `NonWhiteSpace()`, and to the white space of `WithComments`.

```c#
Parser<T> WithWhiteSpaceSet<T>(this Parser<T> parser, WhiteSpaceSet whiteSpaceSet)
```

| Set | White space | New lines |
|---|---|---|
| `WhiteSpaceSet.Default` | `\t`, `\f`, space, U+00A0, U+1680, U+2000-U+200A, U+202F, U+205F, U+3000 (`Character.WhiteSpaces`) | `\n`, `\r`, `\v`, U+0085, U+2028, U+2029 (`Character.NewLines`) |
| `WhiteSpaceSet.Ascii` | `\t`, `\f`, space (`Character.AsciiWhiteSpaces`) | `\n`, `\r`, `\v` (`Character.AsciiNewLines`) |
| `WhiteSpaceSet.SpaceTabCrLf` | `\t`, space (`Character.SpaceTabCrLfWhiteSpaces`) | `\n`, `\r` (`Character.SpaceTabCrLfNewLines`) |

`Default` is the Unicode `White_Space` property: the chars `char.IsWhiteSpace` and the `\s` of .NET regular expressions accept.
Its new lines are the line breaks, including the next line char U+0085 and the line and paragraph separators U+2028 and
U+2029. As with the other new lines, `Terms` parsers skip them unless `UseNewLines` is set. Positions only start a new line at
`\n`, so like `\r` and `\v` these count as a column, and single line comments end at `\n` only.

> **Changed after Parlot 2.1.** The default set used to be `\t`, `\f`, space, U+00A0, U+1680, U+180E, U+2000-U+200A, U+202F,
> U+205F, U+3000 and U+FEFF, with `\n`, `\r` and `\v` as new lines. It now follows Unicode:
>
> - U+FEFF, the byte order mark, is no longer white space. A text that starts with one now fails to parse with a `Terms`
>   parser. `File.ReadAllText` and `StreamReader` remove it, but `Encoding.UTF8.GetString` keeps it: decode with a
>   `StreamReader`, or remove it with `text.TrimStart('\uFEFF')`.
> - U+180E, the Mongolian vowel separator, is no longer white space, as in Unicode since version 6.3.
> - U+0085, U+2028 and U+2029 are new lines of the set: `Terms` parsers skip them, unless `UseNewLines` is set.
> - `Character.WhiteSpaces`, `Character.NewLines` and `Character.WhiteSpaceOrNewLines` changed accordingly, and so did
>   `Character.IsWhiteSpace(char)`, `Character.IsWhiteSpaceOrNewLine(char)` and `Character.IsNewLine(char)`. The constants are
>   compiled into the assemblies that use them, which keep the previous values until they are rebuilt.
>
> The `Ascii` set didn't change. Its new lines are in the new `Character.AsciiNewLines`.

`Ascii` is the white space of C's `isspace` and Ruby's `\s`: a no-break space or another Unicode space is then a char like any
other, which a grammar can match or reject.

`SpaceTabCrLf` is `Ascii` without `\f` and `\v`: the white space of JSON (RFC 8259), XML 1.0, YAML, TOML, Go, and many SQL
dialects such as Azure Cosmos DB's. With `UseNewLines` it skips only space and `\t`.
A grammar which would otherwise use `WithWhiteSpaceParser(Literals.AnyOf(" \t\r\n"))` selects it to keep the fast path described below.

Usage:

```c#
var list = Separated(Terms.Char(','), Terms.Integer());

list.Parse("1,\u00A02");                                         // [1, 2]
list.WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Parse("1,\u00A02");  // [1], the no-break space is not skipped
list.WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Parse("1,\n\t2");     // [1, 2]
```

The set can also be selected for a whole parse with `ParseContext.WhiteSpaceSet`, for instance from
`StreamParseOptions.ContextFactory`:

```c#
var context = new ParseContext(new Scanner(text)) { WhiteSpaceSet = WhiteSpaceSet.Ascii };
```

New lines follow `UseNewLines` as with the default set: when it is `true` the `Terms` parsers skip `\t`, `\f` and space only.
`Literals.WhiteSpace(includeNewLines: true)` and `NonWhiteSpace(includeNewLines: true)` include the new lines of the set.

Unlike a `WithWhiteSpaceParser` that would match the same chars, a set keeps the white space fast path: skipping is still one
table lookup per char, and `OneOf` still skips white space once for all its alternatives. A white space parser is only invoked
before the chars it can start with, but each invocation costs more than a table lookup. A `WhiteSpaceParser`, when one is set,
is used instead of the set to skip white space, but the `Literals.WhiteSpace()` it contains reads the set. To skip ASCII white
space and comments:

```c#
var parser = grammar
    .WithComments(builder => builder.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
    .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
```

`WithWhiteSpaceSet` is part of the grammar, so a [source-generated](source-generation.md) parser honors it too.
Each set is a bit pair of the char table `Character` reads, which has no room for another one: use `WithWhiteSpaceParser` for
any other set of chars.

On the JSON benchmark, whose text is indented with spaces and new lines, parsing with these options measured (Apple M4 Pro,
.NET 10, `WhiteSpaceSetBenchmarks`):

| White space | Runtime | Ratio | Generated | Ratio |
|---|---:|---:|---:|---:|
| `WhiteSpaceSet.Default` | 31.8 us | 1.00 | 31.9 us | 1.00 |
| `WhiteSpaceSet.Ascii` | 32.0 us | 1.01 | 29.6 us | 0.93 |
| `WhiteSpaceSet.SpaceTabCrLf` | 32.4 us | 1.02 | 29.0 us | 0.91 |
| `WithWhiteSpaceParser(Literals.AnyOf(" \t\r\n"))` | 40.3 us | 1.27 | 39.7 us | 1.25 |
| `WithWhiteSpaceParser(Literals.AnyOf(" \t\f\n\r\v"))` | 43.5 us | 1.37 | | |

Every option allocates the same 64 KB. The sets measure the same, within the noise, and a white space parser skipping the same
chars is 25% to 37% slower.

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

The parser of the comments is only invoked before white space and the chars the comments start with. On the JSON of
`CommentsBenchmarks`, indented with spaces and new lines, `WithComments` adds 30% to the parse time of the default white space
set, down from 54% to 70% when it was invoked before every token.

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

A `Deferred` declares the chars its parser can start with once `Parser` is set, and the declaration follows the parser when it is replaced. A `OneOf` built before the parser was set reads the declaration again the first time it parses, so the usual `var expression = Deferred<T>(); ... expression.Parser = ...;` grammar gets a complete lookup table. A parser that reaches itself through a choice, directly or through other deferred parsers, can start with any char there.

### Recursive

Creates a parser that can reference itself.

```c#
Deferred<T> Recursive<T>(Func<Deferred<T>, Parser<T>> parser)
```

Like a `Deferred`, a `Recursive` parser is seekable: it declares the chars its parser can start with, so `OneOf` can skip it when they don't match. The declaration follows the parser when `Parser` is replaced.

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

The parser doesn't build its value, for instance its lists, unless a callback reads it. `parser.TryMatch(text, out var match)`
returns the same text without a `Capture` parser, see [Matching without a value](#matching-without-a-value).

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
Parser<U> Then<U>(Func<ParseContext, int, int, T, U> conversion)
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

#### Accessing Start and End Positions

The `Func<ParseContext, int, int, T, U>` overload provides access to the start and end offsets of the parsed result:

```c#
var parser = Literals.Identifier().Then((context, start, end, value) =>
{
    var length = end - start;
    return $"Parsed '{value}' at offset {start}, length {length}";
});

parser.Parse("hello");
```

Result:

```
"Parsed 'hello' at offset 0, length 5"
```

> **Note:** The start and end parameters are integer offsets (positions in the input buffer), not `TextPosition` objects.

### ToPooledString

Returns the text of a `Parser<TextSpan>` as a string taken from a pool, such that a value the parser matches many times
is allocated once, instead of once per `ToString()`.

```c#
Parser<string> ToPooledString(this Parser<TextSpan> parser)
```

Usage:

```c#
var identifiers = ZeroOrMany(Terms.Identifier().ToPooledString());
var names = identifiers.Parse("foo bar foo");
// ReferenceEquals(names[0], names[2]) == true
```

The pool belongs to the parser `ToPooledString()` returns and lives as long as it does: all its parses share it, also
concurrent ones, finding a value takes no lock and adding one does. It is bounded. It holds up to 4096 values of up to 64
chars, and longer values are created as usual. A full pool keeps its values. A pool whose values rarely repeat
(ids, numbers, timestamps) switches itself off: when nearly all its first 512 values were new, or when most lookups miss
once it is full.

A lookup costs about as much as creating a short string, so pooling pays off when the strings are kept, like the
identifiers or member names of a syntax tree held in memory, rather than when each one is used and dropped.

On `Terms.String()` or `Literals.String()`, the string literal also decodes its escape sequences into the pool, so that
an escaped value isn't allocated either when the pool has it. The literal it is called on is unchanged.

`ToPooledString()` takes a `Parser<TextSpan>`: the string of a `Parser<string>` is already created, a pool would only
share it. Case-insensitive `Text` and `Keyword` parsers returning the matched text keep the cases they return by
themselves.

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

`Error` declares the chars of the parser it is applied to, so a `OneOf` only tries it, and only raises the
error, for one of them.

### Warning

Reports a warning when the inner parser matched, keeping its result. Warnings are only recorded when
`ParseContext.CollectDiagnostics` is enabled, and are located where the matched input starts, after
any whitespace skipped by the inner parser. With `ParseContext.TreatWarningsAsErrors` the warning is
reported as an error and the parser fails.

```c#
Parser<T> Warning(string message)
```

Usage:

```c#
var parser = Terms.Text("var").Warning("'var' is deprecated, use 'let'")
    .Or(Terms.Text("let"));

var context = new ParseContext(new Scanner(" var")) { CollectDiagnostics = true };
parser.TryParse(context, out var value, out _);
```

Result:

```
"var"
context.Diagnostics: Warning (1:2): 'var' is deprecated, use 'let'
```

### When

Adds some additional logic for a parser to succeed. The condition is executed when the previous parser succeeds. If the predicate returns `false`, the parser fails.

```c#
Parser<T> When(Func<ParseContext, T, bool> predicate)
```

To evaluate a condition before a parser is executed use the `If` parser instead.

### WhenFollowedBy

Ensures that the result of the current parser is followed by another parser without consuming its input. This implements positive lookahead.

```c#
Parser<T> WhenFollowedBy<U>(Parser<U> lookahead)
```

Usage:

```c#
// Parse a number only if it's followed by a colon
var parser = Literals.Integer().WhenFollowedBy(Literals.Char(':'));
parser.Parse("42:"); // success, returns 42
parser.Parse("42");  // failure, lookahead doesn't match
```

The lookahead parser is checked at the current position but doesn't consume input. If the lookahead fails, the entire parser fails and the cursor is reset to the beginning.

### WhenNotFollowedBy

Ensures that the result of the current parser is NOT followed by another parser without consuming its input. This implements negative lookahead.

```c#
Parser<T> WhenNotFollowedBy<U>(Parser<U> lookahead)
```

Usage:

```c#
// Parse a number only if it's NOT followed by a colon
var parser = Literals.Integer().WhenNotFollowedBy(Literals.Char(':'));
parser.Parse("42");  // success, returns 42
parser.Parse("42:"); // failure, lookahead matches
```

The lookahead parser is checked at the current position but doesn't consume input. If the lookahead succeeds, the entire parser fails and the cursor is reset to the beginning.

### If

Executes a parser only if a condition is true.

```c#
Parser<T> If<T>(Func<bool> condition, Parser<T> parser)
Parser<T> If<T>(Func<ParseContext, bool> condition, Parser<T> parser)
Parser<T> If<C, T>(Func<C, bool> condition, Parser<T> parser) where C : ParseContext
```

The condition is evaluated once per parse, at the current cursor position before any branch skips whitespace.
When it is false, the two-argument form fails without consuming input or changing the result.
When it is true, the parser succeeds only if the selected child succeeds.

When every branch is seekable and they agree on skipping whitespace, `If` declares the chars any branch can start
with, so a `OneOf` only tries it, and only evaluates the condition, for one of these chars. When the branches skip
whitespace the `OneOf` skips it once before the condition is evaluated. `Select` declares its branches the same way.

Provide an else parser to select between two fixed branches:

```c#
Parser<T> If<T>(Func<bool> condition, Parser<T> thenParser, Parser<T> elseParser)
Parser<T> If<T>(Func<ParseContext, bool> condition, Parser<T> thenParser, Parser<T> elseParser)
Parser<T> If<C, T>(Func<C, bool> condition, Parser<T> thenParser, Parser<T> elseParser) where C : ParseContext

var allowWhiteSpace = true;
var parser = If(() => allowWhiteSpace, Terms.Integer(), Literals.Integer());
```

Only the selected branch executes. If it fails, the cursor is restored and `If` fails;
it never falls back to the other branch. Conditions and selectors should not consume input.
To validate a parsed value instead, use `When`.

The obsolete overloads taking a separate state argument and the `If<C, S, T>` type have been removed
in this major version. Capture state in the condition or read it from a custom `ParseContext`.

### Switch

Returns a parser using custom logic based on previous results.

```c#
Parser<U> Switch<U>(Func<ParseContext, T, int> selector, params Parser<U>[] parsers)
```

Usage:

```c#
var odd = Terms.Text("is odd");
var even = Terms.Text("is even");

var parser = Terms.Integer().Switch(
    (context, i) => i % 2 == 0 ? 1 : 0,
    odd,
    even
);
```

This signature prevents allocating new parsers in the selector.

`Switch` declares the chars of the parser it is applied to, so a `OneOf` only tries it for one of them.

### Eof

The extension `parser.Eof()` matches the parser followed by the end of the input, preserving its
result. If trailing input remains, it restores the cursor to where the parser started so alternatives
can retry. It declares the chars of the parser, so a `OneOf` only tries it for one of them.

```c#
Parser<T> Eof()
```

The standalone factory `Parsers.Eof()` checks only the current position. It returns `null` on success
and never consumes input or skips whitespace.

```c#
Parser<object?> Parsers.Eof()
```

Attach an error to the standalone check to report the position of trailing input, rather than the
start of the preceding parser:

```c#
var parser = Terms.Integer().AndSkip(Eof().ElseError("Unexpected trailing input"));
parser.Parse("42a"); // Throws ParseException at (1:3).
```

In contrast, `Terms.Integer().Eof().ElseError("Unexpected trailing input")` reports (1:1) because
the entire integer-and-EOF parser rolls back before `ElseError` reports the failure.
Both forms support source generation and streaming.

### Discard

> This parser has been obsoleted. Use `Then` instead as it provides the same behavior
and more options.

Discards the previous result and replaces it with the default value or a custom one.

```c#
Parser<U> Discard<U>()
Parser<U> Discard<U>(U value)
```

### Lookup

Builds a parser that lists all possible matches to improve performance. Most parsers implement `ISeekable` parsers in order to provide `OneOf` a way to build a lookup table and identify the potential next parsers in the chain. Some parsers can't declare the chars they start with, like a custom parser or one built from a predicate. The `Lookup` parser circumvents that lack.

```c#
Parser<T> Lookup<U>(params ReadOnlySpan<char> expectedChars)
Parser<T> Lookup(params ISeekable[] parsers)
```

The hints taken from other parsers are only used when every one of them can seek and they all skip white space the same way. Otherwise the resulting parser declares that it can start with any char, like the original one.

A parser that can start with any non-ASCII char lists its ASCII chars and `OneOf<T>.NonAsciiSeekableChar`, so that the choices which hold it don't build an entry for each non-ASCII char. More than 64 non-ASCII chars are listed this way:

```c#
var identifier = Literals.Pattern(char.IsLetter).Lookup(false, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" + OneOf<TextSpan>.NonAsciiSeekableChar);
```

## Other parsers

### AnyCharBefore

Returns any characters until the specified parser is matched.

```c#
Parser<TextSpan> AnyCharBefore<T>(Parser<T> parser, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
```

With `consumeDelimiter: true` the cursor moves past the delimiter. The returned `TextSpan` never includes the delimiter,
while the end position of the result (as seen by `Then((context, start, end, value) => ...)`) is the cursor position, after it.

It is important to use `AnyCharBefore(a.Or(b))` instead of `AnyCharBefore(a).Or(AnyCharBefore(b))` for performance reasons. Otherwise the first parser will have to look ahead for the whole source if only the second parser can be matched. By using a single `AnyCharBefore`, it will check whatever is first in the source, and then jump to the next option.

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