using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that temporarily sets a custom whitespace parser for its inner parser.
/// </summary>
public sealed class WithWhiteSpaceParser<T> : Parser<T>, ICompilable, ISeekable
{
    private readonly Parser<T> _parser;
    private readonly Parser<TextSpan> _whiteSpaceParser;

    public WithWhiteSpaceParser(Parser<T> parser, Parser<TextSpan> whiteSpaceParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _whiteSpaceParser = whiteSpaceParser ?? throw new ArgumentNullException(nameof(whiteSpaceParser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // Save the current whitespace parser
        var previousWhiteSpaceParser = context.WhiteSpaceParser;

        try
        {
            // Set the custom whitespace parser
            context.WhiteSpaceParser = _whiteSpaceParser;

            // Parse with the custom whitespace parser
            var success = _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }
        finally
        {
            // Restore the previous whitespace parser
            context.WhiteSpaceParser = previousWhiteSpaceParser;
        }
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // We need to save and restore the WhiteSpaceParser property
        var whiteSpaceParserProperty = Expression.Property(
            context.ParseContext,
            typeof(ParseContext).GetProperty(nameof(ParseContext.WhiteSpaceParser))!
        );

        var previousWhiteSpaceParser = Expression.Variable(typeof(Parser<TextSpan>), "previousWhiteSpaceParser");
        var whiteSpaceParserConstant = Expression.Constant(_whiteSpaceParser, typeof(Parser<TextSpan>));

        result.Variables.Add(previousWhiteSpaceParser);

        var parserCompileResult = _parser.Build(context);

        var blockExpressions = new System.Collections.Generic.List<Expression>
        {
            // Save current whitespace parser
            Expression.Assign(previousWhiteSpaceParser, whiteSpaceParserProperty),
            // Set custom whitespace parser
            Expression.Assign(whiteSpaceParserProperty, whiteSpaceParserConstant),
            // Try to parse
            Expression.TryFinally(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(
                        parserCompileResult.Body
                    ),
                    Expression.IfThen(
                        parserCompileResult.Success,
                        context.DiscardResult ?
                            Expression.Empty() :
                            Expression.Assign(result.Value, parserCompileResult.Value)
                    ),
                    Expression.Assign(result.Success, parserCompileResult.Success)
                ),
                // Restore previous whitespace parser
                Expression.Assign(whiteSpaceParserProperty, previousWhiteSpaceParser)
            )
        };

        result.Body.AddRange(blockExpressions);

        return result;
    }

    public override string ToString() => $"{_parser} (With Custom WS)";
}
