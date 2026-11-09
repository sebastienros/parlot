using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// Parsers that can start with many non-ASCII chars declare them as <see cref="OneOf{T}.NonAsciiSeekableChar"/>, so that a
/// choice keeps an exact entry for each ASCII char and invokes them for any non-ASCII char.
/// </summary>
public class NonAsciiSeekTests
{
    private const char NonAscii = Fluent.OneOf<object>.NonAsciiSeekableChar;

    // Like JavaScript's ID_Start and ID_Continue, in the BMP
    private static readonly string IdentifierStart = CreateChars(static c => c is '$' or '_' || char.IsLetter(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber);

    private static readonly string IdentifierPart = CreateChars(static c => c is '$' or '_' || char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c)
        is UnicodeCategory.LetterNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation);

    private static string CreateChars(Func<char, bool> predicate)
    {
        var builder = new StringBuilder();

        for (var c = 0; c <= char.MaxValue; c++)
        {
            if (!char.IsSurrogate((char)c) && predicate((char)c))
            {
                builder.Append((char)c);
            }
        }

        return builder.ToString();
    }

    private static int ScanIdentifier(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty || IdentifierStart.IndexOf(span[0]) < 0)
        {
            return 0;
        }

        var length = 1;

        while (length < span.Length && IdentifierPart.IndexOf(span[length]) >= 0)
        {
            length++;
        }

        return length;
    }

    private static Parser<TextSpan> UnicodeIdentifier() => Literals.Scan(ScanIdentifier, IdentifierStart.AsSpan());

    private static ISeekable Seekable<T>(Parser<T> parser) => Assert.IsAssignableFrom<ISeekable>(parser);

    // An alternative that counts its invocations, with the hints of its parser
    private sealed class Counting<T> : Parser<T>, ISeekable
    {
        private readonly Parser<T> _parser;

        public Counting(Parser<T> parser) => _parser = parser;

        public int Count { get; set; }

        public bool CanSeek => Seekable(_parser).CanSeek;

        public char[] ExpectedChars => Seekable(_parser).ExpectedChars;

        public bool SkipWhitespace => Seekable(_parser).SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);
            Count++;
            var success = _parser.Parse(context, ref result);
            context.ExitParser(this);
            return success;
        }
    }

    [Fact]
    public void LargeNonAsciiStartSetsAreDeclaredAsAnyNonAsciiChar()
    {
        var chars = Seekable(UnicodeIdentifier()).ExpectedChars;

        Assert.Contains(NonAscii, chars);
        Assert.Contains('a', chars);
        Assert.Contains('$', chars);
        Assert.DoesNotContain('é', chars);
        Assert.DoesNotContain('(', chars);
        Assert.All(chars, static c => Assert.True(c < 0x80 || c == NonAscii));

        // A few non-ASCII chars are kept, ASCII ones are unchanged
        Assert.Equal(['a', 'é', 'b'], Seekable(Literals.Scan(ScanIdentifier, "aébé".AsSpan())).ExpectedChars);
        Assert.Equal(['a', 'b'], Seekable(Literals.Scan(ScanIdentifier, "ab".AsSpan())).ExpectedChars);
        Assert.Equal(['x', NonAscii], Seekable(Literals.Pattern(char.IsLetter).Lookup(false, "x" + IdentifierStart.Substring(100))).ExpectedChars.OrderBy(static c => c));
    }

    [Fact]
    public void ChoicesDispatchAsciiCharsExactly()
    {
        var identifier = new Counting<TextSpan>(UnicodeIdentifier());
        var choice = OneOf(
            Literals.Char('(').Then(static _ => new TextSpan("(")),
            Literals.Char(')').Then(static _ => new TextSpan(")")),
            Literals.String(),
            identifier);

        // The choice forwards the hint rather than every non-ASCII char
        var chars = Seekable(choice).ExpectedChars;
        Assert.Contains(NonAscii, chars);
        Assert.True(chars.Length < 128);

        Assert.Equal("(", choice.Parse("("));
        Assert.Equal("a", choice.Parse("\"a\"").ToString());
        Assert.Equal(0, identifier.Count);

        // ASCII chars which can't start an identifier don't invoke it
        Assert.False(choice.TryParse("#", out _));
        Assert.False(choice.TryParse("1", out _));
        Assert.Equal(0, identifier.Count);

        foreach (var text in new[] { "abc", "éa", "ℵ0", "中文", "Ωmega", "_x", "$" })
        {
            Assert.Equal(text, choice.Parse(text).ToString());
        }

        Assert.Equal(7, identifier.Count);

        // Non-ASCII chars that can't start an identifier invoke it, and it fails
        Assert.False(choice.TryParse("€", out _));
        Assert.False(choice.TryParse(" ", out _));
        Assert.Equal(9, identifier.Count);
    }

    [Fact]
    public void ChoicesKeepTheOrderOfTheAlternatives()
    {
        var identifier = UnicodeIdentifier().Then(static x => "id:" + x);
        var keyword = Literals.Text("é").Then(static _ => "keyword");
        var any = Literals.Pattern(static c => c == 'é' || c == '#', maxSize: 1).Then(static _ => "any");

        Assert.Equal("id:é", OneOf(identifier, keyword).Parse("é"));
        Assert.Equal("keyword", OneOf(keyword, identifier).Parse("é"));
        Assert.Equal("id:ü", OneOf(keyword, identifier).Parse("ü"));

        // An alternative which can start with any char is tried in its place for ASCII and non-ASCII chars
        Assert.Equal("any", OneOf(any, identifier).Parse("é"));
        Assert.Equal("id:é", OneOf(identifier, any).Parse("é"));
        Assert.Equal("any", OneOf(identifier, any).Parse("#"));
        Assert.Equal("id:ü", OneOf(any, identifier, keyword).Parse("ü"));
        Assert.Equal("never", OneOf(keyword.Then(static _ => "never"), any, identifier).Parse("é"));
    }

    [Fact]
    public void NestedChoicesForwardTheHint()
    {
        var identifier = new Counting<TextSpan>(UnicodeIdentifier());
        var inner = OneOf(Literals.Text("if").Then(static x => new TextSpan(x)), identifier);
        var outer = OneOf(Literals.Char('(').Then(static _ => new TextSpan("(")), inner, Literals.Text("€").Then(static x => new TextSpan(x)));

        Assert.Contains(NonAscii, Seekable(inner).ExpectedChars);
        Assert.Contains(NonAscii, Seekable(outer).ExpectedChars);
        Assert.Contains('€', Seekable(outer).ExpectedChars);

        Assert.Equal("(", outer.Parse("("));
        Assert.Equal("if", outer.Parse("if"));
        Assert.Equal(0, identifier.Count);

        Assert.Equal("ℵ", outer.Parse("ℵ"));
        Assert.Equal("€", outer.Parse("€"));
        Assert.Equal(2, identifier.Count);
    }

    [Fact]
    public void DeferredAlternativesForwardTheHint()
    {
        var deferred = Deferred<TextSpan>();
        var choice = OneOf(Terms.Char('(').Then(static _ => new TextSpan("(")), deferred);
        deferred.Parser = Terms.Scan(ScanIdentifier, IdentifierStart.AsSpan());

        Assert.Equal("中", choice.Parse(" 中"));
        Assert.Equal("(", choice.Parse(" ("));
        Assert.Contains(NonAscii, Seekable(choice).ExpectedChars);
    }

    [Fact]
    public void FailingChoicesRestoreTheCursor()
    {
        var choice = OneOf(Terms.Char('(').Then(static _ => new TextSpan("(")), Terms.Scan(ScanIdentifier, IdentifierStart.AsSpan()));

        foreach (var text in new[] { " #", " €", " ", "" })
        {
            var context = new ParseContext(new Scanner(text));
            var result = new ParseResult<TextSpan>();

            Assert.False(choice.Parse(context, ref result));
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
    }

    [Fact]
    public void ReadersDispatchNonAsciiChars()
    {
        var choice = ZeroOrMany(OneOf(Terms.Char('(').Then(static _ => new TextSpan("(")), Terms.Scan(ScanIdentifier, IdentifierStart.AsSpan())));

        Assert.Equal(["(", "éa", "(", "中文"], choice.Parse(new StringReader("( éa ( 中文")).Select(static x => x.ToString()));
    }

    [Fact]
    public void TextBeforeDoesNotJumpOverNonAsciiChars()
    {
        // The delimiter can start with any non-ASCII char, which the text isn't searched for
        Assert.Equal("12 ", AnyCharBefore(Literals.Scan(ScanIdentifier, IdentifierStart.AsSpan())).Parse("12 ü").ToString());
    }

    [Fact]
    public void ChoicesOfUnicodeIdentifiersAreBuiltFast()
    {
        var stopwatch = Stopwatch.StartNew();
        var identifier = UnicodeIdentifier();

        // Like the choices of a JavaScript grammar which can start with an identifier
        for (var i = 0; i < 100; i++)
        {
            var choice = OneOf(Literals.Text("if").Then(static x => new TextSpan(x)), Literals.Char('(').Then(static _ => new TextSpan("(")), identifier);
            Assert.True(Seekable(choice).ExpectedChars.Length < 128);
        }

        // Each choice built a table of every identifier start char before, about 20 ms
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), stopwatch.Elapsed.ToString());
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void IdentifiersDeclareAnyNonAsciiChar()
    {
        var fromChars = Seekable(Literals.Identifier(IdentifierStart.AsSpan(), IdentifierPart.AsSpan()));

        Assert.True(fromChars.CanSeek);
        Assert.Contains(NonAscii, fromChars.ExpectedChars);
        Assert.All(fromChars.ExpectedChars, static c => Assert.True(c < 0x80 || c == NonAscii));

        // The start chars of search values are found from them
        var fromSearchValues = Seekable(Literals.Identifier(System.Buffers.SearchValues.Create(IdentifierStart), System.Buffers.SearchValues.Create(IdentifierPart)));

        Assert.True(fromSearchValues.CanSeek);
        Assert.Equal(fromChars.ExpectedChars.OrderBy(static c => c), fromSearchValues.ExpectedChars.OrderBy(static c => c));

        var ascii = Seekable(Literals.Identifier(System.Buffers.SearchValues.Create("ab"), System.Buffers.SearchValues.Create("abc")));
        Assert.Equal(['a', 'b'], ascii.ExpectedChars);

        var few = Seekable(Literals.Identifier(System.Buffers.SearchValues.Create("aé"), System.Buffers.SearchValues.Create("abc")));
        Assert.Equal(['a', 'é'], few.ExpectedChars);

        var identifier = new Counting<TextSpan>(Terms.Identifier(IdentifierStart.AsSpan(), IdentifierPart.AsSpan()));
        var choice = ZeroOrMany(OneOf(Terms.Text("if").Then(static x => new TextSpan(x)), Terms.Char('(').Then(static _ => new TextSpan("(")), Terms.Char(')').Then(static _ => new TextSpan(")")), identifier));

        Assert.Equal(["if", "(", "é", ")", "ℵ0", "中文"], choice.Parse("if (é) ℵ0 中文").Select(static x => x.ToString()));

        // The identifier isn't invoked before 'if', '(' and ')'
        Assert.Equal(3, identifier.Count);
    }
#endif
}
