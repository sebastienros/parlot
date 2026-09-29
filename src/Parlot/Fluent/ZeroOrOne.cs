using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

/// <summary>
/// Returns a list containing zero or one result from the inner parser.
/// </summary>
public sealed class ZeroOrOne<T> : Parser<IReadOnlyList<T>>, ISourceable
{
    private readonly Parser<T> _parser;

    public ZeroOrOne(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? [parsed.Value] : Array.Empty<T>());

        // ZeroOrOne always succeeds
        context.ExitParser(this);
        return true;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ZeroOrOne requires a source-generatable parser.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: true,
            defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_ZeroOrOne_Parser", elementTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        if (context.DiscardResult)
        {
            result.Body.Add($"{helperName}({context.ParseContextName}, out _);");
        }
        else
        {
            var itemValueName = $"itemValue{context.NextNumber()}";
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out var {itemValueName}))");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = new {elementTypeName}[] {{ {itemValueName} }};");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser}?";
}
