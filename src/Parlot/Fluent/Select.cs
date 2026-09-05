using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Selects a parser instance at runtime and delegates parsing to it.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class Select<C, T> : Parser<T>, ISourceable where C : ParseContext
{
    private readonly Parser<T>[] _parsers;
    private readonly Func<C, int>? _contextSelector;
    private readonly Func<int>? _selector;

    /// <summary>
    /// Creates a parser that executes the branch selected using the current context.
    /// </summary>
    /// <param name="selector">The selector to evaluate once before parsing.</param>
    /// <param name="parsers">The fixed set of parsers to select from.</param>
    public Select(Func<C, int> selector, params Parser<T>[] parsers)
        : this(parsers)
    {
        _contextSelector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// Creates a parser that executes the branch selected by a context-free callback.
    /// </summary>
    /// <param name="selector">The selector to evaluate once before parsing.</param>
    /// <param name="parsers">The fixed set of parsers to select from.</param>
    public Select(Func<int> selector, params Parser<T>[] parsers)
        : this(parsers)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    private Select(Parser<T>[] parsers)
    {
        ThrowHelper.ThrowIfNull(parsers, nameof(parsers));

        _parsers = parsers;

        for (var i = 0; i < _parsers.Length; i++)
        {
            _parsers[i] = _parsers[i] ?? throw new ArgumentException("Parsers array must not contain null elements.", nameof(parsers));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var index = _selector is not null ? _selector() : _contextSelector!((C)context);

        if ((uint)index >= (uint)_parsers.Length)
        {
            context.ExitParser(this);
            return false;
        }

        var nextParser = _parsers[index];

        var parsed = new ParseResult<T>();

        if (nextParser.Parse(context, ref parsed))
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

        for (var i = 0; i < _parsers.Length; i++)
        {
            if (_parsers[i] is not ISourceable)
            {
                throw new NotSupportedException("Select requires all target parsers to be source-generatable.");
            }
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var selectorCall = _selector is not null
            ? $"{context.RegisterLambda(_selector)}()"
            : $"{context.RegisterLambda(_contextSelector!)}(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx})";

        var indexName = $"index{context.NextNumber()}";
        result.Body.Add($"var {indexName} = {selectorCall};");
        result.Body.Add($"switch ({indexName})");
        result.Body.Add("{");

        for (var i = 0; i < _parsers.Length; i++)
        {
            var parser = _parsers[i];
            var parserSourceable = (ISourceable)parser;

            var helperName = context.Helpers
                .GetOrCreate(parser, $"{context.MethodNamePrefix}_Select_{i}", valueTypeName, () => parserSourceable.GenerateSource(context))
                .MethodName;

            result.Body.Add($"    case {i}:");
            result.Body.Add("    {");
            var outTarget = context.DiscardResult ? "_" : result.ValueVariable;
            result.Body.Add($"        if ({helperName}({ctx}, out {outTarget}))");
            result.Body.Add("        {");
            result.Body.Add($"            {result.SuccessVariable} = true;");
            result.Body.Add("        }");
            result.Body.Add("        break;");
            result.Body.Add("    }");
        }

        result.Body.Add("}");

        return result;
    }

    public override string ToString() => "(Select)";
}
