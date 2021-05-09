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
            return true;
        }

        context.Scanner.Cursor.ResetPosition(start);
    }

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
            return true;
        }
    }

    return false;
}
```

In this case it is never necessary to invoke `ResetPosition` since as soon as a parser is successful we exit the method.

> ✔️ DO: Create a unit test to ensure that the parser resets the position when it's failing.
