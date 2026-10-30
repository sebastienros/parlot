using Parlot.Rewriting;
using System;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

/// <summary>
/// Returns a new <see cref="Parser{U}" /> converting the input value of
/// type T to the output value of type U using a custom function.
/// </summary>
/// <typeparam name="T">The input parser type.</typeparam>
/// <typeparam name="U">The output parser type.</typeparam>
public sealed class Then<T, U> : Parser<U>, ISeekable, ISourceable
{
    private readonly Func<T, U>? _action1;
    private readonly Func<ParseContext, T, U>? _action2;
    private readonly Func<ParseContext, int, int, T, U>? _action3;
    private readonly U? _value;
    private readonly Parser<T> _parser;

    private Then(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public Then(Parser<T> parser, Func<T, U> action) : this(parser)
    {
        _action1 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, Func<ParseContext, T, U> action) : this(parser)
    {
        _action2 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, Func<ParseContext, int, int, T, U> action) : this(parser)
    {
        _action3 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, U value) : this(parser)
    {
        _value = value;
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            if (_action1 != null)
            {
                result.Set(parsed.Start, parsed.End, _action1.Invoke(parsed.Value));
            }
            else if (_action2 != null)
            {
                result.Set(parsed.Start, parsed.End, _action2.Invoke(context, parsed.Value));
            }
            else if (_action3 != null)
            {
                result.Set(parsed.Start, parsed.End, _action3.Invoke(context, parsed.Start, parsed.End, parsed.Value));
            }
            else
            {
                // _value can't be null if action1, action2, and action3 are null
                result.Set(parsed.Start, parsed.End, _value!);
            }

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
            throw new NotSupportedException("Then requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(U));
        var ctx = context.ParseContextName;
        var parsedName = $"parsed{context.NextNumber()}";
        var parsedTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(U));

        // Register helper for the inner parser
        static Type GetParserValueType(object parser)
        {
            var type = parser.GetType();
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Parlot.Fluent.Parser`1")
                {
                    return type.GetGenericArguments()[0];
                }
                type = type.BaseType!;
            }
            throw new InvalidOperationException("Unable to determine parser value type.");
        }

        var innerValueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(sourceable));
        var helperKey = $"{context.MethodNamePrefix}_Then_{context.NextNumber()}";
        // Callbacks consume the inner value even when their output is discarded; constants do not.
        var discardInput = _action1 == null && _action2 == null && _action3 == null;
        var helperName = context.WithDiscardResult(discardInput, () => context.Helpers
            .GetOrCreate(sourceable, helperKey, innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        var outTarget = discardInput ? "_" : $"var {parsedName}Value";
        var cursorName = context.CursorName;
        string? startName = null;
        string? resetName = null;

        if (_action3 != null)
        {
            // The callback receives the offsets of the inner match. The runtime reports the start of the
            // content, after any white space a Terms parser skipped, so a SkipWhiteSpace wrapper is
            // unfolded here: the white space is skipped first and the start is read after it.
            startName = $"start{context.NextNumber()}";

            if (_parser is SkipWhiteSpace<T> { Parser: ISourceable contentSourceable })
            {
                helperName = context.Helpers
                    .GetOrCreate(contentSourceable, helperKey, innerValueTypeName, () => contentSourceable.GenerateSource(context))
                    .MethodName;
                resetName = $"reset{context.NextNumber()}";
                result.Body.Add($"var {resetName} = {cursorName}.Position;");
                result.Body.Add($"if ({ctx}.MaySkipWhiteSpace({cursorName}.Current))");
                result.Body.Add("{");
                result.Body.Add($"    {ctx}.SkipWhiteSpace();");
                result.Body.Add("}");
            }

            result.Body.Add($"var {startName} = {cursorName}.Offset;");
        }

        result.Body.Add($"if ({helperName}({ctx}, out {outTarget}))");
        result.Body.Add("{");

        if (_action1 != null)
        {
            var lambdaName = context.RegisterLambda(_action1);
            if (context.DiscardResult)
            {
                result.Body.Add($"    {lambdaName}({parsedName}Value);");
            }
            else
            {
                result.Body.Add($"    {result.ValueVariable} = {lambdaName}({parsedName}Value);");
            }
        }
        else if (_action2 != null)
        {
            var lambdaName = context.RegisterLambda(_action2);
            if (context.DiscardResult)
            {
                result.Body.Add($"    {lambdaName}({ctx}, {parsedName}Value);");
            }
            else
            {
                result.Body.Add($"    {result.ValueVariable} = {lambdaName}({ctx}, {parsedName}Value);");
            }
        }
        else if (_action3 != null)
        {
            var lambdaName = context.RegisterLambda(_action3);
            if (context.DiscardResult)
            {
                result.Body.Add($"    {lambdaName}({ctx}, {startName}, {cursorName}.Offset, {parsedName}Value);");
            }
            else
            {
                result.Body.Add($"    {result.ValueVariable} = {lambdaName}({ctx}, {startName}, {cursorName}.Offset, {parsedName}Value);");
            }
        }
        else
        {
            // Value-based: try to use LiteralHelper for supported types
            var valueExpr = LiteralHelper.ToLiteral(_value);
            if (valueExpr == null)
            {
                throw new NotSupportedException(
                    $"Then<{typeof(T).Name}, {typeof(U).Name}> with a value of type '{typeof(U).Name}' cannot be source-generated. " +
                    $"Use a lambda instead, e.g., .Then(static _ => yourValue)");
            }

            if (!context.DiscardResult)
            {
                result.Body.Add($"    {result.ValueVariable} = {valueExpr};");
            }
        }

        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        if (resetName != null)
        {
            // The white space skipped above is given back when the content doesn't match
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({resetName});");
            result.Body.Add("}");
        }

        return result;
    }

override public string ToString() => $"{_parser} (Then)";
}
