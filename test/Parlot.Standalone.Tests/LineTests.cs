using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Parlot.Standalone.Tests;

public class LineTests
{
    private const string JavaScriptNewLines = "\n\r\u2028\u2029";

    // The line and column of each offset of a text, its end included, counted char by char from its start
    private static (int Line, int Column)[] References(string text)
    {
        var positions = new (int, int)[text.Length + 1];
        var line = 1;
        var column = 1;

        for (var i = 0; i < text.Length; i++)
        {
            positions[i] = (line, column);
            var c = text[i];

            if (JavaScriptNewLines.IndexOf(c) >= 0 && !(c == '\r' && i + 1 < text.Length && text[i + 1] == '\n'))
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

    private static (string Text, string Expected) Program()
    {
        var random = new System.Random(586);
        var separators = new[] { " ", "\n", "\r", "\r\n", "\u2028", "\u2029", "  \r\n\r" };
        var builder = new StringBuilder();
        var identifiers = new List<(string Name, int Start, int End)>();

        for (var i = 0; i < 3_000; i++)
        {
            var name = "id" + i;
            identifiers.Add((name, builder.Length, builder.Length + name.Length));
            builder.Append(name).Append(separators[random.Next(separators.Length)]);
        }

        builder.Append(';');

        var text = builder.ToString();
        var positions = References(text);
        var expected = string.Join(",", identifiers.Select(identifier =>
            identifier.Name + "@" + positions[identifier.Start].Line + ":" + positions[identifier.Start].Column + "-" +
            positions[identifier.End].Line + ":" + positions[identifier.End].Column));

        return (text, expected);
    }

    [Fact]
    public void CallbacksGetThePositionsOfTheLineTerminators()
    {
        var (text, expected) = Program();

        Assert.True(LineGrammars.Locate(text, out var value));
        Assert.Equal(expected, value);

        Assert.True(LineGrammars.Locate(new StringReader(text), out value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void FailuresUseTheLineTerminators()
    {
        const string text = "a\r\r\nb\r\n\u2028  c?";

        Assert.False(LineGrammars.Locate(text, out _, out var failure));
        Assert.Equal((11, 5, 4), failure);

        Assert.False(LineGrammars.Locate(new StringReader(text), out _, out failure));
        Assert.Equal((11, 5, 4), failure);

        // '\r' occupies a column by default
        Assert.False(LineGrammars.LocateDefault(text, out _, out failure));
        Assert.Equal((11, 3, 5), failure);
    }
}
