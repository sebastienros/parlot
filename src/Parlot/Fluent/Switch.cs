using Parlot.SourceGeneration;
using System;
using System.Linq;

namespace Parlot.Fluent;


/// <summary>
/// Routes the parsing based on a custom delegate.
/// </summary>
public sealed class Switch<T, U> : Parser<U>, ISourceable
{
    private readonly Parser<T> _previousParser;
    private readonly Parser<U>[] _parsers;
    private readonly Func<ParseContext, T, int> _selector;

    public Switch(Parser<T> previousParser, Func<ParseContext, T, int> selector, params Parser<U>[] parsers)
    {
        _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));

        _selector = selector ?? throw new ArgumentNullException(nameof(selector));

        ThrowHelper.ThrowIfNull(parsers, nameof(parsers));

        _parsers = parsers;

        for (var i = 0; i < _parsers.Length; i++)
        {
            _parsers[i] = _parsers[i] ?? throw new ArgumentException("Parsers array must not contain null elements.", nameof(parsers));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var previousResult = new ParseResult<T>();

        if (!_previousParser.Parse(context, ref previousResult))
        {
            context.ExitParser(this);
            return false;
        }

        var index = _selector(context, previousResult.Value);

        if ((uint)index >= (uint)_parsers.Length)
        {
            context.ExitParser(this);
            return false;
        }

        var nextParser = _parsers[index];

        var parsed = new ParseResult<U>();

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

        if (_previousParser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Switch requires a source-generatable previous parser.");
        }

        for (var i = 0; i < _parsers.Length; i++)
        {
            if (_parsers[i] is not ISourceable)
            {
                throw new NotSupportedException("Switch requires all target parsers to be source-generatable.");
            }
        }

        var result = context.CreateResult(typeof(U));
        var ctx = context.ParseContextName;
        var previousValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(U));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Switch", previousValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // Register the selector lambda
        var selectorLambda = context.RegisterLambda(_selector);

        var previousValueName = $"previousValue{context.NextNumber()}";
        var indexName = $"index{context.NextNumber()}";

        result.Body.Add($"if ({helperName}({ctx}, out var {previousValueName}))");
        result.Body.Add("{");
        result.Body.Add($"    var {indexName} = {selectorLambda}({ctx}, {previousValueName});");
        result.Body.Add($"    switch ({indexName})");
        result.Body.Add("    {");

        for (var i = 0; i < _parsers.Length; i++)
        {
            var parser = _parsers[i];
            var parserSourceable = (ISourceable)parser;

            var targetHelperName = context.Helpers
                .GetOrCreate(parser, $"{context.MethodNamePrefix}_Switch_{i}", valueTypeName, () => parserSourceable.GenerateSource(context))
                .MethodName;

            result.Body.Add($"        case {i}:");
            result.Body.Add("        {");
            var outTarget = context.DiscardResult ? "_" : result.ValueVariable;
            result.Body.Add($"            if ({targetHelperName}({ctx}, out {outTarget}))");
            result.Body.Add("            {");
            result.Body.Add($"                {result.SuccessVariable} = true;");
            result.Body.Add("            }");
            result.Body.Add("            break;");
            result.Body.Add("        }");
        }

        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_previousParser} (Switch)";
}
