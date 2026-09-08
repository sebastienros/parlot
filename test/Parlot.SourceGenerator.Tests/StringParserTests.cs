using System;
using System.Collections.Generic;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class StringParserTests
{
    [Theory]
    [InlineData("'hello\\nworld'")]
    [InlineData("\"hello\\u0041\"")]
    public void Standard_Quoted_Strings_Can_Be_Captured_Without_Decoding(string input)
    {
        Assert.True(StringGrammars.TryParseCapturedStandard(input, out var length));
        Assert.Equal(input.Length, length);
    }

    [Theory]
    [InlineData("%%", "")]
    [InlineData("%hello%", "hello")]
    [InlineData("  %hello\\nworld%", "hello\nworld")]
    [InlineData("%\\u0041\\x42%", "AB")]
    public void Custom_Strings_Are_Decoded_Only_When_Returned(string input, string expected)
    {
        Assert.True(StringGrammars.TryParseDecoded(input, out var decoded));
        Assert.Equal(expected, decoded);
        Assert.True(StringGrammars.TryParseCaptured(input, out var length));
        Assert.Equal(input.Length, length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("'hello'")]
    [InlineData("%unterminated")]
    [InlineData("%hello%trailing")]
    [InlineData("%bad\\q%")]
    [InlineData("%bad\\u12%")]
    [InlineData("%bad\\x%")]
    public void Captured_And_Decoded_Strings_Reject_Invalid_Input(string input)
    {
        Assert.False(StringGrammars.TryParseDecoded(input, out var decoded));
        Assert.Null(decoded);
        Assert.False(StringGrammars.TryParseCaptured(input, out var length));
        Assert.Equal(0, length);
    }

    [Theory]
    [InlineData("  %bad\\q%")]
    [InlineData("  %bad\\u12%")]
    [InlineData("  %unterminated")]
    [InlineData("  %hello%?")]
    public void Failed_Captured_Strings_Restore_Input_For_The_Next_Alternative(string input)
    {
        Assert.True(StringGrammars.TryParseFallback(input, out var value));
        Assert.Equal(input, value);
    }

    [Fact]
    public void Shared_String_Parsers_Work_In_Both_Result_Mode_Orders()
    {
        Assert.True(StringGrammars.TryParseCapturedThenDecoded("%a\\n% %b\\t%", out var captureFirst));
        Assert.Equal(("%a\\n%", "b\t"), captureFirst);
        Assert.True(StringGrammars.TryParseDecodedThenCaptured("%a\\n% %b\\t%", out var decodeFirst));
        Assert.Equal(("a\n", " %b\\t%"), decodeFirst);
    }

    [Theory]
    [InlineData("Value")]
    [InlineData("Context")]
    [InlineData("Span")]
    public void Captured_Then_Callbacks_Receive_Decoded_Values_And_Still_Run(string overload)
    {
        const string input = "%hello\\nworld%";
        var state = new StringCallbackState();
        var length = 0;
        var success = overload switch
        {
            "Value" => StringGrammars.TryParseCapturedThen(input, state, out length),
            "Context" => StringGrammars.TryParseCapturedThenContext(input, state, out length),
            _ => StringGrammars.TryParseCapturedThenSpan(input, state, out length)
        };

        Assert.True(success);
        Assert.Equal(input.Length, length);
        Assert.Equal("hello\nworld", Assert.Single(state.Values));
    }

    [Fact]
    public void Captured_Callback_Exceptions_Still_Propagate()
    {
        var state = new StringCallbackState { Throw = true };
        Assert.Throws<InvalidOperationException>(() =>
            StringGrammars.TryParseCapturedThen("%hello%", state, out _));
    }

    [Fact]
    public void Captured_Predicates_And_Selectors_Receive_Decoded_Values()
    {
        Assert.True(StringGrammars.TryParseCapturedWhen("%a\\n%", out var length));
        Assert.Equal(5, length);
        Assert.False(StringGrammars.TryParseCapturedWhen("%other%", out _));
        Assert.True(StringGrammars.TryParseCapturedSwitch("%a\\n%!", out length));
        Assert.Equal(6, length);
        Assert.False(StringGrammars.TryParseCapturedSwitch("%a\\n%?", out _));
        Assert.True(StringGrammars.TryParseCapturedSwitch("%other%?", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured_Operator_Callbacks_Receive_Intermediate_Values(bool withContext)
    {
        const string binary = "%a\\n%+%b%+%c%";
        const string unary = "!!%a\\n%";
        int binaryLength;
        int unaryLength;
        if (withContext)
        {
            Assert.True(StringGrammars.TryParseCapturedLeftAssociativeContext(binary, out binaryLength));
            Assert.True(StringGrammars.TryParseCapturedUnaryContext(unary, out unaryLength));
        }
        else
        {
            Assert.True(StringGrammars.TryParseCapturedLeftAssociative(binary, out binaryLength));
            Assert.True(StringGrammars.TryParseCapturedUnary(unary, out unaryLength));
        }

        Assert.Equal(binary.Length, binaryLength);
        Assert.Equal(unary.Length, unaryLength);
    }
}

public static partial class StringGrammars
{
    public static partial bool TryParseDecoded(string text, out string value);
    public static partial bool TryParseCaptured(string text, out int value);
    public static partial bool TryParseCapturedStandard(string text, out int value);
    public static partial bool TryParseFallback(string text, out string value);
    public static partial bool TryParseCapturedThenDecoded(string text, out (string, string) value);
    public static partial bool TryParseDecodedThenCaptured(string text, out (string, string) value);
    public static partial bool TryParseCapturedThen(string text, StringCallbackState state, out int value);
    public static partial bool TryParseCapturedThenContext(string text, StringCallbackState state, out int value);
    public static partial bool TryParseCapturedThenSpan(string text, StringCallbackState state, out int value);
    public static partial bool TryParseCapturedWhen(string text, out int value);
    public static partial bool TryParseCapturedSwitch(string text, out int value);
    public static partial bool TryParseCapturedLeftAssociative(string text, out int value);
    public static partial bool TryParseCapturedLeftAssociativeContext(string text, out int value);
    public static partial bool TryParseCapturedUnary(string text, out int value);
    public static partial bool TryParseCapturedUnaryContext(string text, out int value);
}

public sealed class StringCallbackState
{
    public List<string> Values { get; } = [];
    public bool Throw { get; init; }

    public int Record(string value)
    {
        if (Throw)
        {
            throw new InvalidOperationException("Callback failed.");
        }

        Values.Add(value);
        return value.Length;
    }
}

public static class StringParserCallbacks
{
    public static string Combine(string left, string right)
    {
        if ((left, right) is not ("a\n", "b") and not ("a\nb", "c"))
        {
            throw new InvalidOperationException("Incorrect accumulated value.");
        }

        return left + right;
    }

    public static string Prefix(string value)
    {
        if (value is not "a\n" and not "!a\n")
        {
            throw new InvalidOperationException("Incorrect recursive value.");
        }

        return "!" + value;
    }
}
