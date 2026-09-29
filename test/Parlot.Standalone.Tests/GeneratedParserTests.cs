using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace Parlot.Standalone.Tests;

public class GeneratedParserTests
{
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
        Assert.Throws<ArgumentNullException>(() => Grammar.TryParseNumber(null, out _));
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
