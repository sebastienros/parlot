using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

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

    [Fact]
    public void OptionEqualityAndHelpersShouldWork()
    {
        var none = Option<string>.None;
        var someNull = new Option<string>(null);
        var someA = new Option<string>("a");
        var someA2 = new Option<string>("a");
        var someB = new Option<string>("b");

        Assert.False(none.HasValue);
        Assert.Null(none.OrDefault());
        Assert.Equal("a", someA.OrDefault());
        Assert.Equal(default, Option<int>.None.OrDefault());
        Assert.Equal(42, new Option<int>(42).OrDefault());

        Assert.True(none == default);
        Assert.True(none != someNull);
        Assert.True(someA == someA2);
        Assert.True(someA != someB);
        Assert.True(someA.Equals((object)someA2));
        Assert.False(someA.Equals("a"));
        Assert.Equal(someA.GetHashCode(), someA2.GetHashCode());
        Assert.NotEqual(none.GetHashCode(), someNull.GetHashCode());
        Assert.Equal("a", someA.ToString());
        Assert.Equal(string.Empty, none.ToString());
    }

    [Fact]
    public void TextPositionEqualityShouldWork()
    {
        var p1 = new TextPosition(10, 2, 5);
        var p2 = new TextPosition(10, 2, 5);
        var p3 = new TextPosition(11, 2, 6);

        Assert.True(p1 == p2);
        Assert.False(p1 != p2);
        Assert.True(p1 != p3);
        Assert.True(p1.Equals(p2));
        Assert.True(p1.Equals((object)p2));
        Assert.False(p1.Equals("not a position"));
        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void FluentFactoryAndContextOverloadErgonomicsShouldWork()
    {
        // Cached builder singletons
        Assert.Same(Literals, Literals);
        Assert.Same(Terms, Terms);

        // Parsers.Optional static factory
        var opt = Optional(Terms.Integer());
        Assert.Equal(new Option<long>(42), opt.Parse(" 42"));
        Assert.Equal(Option<long>.None, opt.Parse("abc"));

        // Custom-quote String overloads on Literals and Terms
        Assert.Equal("a%b", Literals.String('%').Parse("%a\\%b%").ToString());
        Assert.Equal("a\\%b", Literals.String('%', returnDecoded: false).Parse("%a\\%b%").ToString());
        Assert.Equal("hello", Terms.String('`').Parse("  `hello`").ToString());

        // Deferred<T>.Named returns Deferred<T>
        var expr = Deferred<long>().Named("expr");
        expr.Parser = Terms.Integer();
        Assert.Equal("expr", expr.Name);
        Assert.Equal(7, expr.Parse(" 7"));

        // Select.ToString includes branch descriptions
        var selected = Select(static () => 0, Literals.Char('a'), Literals.Char('b'));
        Assert.Contains("Char('a')", selected.ToString());
        Assert.Contains("Char('b')", selected.ToString());

        // TryParse(ParseContext, out T?) and TryMatch(ParseContext, out TextSpan)
        var parser = Terms.Text("hello").When(static (_, _) => false, "rejected");
        var ctxFail = new ParseContext(new Scanner("hello"));
        Assert.False(parser.TryParse(ctxFail, out var failedValue));
        Assert.Null(failedValue);
        Assert.Equal(0, ctxFail.Scanner.Cursor.Offset);

        var ctxMatchFail = new ParseContext(new Scanner("hello"));
        Assert.False(parser.TryMatch(ctxMatchFail, out var failedMatch));
        Assert.Equal(0, failedMatch.Length);
        Assert.Equal(0, ctxMatchFail.Scanner.Cursor.Offset);

        var okParser = Terms.Text("hello");
        var ctxOk = new ParseContext(new Scanner("  hello!"));
        Assert.True(okParser.TryParse(ctxOk, out var parsedValue));
        Assert.Equal("hello", parsedValue);
        Assert.Equal(7, ctxOk.Scanner.Cursor.Offset);

        var ctxMatchOk = new ParseContext(new Scanner("  hello!"));
        Assert.True(okParser.TryMatch(ctxMatchOk, out var matchedSpan));
        Assert.Equal("  hello", matchedSpan.ToString());
        Assert.Equal(7, ctxMatchOk.Scanner.Cursor.Offset);

        // Then<U>() on non-IConvertible sealed/struct type returns constant default
        var spanToInt = Literals.Identifier().Then<int>();
        Assert.Equal(0, spanToInt.Parse("abc"));
    }
}
