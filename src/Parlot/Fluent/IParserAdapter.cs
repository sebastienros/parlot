using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Adapts an IParser&lt;T&gt; to a Parser&lt;T&gt; for use in contexts that require Parser.
/// This is used internally to support covariance.
/// </summary>
internal sealed class IParserAdapter<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly IParser<T> _parser;

    // The wrapped parser when it is a Parser<T>, to call its typed Parse without boxing the value
    private readonly Parser<T>? _typedParser;

    public IParserAdapter(IParser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _typedParser = parser as Parser<T>;

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
        if (_typedParser != null)
        {
            return _typedParser.Parse(context, ref result);
        }

        // The object path is kept for the covariant implementations, e.g. a Parser<Dog> used as an IParser<Animal>
        var success = _parser.Parse(context, out int start, out int end, out object? value);
        if (success)
        {
            result.Set(start, end, (T)value!);
        }
        return success;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // If the wrapped parser is actually a Parser<T> that implements ISourceable, delegate to it
        if (_parser is Parser<T> { } parser && parser is ISourceable sourceable)
        {
            return sourceable.GenerateSource(context);
        }

        // Otherwise, fall back to using the Parse method
        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        var parseResultName = $"parseResult{context.NextNumber()}";

        // Create a lambda that captures this instance
        var adapterLambda = context.RegisterLambda(new Func<IParserAdapter<T>>(() => this));

        result.Body.Add($"var {parseResultName} = new global::Parlot.ParseResult<{SourceGenerationContext.GetTypeName(typeof(T))}>();");
        result.Body.Add($"{result.SuccessVariable} = {adapterLambda}().Parse({ctx}, ref {parseResultName});");
        result.Body.Add($"if ({result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {parseResultName}.Value;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => _parser.ToString() ?? "IParserAdapter";
}
