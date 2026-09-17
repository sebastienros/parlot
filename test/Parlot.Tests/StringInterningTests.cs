using System;
using System.Threading.Tasks;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class StringInterningTests
{
    [Fact]
    public void WholeStringsAndCanonicalLiteralsKeepExistingInstances()
    {
        var text = new string('z', 20);
        Assert.Same(text, new TextSpan(text).ToString());
        Assert.Same(text, Character.DecodeString(text));
        Assert.Same(text, Literals.Text(text, caseInsensitive: true).Parse(text.ToUpperInvariant()));
        Assert.Equal(string.Empty, default(TextSpan).ToString());
        Assert.Equal(string.Empty, new TextSpan("abc", 1, 0).ToString());
    }

    [Fact]
    public void MaterializationAndDecodingShareTheSameCache()
    {
        var token = Guid.NewGuid().ToString("N") + "\n雪😀";
        var buffer = "[" + token + "]";
        var span = new TextSpan(buffer, 1, token.Length);
        _ = span.ToString();
        var admitted = span.ToString();
        Assert.Same(admitted, span.ToString());
        Assert.Same(admitted, Character.DecodeString(token.Replace("\n", @"\n")));
    }

    [Fact]
    public void MatchedTextUsesTheCacheAndPreservesCasing()
    {
        var token = Guid.NewGuid().ToString("N");
        var matched = token.ToUpperInvariant();
        var parser = Literals.Text(token, caseInsensitive: true, returnMatchedText: true);
        _ = parser.Parse(matched + "!");
        var admitted = parser.Parse(matched + "!");
        Assert.Equal(matched, admitted);
        Assert.Same(admitted, parser.Parse(matched + "!"));
    }

    [Fact]
    public void StringsBeyondLimitAreNotRetained()
    {
        var buffer = "[" + new string('x', 257) + "]";
        var span = new TextSpan(buffer, 1, 257);
        Assert.NotSame(span.ToString(), span.ToString());
    }

    [Fact]
    public void FailedStringRestoresCursor()
    {
        var context = new ParseContext(new Scanner("\"unterminated"));
        var result = default(ParseResult<TextSpan>);
        Assert.False(Literals.String().Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ConcurrentMaterializationKeepsExactValues()
    {
        Parallel.For(0, 10000, i =>
        {
            var expected = "雪" + i % 521 + "\0😀";
            var buffer = "[" + expected + "]";
            Assert.Equal(expected, new TextSpan(buffer, 1, expected.Length).ToString());
        });
    }
}
