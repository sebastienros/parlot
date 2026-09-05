using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Evaluates a condition once and executes only the selected parser.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class If<C, T> : Parser<T>, ISourceable where C : ParseContext
{
    private readonly Func<C, bool>? _contextCondition;
    private readonly Func<bool>? _condition;
    private readonly Parser<T> _thenParser;
    private readonly Parser<T>? _elseParser;

    /// <summary>
    /// Creates a parser that fails without consuming input when <paramref name="condition"/> is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate before parsing.</param>
    /// <param name="parser">The parser to execute when the condition is true.</param>
    public If(Func<C, bool> condition, Parser<T> parser)
    {
        _contextCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenParser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Creates a parser that fails without consuming input when <paramref name="condition"/> is false.
    /// </summary>
    /// <param name="condition">The condition to evaluate before parsing.</param>
    /// <param name="parser">The parser to execute when the condition is true.</param>
    public If(Func<bool> condition, Parser<T> parser)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenParser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Creates a parser that executes only the selected branch, without falling back if it fails.
    /// </summary>
    /// <param name="condition">The condition to evaluate before parsing.</param>
    /// <param name="thenParser">The parser to execute when the condition is true.</param>
    /// <param name="elseParser">The parser to execute when the condition is false.</param>
    public If(Func<C, bool> condition, Parser<T> thenParser, Parser<T> elseParser)
    {
        _contextCondition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenParser = thenParser ?? throw new ArgumentNullException(nameof(thenParser));
        _elseParser = elseParser ?? throw new ArgumentNullException(nameof(elseParser));
    }

    /// <summary>
    /// Creates a parser that executes only the selected branch, without falling back if it fails.
    /// </summary>
    /// <param name="condition">The condition to evaluate before parsing.</param>
    /// <param name="thenParser">The parser to execute when the condition is true.</param>
    /// <param name="elseParser">The parser to execute when the condition is false.</param>
    public If(Func<bool> condition, Parser<T> thenParser, Parser<T> elseParser)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenParser = thenParser ?? throw new ArgumentNullException(nameof(thenParser));
        _elseParser = elseParser ?? throw new ArgumentNullException(nameof(elseParser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var condition = _condition is not null ? _condition() : _contextCondition!((C)context);
        var parser = condition ? _thenParser : _elseParser;
        var parsed = new ParseResult<T>();

        if (parser is not null && parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);
            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_thenParser is not ISourceable thenSourceable || (_elseParser is not null && _elseParser is not ISourceable))
        {
            throw new NotSupportedException("If requires all branch parsers to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;

        var conditionCall = _condition is not null
            ? $"{context.RegisterLambda(_condition)}()"
            : $"{context.RegisterLambda(_contextCondition!)}(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx})";

        var thenHelper = context.Helpers
            .GetOrCreate(_thenParser, $"{context.MethodNamePrefix}_If_Then", valueTypeName, () => thenSourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add($"if ({conditionCall})");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {thenHelper}({ctx}, out {outTarget});");
        result.Body.Add("}");

        if (_elseParser is ISourceable elseSourceable)
        {
            var elseHelper = context.Helpers
                .GetOrCreate(_elseParser, $"{context.MethodNamePrefix}_If_Else", valueTypeName, () => elseSourceable.GenerateSource(context))
                .MethodName;

            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {result.SuccessVariable} = {elseHelper}({ctx}, out {outTarget});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => _elseParser is null ? $"{_thenParser} (If)" : $"{_thenParser} (If) {_elseParser} (Else)";
}
