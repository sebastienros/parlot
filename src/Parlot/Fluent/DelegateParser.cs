namespace Parlot.Fluent;

/// <summary>
/// A parser that delegates to a function. Used by source generation to wrap helper methods as parsers.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public sealed class DelegateParser<T> : Parser<T>
{
    /// <summary>
    /// A delegate that represents the Parse method signature.
    /// </summary>
    /// <param name="context">The parse context.</param>
    /// <param name="value">The output value when parsing succeeds.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public delegate bool TryParseFunc(ParseContext context, out T value);

    private readonly TryParseFunc _func;

    /// <summary>
    /// Creates a new DelegateParser wrapping the given delegate.
    /// </summary>
    /// <param name="func">The function to wrap.</param>
    public DelegateParser(TryParseFunc func)
    {
        _func = func ?? throw new System.ArgumentNullException(nameof(func));
    }

    /// <inheritdoc/>
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);
        
        var success = _func(context, out var value);
        if (success)
        {
            result.Set(context.Scanner.Cursor.Offset, context.Scanner.Cursor.Offset, value);
        }

        context.ExitParser(this);
        return success;
    }
}
