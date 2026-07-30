using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET
using System.Linq;
#endif

namespace Parlot.Fluent;

/// <summary>
/// Ensure the given parser is valid based on a condition, and backtracks if not.
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class When<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly Func<ParseContext, T, bool> _action;
    private readonly Parser<T> _parser;

    [Obsolete("Use When(Parser<T> parser, Func<ParseContext, T, bool> action) instead.")]
    public When(Parser<T> parser, Func<T, bool> action)
    {
        _action = action != null ? (c, t) => action(t) : throw new ArgumentNullException(nameof(action));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        InitializeSeekable();
    }

    public When(Parser<T> parser, Func<ParseContext, T, bool> action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        InitializeSeekable();
    }

    private void InitializeSeekable()
    {
        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; private set; }

    public char[] ExpectedChars { get; private set; } = [];

    public bool SkipWhitespace { get; private set; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        var valid = _parser.Parse(context, ref result) && _action(context, result.Value);

        if (!valid)
        {
            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return valid;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("When requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_When", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // Register the action lambda
        var lambdaId = context.RegisterLambda(_action);

        // if (Helper(context, out var innerValue) && _action(context, innerValue))
        // {
        //     success = true;
        //     value = innerValue;
        // }
        // else
        // {
        //     cursor.ResetPosition(start);
        //     success = false;
        // }
        
        var innerValueName = $"innerValue{context.NextNumber()}";
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out var {innerValueName}) && {lambdaId}({context.ParseContextName}, {innerValueName}))");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {innerValueName};");
        }
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (When)";
}
