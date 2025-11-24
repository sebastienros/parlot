using Parlot.Compilation;
using Parlot.Rewriting;
using System;
#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Ensure the given parser is valid based on a condition, and backtracks if not.
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class When<T> : Parser<T>, ICompilable, ISeekable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var parserCompileResult = _parser.Build(context, requireResult: true);

        // success = false;
        // value = default;
        // start = context.Scanner.Cursor.Position;
        // parser instructions
        // 
        // if (parser.Success && _action(value))
        // {
        //   success = true;
        //   value = parser.Value;
        // }
        // else
        // {
        //    context.Scanner.Cursor.ResetPosition(start);
        // }
        //

        var start = context.DeclarePositionVariable(result);

        var block = Expression.Block(
                parserCompileResult.Variables,
                parserCompileResult.Body
                .Append(
                    Expression.IfThenElse(
                        Expression.AndAlso(
                            parserCompileResult.Success,
                            Expression.Invoke(Expression.Constant(_action), [context.ParseContext, parserCompileResult.Value])
                            ),
                        Expression.Block(
                            Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                            context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(result.Value, parserCompileResult.Value)
                            ),
                        context.ResetPosition(start)
                        )
                    )
                );


        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser} (When)";
}
