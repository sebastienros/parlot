# Writing parsers, best practices

## When to call or not to call `Cursor.ResetPosition`

`ResetPosition` is called when a parser advances the cursor but finally didn't match what it was supposed to.
In that case the cursor position needs to be reset to its initial position, saved from `Cursor.Offset`.

> ✔️ DO: Always return from a non-successful parser with the position of the cursor as when it was invoked.

For instance the `Sequence` parser will successively call two individual parsers:

```c#
public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
{
    context.EnterParser(this);

    var parseResult1 = new ParseResult<T1>();

    var start = context.Scanner.Cursor.Offset;

    if (_parser1.Parse(context, ref parseResult1))
    {
        var parseResult2 = new ParseResult<T2>();

        if (_parser2.Parse(context, ref parseResult2))
        {
            result.Set(parseResult1.Start, parseResult2.End, new ValueTuple<T1, T2>(parseResult1.Value, parseResult2.Value));

            context.ExitParser(this);
            return true;
        }

        context.Scanner.Cursor.ResetPosition(start);
    }

    context.ExitParser(this);
    return false;
}
```

Because `parser1` could succeed and `parser2` fail, not resetting the position of the cursor would leave it at the position where `parser1` ended.
If another parser was trying to then parse the rest of the input, it would then start from there.

In this case, we need to call `ResetPosition` like it is done in the example. However we don't need to call it if `parser1` fails, since `parser1` is assumed to reset the cursor if it advanced it.

The following example is from the `OneOf` parser:

```c#
public override bool Parse(ParseContext context, ref ParseResult<T> result)
{
    context.EnterParser(this);

    foreach (var parser in _parsers)
    {
        if (parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return true;
        }
    }

    context.ExitParser(this);
    return false;
}
```

In this case it is never necessary to invoke `ResetPosition` since as soon as a parser is successful we exit the method.

> ✔️ DO: Create a unit test to ensure that the parser resets the position when it's failing.

> ❌ DON'T: Save `Cursor.Position` to move back to it. The cursor only tracks its offset, reading `Position` computes the
> line and the column (see [Moving the cursor](#moving-the-cursor)). Save `Cursor.Offset` and call `ResetPosition(int)`.

## Reading the text directly

When parsing a stream, the parser runs on a window of the text which more text may follow (see [Parsing streams](streaming.md)).
The `Cursor` methods record when a result depends on that text. A parser which reads `Cursor.Span` or `Cursor.Buffer` directly,
and fails or stops because the text is too short without advancing to its end, must call `Cursor.MarkHitEnd()`. It does
nothing when parsing a `string`.

A parser which reads the cursor is a *token* of the compacting buffer, which replaces the window while parsing a stream.
It must start with:

```c#
if (context.IsCompacting)
{
    return context.ParseToken(this, ref result);
}
```

`Cursor.Offset` is then an absolute offset: don't index `Cursor.Buffer` with it, use `Cursor.Span`, `Cursor.GetSpan`
and `Cursor.CreateSpan`. A parser which moves the cursor back to read the same text again, not only to report a failure,
must keep that text buffered with `context.Pin()` and `context.Unpin(pin)`. Both cost a branch when parsing a `string`.

> ✔️ DO: Test the parser with `Parse(TextReader)` over a reader returning one character at a time, with `BufferSize = 1`, and compare with `Parse(string)`.

## Moving the cursor

`Cursor.Position` is the absolute offset, the line and the column of the current char. The line starts at 1 and
increases after each `'\n'`. The column starts at 1 on each line and counts every char other than `'\n'`, `'\r'` included:
it is one more than the number of chars between the previous `'\n'` (or the start of the text) and the position. A
position at the end of the text follows the same rule, so after `"ab\n"` the cursor is at line 2, column 1, and after
`"ab\r"` at line 1, column 4. This is also what source generated parsers report.

The cursor only tracks its offset. The line and the column are computed when `Position` is read, by counting the `'\n'`
chars between the last position read and this one (vectorized on .NET 8 and later), as Roslyn computes the lines of a
`SourceText` only when they are asked for. Reading a position thus costs a scan of the text moved over since the last
read, which is why parsers save `Cursor.Offset` rather than `Cursor.Position` to move back. Read `Position` where it is
reported: in an error, or in a callback which records it in a result.

All the ways of moving the cursor forward only move the offset, so the chars they leave can contain `'\n'`:

- `Advance()` moves by one char and `Advance(count)` by `count` chars. Both stop at the end of the text, where they do
  nothing. A negative `count` is ignored.
- `AdvanceNoNewLines(count)` is `Advance(count)` for chars which are known to be in the buffer, such as a match. Its name
  dates from when the cursor tracked the lines as it moved.
- `AdvanceBy(consumed, newLines, trailingSegmentLength)` moves by `consumed` chars. The other arguments were the line
  and column deltas of the text when the cursor tracked them, they are ignored.

`ResetPosition(int)` moves the cursor to an offset. `ResetPosition(TextPosition)` only uses the offset of the position
and ignores its line and column. That only makes a difference for a position created by the caller with a line or a
column which don't match its offset: the cursor then reports the ones counted from the text. The line and column of the
initial position given to the `Cursor` or `Scanner` constructor are kept, and the positions after it are counted from it.

A parser which counts the chars of a match before moving the cursor should move it once, with `AdvanceNoNewLines(size)`,
as `Pattern` does.

> ✔️ DO: Assert the line and the column of the cursor after a match, not only its offset, including after a `"\r\n"` and when the match ends the text.

## Left recursion

A `Deferred` parser that is invoked again at the position where it is already active, like `expression` in
`expression.And(plus).And(term).Or(term)`, fails there instead of overflowing the stack. Looking for itself in the active
parsers costs every invocation of a `Deferred`, so the first parse of a grammar proves which ones can't be invoked again
where they are active, and only these skip it. This is the well-formedness check of parsing expression grammars: a
`Deferred` keeps the check when it is in a cycle of parsers that each invoke the next one before reading anything, like
the alternatives of a choice, the parser after an optional one, or a lookahead. The other ones, as in most grammars, behave
the same without it. The proof runs once and again when a `Deferred.Parser` is replaced. `MaxRecursionDepth` still applies
to every `Deferred`.

The proof assumes that a parser doesn't move the cursor before the position where it was invoked, and that the callbacks
of a grammar (conversions, predicates, selectors) don't invoke parsers.

A custom parser can be invoked before anything is read and can then invoke any parser, so a `Deferred` which can invoke one
before reading anything keeps the check. A custom parser whose fields only hold plain data (numbers, enums, strings, and
arrays or structs of these), and whose enclosing types too, is assumed not to invoke other parsers, like a parser that reads
a token. This can't be known in a native AOT application, where every custom parser keeps the check.

The parser a context skips white space with, `ParseContext.WhiteSpaceParser` or `WithWhiteSpaceParser`, is invoked by every
parser that skips white space. When it can invoke a `Deferred` parser before reading anything, every `Deferred` keeps the
check while it is used.

> ✔️ DO: Never move the cursor before the position where the parser was invoked.

## Lookup tables

The `OneOf` parser, which can also be created using `a.Or(b)`, is able to create a lookup table to optimize parsing.

Example:

```c#
var integer = Terms.Integer();
var hello = Terms.Text("Hello", caseInsensitive: true);
var intOrHello = integer.Or(hello);
```

Note: when `caseInsensitive: true`, `Text("Hello")` returns the canonical requested text ("Hello") by default to avoid allocating a new string. If you need the matched input text (e.g. "HELLO"), use `returnMatchedText: true`.
Matched text reuses the requested string if its casing is exact, or the input string if
the match covers the entire input; only a differently cased substring is materialized.
Similarly, `TextSpan.ToString()` reuses its buffer when the span covers it entirely.

Both **integer** and **hello** have well-known characters that can be at the start of their potential values: 
- **integers** can start with `[0-9\.\-]`.
- **hello** can only start with an `h` or `H`.

This information allows us to make quick decisions when the **intOrHello** parser is invoked. If the next char is an `h` then the only possible 
match is **hello** so only this parser will be invoked. If the char is `z` then none of these can match so it will return a failure without testing either parser. Also because these two parsers also accept white spaces the outer parser will be able to do it once.

To be able to take advantage of this optimization, a parser type can implement `ISeekable`. Even when the parser may or may not be able to provide a list of "expected chars", the interface can be implemented and its `CanSeek` property can be set accordingly.

The expected chars have to include every char the parser can start with, since a parser is not invoked for any other one. They are computed when the parser is built, so they can't depend on something that is only known when it parses, like the current culture. A parser which can't tell sets `CanSeek` to `false`: a text compared with `StringComparison.CurrentCultureIgnoreCase` does.

A `Deferred` parser reads the hints from its `Parser` whenever they are asked for, so they are known once the grammar is complete. So do the parsers that take their hints from the parsers they start with, like sequences, `Then`, `Between`, `If`, and the operator parsers `Unary`, `LeftAssociative` and `RightAssociative`: a sequence built before the `Deferred` it starts with is set tells its chars once the grammar is complete. A `OneOf` which held an alternative that couldn't tell when it was built, a `Deferred` set later or another `OneOf` holding one, builds its table again from the current hints the first time it parses (once, under a lock shared by every parser), along with the choices its alternatives start with. A `Deferred` that reaches itself while its hints are read reports `CanSeek == false`, so a left-recursive alternative is tried for any char. The source generator runs the factory to completion before emitting, so generated choices use the same table.

`ElseError` doesn't implement `ISeekable`: a choice skipping it for a char its parser doesn't start with would fail where it reports an error.

Parlot's ordinal text and keyword parsers, and the parsers that start with one, also tell choices their whole text, so
that a choice can skip the alternatives that share a first char with the input but not the rest of their text. A custom
`ISeekable` parser only tells its first chars.

Source generation can additionally specialize compatible keyword-only choices using
length and discriminating characters. This does not replace the runtime lookup table
or alter ordered-choice semantics. See [Keyword lookup investigation](keyword-lookups.md)
for the measured tradeoffs and eligibility restrictions.

Compatible ordinal `Text`-only choices use a separate packed-prefix recognizer. It
preserves ordered alternatives even when texts overlap: `Text("if1").Or(Text("if"))`
still returns `"if1"` for `"if12"`, while reversing the alternatives returns `"if"`.
Unlike keywords, these text choices have no boundary requirement.

## Parser factories

Parsers should not allow other parsers to be created dynamically as parser constructors can be expensive (`OneOf` creates lookup tables).

For instance, selecting between parsers should not be done by returning newly created parsers from a lambda.

Use a selector that returns an index into a fixed parser list:

```c#
var a = Terms.Text("a");
var b = Terms.Text("b");
var p = Select(c => c.OptionA ? 0 : 1, a, b);
```

## Grammar rules

[Grammar rules](grammar-rules.md) lists the patterns that make a grammar slower than it could be or keep it from parsing
what it seems to, which the source generator and Parser Explorer report. `GrammarAnalyzer.Analyze` finds them in a
grammar built at runtime.
