using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Returns an option containing a value when the inner parser matches.
/// </summary>
/// <remarks>
/// This parser will always succeed. If the inner parser fails, it will return an option without a value.
/// </remarks>
public sealed class Optional<T> : Parser<Option<T>>, ISourceable, ISpanSourceable
{
    private readonly Parser<T> _parser;
    public Optional(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<Option<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        // The text is read again when the parser fails
        var pin = context.Pin();
        var success = _parser.Parse(context, ref parsed);
        context.Unpin(pin);

        if (success)
        {
            result.Set(parsed.Start, parsed.End, new Option<T>(parsed.Value));
        }
        else
        {
            // Nothing was read: the result is empty at the current position
            var offset = context.Scanner.Cursor.Offset;
            result.Set(offset, offset, new Option<T>());
        }

        // Optional always succeeds
        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Optional requires a source-generatable parser.");
        }

        if (SpanSource.TryGenerate(this, typeof(Option<T>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(Option<T>), defaultSuccess: true);
        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Optional", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out var innerValue))
        // {
        //     value = new Option<T>(innerValue);
        // }
        // else
        // {
        //     value = new Option<T>();
        // }
        // success = true; (always succeeds)
        var innerValueName = $"innerValue{context.NextNumber()}";
        var success = context.PinnedCall(result, $"{helperName}({context.ParseContextName}, out var {innerValueName})");
        result.Body.Add($"if ({success})");
        result.Body.Add("{");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = new global::Parlot.Option<{elementTypeName}>({innerValueName});");
        }
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = new global::Parlot.Option<{elementTypeName}>();");
        }
        result.Body.Add("}");
        result.Body.Add($"{result.SuccessVariable} = true;");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var optionTypeName = SourceGenerationContext.GetTypeName(typeof(Option<T>));
        var item = valueVariable != null ? writer.DeclareLocal(typeof(T), "item") : null;

        SpanSource.GenerateOptional(writer, _parser, item,
            valueVariable != null ? $"{valueVariable} = new {optionTypeName}({item});" : null,
            valueVariable != null ? $"{valueVariable} = new {optionTypeName}();" : null);
    }

    public override string ToString() => $"{_parser}?";
}
