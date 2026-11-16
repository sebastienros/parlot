#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.IO;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class InterpolatedTests
{
    /// <summary>
    /// A JavaScript template literal, whose holes hold identifiers or other templates.
    /// </summary>
    public static Parser<string> CreateTemplate(InterpolationOptions options = InterpolationOptions.None)
    {
        var expression = Deferred<string>();
        var template = Terms.Interpolated("`", "`", "${", "}", expression, options).Then(static x => Render(x));
        expression.Parser = OneOf(Terms.Identifier().Then(static x => x.ToString()), template);
        return template;
    }

    /// <summary>
    /// A C# interpolated string, without new lines, whose holes hold identifiers.
    /// </summary>
    public static Parser<string> CreateInterpolatedString()
        => Terms.Interpolated("$\"", "\"", "{", "}", Terms.Identifier().Then(static x => x.ToString()), InterpolationOptions.NoNewLines).Then(static x => Render(x));

    /// <summary>
    /// Renders the chunks between brackets and the holes between braces.
    /// </summary>
    public static string Render(Interpolation<string> value)
    {
        Assert.Equal(value.Holes.Count + 1, value.Chunks.Count);

        var result = "[" + value.Chunks[0] + "]";

        for (var i = 0; i < value.Holes.Count; i++)
        {
            result += "{" + value.Holes[i] + "}[" + value.Chunks[i + 1] + "]";
        }

        return result;
    }

    [Theory]
    [InlineData("``", "[]")]
    [InlineData("`abc`", "[abc]")]
    [InlineData("`${a}`", "[]{a}[]")]
    [InlineData("`${a}b`", "[]{a}[b]")]
    [InlineData("`a${b}`", "[a]{b}[]")]
    [InlineData("`a${b}${c}d`", "[a]{b}[]{c}[d]")]
    [InlineData("`a${b}c${d}e`", "[a]{b}[c]{d}[e]")]
    [InlineData("`a\\`b`", "[a\\`b]")]
    [InlineData("`a\\${b}`", "[a\\${b}]")]
    [InlineData("`a\\\\${b}`", "[a\\\\]{b}[]")]
    [InlineData("`a$b`", "[a$b]")]
    [InlineData("`$`", "[$]")]
    [InlineData("`a$`", "[a$]")]
    [InlineData("`$$${b}`", "[$$]{b}[]")]
    [InlineData("`{a}`", "[{a}]")]
    [InlineData("`a}b`", "[a}b]")]
    [InlineData("`a\nb\r\nc`", "[a\nb\r\nc]")]
    [InlineData("`${ a }`", "[]{a}[]")]
    [InlineData("`${a\n}`", "[]{a}[]")]
    [InlineData("  `a`", "[a]")]
    [InlineData("`a${`b${c}d`}e`", "[a]{[b]{c}[d]}[e]")]
    [InlineData("`${`${`${a}`}`}`", "[]{[]{[]{a}[]}[]}[]")]
    public void ShouldParseTemplates(string text, string expected)
    {
        var template = CreateTemplate();

        Assert.True(template.TryParse(text, out var value));
        Assert.Equal(expected, value);

        // The same from a stream, read a char at a time
        Assert.True(template.TryParse(new OneCharReader(text), out value, new StreamParseOptions { BufferSize = 1 }));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("`")]
    [InlineData("`abc")]
    [InlineData("`a\\`")]
    [InlineData("`a\\")]
    [InlineData("`a$")]
    [InlineData("`a${")]
    [InlineData("`a${b")]
    [InlineData("`a${b`")]
    [InlineData("`a${}`")]
    [InlineData("`a${b c}`")]
    [InlineData("`a${`b`")]
    [InlineData("'a'")]
    [InlineData("a`")]
    public void ShouldFailWithTheCursorWhereItStarted(string text)
    {
        var template = CreateTemplate();
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<string>();

        Assert.False(template.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        Assert.False(template.TryParse(new OneCharReader(text), out _, new StreamParseOptions { BufferSize = 1 }));
    }

    [Fact]
    public void ShouldFailWithTheCursorWhereItStartedAfterOtherText()
    {
        var parser = Terms.Identifier().And(ZeroOrOne(CreateTemplate()));
        var context = new ParseContext(new Scanner("tag `a${b"));
        var result = new ParseResult<(TextSpan, System.Collections.Generic.IReadOnlyList<string>)>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Empty(result.Value.Item2);
        Assert.Equal(3, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldReturnTheRawChunksAndTheirOffsets()
    {
        var parser = Literals.Interpolated("`", "`", "${", "}", Terms.Identifier());

        Assert.True(parser.TryParse("`a\\n${ b }c`", out var value));
        Assert.Equal(["a\\n", "c"], value.Chunks.Select(static x => x.ToString()));
        Assert.Equal(1, value.Chunks[0].Offset);
        Assert.Equal(10, value.Chunks[1].Offset);
        Assert.Equal("b", value.Holes.Single().ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(40)]
    public void ShouldKeepTheChunksAndHolesApartWhenTheHolesAreTextSpans(int count)
    {
        // The holes are TextSpans, like the chunks: their lists, larger than a list keeps inline, use the same stack
        var parser = Literals.Interpolated("`", "`", "${", "}", Terms.Identifier());
        var text = "`c0" + string.Concat(Enumerable.Range(1, count).Select(static i => $"${{h{i}}}c{i}")) + "`";

        Assert.True(parser.TryParse(text, out var value));
        Assert.Equal(Enumerable.Range(0, count + 1).Select(static i => $"c{i}"), value.Chunks.Select(static x => x.ToString()));
        Assert.Equal(Enumerable.Range(1, count).Select(static i => $"h{i}"), value.Holes.Select(static x => x.ToString()));
        Assert.Equal(0, ScratchCount<TextSpan>());
        Assert.Equal(0, ScratchCount<(TextSpan, TextSpan)>());

        // The same with a hole which isn't closed, then a text which is read after it
        Assert.False(parser.TryParse(text.Substring(0, text.Length - 3), out _));
        Assert.Equal(0, ScratchCount<TextSpan>());
        Assert.Equal(0, ScratchCount<(TextSpan, TextSpan)>());

        Assert.True(ZeroOrMany(Terms.Identifier()).And(SkipWhiteSpace(parser)).TryParse("a b c d e f " + text, out var sequence));
        Assert.Equal(6, sequence.Item1.Count);
        Assert.Equal(count, sequence.Item2.Holes.Count);
        Assert.Equal($"h{count}", sequence.Item2.Holes[count - 1].ToString());
    }

    // The number of items on the scratch stack of the lists of the current thread
    private static int ScratchCount<T>()
    {
        var type = typeof(Parser<>).Assembly.GetType("Parlot.Fluent.ListScratch`1", throwOnError: true)!.MakeGenericType(typeof(T));
        var scratch = type.GetField("t_current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null);
        return scratch == null ? 0 : (int)type.GetField("Count", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(scratch)!;
    }

    [Fact]
    public void ShouldSpanTheDelimitersAndMoveTheCursorAfterThem()
    {
        var parser = Literals.Interpolated("`", "`", "${", "}", Terms.Identifier());
        var context = new ParseContext(new Scanner("`a\r\n${b}\r\nc`d"));
        var result = new ParseResult<Interpolation<TextSpan>>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(0, result.Start);
        Assert.Equal(12, result.End);
        Assert.Equal(12, context.Scanner.Cursor.Offset);
        Assert.Equal(3, context.Scanner.Cursor.Position.Line);
        Assert.Equal(3, context.Scanner.Cursor.Position.Column);
    }

    [Theory]
    [InlineData("`${a}`", true)]
    [InlineData("`${a }`", false)]
    [InlineData("`${a\n}`", false)]
    public void ShouldNotSkipTheWhiteSpaceBeforeTheHoleClose(string text, bool success)
    {
        Assert.Equal(success, CreateTemplate(InterpolationOptions.NoWhiteSpaceBeforeHoleClose).TryParse(text, out _));
    }

    [Theory]
    [InlineData("$\"a{b}c\"", "[a]{b}[c]")]
    [InlineData("$\"a\\\"b\"", "[a\\\"b]")]
    [InlineData("$\"\"", "[]")]
    [InlineData("$\"{ b\n}\"", "[]{b}[]")]
    [InlineData("$\"a\nb\"", null)]
    [InlineData("$\"a\rb\"", null)]
    [InlineData("$\"a\\\nb\"", null)]
    [InlineData("$\"a{b}\n\"", null)]
    [InlineData("$\"a", null)]
    [InlineData("\"a\"", null)]
    [InlineData("$a\"", null)]
    public void ShouldRejectNewLines(string text, string? expected)
    {
        var parser = CreateInterpolatedString();
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<string>();

        Assert.Equal(expected != null, parser.Parse(context, ref result));
        Assert.Equal(expected, result.Value);

        if (expected == null)
        {
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
    }

    [Theory]
    [InlineData("<<a\\>>", "[a\\]")]
    [InlineData("<<a\\>>b>>", "[a\\]")]
    [InlineData("<<a#[[b]]c>>", "[a]{b}[c]")]
    [InlineData("<<a#[b]]>>", "[a#[b]]]")]
    [InlineData("<<a>b#c>>", "[a>b#c]")]
    [InlineData("<<a#[[b]>>", null)]
    public void ShouldReadDelimitersOfSeveralCharsWithoutAnEscape(string text, string? expected)
    {
        var parser = Literals.Interpolated("<<", ">>", "#[[", "]]", Literals.Identifier().Then(static x => x.ToString()), escape: '\0').Then(static x => Render(x));

        Assert.Equal(expected != null, parser.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ShouldUseAnotherEscape()
    {
        var parser = Literals.Interpolated("\"", "\"", "{", "}", Terms.Identifier().Then(static x => x.ToString()), escape: '`').Then(static x => Render(x));

        Assert.True(parser.TryParse("\"a`\"b`{c\\{d}\"", out var value));
        Assert.Equal("[a`\"b`{c\\]{d}[]", value);
    }

    [Fact]
    public void ShouldSeekTheOpeningDelimiter()
    {
        var seekable = (ISeekable)Literals.Interpolated("$\"", "\"", "{", "}", Terms.Identifier());

        Assert.True(seekable.CanSeek);
        Assert.Equal(new[] { '$' }, seekable.ExpectedChars);
        Assert.False(seekable.SkipWhitespace);

        // A choice tells the template from the string by the whole opening delimiter
        var choice = OneOf(
            Terms.Interpolated("$\"", "\"", "{", "}", Terms.Identifier()).Then(static x => "i" + x.Holes.Count),
            Terms.Text("$").Then(static _ => "dollar"),
            Terms.String().Then(static x => "s" + x));

        Assert.Equal("i1", choice.Parse(" $\"a{b}\""));
        Assert.Equal("dollar", choice.Parse(" $x"));
        Assert.Equal("sa", choice.Parse(" \"a\""));
    }

    [Fact]
    public void ShouldValidateTheDelimiters()
    {
        var hole = Terms.Identifier();

        Assert.Throws<ArgumentException>(() => Literals.Interpolated("", "`", "${", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "", "${", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "`", "", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "`", "${", "", hole));
        Assert.Throws<ArgumentNullException>(() => Literals.Interpolated(null!, "`", "${", "}", hole));
        Assert.Throws<ArgumentNullException>(() => Literals.Interpolated<TextSpan>("`", "`", "${", "}", null!));

        // The text couldn't tell its end from a hole, or an escaped delimiter from an escape
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "$", "${", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "${{", "${", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "\\`", "${", "}", hole));
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "`", "\\{", "}", hole));

        // The white space before the hole close is skipped
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "`", "${", " }", hole));
        Assert.True(Literals.Interpolated("`", "`", "${", " }", hole, InterpolationOptions.NoWhiteSpaceBeforeHoleClose).TryParse("`${b }`", out _));

        // A new line which is an escape couldn't be rejected
        Assert.Throws<ArgumentException>(() => Literals.Interpolated("`", "`", "${", "}", hole, InterpolationOptions.NoNewLines, '\n'));
        Assert.NotNull(Literals.Interpolated("`", "`", "${", "}", hole, escape: '\n'));

        // There is one more chunk than there are holes
        Assert.Throws<ArgumentException>(() => new Interpolation<TextSpan>(Array.Empty<TextSpan>(), Array.Empty<TextSpan>()));
        Assert.Empty(default(Interpolation<TextSpan>).Chunks);
        Assert.Empty(default(Interpolation<TextSpan>).Holes);
    }

    [Fact]
    public void ShouldExposeTheChunksAndHolesAsLists()
    {
        var value = new Interpolation<int>([new TextSpan("a"), new TextSpan("b"), new TextSpan("c")], [1, 2]);

        Assert.Equal(["a", "b", "c"], value.Chunks.Select(static x => x.ToString()));
        Assert.Equal([1, 2], value.Holes);
        Assert.Equal(3, value.Chunks.Count);
        Assert.Equal("c", value.Chunks[2].ToString());
        Assert.Equal(2, value.Holes[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => value.Chunks[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => value.Holes[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => value.Holes[-1]);

        var count = 0;
        foreach (var hole in value.Holes)
        {
            count += hole;
        }

        Assert.Equal(3, count);

        var single = new Interpolation<int>([new TextSpan("a")], []);
        Assert.Single(single.Chunks);
        Assert.Empty(single.Holes);

        // A text without a hole doesn't allocate its value
        var parser = Literals.Interpolated("`", "`", "${", "}", Literals.Integer());
        var context = new ParseContext(new Scanner("`abc`"));
        var result = new ParseResult<Interpolation<long>>();
        Assert.True(parser.Parse(context, ref result));
        context.Scanner.Cursor.ResetPosition(0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal("abc", result.Value.Chunks[0].ToString());
    }

    [Fact]
    public void ShouldParseManyTemplatesFromAStream()
    {
        var parser = ZeroOrMany(CreateTemplate()).Eof();
        var text = string.Concat(Enumerable.Range(0, 200).Select(static i => $" `chunk {i} ${{x{i}}} and ${{`nested ${{y}}`}} end`"));

        var expected = parser.Parse(text);

        Assert.NotNull(expected);
        Assert.Equal(200, expected!.Count);
        Assert.Equal(expected, parser.Parse(new StringReader(text), new StreamParseOptions { BufferSize = 16 }));
        Assert.Equal(expected, parser.Parse(new OneCharReader(text), new StreamParseOptions { BufferSize = 1 }));
    }

    /// <summary>
    /// A reader returning one char at a time.
    /// </summary>
    private sealed class OneCharReader : TextReader
    {
        private readonly string _text;
        private int _position;

        public OneCharReader(string text)
        {
            _text = text;
        }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;

        public override int Read(char[] buffer, int index, int count)
        {
            if (count == 0 || _position >= _text.Length)
            {
                return 0;
            }

            buffer[index] = _text[_position++];
            return 1;
        }
    }
}
