using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Successful when the cursor is at the end of the string.
/// </summary>
public sealed class Eof<T> : Parser<T>, ICompilable, ISourceable
{
    private readonly Parser<T> _parser;

    public Eof(Parser<T> parser)
    {
        _parser = parser;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (_parser.Parse(context, ref result) && context.Scanner.Cursor.Eof)
        {
            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // parse1 instructions
        // 
        // if (parser1.Success && context.Scanner.Cursor.Eof)
        // {
        //    value = parse1.Value;
        //    success = true;
        // }

        var parserCompileResult = _parser.Build(context);

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.IfThen(
                    Expression.AndAlso(parserCompileResult.Success, context.Eof()),
                    Expression.Block(
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(result.Value, parserCompileResult.Value),
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                        )
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
            throw new NotSupportedException("Eof requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Eof", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out var innerValue) && cursor.Eof)
        // {
        //     success = true;
        //     value = innerValue;
        // }
        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out _) && {context.CursorName}.Eof)");
        }
        else
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out {result.ValueVariable}) && {context.CursorName}.Eof)");
        }
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Eof)";
}
