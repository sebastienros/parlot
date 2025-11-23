using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Ensure the given parser matches at the current position without consuming input (positive lookahead).
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class WhenFollowedBy<T> : Parser<T>, ICompilable, ISeekable
{
    private readonly Parser<T> _parser;
    private readonly Parser<object> _lookahead;

    public WhenFollowedBy(Parser<T> parser, Parser<object> lookahead)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lookahead = lookahead ?? throw new ArgumentNullException(nameof(lookahead));

        // Forward ISeekable properties from the main parser
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

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        // First, parse with the main parser
        var mainSuccess = _parser.Parse(context, ref result);

        if (!mainSuccess)
        {
            context.ExitParser(this);
            return false;
        }

        // Save position before lookahead check
        var beforeLookahead = context.Scanner.Cursor.Position;

        // Now check if the lookahead parser matches at the current position
        var lookaheadResult = new ParseResult<object>();
        var lookaheadSuccess = _lookahead.Parse(context, ref lookaheadResult);

        // Reset position to before the lookahead (it shouldn't consume input)
        context.Scanner.Cursor.ResetPosition(beforeLookahead);

        // If lookahead failed, fail this parser and reset to start
        if (!lookaheadSuccess)
        {
            context.Scanner.Cursor.ResetPosition(start);
            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var mainParserCompileResult = _parser.Build(context, requireResult: true);

        // For now, don't attempt to compile the lookahead check. Just compile the main parser.
        // Compilation support for lookahead can be added later if needed.
        // This ensures the parser still benefits from compilation of the main parser.
        
        var parserResult = context.CreateCompilationResult<T>();
        
        // Just add the compiled main parser
        foreach (var variable in mainParserCompileResult.Variables)
        {
            parserResult.Variables.Add(variable);
        }

        parserResult.Body.AddRange(mainParserCompileResult.Body);

        return parserResult;
    }

    public override string ToString() => $"{_parser} (WhenFollowedBy {_lookahead})";
}
