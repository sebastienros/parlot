using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class OneOf<A, B, T> : Parser<T>, ICompilable, ISourceable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // T value;
        //
        // parse1 instructions
        // 
        // if (parser1.Success)
        // {
        //    success = true;
        //    value = (T) parse1.Value;
        // }
        // else
        // {
        //   parse2 instructions
        //   
        //   if (parser2.Success)
        //   {
        //      success = true;
        //      value = (T) parse2.Value
        //   }
        // }

        var parser1CompileResult = _parserA.Build(context);
        var parser2CompileResult = _parserB.Build(context);

        result.Body.Add(
            Expression.Block(
                parser1CompileResult.Variables,
                Expression.Block(parser1CompileResult.Body),
                Expression.IfThenElse(
                    parser1CompileResult.Success,
                    Expression.Block(
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, Expression.Convert(parser1CompileResult.Value, typeof(T)))
                        ),
                    Expression.Block(
                        parser2CompileResult.Variables,
                        Expression.Block(parser2CompileResult.Body),
                        Expression.IfThen(
                            parser2CompileResult.Success,
                            Expression.Block(
                                Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(result.Value, Expression.Convert(parser2CompileResult.Value, typeof(T)))
                                )
                            )
                        )
                    )
                )
            );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parserA is not ISourceable sourceableA || _parserB is not ISourceable sourceableB)
        {
            throw new NotSupportedException("OneOf requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(T));

        var innerA = sourceableA.GenerateSource(context);
        var innerB = sourceableB.GenerateSource(context);

        // Emit first parser locals and body
        foreach (var local in innerA.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in innerA.Body)
        {
            result.Body.Add(stmt);
        }

        result.Body.Add($"if ({innerA.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add($"    {result.ValueVariable} = ({SourceGenerationContext.GetTypeName(typeof(T))}){innerA.ValueVariable};");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        
        // Emit second parser locals and body
        foreach (var local in innerB.Locals)
        {
            result.Body.Add($"    {local}");
        }

        foreach (var stmt in innerB.Body)
        {
            result.Body.Add($"    {stmt}");
        }

        result.Body.Add($"    if ({innerB.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = ({SourceGenerationContext.GetTypeName(typeof(T))}){innerB.ValueVariable};");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parserA} | {_parserB}";
}
