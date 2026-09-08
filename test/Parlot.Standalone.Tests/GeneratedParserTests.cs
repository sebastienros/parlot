using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Parlot.Standalone.Tests;

public class GeneratedParserTests
{
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
