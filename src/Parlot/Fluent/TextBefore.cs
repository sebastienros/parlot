using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

using System.Reflection;

namespace Parlot.Fluent;

public sealed class TextBefore<T> : Parser<TextSpan>, ISourceable
{
    private static readonly MethodInfo _jumpToNextExpectedCharMethod = typeof(TextBefore<T>).GetMethod(nameof(JumpToNextExpectedChar), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Parser<T> _delimiter;
    private readonly bool _canBeEmpty;
    private readonly bool _failOnEof;
    private readonly bool _consumeDelimiter;
    private readonly bool _canJumpToNextExpectedChar;
#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _expectedSearchValues;
#else
    private readonly char[]? _expectedChars;
#endif
    public TextBefore(Parser<T> delimiter, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
    {
        _delimiter = delimiter;
        _canBeEmpty = canBeEmpty;
        _failOnEof = failOnEof;
        _consumeDelimiter = consumeDelimiter;

        if (_delimiter is ISeekable seekable && seekable.CanSeek)
        {
#if NET8_0_OR_GREATER
            _expectedSearchValues = SearchValues.Create(seekable.ExpectedChars);
#else
            _expectedChars = seekable.ExpectedChars;
#endif
            _canJumpToNextExpectedChar = true;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        var parsed = new ParseResult<T>();

        while (true)
        {
            if (_canJumpToNextExpectedChar)
            {
#if NET8_0_OR_GREATER
                JumpToNextExpectedChar(context, _expectedSearchValues!);
#else
                JumpToNextExpectedChar(context, _expectedChars!);
#endif
            }

            var previous = context.Scanner.Cursor.Position;

            if (context.Scanner.Cursor.Eof)
            {
                if (_failOnEof)
                {
                    context.Scanner.Cursor.ResetPosition(start);

                    context.ExitParser(this);
                    return false;
                }

                var length = previous - start;

                if (length == 0 && !_canBeEmpty)
                {
                    context.ExitParser(this);
                    return false;
                }

                result.Set(start.Offset, previous.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                context.ExitParser(this);
                return true;
            }

            var delimiterFound = _delimiter.Parse(context, ref parsed);

            if (delimiterFound)
            {
                var length = previous - start;

                if (!_consumeDelimiter)
                {
                    context.Scanner.Cursor.ResetPosition(previous);
                }

                if (length == 0 && !_canBeEmpty)
                {
                    context.ExitParser(this);
                    return false;
                }

                result.Set(start.Offset, previous.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.Advance();
        }
    }

#if NET8_0_OR_GREATER
    private static void JumpToNextExpectedChar(ParseContext context, SearchValues<char> expectedChars)
    {
        var index = context.Scanner.Cursor.Span.IndexOfAny(expectedChars);

        switch (index)
        {
            case >= 0:
                context.Scanner.Cursor.Advance(index);
                break;
            case -1:
                // No expected char found, move to the end
                context.Scanner.Cursor.Advance(context.Scanner.Cursor.Span.Length);
                break;
        }
    }
#else
    private static void JumpToNextExpectedChar(ParseContext context, char[] expectedChars)
    {
        var indexOfAny = int.MaxValue;
        var span = context.Scanner.Cursor.Span;

        foreach (var c in expectedChars)
        {
            var index = span.IndexOf(c);

            if (index >= 0)
            {
                indexOfAny = Math.Min(indexOfAny, index);
            }
        }

        if (indexOfAny < int.MaxValue)
        {
            context.Scanner.Cursor.Advance(indexOfAny);
        }
        else
        {
            // No expected char found, move to the end
            context.Scanner.Cursor.Advance(context.Scanner.Cursor.Span.Length);
        }
    }
#endif


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_delimiter is not ISourceable sourceable)
        {
            throw new NotSupportedException("TextBefore requires a source-generatable delimiter parser.");
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;

        var startName = $"start{context.NextNumber()}";
        var previousName = $"previous{context.NextNumber()}";
        var lengthName = $"length{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Position;");

        var delimiterValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var delimiterHelperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_TextBefore_Delimiter", delimiterValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    var {previousName} = {cursorName}.Position;");
        result.Body.Add($"    if ({cursorName}.Eof)");
        result.Body.Add("    {");
        
        if (_failOnEof)
        {
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add($"        {result.SuccessVariable} = false;");
            result.Body.Add("        break;");
        }
        else
        {
            result.Body.Add($"        var {lengthName} = {previousName}.Offset - {startName}.Offset;");
            if (!_canBeEmpty)
            {
                result.Body.Add($"        if ({lengthName} == 0)");
                result.Body.Add("        {");
                result.Body.Add($"            {result.SuccessVariable} = false;");
                result.Body.Add("            break;");
                result.Body.Add("        }");
            }
            if (!context.DiscardResult)
            {
                result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}.Offset, {lengthName});");
            }
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("        break;");
        }
        
        result.Body.Add("    }");

        // Try to parse delimiter
        result.Body.Add($"    if ({delimiterHelperName}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        var {lengthName} = {previousName}.Offset - {startName}.Offset;");
        
        if (!_consumeDelimiter)
        {
            result.Body.Add($"        {cursorName}.ResetPosition({previousName});");
        }

        if (!_canBeEmpty)
        {
            result.Body.Add($"        if ({lengthName} == 0)");
            result.Body.Add("        {");
            result.Body.Add($"            {result.SuccessVariable} = false;");
            result.Body.Add("            break;");
            result.Body.Add("        }");
        }

        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}.Offset, {lengthName});");
        }
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        result.Body.Add($"    {cursorName}.Advance();");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"TextBefore({_delimiter})";
}
