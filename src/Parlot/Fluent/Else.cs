using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Returns a default value if the previous parser failed.
/// </summary>
public sealed class Else<T> : Parser<T>, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;
    private readonly T? _value;
    private readonly Func<ParseContext, T>? _func;

    // Whether the parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    public Else(Parser<T> parser, T value)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _withoutValue = parser.CanParseWithoutValue;
        _value = value;
    }

    public Else(Parser<T> parser, Func<ParseContext, T> func)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _func = func ?? throw new ArgumentNullException(nameof(func));
        _withoutValue = parser.CanParseWithoutValue;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        return true;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // The text is read again when the parser fails
        var pin = context.Pin();
        var success = _parser.Parse(context, ref result);
        context.Unpin(pin);

        if (!success)
        {
            // The parser reset the cursor, and may have left the positions of a partial match in the result
            var offset = context.Scanner.Cursor.Offset;

            if (_func != null)
            {
                result.Set(offset, offset, _func(context));
            }
            else
            {
                // _value can't be null if _func is null
                result.Set(offset, offset, _value!);
            }
        }

        context.ExitParser(this);
        return true;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    // Parse, with the parser invoked without its value. The function is still invoked, as in generated code.
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // The text is read again when the parser fails
        var pin = context.Pin();
        var success = _withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result);
        context.Unpin(pin);

        if (!success)
        {
            // The parser reset the cursor, and may have left the positions of a partial match in the result
            var offset = context.Scanner.Cursor.Offset;

            if (_func != null)
            {
                result.Set(offset, offset, _func(context));
            }
            else
            {
                // _value can't be null if _func is null
                result.Set(offset, offset, _value!);
            }
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Else requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T), defaultSuccess: true);
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Else", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out value))
        // {
        //     // value already assigned
        // }
        // else
        // {
        //     value = _func != null ? _func(context) : _value;
        // }
        // success = true; (always succeeds)

        var success = context.PinnedCall(result, $"{helperName}({context.ParseContextName}, out {result.ValueVariable})");
        result.Body.Add($"if (!{success})");
        result.Body.Add("{");

        if (_func != null)
        {
            var lambdaId = context.RegisterLambda(_func);
            if (context.DiscardResult)
            {
                result.Body.Add($"    {lambdaId}({context.ParseContextName});");
            }
            else
            {
                result.Body.Add($"    {result.ValueVariable} = {lambdaId}({context.ParseContextName});");
            }
        }
        else
        {
            // Value-based: try to use LiteralHelper for supported types
            var valueExpr = LiteralHelper.ToLiteral(_value);
            if (valueExpr == null)
            {
                throw new NotSupportedException(
                    $"Else<{typeof(T).Name}> with a value of type '{typeof(T).Name}' cannot be source-generated. " +
                    $"Use a lambda instead, e.g., .Else(static _ => yourValue)");
            }

            if (!context.DiscardResult)
            {
                result.Body.Add($"    {result.ValueVariable} = {valueExpr};");
            }
        }

        result.Body.Add("}");
        result.Body.Add($"{result.SuccessVariable} = true;");

        return result;
    }

    public override string ToString() => $"{_parser} (Else)";
}
