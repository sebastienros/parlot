using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class OneOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable, ISeekable
{
    private readonly Parser<T> _parser;
    private static readonly MethodInfo _listAddMethodInfo = typeof(List<T>).GetMethod("Add")!;

    public OneOrMany(Parser<T> parser)
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

        var parsed = new ParseResult<T>();

        if (!_parser.Parse(context, ref parsed))
        {
            return false;
        }

        var start = parsed.Start;
        var results = new List<T>();

        int end;

        do
        {
            end = parsed.End;
            results.Add(parsed.Value);

        } while (_parser.Parse(context, ref parsed));

        result.Set(start, end, results);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<List<T>>(false, Expression.New(typeof(List<T>)));

        // value = new List<T>();
        //
        // while (true)
        // {
        //   parse1 instructions
        // 
        //   if (parser1.Success)
        //   {
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
        //
        // if (value.Count > 0)
        // {
        //     success = true;
        // }
        // 

        var parserCompileResult = _parser.Build(context);

        var breakLabel = Expression.Label("break");

        var block = Expression.Block(
            parserCompileResult.Variables,
            Expression.Loop(
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Call(result.Value, _listAddMethodInfo, parserCompileResult.Value),
                            Expression.Assign(result.Success, Expression.Constant(true))
                            ),
                        Expression.Break(breakLabel)
                        ),
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                        )),
                breakLabel)
        );

        result.Body.Add(block);

        return result;
    }
}
