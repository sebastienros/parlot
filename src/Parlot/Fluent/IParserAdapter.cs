using Parlot.Rewriting;

namespace Parlot.Fluent;

/// <summary>
/// Adapts an IParser&lt;T&gt; to a Parser&lt;T&gt; for use in contexts that require Parser.
/// This is used internally to support covariance.
/// </summary>
internal sealed class IParserAdapter<T> : Parser<T>, ISeekable
{
    private readonly IParser<T> _parser;

    public IParserAdapter(IParser<T> parser)
    {
        _parser = parser ?? throw new System.ArgumentNullException(nameof(parser));

        // Forward ISeekable properties from the wrapped parser if it implements ISeekable
        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        var success = _parser.Parse(context, out int start, out int end, out object? value);
        if (success)
        {
            result.Set(start, end, (T)value!);
        }
        return success;
    }

    public override string ToString() => _parser.ToString() ?? "IParserAdapter";
}
