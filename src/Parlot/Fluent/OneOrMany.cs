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

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>();
        var results = result.DeclareVariable<List<T>>($"results{context.NextNumber}", Expression.New(typeof(List<T>)));

        // value = new List<T>();
        //
        // while (true)
        // {
        //   parse1 instructions
        // 
        //   if (parser1.Success)
        //   {
        //      value.Add(parse1.Value);
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
        //     result = value;
        // }
        // 

        var parserCompileResult = _parser.Build(context);

        var breakLabel = Expression.Label($"exitWhile{context.NextNumber}");

        var block = Expression.Block(
            parserCompileResult.Variables,
            Expression.Loop(
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ?
                                Expression.Empty() :
                                Expression.Call(results, _listAddMethodInfo, parserCompileResult.Value),
                            Expression.Assign(result.Success, Expression.Constant(true))
                            ),
                        Expression.Break(breakLabel)
                        ),
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                        )),
                breakLabel),
            context.DiscardResult ?
                Expression.Empty() :
                Expression.Assign(result.Value, results)
        );

        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser}+";
}
