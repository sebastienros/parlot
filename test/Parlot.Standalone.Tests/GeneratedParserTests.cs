using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace Parlot.Standalone.Tests;

public class GeneratedParserTests
{
    [Theory]
    [InlineData("abc-->def", true, "abc")]
    [InlineData("a-b->c-->d", true, "a-b->c")]
    [InlineData("a==>b", true, "a")]
    [InlineData("a-=-=b==>", true, "a-=-=b")]
    [InlineData("abc", true, "abc")]
    [InlineData("-->", false, null)]
    [InlineData("", false, null)]
    public void Text_Before_A_Seekable_Delimiter_Is_Searched(string input, bool success, string expected)
    {
        Assert.Equal(success, Grammar.TryParseTextBefore(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseTextBefore(reader, out value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc;def", "abc")]
    [InlineData(";", "")]
    [InlineData("", "")]
    [InlineData("a\nb;c", "a\nb")]
    public void Text_Before_A_Char_Is_Searched(string input, string expected)
    {
        Assert.True(Grammar.TryParseTextBeforeChar(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.True(Grammar.TryParseTextBeforeChar(reader, out value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc", true, "abc")]
    [InlineData("_a1-b", true, "_a1-b")]
    [InlineData("@x y", true, "@x")]
    [InlineData("1abc", false, null)]
    [InlineData("", false, null)]
    public void Identifier_With_Predicates_Counts_Its_Chars(string input, bool success, string expected)
    {
        Assert.Equal(success, Grammar.TryParseIdentifierWithPredicates(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseIdentifierWithPredicates(reader, out value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("12", true, "12")]
    [InlineData("12345", true, "1234")]
    [InlineData("12a", true, "12")]
    [InlineData("1", false, null)]
    [InlineData("1a", false, null)]
    [InlineData("", false, null)]
    public void Pattern_Counts_Its_Chars(string input, bool success, string expected)
    {
        Assert.Equal(success, Grammar.TryParsePattern(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParsePattern(reader, out value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" ", false)]
    [InlineData("a", false)]
    public void Standalone_Eof_Checks_Only_The_Current_Position(string input, bool success)
    {
        Assert.Equal(success, Grammar.TryParseEof(input, out var value));
        Assert.Null(value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseEof(reader, out value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("42", true)]
    [InlineData(" 42", true)]
    [InlineData("42a", false)]
    [InlineData("42 ", false)]
    [InlineData("", false)]
    public void Standalone_Eof_Preserves_The_Preceding_Result(string input, bool success)
    {
        Assert.Equal(success, Grammar.TryParseNumberWithStandaloneEof(input, out var value));
        Assert.Equal(success ? 42 : 0, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseNumberWithStandaloneEof(reader, out value));
        Assert.Equal(success ? 42 : 0, value);
    }

    [Fact]
    public void Both_Eof_Forms_Allow_Alternatives_To_Retry()
    {
        Assert.True(Grammar.TryParseEofFallback("42a", out var value));
        Assert.Equal(42, value);
        Assert.True(Grammar.TryParseStandaloneEofFallback("42a", out value));
        Assert.Equal(42, value);
        using var genericReader = new StringReader("42a");
        Assert.True(Grammar.TryParseEofFallback(genericReader, out value));
        Assert.Equal(42, value);
        using var standaloneReader = new StringReader("42a");
        Assert.True(Grammar.TryParseStandaloneEofFallback(standaloneReader, out value));
        Assert.Equal(42, value);
    }

    [Theory]
    [InlineData("255KeYwOrDVaLuEEnD", true)]
    [InlineData("256KeYwOrDVaLuEEnD", false)]
    [InlineData("-1KeYwOrDVaLuEEnD", false)]
    [InlineData("255KeYwOrDVaLuE?", false)]
    public void Skipped_Values_Preserve_Numeric_Validation_And_Retained_Casing(string input, bool success)
    {
        Assert.Equal(success, Grammar.TryParseSkippedValues(input, out var value));
        Assert.Equal(success ? "VaLuE" : null, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseSkippedValues(reader, out value));
        Assert.Equal(success ? "VaLuE" : null, value);
    }

    [Fact]
    public void Text_Choices_Preserve_Order_Backtracking_Capture_And_Custom_Whitespace()
    {
        Assert.True(Grammar.TryParseTextChoice("if12", out var value));
        Assert.Same("if1", value);
        Assert.True(Grammar.TryParseTextChoice("ifx", out value));
        Assert.Same("if", value);
        Assert.True(Grammar.TryParseTextChoice("Content-Unknown", out value));
        Assert.Same("Content", value);
        Assert.True(Grammar.TryParseTextChoice("Content-Encoding-tail", out value));
        Assert.Same("Content-Encoding", value);
        Assert.True(Grammar.TryParseTextChoiceFallback(" \r\nunknown!", out value));
        Assert.Equal(" \r\nunknown!", value);
        Assert.True(Grammar.TryParseTextChoiceCapture("! \r\nline\r\nend!", out value));
        Assert.Equal(" \r\nline\r\nend", value);
        Assert.True(Grammar.TryParseTextChoiceCustomWhitespace("___Content-Type!", out value));
        Assert.Equal("Content-Type", value);
        Assert.False(Grammar.TryParseTextChoiceCustomWhitespace(" Content-Type!", out _));
        Assert.True(Grammar.TryParseTextChoiceDiscarded(" \r\nif1!", out var discarded));
        Assert.Equal('!', discarded);
        Assert.False(Grammar.TryParseTextChoiceDiscarded(" \r\nif12!", out _));
        Assert.False(Grammar.TryParseTextChoice(new string('a', 100_000), out _));
        Assert.True(Grammar.TryParseTextChoicePosition(" \r\nline\r\nend!", out var position));
        // " \r\n" is skipped and "line\r\nend" matched: two line feeds, the CRLFs do not count twice
        Assert.Equal((12, 3, 4), position);
    }

    [Fact]
    public void Scanned_Tokens_Track_New_Lines()
    {
        string[] inputs =
        [
            "abc d", "ab\ncd\ne f", "ab\ncd", "\nab c", "ab\n\n\n c", "a\r\nb\rc\n d",
            new string('a', 70) + "\n" + new string('b', 70) + " x", new string('\n', 100) + "a",
        ];

        foreach (var input in inputs)
        {
            var token = input.Split(' ')[0];
            var line = 1;
            var column = 1;

            // The cursor only ends a line on '\n'
            foreach (var c in token)
            {
                if (c == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            Assert.True(Grammar.TryParseScanPosition(input, out var position));
            Assert.Equal((token, token.Length, line, column), position);

            using var reader = new StringReader(input);
            Assert.True(Grammar.TryParseScanPosition(reader, out position));
            Assert.Equal((token, token.Length, line, column), position);
        }
    }

    [Fact]
    public void Text_Choices_Agree_With_An_Ordered_Unicode_Prefix_Reference()
    {
        string[] words = ["if1", "if", "if12", "Content-Encoding", "Content-Type", "Content", "\u00e9\0x", "\ud800\udc00", "line\r\nend", "line\r\n", "if"];
        foreach (var word in words)
        {
            Check(word);
            Check(word + "tail");
            for (var offset = 0; offset < word.Length; offset++)
            {
                Check(word.Substring(0, offset));
                for (var c = 0; c < 256; c++)
                {
                    Check(word.Substring(0, offset) + (char)c + word.Substring(offset + 1));
                }

                foreach (var c in new[] { '\u017f', '\u212a', '\ud800', '\udc00', '\uffff' })
                {
                    Check(word.Substring(0, offset) + c + word.Substring(offset + 1));
                }
            }
        }

        var random = new Random(217);
        for (var i = 0; i < 2000; i++)
        {
            var input = new char[random.Next(0, 32)];
            for (var j = 0; j < input.Length; j++)
            {
                input[j] = (char)random.Next(0, 65536);
            }

            Check(new string(input));
        }

        void Check(string input)
        {
            input = " \r\n" + input;
            var expected = words.FirstOrDefault(word => input.TrimStart().StartsWith(word, StringComparison.Ordinal));
            Assert.Equal(expected != null, Grammar.TryParseTextChoice(input, out var value));
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void ZeroOrOne_Returns_Zero_Or_One_Items()
    {
        Assert.True(Grammar.TryParseZeroOrOne("a", out var matched));
        Assert.Equal(['a'], matched);
        Assert.True(Grammar.TryParseZeroOrOne("b", out var missing));
        Assert.Empty(missing);
        Assert.True(Grammar.TryParseZeroOrOneDiscarded("ab", out var afterMatch));
        Assert.Equal('b', afterMatch);
        Assert.True(Grammar.TryParseZeroOrOneDiscarded("b", out var afterMiss));
        Assert.Equal('b', afterMiss);
        Assert.False(Grammar.TryParseZeroOrOneDiscarded("c", out _));
    }

    [Theory]
    [InlineData("int", "int")]
    [InlineData("interface", "interface")]
    [InlineData(" \r\ninternal!", "internal")]
    [InlineData("if1", "if")]
    [InlineData("if_", "if")]
    [InlineData("if\u00e9", "if")]
    [InlineData("if\0", "if")]
    [InlineData("class!", "class")]
    [InlineData("IF", null)]
    [InlineData("ifx", null)]
    [InlineData("", null)]
    [InlineData(" \r\nunknown", null)]
    public void Keyword_Choices_Preserve_Boundaries_And_Canonical_Values(string input, string expected)
    {
        Assert.Equal(expected != null, Grammar.TryParseKeyword(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Keyword_Choices_Preserve_Backtracking_Capture_And_Custom_Whitespace()
    {
        Assert.True(Grammar.TryParseKeywordFallback(" \r\nunknown!", out var value));
        Assert.Equal(" \r\nunknown!", value);
        Assert.True(Grammar.TryParseKeywordCapture("! \r\ninterface1", out value));
        Assert.Equal(" \r\ninterface", value);
        Assert.True(Grammar.TryParseKeywordCustomWhitespace("___return!", out value));
        Assert.Equal("return", value);
        Assert.False(Grammar.TryParseKeywordCustomWhitespace(" return!", out _));
        Assert.False(Grammar.TryParseKeyword(new string('a', 100_000), out _));
    }

    [Fact]
    public void Keyword_Choices_Agree_With_An_Ordered_Reference()
    {
        string[] words = ["if", "else", "while", "return", "int", "interface", "internal", "yield", "case", "catch", "const", "class"];
        foreach (var word in words)
        {
            Check(word);
            Check(word.Substring(0, word.Length - 1));
            Check(word.Substring(1));
            Check(word + "x");
            Check(word + "9");
            Check(word + "\u00e9");
            for (var offset = 0; offset < word.Length; offset++)
            {
                for (var c = 0; c < 128; c++)
                {
                    Check(word.Substring(0, offset) + (char)c + word.Substring(offset + 1));
                }
            }
        }

        var random = new Random(923);
        for (var i = 0; i < 5000; i++)
        {
            var input = new char[random.Next(0, 24)];
            for (var j = 0; j < input.Length; j++)
            {
                input[j] = (char)random.Next(0, 65536);
            }

            Check(new string(input));
        }

        void Check(string input)
        {
            input = " \r\n" + input;
            var text = input.TrimStart();
            var expected = words.FirstOrDefault(word => text.StartsWith(word, StringComparison.Ordinal)
                && (text.Length == word.Length || text[word.Length] is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z')));
            Assert.Equal(expected != null, Grammar.TryParseKeyword(input, out var value));
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void Prefix_Dispatched_Keywords_Agree_With_An_Ordered_Reference()
    {
        var words = Grammar.PrefixKeywordWords;

        foreach (var input in PrefixInputs(words, seed: 431))
        {
            var text = " \r\n" + input;
            var trimmed = text.TrimStart();
            var offset = text.Length - trimmed.Length;
            (int Value, int Offset)? expected = null;

            for (var i = 0; i <= words.Length && expected == null; i++)
            {
                if (i == Grammar.PrefixKeywordFallback)
                {
                    var length = trimmed.TakeWhile(static c => c is >= 'a' and <= 'z').Count();
                    expected = length > 0 ? (-1, offset + length) : null;
                    continue;
                }

                var index = i < Grammar.PrefixKeywordFallback ? i : i - 1;
                var word = words[index];

                if (trimmed.StartsWith(word, StringComparison.Ordinal)
                    && (trimmed.Length == word.Length || trimmed[word.Length] is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z')))
                {
                    expected = (index, offset + word.Length);
                }
            }

            Assert.Equal(expected.HasValue, Grammar.TryParsePrefixKeywords(text, out var value));
            Assert.Equal(expected ?? default, value);
        }
    }

    [Fact]
    public void Prefix_Dispatched_Texts_Agree_With_An_Ordered_Reference()
    {
        var operators = Grammar.PrefixOperatorTexts;

        foreach (var input in PrefixInputs(operators, seed: 577))
        {
            var text = " \r\n" + input;
            var trimmed = text.TrimStart();
            var index = Array.FindIndex(operators, op => trimmed.StartsWith(op, StringComparison.Ordinal));
            (int Value, int Offset) expected = index < 0 ? default : (index, text.Length - trimmed.Length + operators[index].Length);

            Assert.Equal(index >= 0, Grammar.TryParsePrefixOperators(text, out var value));
            Assert.Equal(expected, value);

            index = Array.FindIndex(operators, op => input.StartsWith(op + " ", StringComparison.Ordinal));
            expected = index < 0 ? default : (index, operators[index].Length + 1);

            Assert.Equal(index >= 0, Grammar.TryParsePrefixOperatorSequences(input, out value));
            Assert.Equal(expected, value);
        }
    }

    // Each alternative, shortened, extended and with each of its chars replaced, then random texts of their chars
    private static string[] PrefixInputs(string[] words, int seed)
    {
        var inputs = new System.Collections.Generic.List<string> { "", " ", "x" };
        var alphabet = string.Concat(words) + " xX9\u00e9\0";

        foreach (var word in words)
        {
            foreach (var other in words)
            {
                inputs.Add(word + other);
                inputs.Add(word + " " + other);
            }

            inputs.Add(word.Substring(0, word.Length - 1));
            inputs.Add(word.Substring(1));
            inputs.Add(word + "x");
            inputs.Add(word + "9");
            inputs.Add(word + "\u00e9");

            for (var offset = 0; offset < word.Length; offset++)
            {
                for (var c = 0; c < 128; c++)
                {
                    inputs.Add(word.Substring(0, offset) + (char)c + word.Substring(offset + 1));
                }
            }
        }

        var random = new Random(seed);

        for (var i = 0; i < 5000; i++)
        {
            var input = new char[random.Next(0, 16)];

            for (var j = 0; j < input.Length; j++)
            {
                input[j] = random.Next(8) == 0 ? (char)random.Next(0, 65536) : alphabet[random.Next(alphabet.Length)];
            }

            inputs.Add(new string(input));
        }

        return inputs.ToArray();
    }

    [Theory]
    [InlineData(" 42", true, 42)]
    [InlineData("-17", true, -17)]
    [InlineData("42x", false, 0)]
    [InlineData("x", false, 0)]
    [InlineData("2147483648", false, 0)]
    public void Number_Parsing(string text, bool success, int expected)
    {
        Assert.Equal(success, Grammar.TryParseNumber(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Prefix_Parsing_Does_Not_Require_End_Of_Input()
    {
        Assert.True(Grammar.TryParsePrefix("hello world", out var value));
        Assert.Equal("hello", value);
        Assert.False(Grammar.TryParsePrefix("goodbye", out value));
        Assert.Null(value);
    }

    [Fact]
    public void Matched_Text_And_Full_Buffer_Span_Reuse_Existing_Strings()
    {
        var exact = new string(['h', 'e', 'l', 'l', 'o']);
        var differentCase = new string(['H', 'E', 'L', 'L', 'O']);

        Assert.True(Grammar.TryParseMatchedText(exact, out var value));
        Assert.Same("hello", value);
        Assert.True(Grammar.TryParseMatchedText(differentCase, out value));
        Assert.Same(differentCase, value);
        Assert.True(Grammar.TryParseMatchedText("HELLO!", out value));
        Assert.Equal("HELLO", value);
        Assert.True(Grammar.TryParseIdentifier(differentCase, out value));
        Assert.Same(differentCase, value);
        Assert.True(Grammar.TryParseIdentifier("HELLO!", out value));
        Assert.Equal("HELLO", value);
    }

    [Fact]
    public void Failed_Sequence_Restores_Input_And_Whitespace_For_Next_Alternative()
    {
        Assert.True(Grammar.TryParseAlternative("  ac", out var value));
        Assert.Equal('c', value);
        Assert.False(Grammar.TryParseAlternative("  ad", out value));
        Assert.Equal(default, value);
    }

    [Fact]
    public void String_Escapes_And_Collection_Storage_Work()
    {
        Assert.True(Grammar.TryParseString("\"line\\nquote\\\"\"", out var value));
        Assert.Equal("line\nquote\"", value);
        Assert.False(Grammar.TryParseString("\"unterminated", out _));
        Assert.True(Grammar.TryParseNumbers("1, 2, 3, 4, 5, 6", out var values));
        Assert.Equal([1, 2, 3, 4, 5, 6], values);
        Assert.False(Grammar.TryParseNumbers("1, x", out _));
    }

    [Fact]
    public void Optional_Result_Remains_Internal()
    {
        Assert.True(Grammar.TryParseOptional("", out var value));
        Assert.Equal(-1, value);
        Assert.True(Grammar.TryParseOptional("42", out value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Recursive_Grammar_And_Custom_Whitespace_Work()
    {
        Assert.True(Grammar.TryParseRecursive("(((x)))", out var depth));
        Assert.Equal(3, depth);
        Assert.False(Grammar.TryParseRecursive("((x)", out _));
        Assert.True(Grammar.TryParseCustomWhitespace("__hello___world", out var value));
        Assert.Equal("hello", value);
        Assert.False(Grammar.TryParseCustomWhitespace("hello world", out _));
    }

    [Fact]
    public void Configuration_Is_Read_At_Parse_Time_Without_Build_Time_Effects()
    {
        var options = new GrammarOptions { Formal = true, Prefix = "one:" };
        Assert.True(Grammar.TryParseConfigured("Hello", options, out var value));
        Assert.Equal("one:Hello", value);
        Assert.Equal(1, options.Evaluations);
        options.Formal = false;
        options.Prefix = "two:";
        Assert.True(Grammar.TryParseConfigured("Hi", options, out value));
        Assert.Equal("two:Hi", value);
        Assert.Equal(2, options.Evaluations);
        Assert.False(Grammar.TryParseConfigured("Hello", options, out value));
        Assert.Null(value);
        Assert.Equal(3, options.Evaluations);
    }

    [Fact]
    public void Custom_Whitespace_And_Parser_Helpers_Share_Current_Configuration()
    {
        var options = new GrammarOptions { Formal = true, Prefix = "one:" };
        Assert.True(Grammar.TryParseConfiguredWhitespace("__hello_world", options, out var value));
        Assert.Equal("one:hello", value);
        Assert.False(Grammar.TryParseConfiguredWhitespace("-hello-world", options, out _));
        options.Formal = false;
        options.Prefix = "two:";
        Assert.True(Grammar.TryParseConfiguredWhitespace("-hello--world", options, out value));
        Assert.Equal("two:hello", value);
        Assert.False(Grammar.TryParseConfiguredWhitespace("_hello_world", options, out _));
    }

    [Fact]
    public void Grammar_Errors_Are_Failures_But_Application_Exceptions_Propagate()
    {
        Assert.False(Grammar.TryParseError("z", out var value));
        Assert.Equal(default, value);
        var exception = Assert.Throws<InvalidOperationException>(() => Grammar.TryParseThrowingCallback("x", out _));
        Assert.Equal("Callback failed", exception.Message);
        Assert.Throws<ArgumentNullException>(() => Grammar.TryParseNumber((string)null, out _));
    }

    [Theory]
    [InlineData("1+2", true, 1)]
    [InlineData("42", true, 42)]
    [InlineData("x", false, 0)]
    public void Left_Recursive_Deferred_Fails_Instead_Of_Overflowing(string input, bool success, int expected)
    {
        Assert.Equal(success, Grammar.TryParseLeftRecursive(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.Equal(success, Grammar.TryParseLeftRecursive(reader, out value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("12a", "12")]
    [InlineData("  12a", "12")]
    [InlineData("  1234x", "1234")]
    [InlineData("1a", null)]
    [InlineData("12", null)]
    public void Char_Sets_Accept_String_Literals_On_Every_Target(string input, string expected)
    {
        // AnyOf("...") and NoneOf("...") bind to the string overloads, which a factory compiled for
        // net472 or netstandard2.0 needs since a string doesn't convert to ReadOnlySpan<char> there.
        Assert.Equal(expected != null, Grammar.TryParseDigits(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new StringReader(input);
        Assert.Equal(expected != null, Grammar.TryParseDigits(reader, out value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Explicit_Lambda_Return_Types_Are_Supported()
    {
        Assert.True(Grammar.TryParseExplicitLambda("x", out var value));
        Assert.Equal("x", value);
        Assert.False(Grammar.TryParseExplicitLambda("y", out _));
    }

    [Fact]
    public void Cancellation_Is_Optional_And_Does_Not_Change_Match_Behavior()
    {
        Assert.True(Grammar.TryParseCancelableNumber(" 42", CancellationToken.None, out var value));
        Assert.Equal(42, value);
        Assert.False(Grammar.TryParseCancelableNumber("42x", CancellationToken.None, out value));
        Assert.Equal(0, value);
        using var source = new CancellationTokenSource();
        Assert.True(Grammar.TryParseCancelableRecursive("(((x)))", source.Token, out var depth));
        Assert.Equal(3, depth);
    }

    [Fact]
    public void PreCanceled_Token_Throws_Instead_Of_Returning_Failure()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(() =>
            Grammar.TryParseCancelableNumber("42", source.Token, out _));
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Throws<OperationCanceledException>(() =>
            Grammar.TryParseCancelableRecursive("(((x)))", source.Token, out _));
        var options = new CancellationOptions { Source = source };
        Assert.Throws<OperationCanceledException>(() =>
            Grammar.TryParseCancelableSequence("x", options, source.Token, out _));
        Assert.Equal(0, options.Evaluations);
        Assert.True(Grammar.TryParseCancelableNumber("17", CancellationToken.None, out var value));
        Assert.Equal(17, value);
    }

    [Fact]
    public void Cancellation_During_Parsing_Stops_Repetition_And_Custom_Whitespace()
    {
        using var source = new CancellationTokenSource();
        var options = new CancellationOptions { Source = source };
        var exception = Assert.Throws<OperationCanceledException>(() =>
            Grammar.TryParseCancelableSequence(new string('x', 1000), options, source.Token, out _));
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.InRange(options.Evaluations, 1, 64);

        using var whitespaceSource = new CancellationTokenSource();
        var whitespaceOptions = new CancellationOptions { Source = whitespaceSource };
        var whitespaceException = Assert.Throws<OperationCanceledException>(() =>
            Grammar.TryParseCancelableWhitespace(new string('_', 1000) + "hello", whitespaceOptions, whitespaceSource.Token, out _));
        Assert.Equal(whitespaceSource.Token, whitespaceException.CancellationToken);
        Assert.InRange(whitespaceOptions.Evaluations, 1, 64);
    }

    [Fact]
    public void Ordinary_Token_Configuration_Does_Not_Implicitly_Enable_Engine_Cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.True(Grammar.TryParseTokenConfiguration("x", source.Token, out var value));
        Assert.True(value);
    }

    [Fact]
    public void Application_Has_No_Parlot_Assembly_Reference_Or_Public_Support_Types()
    {
        var assembly = typeof(Grammar).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(),
            static reference => reference.Name is "Parlot" or "Parlot.SourceGenerator");
        Assert.DoesNotContain(typeof(Grammar).GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            static method => method.Name == "Number" || method.Name == "Configured");
        Assert.All(assembly.GetTypes().Where(static type => type.Namespace?.StartsWith("Parlot.Generated", StringComparison.Ordinal) == true),
            static type => Assert.False(type.IsPublic));
        Assert.All(typeof(Grammar).GetMethods(BindingFlags.Public | BindingFlags.Static),
            static method => Assert.Equal(typeof(bool), method.ReturnType));
    }
}
