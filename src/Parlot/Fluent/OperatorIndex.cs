using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Matches an operator parser and returns its index in the list of operators, so a <see cref="OneOf{T}"/>
/// can select the operator and the factory to apply with it.
/// </summary>
/// <typeparam name="T">The type of the operator parser.</typeparam>
internal sealed class OperatorIndex<T> : Parser<int>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, IInvokesOnlyLiterals
{
    private readonly Parser<T> _parser;
    private readonly int _index;
    private readonly bool _withoutValue;

    public OperatorIndex(Parser<T> parser, int index)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _index = index;
        _withoutValue = parser.CanParseWithoutValue;
    }

    /// <summary>
    /// Wraps <paramref name="parser"/> with its index, hoisting a top-level <see cref="SkipWhiteSpace{T}"/> outside
    /// so <see cref="OneOf{T}"/> can strip it when hoisting whitespace for a choice of operators.
    /// </summary>
    internal static Parser<int> Wrap(Parser<T> parser, int index)
    {
        if (parser is SkipWhiteSpace<T> skip)
        {
            return new SkipWhiteSpace<int>(new OperatorIndex<T>(skip.Parser, index)) { Name = skip.Name };
        }

        return new OperatorIndex<T>(parser, index);
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

    public override bool Parse(ParseContext context, ref ParseResult<int> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, _index);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException($"OperatorIndex requires a source-generatable operator parser, but {SourceGenerationContext.DescribeParser(_parser)} does not implement ISourceable.");
        }

        var result = context.CreateResult(typeof(int));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // The operator's value is never used, only which one matched
        var helperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Operator", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        result.Body.Add($"{result.SuccessVariable} = {helperName}({context.ParseContextName}, out _);");

        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({result.SuccessVariable})");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = {_index};");
            result.Body.Add("}");
        }

        return result;
    }

    private protected override bool IsTextWrapper => true;

    public override string ToString() => FormatOnce($"{_parser}");
}
