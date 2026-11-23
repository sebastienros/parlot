using System;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

/// <summary>
/// Shared non-span source generator for <see cref="ISequenceSourceable"/> implementations
/// (<c>Sequence</c>, <c>SequenceAndSkip</c>, <c>SequenceSkipAnd</c>).
/// </summary>
internal static class SequenceSource
{
    public static SourceResult Generate(
        ISequenceSourceable sequence,
        Type resultType,
        string kind,
        SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var parts = sequence.Parts;
        var part0 = parts[0];
        var part1 = parts[1];

        if (part0.Parser is not ISourceable sourceable0 || part1.Parser is not ISourceable sourceable1)
        {
            var unsupported = part0.Parser is not ISourceable ? part0.Parser : part1.Parser;
            throw new NotSupportedException($"{kind} requires source-generatable parsers, but {SourceGenerationContext.DescribeParser(unsupported)} does not implement ISourceable.");
        }

        if (sequence is ISpanSourceable spanSourceable && SpanSource.TryGenerate(spanSourceable, resultType, context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(resultType);
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        var discard0 = context.DiscardResult || !part0.Keep;
        var discard1 = context.DiscardResult || !part1.Keep;

        var helper0 = context.WithDiscardResult(discard0, () => context.Helpers
            .GetOrCreate(sourceable0, $"{context.MethodNamePrefix}_{kind}_P1", SourceGenerationContext.GetTypeName(part0.ValueType), () => sourceable0.GenerateSource(context))
            .MethodName);
        var helper1 = context.WithDiscardResult(discard1, () => context.Helpers
            .GetOrCreate(sourceable1, $"{context.MethodNamePrefix}_{kind}_P2", SourceGenerationContext.GetTypeName(part1.ValueType), () => sourceable1.GenerateSource(context))
            .MethodName);

        var direct0 = !discard0 && discard1 && part0.ValueType == resultType;
        var direct1 = discard0 && !discard1 && part1.ValueType == resultType;

        string? value0 = null;
        string out0;
        if (discard0)
        {
            out0 = "_";
        }
        else if (direct0)
        {
            out0 = result.ValueVariable;
            value0 = result.ValueVariable;
        }
        else
        {
            value0 = $"h1Value{context.NextNumber()}";
            out0 = $"var {value0}";
        }

        string? value1 = null;
        string out1;
        if (discard1)
        {
            out1 = "_";
        }
        else if (direct1)
        {
            out1 = result.ValueVariable;
            value1 = result.ValueVariable;
        }
        else
        {
            value1 = $"h2Value{context.NextNumber()}";
            out1 = $"var {value1}";
        }

        result.Body.Add($"if ({helper0}({context.ParseContextName}, out {out0}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helper1}({context.ParseContextName}, out {out1}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult && !direct0 && !direct1)
        {
            result.Body.Add($"        {result.ValueVariable} = {sequence.CreateValue([value0, value1])};");
        }
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }
}
