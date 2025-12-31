# Writing parsers, best practices

## When to call or not to call `Cursor.ResetPosition(TextPosition)`

`ResetPosition` is called when a parser advances the cursor but finally didn't match what it was supposed to.
In that case the cursor position needs to be reset to its initial position.

> ✔️ DO: Always return from a non-successful parser with the position of the cursor as when it was invoked.

For instance the `Sequence` parser will successively call two individual parsers:

```c#
public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
{
    context.EnterParser(this);

    var parseResult1 = new ParseResult<T1>();

    var start = context.Scanner.Cursor.Position;

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

## Lookup tables

The `OneOf` parser, which can also be created using `a.Or(b)`, is able to create a lookup table to optimize parsing.

Example:

```c#
var integer = Terms.Integer();
var hello = Terms.Text("Hello", caseInsensitive: true);
var intOrHello = integer.Or(hello);
```

Note: when `caseInsensitive: true`, `Text("Hello")` returns the canonical requested text ("Hello") by default to avoid allocating a new string. If you need the matched input text (e.g. "HELLO"), use `returnMatchedText: true`.

Both **integer** and **hello** have well-known characters that can be at the start of their potential values: 
- **integers** can start with `[0-9\.\-]`.
- **hello** can only start with an `h` or `H`.

This information allows us to make quick decisions when the **intOrHello** parser is invoked. If the next char is an `h` then the only possible 
match is **hello** so only this parser will be invoked. If the char is `z` then none of these can match so it will return a failure without testing either parser. Also because these two parsers also accept white spaces the outer parser will be able to do it once.

To be able to take advantage of this optimization, a parser type can implement `ISeekable`. Even when the parser may or may not be able to provide a list of "expected chars", the interface can be implemented and its `CanSeek` property can be set accordingly.

## Parser factories

Parsers should not allow other parsers to be created dynamically as parser constructors can be expensive (`OneOf` creates lookup tables).

For instance, selecting between parsers should not be done by returning newly created parsers from a lambda.

Use a selector that returns an index into a fixed parser list:

```c#
var a = Terms.Text("a");
var b = Terms.Text("b");
var p = Select(c => c.OptionA ? 0 : 1, a, b);
```
