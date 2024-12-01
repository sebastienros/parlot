using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Wraps an existing parser as an <see cref="ISeekable"/> implementation by provide the seekable properties.
/// </summary>
internal sealed class Seekable<T> : Parser<T>, ISeekable
{
    public bool CanSeek { get; }

    public char[] ExpectedChars { get; set; }

    public bool SkipWhitespace { get; }

    public Parser<T> Parser { get; }

    public Seekable(Parser<T> parser, bool skipWhiteSpace, params ReadOnlySpan<char> expectedChars)
    {
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));
        ExpectedChars = expectedChars.ToArray().Distinct().ToArray();
        SkipWhitespace = skipWhiteSpace;

        Name = $"{parser.Name} (Seekable)";
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var success = Parser.Parse(context, ref result);

        context.ExitParser(this);
        return success;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true);

        var parserCompileResult = Parser.Build(context, requireResult: true);

        var block = Expression.Block(
            parserCompileResult.Variables,
            parserCompileResult.Body
        );

        result.Body.Add(block);

        return result;
    }
}

