using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq;

namespace Parlot.Fluent;

/// <summary>
/// A parser that skips white space and comments in a single loop, the white space parser of
/// <see cref="ParserExtensions.WithComments{T}(Parser{T}, Action{CommentsBuilder})"/>.
/// </summary>
/// <remarks>
/// It is built by <see cref="CommentsBuilder.Build"/>. At each position it skips the white space of the context's
/// <see cref="ParseContext.WhiteSpaceSet"/>, when the builder includes it, then the first comment, in the order they were
/// added, that starts there. It succeeds when it skipped something.
/// </remarks>
public sealed class CommentsParser : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable
{
    private readonly bool _skipWhiteSpace;
    private readonly bool _includeNewLines;
    private readonly CommentSyntax[] _comments;

    // The first chars of the comments, to tell at once that no comment starts at a char
    private readonly string _firstChars;

    internal CommentsParser(bool skipWhiteSpace, bool includeNewLines, CommentSyntax[] comments)
    {
        _skipWhiteSpace = skipWhiteSpace;
        _includeNewLines = includeNewLines;
        _comments = comments;
        _firstChars = new string(comments.Select(static c => c.Start[0]).Distinct().ToArray());

        // The chars of the default set contain the ones of every WhiteSpaceSet
        var whiteSpaceChars = skipWhiteSpace ? new WhiteSpaceLiteral(includeNewLines).ExpectedChars : [];
        ExpectedChars = whiteSpaceChars.Concat(_firstChars).Distinct().ToArray();

        Name = "CommentsParser";
    }

    /// <inheritdoc/>
    public bool CanSeek => true;

    /// <summary>
    /// The chars of the white space it skips, and the first char of each comment.
    /// </summary>
    public char[] ExpectedChars { get; }

    /// <inheritdoc/>
    public bool SkipWhitespace => false;

    // It invokes no parser, and fails when it skips nothing
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    /// <inheritdoc/>
    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var scanner = context.Scanner;
        var cursor = scanner.Cursor;
        var start = cursor.Offset;

        while (true)
        {
            if (_skipWhiteSpace)
            {
                if (_includeNewLines)
                {
                    scanner.SkipWhiteSpaceOrNewLine(context.WhiteSpaceOrNewLineMask);
                }
                else
                {
                    scanner.SkipWhiteSpace(context.WhiteSpaceMask);
                }
            }

            // At the end of the text Current is the null char, which a delimiter can start with: Match tells
            if (_firstChars.IndexOf(cursor.Current) < 0 || !SkipComment(context, cursor))
            {
                break;
            }
        }

        var end = cursor.Offset;

        context.ExitParser(this);

        if (start == end)
        {
            return false;
        }

        result.Set(start, end, cursor.CreateSpan(start, end - start));
        return true;
    }

    // Skips the first comment at the cursor, whether one was skipped
    private bool SkipComment(ParseContext context, Cursor cursor)
    {
        foreach (var comment in _comments)
        {
            // Match records the end of the text when it reads a start which might continue there
            if (!cursor.Match(comment.Start))
            {
                continue;
            }

            if (comment.End is null)
            {
                context.Scanner.SkipSingleLineComment(comment.Start.Length, comment.LineTerminators.AsSpan());
                return true;
            }

            var commentStart = cursor.Offset;

            if (context.Scanner.SkipMultiLineComment(comment.Start.Length, comment.End.AsSpan()))
            {
                return true;
            }

            if (comment.Unterminated == UnterminatedComment.ToEnd)
            {
                cursor.Advance(cursor.Span.Length);
                return true;
            }

            if (comment.Unterminated == UnterminatedComment.NotAComment)
            {
                // The comments added after this one can start here
                cursor.ResetPosition(commentStart);
                continue;
            }

            // The error of Literals.Comments(start, end), after the start of the comment
            if (!context.CollectDiagnostics)
            {
                context.ExitParser(this);
                throw new ParseException(comment.Message!, cursor.Position);
            }

            context.AddDiagnostic(comment.Message!, cursor.Position, ParseDiagnosticSeverity.Error);
            cursor.ResetPosition(commentStart);
            return false;
        }

        return false;
    }

    /// <inheritdoc/>
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(TextSpan));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";

        // var start = cursor.Offset;
        // while (true)
        // {
        //     scanner.SkipWhiteSpaceOrNewLine(context.WhiteSpaceOrNewLineMask);
        //     if (cursor.Current == '/')
        //     {
        //         if (cursor.Match("//".AsSpan())) { scanner.SkipSingleLineComment(2, default); continue; }
        //         if (cursor.Match("/*".AsSpan())) { if (scanner.SkipMultiLineComment(2, "*/".AsSpan())) continue; throw ...; }
        //     }
        //     break;
        // }

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add("while (true)");
        result.Body.Add("{");

        if (_skipWhiteSpace)
        {
            result.Body.Add(_includeNewLines
                ? $"    {scannerName}.SkipWhiteSpaceOrNewLine({ctx}.WhiteSpaceOrNewLineMask);"
                : $"    {scannerName}.SkipWhiteSpace({ctx}.WhiteSpaceMask);");
        }

        // The comments starting with each char, in the order they were added
        foreach (var first in _firstChars)
        {
            result.Body.Add($"    if ({cursorName}.Current == {LiteralHelper.CharToLiteral(first)})");
            result.Body.Add("    {");

            foreach (var comment in _comments)
            {
                if (comment.Start[0] != first)
                {
                    continue;
                }

                var start = LiteralHelper.StringToLiteral(comment.Start);

                result.Body.Add($"        if ({cursorName}.Match({start}.AsSpan()))");
                result.Body.Add("        {");

                if (comment.End is null)
                {
                    var terminators = comment.LineTerminators is null ? "default" : $"{LiteralHelper.StringToLiteral(comment.LineTerminators)}.AsSpan()";
                    result.Body.Add($"            {scannerName}.SkipSingleLineComment({comment.Start.Length}, {terminators});");
                    result.Body.Add("            continue;");
                }
                else
                {
                    var commentStartName = $"commentStart{context.NextNumber()}";
                    var throws = comment.Unterminated == UnterminatedComment.Throw;

                    if (comment.Unterminated == UnterminatedComment.NotAComment || throws && context.CollectDiagnostics)
                    {
                        result.Body.Add($"            var {commentStartName} = {cursorName}.Offset;");
                    }

                    result.Body.Add($"            if ({scannerName}.SkipMultiLineComment({comment.Start.Length}, {LiteralHelper.StringToLiteral(comment.End)}.AsSpan()))");
                    result.Body.Add("            {");
                    result.Body.Add("                continue;");
                    result.Body.Add("            }");

                    if (comment.Unterminated == UnterminatedComment.ToEnd)
                    {
                        result.Body.Add($"            {cursorName}.Advance({cursorName}.Span.Length);");
                        result.Body.Add("            continue;");
                    }
                    else if (comment.Unterminated == UnterminatedComment.NotAComment)
                    {
                        // The comments added after this one can start here
                        result.Body.Add($"            {cursorName}.ResetPosition({commentStartName});");
                    }
                    else
                    {
                        ErrorSource.AddReport(result, context, comment.Message!, $"{cursorName}.Position", "            ", context.CollectDiagnostics ? commentStartName : null);

                        if (context.CollectDiagnostics)
                        {
                            result.Body.Add("            break;");
                        }
                    }
                }

                result.Body.Add("        }");
            }

            result.Body.Add("    }");
        }

        result.Body.Add("    break;");
        result.Body.Add("}");

        result.Body.Add($"var {endName} = {cursorName}.Offset;");
        result.Body.Add($"if ({startName} != {endName})");
        result.Body.Add("{");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}, {endName} - {startName});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    /// <inheritdoc/>
    public override string ToString() => "Comments";

    // A comment of the builder: a multi line comment has an end, a single line comment can have line terminators
    internal sealed class CommentSyntax
    {
        public CommentSyntax(string start, string? end, string? lineTerminators, UnterminatedComment unterminated = UnterminatedComment.Throw)
        {
            Start = start;
            End = end;
            LineTerminators = lineTerminators;
            Unterminated = unterminated;
            Message = end is null ? null : $"End-of-file found, '{end}' expected";
        }

        public string Start { get; }

        public string? End { get; }

        // What a multi line comment without its end is
        public UnterminatedComment Unterminated { get; }

        // null: "\r\n" or "\n", as Literals.Comments(start)
        public string? LineTerminators { get; }

        public string? Message { get; }
    }
}
