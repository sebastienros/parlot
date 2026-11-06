using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Matches an operator parser and returns its index in a list of operators with its value, so a <see cref="OneOf{T}"/>
/// can select the operator and the factory to apply with its value.
/// </summary>
/// <typeparam name="T">The type of the operator parser.</typeparam>
internal sealed class IndexedOperator<T> : Parser<(int Index, T Value)>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly int _index;

    public IndexedOperator(Parser<T> parser, int index)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _index = index;
    }

    /// <summary>
    /// Creates the choice of a level of operators whose factories receive their values, which returns the index of the
    /// operator that matched and its value, with the parser and the factory of each operator.
    /// </summary>
    internal static Parser<(int Index, T Value)> CreateChoice<TFactory>((Parser<T> Op, TFactory Factory)[] operators, string parameterName, out TFactory[] factories, out IndexedOperator<T>[] parsers)
        where TFactory : Delegate
    {
        if (operators is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", parameterName);
        }

        parsers = new IndexedOperator<T>[operators.Length];
        factories = new TFactory[operators.Length];

        for (var i = 0; i < operators.Length; i++)
        {
            var (op, factory) = operators[i];
            parsers[i] = new IndexedOperator<T>(op ?? throw new ArgumentException("Operators must not contain null parsers.", parameterName), i);
            factories[i] = factory ?? throw new ArgumentException("Operators must not contain null factories.", parameterName);
        }

        if (parsers.Length == 1)
        {
            // A single operator is invoked directly
            return parsers[0];
        }

        // The choice gets an array of its own type, which it can write
        var choices = new Parser<(int, T)>[parsers.Length];
        Array.Copy(parsers, choices, parsers.Length);

        return new OneOf<(int, T)>(choices);
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<(int Index, T Value)> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, (_index, parsed.Value));

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
            throw new NotSupportedException("Operators require all operator parsers to be source-generatable.");
        }

        var result = context.CreateResult(typeof((int, T)));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // The operator's value is passed to its factory
        var helperName = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Operator", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        var valueName = $"operatorValue{context.NextNumber()}";

        result.Body.Add($"{result.SuccessVariable} = {helperName}({context.ParseContextName}, out var {valueName});");

        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({result.SuccessVariable})");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = ({_index}, {valueName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => _parser.ToString() ?? "";
}
