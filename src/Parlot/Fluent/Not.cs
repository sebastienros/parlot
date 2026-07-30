using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Not<T> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;

    public Not(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return true;
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Not requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Not", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     cursor.ResetPosition(start);
        //     success = false;
        // }
        // else
        // {
        //     success = true;
        // }
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"Not ({_parser})";
}
