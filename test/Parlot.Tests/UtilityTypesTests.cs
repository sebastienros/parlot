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
        var buffer = new string(['h', 'e', 'l', 'l', 'o']);
        var span = new TextSpan(buffer);

        Assert.Equal(5, span.Length);
        Assert.Equal(0, span.Offset);
        Assert.Equal("hello", span.Buffer);
        Assert.Equal("hello", span.ToString());
        Assert.Same(buffer, span.ToString());
    }

    [Fact]
    public void TextSpanShouldReuseEntireBufferAndEmptyBuffer()
    {
        var buffer = new string(['h', 'e', 'l', 'l', 'o']);

        Assert.Same(buffer, new TextSpan(buffer, 0, buffer.Length).ToString());
        Assert.Same(string.Empty, new TextSpan(string.Empty, 0, 0).ToString());
        Assert.Equal(string.Empty, new TextSpan(buffer, 2, 0).ToString());
    }

    [Fact]
    public void TextSpanShouldShareSingleAsciiChars()
    {
        var buffer = "a=b, a\u00e9";

        Assert.Same(new TextSpan(buffer, 0, 1).ToString(), new TextSpan(buffer, 5, 1).ToString());
        Assert.Same(new TextSpan(buffer, 1, 1).ToString(), new TextSpan("=", 0, 1).ToString());
        Assert.Equal("a", new TextSpan(buffer, 0, 1).ToString());
        Assert.Equal("\0", new TextSpan("\0", 0, 1).ToString());

        // Other chars are new strings
        Assert.Equal("\u00e9", new TextSpan(buffer, 6, 1).ToString());
        Assert.NotSame(new TextSpan(buffer, 6, 1).ToString(), new TextSpan(buffer, 6, 1).ToString());

        // An invalid span still throws
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new TextSpan(buffer, 7, 1).ToString());
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new TextSpan(buffer, -1, 1).ToString());
    }

    [Fact]
    public void DecodedSingleAsciiCharsAreShared()
    {
        Assert.Same(Character.DecodeString("\\n"), Character.DecodeString("\\n"));
        Assert.Equal("\n", Character.DecodeString("\\n"));
        Assert.Equal("\u00e9", Character.DecodeString("\\u00e9"));
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
        // new string(ReadOnlySpan<char>) is .NET Core 2.1+. Constructors cannot be polyfilled as
        // extension members, so the call site uses ToString() instead.
        Assert.Equal("world", readOnlySpan.ToString());
    }

    [Fact]
    public void TextSpanSpanPropertyShouldHandleNullBuffer()
    {
        var span = new TextSpan(null);
        var readOnlySpan = span.Span;

        Assert.Equal(0, readOnlySpan.Length);
    }
}
