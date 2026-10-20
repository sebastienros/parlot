using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class ZeroOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable
{
    private static readonly MethodInfo _listAdd = typeof(List<T>).GetMethod("Add")!;

    private readonly Parser<T> _parser;

    public ZeroOrMany(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        HybridList<T>? results = null;

        // An empty result is at the current position
        var start = context.Scanner.Cursor.Offset;
        var end = start;

        var first = true;
        var parsed = new ParseResult<T>();

        while (true)
        {
            var previousOffset = context.Scanner.Cursor.Offset;
            if (!_parser.Parse(context, ref parsed)
                || context.Scanner.Cursor.Offset == previousOffset)
            {
                break;
            }

            if (first)
            {
                results = [];
                first = false;
                start = parsed.Start;
            }

            end = parsed.End;

            results!.Add(parsed.Value);
        }

        result.Set(start, end, results?.AsReadOnlyList() ?? (IReadOnlyList<T>)[]);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>(true, ExpressionHelper.ArrayEmpty<T>());

        var results = result.DeclareVariable<List<T>>($"results{context.NextNumber}");
        var first = result.DeclareVariable<bool>($"first{context.NextNumber}", Expression.Constant(true));
        var previousOffset = result.DeclareVariable<int>($"previousOffset{context.NextNumber}");

        // success = true;
        //
        // IReadonlyList<T> value = Array.Empty<T>();
        // List<T> results = null;
        //
        // while (true)
        // {
        //   var previousOffset = context.Scanner.Cursor.Offset;
        //   parse1 instructions
        // 
        //   if (parser1.Success && context.Scanner.Cursor.Offset != previousOffset)
        //   {
        //      if (results == null)
        //      {
        //          results = new List<T>();
        //          value = results;
        //      }
        //
        //      results.Add(parse1.Value);
        //   }
        //   else
        //   {
        //      break;
        //   }
        //
        //   if (context.Scanner.Cursor.Eof)
        //   {
        //      break;
        //   }
        // }

        var parserCompileResult = _parser.Build(context);

        var breakLabel = Expression.Label($"break{context.NextNumber}");

        var block =
            Expression.Loop(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Assign(previousOffset, context.Offset()),
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        Expression.AndAlso(parserCompileResult.Success, Expression.NotEqual(context.Offset(), previousOffset)),
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Block(
                            Expression.IfThen(
                                Expression.IsTrue(first),
                                Expression.Block(
                                    Expression.Assign(first, Expression.Constant(false)),
                                    Expression.Assign(results, ExpressionHelper.New<List<T>>()),
                                    Expression.Assign(result.Value, results)
                                    )
                                ),
                            Expression.Call(results, _listAdd, parserCompileResult.Value)
                            ),
                        Expression.Break(breakLabel)
                        ),
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                        )),
                breakLabel
                );

        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser}*";
}
