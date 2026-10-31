using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot.Fluent;

public sealed class TextBefore<T> : Parser<TextSpan>, ISourceable
{
    private readonly Parser<T> _delimiter;
    private readonly bool _canBeEmpty;
    private readonly bool _failOnEof;
    private readonly bool _consumeDelimiter;
    private readonly bool _canJumpToNextExpectedChar;
    private readonly char[]? _expectedChars;
#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _expectedSearchValues;
#endif
    public TextBefore(Parser<T> delimiter, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
    {
        _delimiter = delimiter;
        _canBeEmpty = canBeEmpty;
        _failOnEof = failOnEof;
        _consumeDelimiter = consumeDelimiter;

        // The text is searched for the chars the delimiter can start with, unless it can start somewhere else:
        // - with a white space, which is skipped before the expected chars
        // - with any char, which a OneOf with parsers that are not seekable declares with OtherSeekableChar
        if (_delimiter is ISeekable seekable
            && seekable.CanSeek
            && !seekable.SkipWhitespace
            && Array.IndexOf(seekable.ExpectedChars, OneOf<T>.OtherSeekableChar) < 0)
        {
            _expectedChars = seekable.ExpectedChars;
#if NET8_0_OR_GREATER
            _expectedSearchValues = SearchValues.Create(seekable.ExpectedChars);
#endif
            _canJumpToNextExpectedChar = true;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

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

            var previous = context.Scanner.Cursor.Offset;

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

                result.Set(start, previous, context.Scanner.Cursor.CreateSpan(start, length));

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
                    // The delimiter might have been consumed
                    context.Scanner.Cursor.ResetPosition(start);

                    context.ExitParser(this);
                    return false;
                }

                // The result ends where the cursor is: after the delimiter when it is consumed
                result.Set(start, context.Scanner.Cursor.Offset, context.Scanner.Cursor.CreateSpan(start, length));

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
        var span = context.Scanner.Cursor.Span;

        // No expected char found, move to the end
        var indexOfAny = span.Length;

        if (expectedChars.Length == 1)
        {
            var index = span.IndexOf(expectedChars[0]);

            if (index >= 0)
            {
                indexOfAny = index;
            }
        }
        else
        {
            // The text is read once and only up to the first expected char. Searching for each expected char
            // in turn reads the whole text for the ones it doesn't contain, each time the delimiter doesn't match.
            for (var i = 0; i < span.Length && indexOfAny == span.Length; i++)
            {
                var c = span[i];

                foreach (var expected in expectedChars)
                {
                    if (c == expected)
                    {
                        indexOfAny = i;
                        break;
                    }
                }
            }
        }

        context.Scanner.Cursor.Advance(indexOfAny);
    }
#endif


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

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

        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        var delimiterValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var delimiterHelperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_TextBefore_Delimiter", delimiterValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        result.Body.Add("while (true)");
        result.Body.Add("{");

        if (_canJumpToNextExpectedChar)
        {
            GenerateJumpToNextExpectedChar(context, result, _expectedChars!);
        }

        result.Body.Add($"    var {previousName} = {cursorName}.Offset;");
        result.Body.Add($"    if ({cursorName}.Eof)");
        result.Body.Add("    {");

        if (_failOnEof)
        {
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add($"        {result.SuccessVariable} = false;");
        }
        else
        {
            result.Body.Add($"        var {lengthName} = {previousName} - {startName};");
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
                result.Body.Add($"        {result.ValueVariable} = {context.CursorName}.CreateSpan({startName}, {lengthName});");
            }
            result.Body.Add($"        {result.SuccessVariable} = true;");
        }

        result.Body.Add("        break;");

        result.Body.Add("    }");

        // Try to parse delimiter
        result.Body.Add($"    if ({delimiterHelperName}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        var {lengthName} = {previousName} - {startName};");

        if (!_consumeDelimiter)
        {
            result.Body.Add($"        {cursorName}.ResetPosition({previousName});");
        }

        if (!_canBeEmpty)
        {
            result.Body.Add($"        if ({lengthName} == 0)");
            result.Body.Add("        {");
            if (_consumeDelimiter)
            {
                result.Body.Add($"            {cursorName}.ResetPosition({startName});");
            }
            result.Body.Add($"            {result.SuccessVariable} = false;");
            result.Body.Add("            break;");
            result.Body.Add("        }");
        }

        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {context.CursorName}.CreateSpan({startName}, {lengthName});");
        }
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        result.Body.Add($"    {cursorName}.Advance();");
        result.Body.Add("}");

        return result;
    }

    // Emits the same jump as JumpToNextExpectedChar: the cursor moves to the first char the delimiter can start with,
    // or to the end of the text. A single char is searched with IndexOf on every framework; several chars with
    // SearchValues on .NET 8+, and with a loop reading the text once below.
    private static void GenerateJumpToNextExpectedChar(SourceGenerationContext context, SourceResult result, char[] expectedChars)
    {
        var cursorName = context.CursorName;
        var spanName = $"span{context.NextNumber()}";
        var indexName = $"index{context.NextNumber()}";

        result.Body.Add($"    var {spanName} = {cursorName}.Span;");

        if (expectedChars.Length == 1)
        {
            result.Body.Add($"    var {indexName} = {spanName}.IndexOf({LiteralHelper.CharToLiteral(expectedChars[0])});");
            result.Body.Add($"    {cursorName}.Advance({indexName} >= 0 ? {indexName} : {spanName}.Length);");
            return;
        }

        context.StaticFields.Add("#if NET8_0_OR_GREATER");
        var fieldName = context.RegisterStaticField(
            "private static readonly global::System.Buffers.SearchValues<char>",
            $"global::System.Buffers.SearchValues.Create({LiteralHelper.StringToLiteral(new string(expectedChars))})");
        context.StaticFields.Add("#endif");

        var iName = $"i{context.NextNumber()}";
        var cName = $"c{context.NextNumber()}";
        var patterns = new string[expectedChars.Length];

        for (var i = 0; i < expectedChars.Length; i++)
        {
            patterns[i] = LiteralHelper.CharToLiteral(expectedChars[i]);
        }

        result.Body.Add("#if NET8_0_OR_GREATER");
        result.Body.Add($"    var {indexName} = {spanName}.IndexOfAny({fieldName});");
        result.Body.Add($"    {cursorName}.Advance({indexName} >= 0 ? {indexName} : {spanName}.Length);");
        result.Body.Add("#else");
        result.Body.Add($"    var {indexName} = {spanName}.Length;");
        result.Body.Add($"    for (var {iName} = 0; {iName} < {spanName}.Length; {iName}++)");
        result.Body.Add("    {");
        result.Body.Add($"        var {cName} = {spanName}[{iName}];");
        result.Body.Add($"        if ({cName} is {string.Join(" or ", patterns)})");
        result.Body.Add("        {");
        result.Body.Add($"            {indexName} = {iName};");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add("    }");
        result.Body.Add($"    {cursorName}.Advance({indexName});");
        result.Body.Add("#endif");
    }

    public override string ToString() => $"TextBefore({_delimiter})";
}
