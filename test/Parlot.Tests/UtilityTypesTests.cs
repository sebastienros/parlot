using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;

namespace Parlot.Tests;

public class UtilityTypesTests
{
    [Fact]
    public void OptionShouldWrapValue()
    {
        var option = new Option<int>(42);

        Assert.True(option.HasValue);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void OptionShouldIndicateNoValue()
    {
        var option = new Option<int>();

        Assert.False(option.HasValue);
        Assert.Equal(default(int), option.Value);
    }

    [Fact]
    public void OptionTryGetValueShouldSucceed()
    {
        var option = new Option<string>("hello");

        Assert.True(option.TryGetValue(out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void OptionTryGetValueShouldFailWhenEmpty()
    {
        var option = new Option<string>();

        Assert.False(option.TryGetValue(out var value));
        Assert.Null(value);
    }

    [Fact]
    public void OptionOrSomeShouldReturnValueWhenSet()
    {
        var option = new Option<int>(42);

        Assert.Equal(42, option.OrSome(10));
    }

    [Fact]
    public void OptionOrSomeShouldReturnDefaultWhenNotSet()
    {
        var option = new Option<int>();

        Assert.Equal(10, option.OrSome(10));
    }

    [Fact]
    public void OptionOrSomeShouldHandleNullableTypes()
    {
        var option = new Option<int?>();

        Assert.Null(option.OrSome((int?)null));
    }

    [Fact]
    public void ParseResultShouldInitializeWithConstructor()
    {
        var result = new ParseResult<string>(5, 10, "test");

        Assert.Equal(5, result.Start);
        Assert.Equal(10, result.End);
        Assert.Equal("test", result.Value);
    }

    [Fact]
    public void ParseResultSetShouldUpdateValues()
    {
        var result = new ParseResult<string>();
        result.Set(15, 25, "updated");

        Assert.Equal(15, result.Start);
        Assert.Equal(25, result.End);
        Assert.Equal("updated", result.Value);
    }

    [Fact]
    public void TextSpanShouldCreateFromString()
    {
        var span = new TextSpan("hello");

        Assert.Equal(5, span.Length);
        Assert.Equal(0, span.Offset);
        Assert.Equal("hello", span.Buffer);
        Assert.Equal("hello", span.ToString());
    }

    [Fact]
    public void TextSpanShouldCreateFromSubstring()
    {
        var span = new TextSpan("hello world", 6, 5);

        Assert.Equal(5, span.Length);
        Assert.Equal(6, span.Offset);
        Assert.Equal("hello world", span.Buffer);
        Assert.Equal("world", span.ToString());
    }

    [Fact]
    public void TextSpanShouldHandleNullBuffer()
    {
        var span = new TextSpan(null);

        Assert.Equal(0, span.Length);
        Assert.Null(span.Buffer);
        Assert.Equal("", span.ToString());
    }

    [Fact]
    public void TextSpanEqualsShouldCompareStrings()
    {
        var span = new TextSpan("hello world", 6, 5);

        Assert.True(span.Equals("world"));
        Assert.False(span.Equals("hello"));
    }

    [Fact]
    public void TextSpanEqualsShouldHandleNull()
    {
        var span = new TextSpan(null);

        Assert.True(span.Equals((string)null!));
        Assert.False(span.Equals("test"));
    }

    [Fact]
    public void TextSpanEqualsShouldCompareTextSpans()
    {
        var span1 = new TextSpan("hello");
        var span2 = new TextSpan("hello");
        var span3 = new TextSpan("world");

        Assert.True(span1.Equals(span2));
        Assert.False(span1.Equals(span3));
    }

    [Fact]
    public void TextSpanOperatorsShouldWork()
    {
        var span1 = new TextSpan("hello");
        var span2 = new TextSpan("hello");
        var span3 = new TextSpan("world");

        Assert.True(span1 == span2);
        Assert.False(span1 == span3);
        Assert.True(span1 != span3);
        Assert.False(span1 != span2);
    }

    [Fact]
    public void TextSpanImplicitOperatorShouldWork()
    {
        TextSpan span = "hello";

        Assert.Equal("hello", span.ToString());
    }

    [Fact]
    public void TextSpanObjectEqualsShouldWork()
    {
        var span1 = new TextSpan("hello");
        object span2 = new TextSpan("hello");
        object notSpan = "hello";

        Assert.True(span1.Equals(span2));
        Assert.False(span1.Equals(notSpan));
    }

    [Fact]
    public void TextSpanGetHashCodeShouldWork()
    {
        var span1 = new TextSpan("hello");
        var span2 = new TextSpan("hello");

        Assert.Equal(span1.GetHashCode(), span2.GetHashCode());
    }

    [Fact]
    public void TextSpanSpanPropertyShouldWork()
    {
        var span = new TextSpan("hello world", 6, 5);
        var readOnlySpan = span.Span;

        Assert.Equal(5, readOnlySpan.Length);
        Assert.Equal("world", new string(readOnlySpan));
    }

    [Fact]
    public void TextSpanSpanPropertyShouldHandleNullBuffer()
    {
        var span = new TextSpan(null);
        var readOnlySpan = span.Span;

        Assert.Equal(0, readOnlySpan.Length);
    }



    [Fact]
    public void CharToStringTableShouldCacheCommonChars()
    {
        // Test the internal CharToStringTable class through reflection
        var type = typeof(Scanner).Assembly.GetType("Parlot.CharToStringTable");
        Assert.NotNull(type);

        var method = type!.GetMethod("GetString", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { 'a' });
        Assert.Equal("a", result);

        var result2 = method.Invoke(null, new object[] { 'Z' });
        Assert.Equal("Z", result2);
    }

    [Fact]
    public void CharToStringTableShouldHandleNonCachedChars()
    {
        // Test characters beyond the cache size
        var type = typeof(Scanner).Assembly.GetType("Parlot.CharToStringTable");
        var method = type!.GetMethod("GetString", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var result = method!.Invoke(null, new object[] { 'α' }); // Greek alpha
        Assert.Equal("α", result);
    }
}
