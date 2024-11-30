using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class ZeroOrOne<T> : Parser<T>, ICompilable, ISeekable
{
    private readonly Parser<T> _parser;
    private readonly T _defaultValue;

    public ZeroOrOne(Parser<T> parser, T defaultValue)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _defaultValue = defaultValue;
        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }

        Name = $"ZeroOrOne({parser.Name})";
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? parsed.Value : _defaultValue);

        // ZeroOrOne always succeeds
        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true, Expression.Constant(_defaultValue, typeof(T)));

        // T value = _defaultValue;
        //
        // parse1 instructions
        // 
        // value = new OptionalResult<T>(parser1.Success, parse1.Value);
        //

        var parserCompileResult = _parser.Build(context);

        var block = Expression.Block(
            parserCompileResult.Variables,
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Assign(result.Value, parserCompileResult.Value),
                            Expression.Assign(result.Value, Expression.Constant(_defaultValue, typeof(T)))
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }
}
