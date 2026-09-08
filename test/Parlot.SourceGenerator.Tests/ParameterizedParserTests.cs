#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class ParameterizedParserTests
{
    [Fact]
    public void Configuration_Is_Evaluated_Per_Call()
    {
        var options = new ConditionalOptions { Enabled = true };

        Assert.True(ParameterizedGrammars.TryParseChoice(" yes", options, out var value));
        Assert.Equal("yes", value);
        Assert.Equal(1, options.Evaluations);

        options.Enabled = false;
        Assert.True(ParameterizedGrammars.TryParseChoice(" no", options, out value));
        Assert.Equal("no", value);
        Assert.Equal(2, options.Evaluations);
    }

    [Fact]
    public void Selected_Branch_Failure_Does_Not_Fall_Back()
    {
        Assert.False(ParameterizedGrammars.TryParseBacktracking("no!", enabled: true, out var value));
        Assert.Null(value);
        Assert.False(ParameterizedGrammars.TryParseBacktracking("yes!", enabled: false, out value));
        Assert.Null(value);
    }

    [Fact]
    public void Multiple_Arguments_Have_Independent_Per_Call_State()
    {
        Assert.True(ParameterizedGrammars.TryParseMultiple("x", 3, 4, "first:", out var first));
        Assert.True(ParameterizedGrammars.TryParseMultiple("x", 10, 20, "second:", out var second));
        Assert.Equal("first:7", first);
        Assert.Equal("second:30", second);
    }

    [Fact]
    public void Select_When_Switch_And_Fallbacks_Support_Captures()
    {
        Assert.True(ParameterizedGrammars.TryParseSelected("no", 1, "no", out var selected));
        Assert.Equal("no", selected);
        Assert.False(ParameterizedGrammars.TryParseSelected("yes", 1, "no", out _));

        Assert.True(ParameterizedGrammars.TryParseSwitched("xno", 1, out var switched));
        Assert.Equal("no", switched);
        Assert.False(ParameterizedGrammars.TryParseSwitched("xyes", 1, out _));

        Assert.True(ParameterizedGrammars.TryParseFallback("", "prefix:", out var fallback));
        Assert.Equal("prefix:0", fallback);
        Assert.True(ParameterizedGrammars.TryParseFallback("x", "prefix:", out fallback));
        Assert.Equal("prefix:x", fallback);
    }

    [Fact]
    public void Recursive_Grammar_Uses_Per_Call_Configuration()
    {
        Assert.True(ParameterizedGrammars.TryParseRecursiveChoice("((x))", enabled: true, out var enabled));
        Assert.Equal("x", enabled);
        Assert.False(ParameterizedGrammars.TryParseRecursiveChoice("(x)", enabled: false, out _));
        Assert.True(ParameterizedGrammars.TryParseRecursiveChoice("x", enabled: false, out var disabled));
        Assert.Equal("x", disabled);
    }

    [Fact]
    public void Tuple_Array_And_Nullable_Configuration_Are_Not_Baked_In()
    {
        Assert.True(ParameterizedGrammars.TryParseValues("x", (2, 3), new[,] { { 4 } }, null, out var first));
        Assert.True(ParameterizedGrammars.TryParseValues("x", (2, 3), new[,] { { 4 } }, 5, out var second));
        Assert.Equal(9, first);
        Assert.Equal(14, second);

        Assert.True(ParameterizedGrammars.TryParseNullable("x", null, out var nullValue));
        Assert.Null(nullValue);
        Assert.True(ParameterizedGrammars.TryParseNullable("x", "value", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Overloaded_Factory_Names_Generate_Independent_Entry_Points()
    {
        Assert.True(ParameterizedGrammars.TryParseOverloadedDefault("default", out var defaultValue));
        Assert.Equal("default", defaultValue);
        Assert.True(ParameterizedGrammars.TryParseOverloadedBoolean("yes", enabled: true, out var booleanValue));
        Assert.Equal("yes", booleanValue);
        Assert.True(ParameterizedGrammars.TryParseOverloadedInteger("x", 42, out var integerValue));
        Assert.Equal("42", integerValue);
    }

    [Fact]
    public void Mutable_Struct_Configuration_Remains_Addressable_Within_A_Parse()
    {
        Assert.True(ParameterizedGrammars.TryParseCountedPair("xx", default, out var value));
        Assert.Equal(12, value);
    }

    [Fact]
    public void Captures_Preserve_Symbol_Identity_Nameof_And_Inferred_Names()
    {
        Assert.True(ParameterizedGrammars.TryParseSymbols("x", "bound", 7, out var symbols));
        Assert.Equal("bound:7:context", symbols);
        Assert.True(ParameterizedGrammars.TryParseInferredNames("x", "value", out var inferred));
        Assert.Equal("valuevalue", inferred);
    }

    [Fact]
    public void Runtime_Helpers_Live_In_Compiled_Code()
    {
        Assert.True(ParameterizedGrammars.TryParseEnclosingMembers("x", "prefix:", out var value));
        Assert.Equal("prefix:grammar:x", value);
        Assert.True(ParameterizedGrammars.TryParseMethodGroup("x", out var methodGroup));
        Assert.Equal("grammar:x", methodGroup);
    }

    [Fact]
    public void User_Callback_Exceptions_Propagate()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ParameterizedGrammars.TryParseCallback("x", new CallbackOptions { Throw = true }, out _));
        Assert.Equal("callback failed", exception.Message);
    }

    [Fact]
    public void Explicit_Grammar_Errors_Return_False_And_Default()
    {
        Assert.False(ParameterizedGrammars.TryParseRequiredBang("x", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Generated_Configuration_Uses_The_Existing_Execution_Context()
    {
        var wrappers = typeof(ParameterizedGrammars).GetNestedTypes(BindingFlags.NonPublic)
            .Where(static type => type.Name.StartsWith("GeneratedParser_", StringComparison.Ordinal) && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(wrappers);
        Assert.All(wrappers, static wrapper =>
        {
            Assert.True(wrapper.IsSealed);
            Assert.Equal("Parlot.Generated.Fluent.ParseContext", wrapper.BaseType!.FullName);
        });
    }
}

public sealed class ConditionalOptions
{
    private bool _enabled;

    public int Evaluations { get; private set; }

    public bool Enabled
    {
        get
        {
            Evaluations++;
            return _enabled;
        }
        set => _enabled = value;
    }
}

public sealed class CallbackOptions
{
    public bool Throw { get; init; }
}

public static class ParameterizedCallbacks
{
    public static string Format(char value) => "grammar:" + value;

    public static string Transform(CallbackOptions options, char value)
    {
        if (options.Throw)
        {
            throw new InvalidOperationException("callback failed");
        }

        return value.ToString();
    }
}

public struct ConditionalCounter
{
    private int _value;

    public int Next() => ++_value;
}

public static partial class ParameterizedGrammars
{
    internal const string Name = "grammar";

    public static partial bool TryParseChoice(string text, ConditionalOptions options, out string value);
    public static partial bool TryParseBacktracking(string text, bool enabled, out string value);
    public static partial bool TryParseMultiple(string text, int first, int second, string prefix, out string value);
    public static partial bool TryParseSelected(string text, int index, string expected, out string value);
    public static partial bool TryParseSwitched(string text, int index, out string value);
    public static partial bool TryParseFallback(string text, string prefix, out string value);
    public static partial bool TryParseRecursiveChoice(string text, bool enabled, out string value);
    public static partial bool TryParseValues(
        string text,
        (int First, int Second) pair,
        int[,] values,
        int? optional,
        out int value);
    public static partial bool TryParseNullable(string text, string? configuredValue, out string? value);
    public static partial bool TryParseEnclosingMembers(string text, string prefix, out string value);
    public static partial bool TryParseCallback(string text, CallbackOptions options, out string value);
    public static partial bool TryParseRequiredBang(string text, out string value);
    public static partial bool TryParseOverloadedDefault(string text, out string value);
    public static partial bool TryParseOverloadedBoolean(string text, bool enabled, out string value);
    public static partial bool TryParseOverloadedInteger(string text, int number, out string value);
    public static partial bool TryParseCountedPair(string text, ConditionalCounter counter, out int value);
    public static partial bool TryParseSymbols(string text, string context, int @class, out string value);
    public static partial bool TryParseInferredNames(string text, string configuredValue, out string value);
    public static partial bool TryParseMethodGroup(string text, out string value);
}
