using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class Not<T> : Parser<T>, ICompilable, ISourceable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // var start = context.Scanner.Cursor.Position;

        var start = context.DeclarePositionVariable(result);

        var parserCompileResult = _parser.Build(context);

        // success = false;
        //
        // parser instructions
        // 
        // if (parser.succcess)
        // {
        //     context.Scanner.Cursor.ResetPosition(start);
        // }
        // else
        // {
        //     success = true;
        // }
        // 

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.IfThenElse(
                    parserCompileResult.Success,
                    context.ResetPosition(start),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                    )
                )
            );

        return result;
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
        
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // if (inner.success)
        // {
        //     cursor.ResetPosition(start);
        //     success = false;
        // }
        // else
        // {
        //     success = true;
        // }
        result.Body.Add($"if ({inner.SuccessVariable})");
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
