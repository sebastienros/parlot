using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class SkipWhiteSpace<T> : Parser<T>, ICompilable, ISeekable
{
    public Parser<T> Parser { get; }

    public SkipWhiteSpace(Parser<T> parser)
    {
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));

        Name = $"{Name} (SkipWhiteSpace)";

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; } = true;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        // If we know there is no custom whitespace parser we can skip the skipper by checking if the 
        // current char is not part of the common alphanumeric chars
        if (context.WhiteSpaceParser is null && Character.IsInRange(cursor.Current, (char)33, (char)126))
        {
            context.ExitParser(this);
            return Parser.Parse(context, ref result);
        }

        var start = cursor.Position;

        // Use the scanner's logic to ignore whitespaces since it knows about multi-line grammars
        context.SkipWhiteSpace();

        if (Parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return true;
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var start = context.DeclarePositionVariable(result);

        var parserCompileResult = Parser.Build(context);

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.IfThen(
                    test: Expression.IsFalse(Expression.And(
                        Expression.Equal(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_WhiteSpaceParser), Expression.Default(typeof(Parser<TextSpan>))),
                        Expression.Invoke(ExpressionHelper.CharacterIsInRange, [ExpressionHelper.Cursor(context), Expression.Constant((char)33), Expression.Constant((char)126)]))),
                    ifTrue: context.ParserSkipWhiteSpace()
                    ),
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, parserCompileResult.Value),
                            Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                            ),
                        context.ResetPosition(start)
                        )
                    )
                )
            );

        return result;
    }
}
