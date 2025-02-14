using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class ZeroOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable, ISeekable
{
    private static readonly MethodInfo _listAdd = typeof(List<T>).GetMethod("Add")!;

    private readonly Parser<T> _parser;

    public ZeroOrMany(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        List<T>? results = null;

        var start = 0;
        var end = 0;

        var first = true;
        var parsed = new ParseResult<T>();

        // TODO: it's not restoring an intermediate failed text position
        // is the inner parser supposed to be clean?

        while (_parser.Parse(context, ref parsed))
        {
            if (first)
            {
                first = false;
                start = parsed.Start;
            }

            end = parsed.End;

            results ??= [];
            results.Add(parsed.Value);
        }

        result.Set(start, end, results ?? (IReadOnlyList<T>)[]);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>(true, ExpressionHelper.ArrayEmpty<T>());

        var results = result.DeclareVariable<List<T>>($"results{context.NextNumber}");
        var first = result.DeclareVariable<bool>($"first{context.NextNumber}", Expression.Constant(true));

        // success = true;
        //
        // IReadonlyList<T> value = Array.Empty<T>();
        // List<T> results = null;
        //
        // while (true)
        // {
        //
        //   parse1 instructions
        // 
        //   if (parser1.Success)
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
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
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
