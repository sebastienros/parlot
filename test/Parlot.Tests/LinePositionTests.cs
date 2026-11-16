using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class LinePositionTests
{
    private const string JavaScriptNewLines = "\n\r\u2028\u2029";
    private const string CSharpNewLines = "\n\r\u0085\u2028\u2029";

    // The line and column of each offset of a text, its end included, counted char by char from its start
    private static (int Line, int Column)[] References(string text, string newLines)
    {
        var crLf = newLines.Contains('\r') && newLines.Contains('\n');
        var positions = new (int, int)[text.Length + 1];
        var line = 1;
        var column = 1;

        for (var i = 0; i < text.Length; i++)
        {
            positions[i] = (line, column);
            var c = text[i];

            if (newLines.Contains(c) && !(crLf && c == '\r' && i + 1 < text.Length && text[i + 1] == '\n'))
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        positions[text.Length] = (line, column);
        return positions;
    }

    private static (int Line, int Column) Reference(string text, int offset, string newLines) => References(text, newLines)[offset];

    private static void AssertPosition(Cursor cursor, int offset, int line, int column)
    {
        var position = cursor.GetPosition(offset);
        Assert.Equal((offset, line, column), (position.Offset, position.Line, position.Column));
    }

    [Fact]
    public void DefaultNewLinesAreLineFeeds()
    {
        var cursor = new Cursor("a\rb\nc\u2028d");

        Assert.Equal("\n", cursor.NewLines);
        AssertPosition(cursor, 2, 1, 3);
        AssertPosition(cursor, 4, 2, 1);
        AssertPosition(cursor, 6, 2, 3);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void EachJavaScriptTerminatorEndsALine(string terminator)
    {
        var text = "ab" + terminator + "cd" + terminator + terminator + "e";
        var cursor = new Cursor(text) { NewLines = JavaScriptNewLines };

        AssertPosition(cursor, 0, 1, 1);
        AssertPosition(cursor, 2, 1, 3);
        AssertPosition(cursor, 3, 2, 1);
        AssertPosition(cursor, 5, 2, 3);
        AssertPosition(cursor, 6, 3, 1);
        AssertPosition(cursor, 7, 4, 1);
        AssertPosition(cursor, 8, 4, 2);
    }

    [Fact]
    public void NextLineEndsALineOnlyWhenInTheSet()
    {
        var text = "a\u0085b";

        AssertPosition(new Cursor(text) { NewLines = JavaScriptNewLines }, 2, 1, 3);
        AssertPosition(new Cursor(text) { NewLines = CSharpNewLines }, 2, 2, 1);
    }

    [Fact]
    public void CarriageReturnLineFeedEndsASingleLine()
    {
        var cursor = new Cursor("ab\r\ncd\r\n\r\ne") { NewLines = JavaScriptNewLines };

        AssertPosition(cursor, 11, 4, 2);
        AssertPosition(cursor, 2, 1, 3);
        // Between the '\r' and the '\n', on the line they end
        AssertPosition(cursor, 3, 1, 4);
        AssertPosition(cursor, 4, 2, 1);
        AssertPosition(cursor, 7, 2, 4);
        AssertPosition(cursor, 8, 3, 1);
        AssertPosition(cursor, 10, 4, 1);
    }

    [Fact]
    public void CarriageReturnAloneEndsALineWithoutLineFeedInTheSet()
    {
        var cursor = new Cursor("a\r\nb") { NewLines = "\r" };

        AssertPosition(cursor, 2, 2, 1);
        AssertPosition(cursor, 3, 2, 2);
        AssertPosition(cursor, 4, 2, 3);
    }

    [Fact]
    public void NewLinesMustNotBeEmpty()
    {
        var cursor = new Cursor("a");

        Assert.Throws<ArgumentNullException>(() => cursor.NewLines = null!);
        Assert.Throws<ArgumentException>(() => cursor.NewLines = "");
        Assert.Equal("\n", cursor.NewLines);
    }

    [Fact]
    public void SettingNewLinesForgetsTheLinesCounted()
    {
        var cursor = new Cursor("a\rb\nc");

        AssertPosition(cursor, 5, 2, 2);

        cursor.NewLines = "\r\n";
        AssertPosition(cursor, 5, 3, 2);
        AssertPosition(cursor, 2, 2, 1);

        cursor.NewLines = "\n";
        AssertPosition(cursor, 5, 2, 2);
        AssertPosition(cursor, 2, 1, 3);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData(JavaScriptNewLines)]
    [InlineData(CSharpNewLines)]
    [InlineData("\r")]
    public void RandomOffsetsMatchTheReference(string newLines)
    {
        var random = new Random(586);
        var pieces = new[] { "\n", "\r", "\r\n", "\u2028", "\u2029", "\u0085", "a", "bc", "def ", "\t" };
        var builder = new StringBuilder();

        while (builder.Length < 20_000)
        {
            builder.Append(pieces[random.Next(pieces.Length)]);
        }

        var text = builder.ToString();
        var expected = References(text, newLines);
        var cursor = new Cursor(text) { NewLines = newLines };

        // Backward and forward, near and far, like an AST builder asking for the end of a node then for its start
        for (var i = 0; i < 5_000; i++)
        {
            var offset = random.Next(text.Length + 1);
            var position = cursor.GetPosition(offset);

            Assert.Equal(expected[offset], (position.Line, position.Column));
            Assert.Equal(offset, position.Offset);
        }

        // Then every offset in reverse order on a new cursor, which counts the whole text first
        cursor = new Cursor(text) { NewLines = newLines };

        for (var offset = text.Length; offset >= 0; offset--)
        {
            var position = cursor.GetPosition(offset);
            Assert.Equal(expected[offset], (position.Line, position.Column));
        }
    }

    [Fact]
    public void OffsetsBeforeTheOriginAreCountedBackward()
    {
        var text = "ab\r\ncd\ref\u2028gx\nh";
        var cursor = new Cursor(text, new TextPosition(11, 10, 2)) { NewLines = JavaScriptNewLines };

        AssertPosition(cursor, 11, 10, 2);
        AssertPosition(cursor, 12, 10, 3);
        AssertPosition(cursor, 13, 11, 1);
        AssertPosition(cursor, 14, 11, 2);
        // Before the origin, the start of the buffer starting a line
        AssertPosition(cursor, 10, 10, 1);
        AssertPosition(cursor, 9, 9, 3);
        AssertPosition(cursor, 7, 9, 1);
        AssertPosition(cursor, 6, 8, 3);
        AssertPosition(cursor, 4, 8, 1);
        AssertPosition(cursor, 3, 7, 4);
        AssertPosition(cursor, 2, 7, 3);
        AssertPosition(cursor, 0, 7, 1);
        // The lines after the origin are still the ones counted from it
        AssertPosition(cursor, 13, 11, 1);
    }

    [Fact]
    public void OffsetsAfterTheEndAreColumnsOfTheLastLine()
    {
        var cursor = new Cursor("a\rb") { NewLines = "\r" };

        cursor.ResetPosition(5);
        Assert.Equal((5, 2, 4), (cursor.Position.Offset, cursor.Position.Line, cursor.Position.Column));
    }

    [Fact]
    public void CarriageReturnEndingANonFinalBufferEndsALineUntilMoreTextIsRead()
    {
        var cursor = new Cursor("ab\r", TextPosition.Start, isFinal: false) { NewLines = JavaScriptNewLines };

        AssertPosition(cursor, 3, 2, 1);
        AssertPosition(cursor, 2, 1, 3);
        AssertPosition(cursor, 3, 2, 1);

        // A final buffer ends the line too
        AssertPosition(new Cursor("ab\r") { NewLines = JavaScriptNewLines }, 3, 2, 1);
    }

    [Fact]
    public void FurthestPositionAndErrorsUseTheNewLines()
    {
        const string text = "a\r\rb\r\n  d";
        var parser = Terms.Text("a").And(Terms.Text("b")).And(Terms.Text("c").ElseError("c expected"));
        var context = new ParseContext(new Scanner(text)) { NewLines = JavaScriptNewLines };

        var exception = Assert.Throws<ParseException>(() => parser.Parse(context));
        Assert.Equal(Reference(text, exception.Position.Offset, JavaScriptNewLines), (exception.Position.Line, exception.Position.Column));
        Assert.Equal(3, exception.Position.Line);

        var failing = Terms.Text("a").And(Terms.Text("b")).And(Terms.Text("c"));
        context = new ParseContext(new Scanner(text)) { NewLines = JavaScriptNewLines };

        Assert.False(failing.TryParse(context, out _, out _));
        var furthest = context.Scanner.Cursor.FurthestPosition;
        Assert.Equal((8, 4, 3), (furthest.Offset, furthest.Line, furthest.Column));
        Assert.Equal(JavaScriptNewLines, context.NewLines);

        // The default new lines
        context = new ParseContext(new Scanner(text));
        Assert.False(failing.TryParse(context, out _, out _));
        furthest = context.Scanner.Cursor.FurthestPosition;
        Assert.Equal((8, 2, 3), (furthest.Offset, furthest.Line, furthest.Column));
    }

    // The identifiers of a text, with their offsets
    private static List<(string Name, int Start, int End)> Identifiers(string text)
    {
        var identifiers = new List<(string, int, int)>();
        var index = 0;

        while ((index = text.IndexOf("id", index, StringComparison.Ordinal)) >= 0)
        {
            var end = index + 2;

            while (end < text.Length && char.IsDigit(text[end]))
            {
                end++;
            }

            identifiers.Add((text.Substring(index, end - index), index, end));
            index = end;
        }

        return identifiers;
    }

    // A list of identifiers, each one with the positions of its start and end
    private static readonly Parser<IReadOnlyList<(string Name, int StartLine, int StartColumn, int EndLine, int EndColumn)>> Located =
        ZeroOrMany(Terms.Identifier().Then(static (context, start, end, name) =>
        {
            // The end first, then the start, as an AST builder does
            var endPosition = context.Scanner.Cursor.GetPosition(end);
            var startPosition = context.Scanner.Cursor.GetPosition(start);
            return (name.ToString(), startPosition.Line, startPosition.Column, endPosition.Line, endPosition.Column);
        }));

    private static string LocatedText()
    {
        var random = new Random(42);
        var separators = new[] { " ", "\n", "\r", "\r\n", "\u2028", "\u2029", "  \r\n\r" };
        var builder = new StringBuilder();

        for (var i = 0; i < 3_000; i++)
        {
            builder.Append("id").Append(i).Append(separators[random.Next(separators.Length)]);
        }

        return builder.ToString();
    }

    [Fact]
    public void TextReaderPositionsAreTheOnesOfTheString()
    {
        var text = LocatedText();

        var context = new ParseContext(new Scanner(text)) { NewLines = JavaScriptNewLines };
        var expected = Located.Parse(context);

        Assert.NotNull(expected);
        Assert.Equal(3_000, expected!.Count);

        var positions = References(text, JavaScriptNewLines);
        var reference = Identifiers(text).Select(identifier =>
        {
            var (startLine, startColumn) = positions[identifier.Start];
            var (endLine, endColumn) = positions[identifier.End];
            return (identifier.Name, startLine, startColumn, endLine, endColumn);
        });

        Assert.Equal(reference, expected);

        // Small blocks, such that windows end between a '\r' and a '\n'
        foreach (var bufferSize in new[] { 1, 2, 3, 7, 64 })
        {
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = JavaScriptNewLines },
            };

            var streamed = Located.Parse(new StringReader(text), options);

            Assert.Equal(expected, streamed);
        }
    }

    [Fact]
    public void TextReaderErrorsUseTheNewLines()
    {
        var text = "a\r\r\nb\r\n\u2028  c?";
        var parser = ZeroOrMany(Terms.Identifier()).AndSkip(Literals.Char('!').ElseError("! expected"));

        foreach (var bufferSize in new[] { 1, 2, 3, 64 })
        {
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = JavaScriptNewLines },
            };

            Assert.False(parser.TryParse(new StringReader(text), out _, out var error, options));
            Assert.Equal(Reference(text, error!.Position.Offset, JavaScriptNewLines), (error.Position.Line, error.Position.Column));
            Assert.Equal((11, 5, 4), (error.Position.Offset, error.Position.Line, error.Position.Column));
        }
    }

    [Fact]
    public async Task ParseManyPositionsUseTheNewLines()
    {
        var text = LocatedText();
        var item = Terms.Identifier().Then(static (context, start, end, name) =>
        {
            var position = context.Scanner.Cursor.GetPosition(start);
            return (name.ToString(), position.Line, position.Column);
        });

        var positions = References(text, JavaScriptNewLines);
        var lines = Identifiers(text).Select(identifier =>
        {
            var (line, column) = positions[identifier.Start];
            return (identifier.Name, line, column);
        }).ToList();

        foreach (var bufferSize in new[] { 1, 3, 64 })
        {
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = JavaScriptNewLines },
            };

            var items = new List<(string, int, int)>();

            await foreach (var parsed in item.ParseManyAsync(new StringReader(text), options))
            {
                items.Add(parsed);
            }

            Assert.Equal(lines, items);
        }
    }

    [Theory]
    [InlineData('\r')]
    [InlineData('\n')]
    [InlineData('\u2028')]
    public async Task ParseManyDelimitersUseTheNewLines(char delimiter)
    {
        var text = "a\r\nb\rc\u2028d\re";
        var item = ZeroOrMany(Terms.Identifier().Then(static (context, start, end, name) =>
        {
            var position = context.Scanner.Cursor.GetPosition(start);
            return (name.ToString(), position.Line, position.Column);
        }));

        var options = new StreamParseOptions
        {
            BufferSize = 2,
            ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = JavaScriptNewLines },
        };

        var items = new List<(string, int, int)>();

        await foreach (var parsed in item.ParseManyAsync(new StringReader(text), delimiter, options))
        {
            items.AddRange(parsed);
        }

        var expected = new[] { "a", "b", "c", "d", "e" }.Select(name =>
        {
            var (line, column) = Reference(text, text.IndexOf(name, StringComparison.Ordinal), JavaScriptNewLines);
            return (name, line, column);
        });

        Assert.Equal(expected, items);
    }

    [Fact]
    public async Task ParseManyFrameEndingWithCarriageReturnBeforeALineFeedDelimiter()
    {
        // The '\r' ending the first frame and the delimiter end a single line
        var item = Terms.Identifier().AndSkip(Literals.Char('\r').Optional()).Then(static (context, start, end, name) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (name.ToString(), position.Line, position.Column);
        });

        var options = new StreamParseOptions
        {
            ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = "\r\n" },
        };

        var items = new List<(string, int, int)>();

        await foreach (var parsed in item.ParseManyAsync(new StringReader("ab\r\ncd\r\nef"), '\n', options))
        {
            items.Add(parsed);
        }

        Assert.Equal(new[] { ("ab", 1, 4), ("cd", 2, 4), ("ef", 3, 3) }, items);
    }

    [Fact]
    public void PositionsRequestedForwardRecordNoLines()
    {
        var text = string.Join("\n", Enumerable.Range(0, 1_000).Select(static i => "id" + i));
        var cursor = new Cursor(text);

        for (var offset = 0; offset <= text.Length; offset += 7)
        {
            Assert.Equal(References(text, "\n")[offset], (cursor.GetPosition(offset).Line, cursor.GetPosition(offset).Column));
        }

        var field = typeof(Cursor).GetField("_lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var lines = field.GetValue(cursor)!;
        Assert.Equal(0, (int)lines.GetType().GetField("Count")!.GetValue(lines)!);

        // The requests behind the furthest one are counted, until they counted many times the text
        AssertPosition(cursor, 3, 1, 4);
        AssertPosition(cursor, 10, 3, 3);
        Assert.Equal(0, (int)lines.GetType().GetField("Count")!.GetValue(lines)!);

        var middle = text.Length / 2;
        var expected = References(text, "\n")[middle];

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(expected, (cursor.GetPosition(middle).Line, cursor.GetPosition(middle).Column));
        }

        // Then the lines up to the furthest position are recorded
        Assert.True((int)lines.GetType().GetField("Count")!.GetValue(lines)! > 900);
        AssertPosition(cursor, 3, 1, 4);
        AssertPosition(cursor, 10, 3, 3);
    }

    [Fact]
    public void ASinglePositionAllocatesNoLineMap()
    {
        var field = typeof(Cursor).GetField("_lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var cursor = new Cursor("ab\ncd\nef");

        AssertPosition(cursor, 7, 3, 2);
        Assert.Null(field.GetValue(cursor));

        // The second one creates it
        AssertPosition(cursor, 4, 2, 2);
        Assert.NotNull(field.GetValue(cursor));
        AssertPosition(cursor, 7, 3, 2);
    }

    [Fact]
    public void NegativeOffsetsAreRejected()
    {
        var cursor = new Cursor("ab");
        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.GetPosition(-1));
    }

    [Fact]
    public void RequestsAfterACarriageReturnEndingANonFinalBufferKeepTheFurthestPosition()
    {
        var cursor = new Cursor("a\nb\r", TextPosition.Start, isFinal: false) { NewLines = "\r\n" };

        AssertPosition(cursor, 4, 3, 1);
        AssertPosition(cursor, 4, 3, 1);
        AssertPosition(cursor, 3, 2, 2);
        AssertPosition(cursor, 2, 2, 1);
        AssertPosition(cursor, 4, 3, 1);
    }

    [Fact]
    public void SettingNewLinesAllocatesNoLineMap()
    {
        var field = typeof(Cursor).GetField("_lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var cursor = new Cursor("a\r\nb\rc") { NewLines = JavaScriptNewLines };

        Assert.Null(field.GetValue(cursor));
        AssertPosition(cursor, 6, 3, 2);
        Assert.Null(field.GetValue(cursor));
        AssertPosition(cursor, 2, 1, 3);
        Assert.NotNull(field.GetValue(cursor));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TextReaderWindowsDontEndAfterACarriageReturn(int bufferSize)
    {
        // The '\r' is consumed at the end of a window, and the error reads the position after it before more text is read
        const string text = "a\r\n;";
        var parser = Literals.Text("a\r").SkipAnd(Literals.Char('x').ElseError("x expected"));

        var options = new StreamParseOptions
        {
            BufferSize = bufferSize,
            ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = "\r\n" },
        };

        Assert.False(parser.TryParse(new StringReader(text), out _, out var error, options));
        Assert.Equal((2, 1, 3), (error!.Position.Offset, error.Position.Line, error.Position.Column));

        Assert.False(parser.TryParse(new ParseContext(new Scanner(text)) { NewLines = "\r\n" }, out _, out error));
        Assert.Equal((2, 1, 3), (error!.Position.Offset, error.Position.Line, error.Position.Column));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ParseManyWindowsDontEndAfterACarriageReturn(int bufferSize)
    {
        var item = Literals.Text("a\r").Then(static (context, start, end, _) =>
        {
            var position = context.Scanner.Cursor.GetPosition(end);
            return (position.Line, position.Column);
        });

        var options = new StreamParseOptions
        {
            BufferSize = bufferSize,
            ContextFactory = static (scanner, token) => new ParseContext(scanner, token) { NewLines = "\r\n" },
        };

        var items = new List<(int, int)>();

        await foreach (var parsed in item.ParseManyAsync(new StringReader("a\r\na\ra\r"), Literals.Char('\n').Optional(), options))
        {
            items.Add(parsed);
        }

        Assert.Equal(new[] { (1, 3), (3, 1), (4, 1) }, items);
    }

    [Theory]
    [InlineData(12, 8, 1)]
    [InlineData(11, 7, 3)]
    [InlineData(9, 7, 1)]
    [InlineData(8, 6, 4)]
    [InlineData(7, 6, 3)]
    [InlineData(3, 5, 8)]
    public void FirstRequestBehindTheFurthestIsCountedFromTheClosestPosition(int offset, int line, int column)
    {
        // The origin is at offset 2, line 5, column 7
        const string text = "xxab\ncd\r\nef\rg";

        var cursor = new Cursor(text, new TextPosition(2, 5, 7)) { NewLines = "\r\n" };

        // The second request creates the map and keeps the furthest position
        AssertPosition(cursor, 13, 8, 2);
        AssertPosition(cursor, 13, 8, 2);

        AssertPosition(cursor, offset, line, column);

        // Then recorded
        AssertPosition(cursor, offset, line, column);
        AssertPosition(cursor, 5, 6, 1);
    }

    [Fact]
    public void FirstRequestBehindTheFurthestOnTheLineOfTheOrigin()
    {
        const string text = "xxabcdefghijklmnop\nq";
        var cursor = new Cursor(text, new TextPosition(2, 5, 7));

        AssertPosition(cursor, 20, 6, 2);
        AssertPosition(cursor, 20, 6, 2);

        // Counted backward from the furthest position, the line starting before the origin
        AssertPosition(cursor, 15, 5, 20);
        AssertPosition(cursor, 19, 6, 1);
        AssertPosition(cursor, 15, 5, 20);
    }

    [Fact]
    public void RequestsBehindOnALongLineEndUpRecorded()
    {
        var text = new string('x', 100_000) + "\ncd";
        var cursor = new Cursor(text);
        var field = typeof(Cursor).GetField("_lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        AssertPosition(cursor, text.Length, 2, 3);
        AssertPosition(cursor, text.Length, 2, 3);

        // Each request scans the long line back to its start, until the lines are recorded
        for (var i = 0; i < 40; i++)
        {
            AssertPosition(cursor, 100_000, 1, 100_001);
        }

        var lines = field.GetValue(cursor)!;
        Assert.Equal(1, (int)lines.GetType().GetField("Count")!.GetValue(lines)!);

        // The line start isn't looked for before the origin
        cursor = new Cursor(text, new TextPosition(99_990, 3, 5));
        AssertPosition(cursor, text.Length, 4, 3);
        AssertPosition(cursor, text.Length, 4, 3);
        AssertPosition(cursor, 100_000, 3, 15);
    }
}
