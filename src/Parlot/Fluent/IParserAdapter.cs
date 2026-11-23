using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Parlot.Fluent;

/// <summary>
/// Adapts an IParser&lt;T&gt; to a Parser&lt;T&gt; for use in contexts that require Parser.
/// This is used internally to support covariance.
/// </summary>
internal sealed class IParserAdapter<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, IInvokesOnlyLiterals, IReadsWith, ILeadingWhiteSpace
{
    private readonly IParser<T> _parser;

    // The wrapped parser when it is a Parser<T>, to call its typed Parse without boxing the value
    private readonly Parser<T>? _typedParser;

    // The wrapped parser when it implements IWithoutValueParser (including covariant Parser<TDerived>)
    private readonly IWithoutValueParser? _withoutValueParser;

    // Whether the wrapped parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    public IParserAdapter(IParser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _typedParser = parser as Parser<T>;
        _withoutValueParser = parser as IWithoutValueParser;
        _withoutValue = _typedParser is not null
            ? _typedParser.CanParseWithoutValue
            : _withoutValueParser is not null && _withoutValueParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    // When the parser it wraps is a literal, see IInvokesOnlyLiterals
    bool IInvokesOnlyLiterals.InvokesOnlyLiterals => InvokesOnlyLiterals.Of(_parser);

    object IReadsWith.Parser => _parser;

    LeadingWhiteSpaceUse ILeadingWhiteSpace.LeadingWhiteSpace => LeadingWhiteSpace.Of(_parser);

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

    internal override bool CanParseWithoutValue => _withoutValue;

    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        if (_typedParser != null)
        {
            return _withoutValue ? _typedParser.ParseWithoutValue(context, ref result) : _typedParser.Parse(context, ref result);
        }

        if (_withoutValueParser != null)
        {
            if (_withoutValueParser.ParseWithoutValue(context, out var start, out var end))
            {
                result.Set(start, end, default!);
                return true;
            }

            return false;
        }

        return Parse(context, ref result);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException($"IParserAdapter requires a source-generatable parser, but {SourceGenerationContext.DescribeParser(_parser)} does not implement ISourceable.");
        }

        // If the wrapped parser is already a Parser<T>, delegate directly
        if (_typedParser != null)
        {
            return sourceable.GenerateSource(context);
        }

        if (!TryGetParserValueType(_parser.GetType(), out var innerType))
        {
            throw new NotSupportedException($"IParserAdapter cannot determine the value type of {SourceGenerationContext.DescribeParser(_parser)} for source generation.");
        }

        if (SpanSource.TryGenerate(this, typeof(T), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(T));
        var innerTypeName = SourceGenerationContext.GetTypeName(innerType);
        var targetTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Adapt", innerTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        if (context.DiscardResult)
        {
            result.Body.Add($"{result.SuccessVariable} = {helperName}({context.ParseContextName}, out _);");
        }
        else
        {
            var innerValueName = $"adapted{context.NextNumber()}";
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out var {innerValueName}))");
            result.Body.Add("{");
            result.Body.Add($"    {result.SuccessVariable} = true;");
            result.Body.Add($"    {result.ValueVariable} = ({targetTypeName}){innerValueName};");
            result.Body.Add("}");
        }

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context)
    {
        if (_parser is not ISpanSourceable spanSourceable)
        {
            return -1;
        }

        if (_typedParser == null && !TryGetParserValueType(_parser.GetType(), out _))
        {
            return -1;
        }

        return spanSourceable.GetSpanSourceWeight(context);
    }

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var spanSourceable = (ISpanSourceable)_parser;

        if (_typedParser != null || valueVariable == null)
        {
            writer.Generate(spanSourceable, valueVariable, fail);
            return;
        }

        if (!TryGetParserValueType(_parser.GetType(), out var innerType))
        {
            throw new NotSupportedException($"IParserAdapter cannot determine the value type of {SourceGenerationContext.DescribeParser(_parser)} for source generation.");
        }

        var innerVar = writer.DeclareLocal(innerType, "adapted");
        writer.Generate(spanSourceable, innerVar, fail);
        writer.Add($"{valueVariable} = ({SourceGenerationContext.GetTypeName(typeof(T))}){innerVar};");
    }

    private static bool TryGetParserValueType(Type parserType, [NotNullWhen(true)] out Type? valueType)
    {
        for (var current = parserType; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Parser<>))
            {
                valueType = current.GetGenericArguments()[0];
                return true;
            }
        }

        valueType = null;
        return false;
    }

    private protected override bool IsTextWrapper => true;

    public override string ToString() => _parser is IParserText
        ? FormatOnce($"{_parser}")
        : _parser.ToString() ?? "IParserAdapter";
}
