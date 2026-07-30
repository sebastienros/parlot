using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class OneOf<A, B, T> : Parser<T>, ISourceable
    where A : T
    where B : T
{
    private readonly Parser<A> _parserA;
    private readonly Parser<B> _parserB;

    public OneOf(Parser<A> parserA, Parser<B> parserB)
    {
        _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
        _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var resultA = new ParseResult<A>();

        if (_parserA.Parse(context, ref resultA))
        {
            result.Set(resultA.Start, resultA.End, resultA.Value);

            context.ExitParser(this);
            return true;
        }

        var resultB = new ParseResult<B>();

        if (_parserB.Parse(context, ref resultB))
        {
            result.Set(resultB.Start, resultB.End, resultB.Value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parserA is not ISourceable sourceableA || _parserB is not ISourceable sourceableB)
        {
            throw new NotSupportedException("OneOf requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(T));
        var valueTypeNameA = SourceGenerationContext.GetTypeName(typeof(A));
        var valueTypeNameB = SourceGenerationContext.GetTypeName(typeof(B));
        var valueTypeNameT = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helpers instead of inlining
        var helperNameA = context.Helpers
            .GetOrCreate(sourceableA, $"{context.MethodNamePrefix}_OneOf_A", valueTypeNameA, () => sourceableA.GenerateSource(context))
            .MethodName;

        var helperNameB = context.Helpers
            .GetOrCreate(sourceableB, $"{context.MethodNamePrefix}_OneOf_B", valueTypeNameB, () => sourceableB.GenerateSource(context))
            .MethodName;

        var valueAName = $"valueA{context.NextNumber()}";
        var valueBName = $"valueB{context.NextNumber()}";

        result.Body.Add($"if ({helperNameA}({context.ParseContextName}, out var {valueAName}))");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = ({valueTypeNameT}){valueAName};");
        }
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperNameB}({context.ParseContextName}, out var {valueBName}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = ({valueTypeNameT}){valueBName};");
        }
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parserA} | {_parserB}";
}
