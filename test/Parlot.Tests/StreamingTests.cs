#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using Parlot.Tests.Sql;
using Xunit;
using static Parlot.Fluent.Parsers;
using CalcExpression = Parlot.Tests.Calc.Expression;

namespace Parlot.Tests;

/// <summary>
/// Compares the parsing of streams, and of non-final prefixes, with the parsing of the whole text.
/// </summary>
public class StreamingTests
{
    private static readonly Dictionary<string, IGrammar> Grammars = new()
    {
        ["calc"] = new Grammar<CalcExpression>(FluentParser.Expression),
        ["calc-eof"] = new Grammar<CalcExpression>(FluentParser.Expression.Eof()),
        ["json"] = new Grammar<IJson>(JsonParser.Json),
        ["json-eof"] = new Grammar<IJson>(JsonParser.Json.Eof()),
        ["sql"] = new Grammar<StatementList>(CreateSqlParser(), static scanner => new ParseContext(scanner, disableLoopDetection: true)),
        ["identifier"] = new Grammar<TextSpan>(Terms.Identifier()),
        ["integer"] = new Grammar<long>(Terms.Integer()),
        ["decimal"] = new Grammar<decimal>(Terms.Decimal()),
        ["double"] = new Grammar<double>(Terms.Number<double>(NumberOptions.Float)),
        ["text"] = new Grammar<string>(Terms.Text("hello")),
        ["text-ci"] = new Grammar<string>(Terms.Text("hello", caseInsensitive: true)),
        ["keyword"] = new Grammar<string>(Terms.Keyword("for")),
        ["choice"] = new Grammar<string>(OneOf(Terms.Text("for"), Terms.Text("foreach"), Terms.Text("forever"), Terms.Text("if"))),
        ["keywords"] = new Grammar<string>(OneOf(Terms.Keyword("for"), Terms.Keyword("foreach"), Terms.Keyword("if"))),
        ["anyof"] = new Grammar<TextSpan>(Literals.AnyOf("abc", minSize: 3)),
        ["anyof-max"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Literals.AnyOf("abc", minSize: 1, maxSize: 2))),
        ["noneof"] = new Grammar<TextSpan>(Literals.NoneOf(",;", minSize: 2)),
        ["pattern"] = new Grammar<TextSpan>(Literals.Pattern(char.IsDigit, minSize: 2, maxSize: 4)),
        ["string"] = new Grammar<TextSpan>(Terms.String()),
        ["whitespace"] = new Grammar<TextSpan>(Literals.WhiteSpace(includeNewLines: true)),
        ["nonwhitespace"] = new Grammar<TextSpan>(Literals.NonWhiteSpace()),
        ["textbefore"] = new Grammar<TextSpan>(AnyCharBefore(Literals.Text("--"), failOnEof: true, consumeDelimiter: true)),
        ["textbefore-eof"] = new Grammar<TextSpan>(AnyCharBefore(Literals.Text("--"))),
        ["capture"] = new Grammar<TextSpan>(Capture(Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer()))),
        ["not"] = new Grammar<TextSpan>(Not(Literals.Text("ab")).SkipAnd(Literals.NonWhiteSpace())),
        ["followed"] = new Grammar<TextSpan>(Terms.Identifier().WhenFollowedBy(Terms.Char('('))),
        ["notfollowed"] = new Grammar<long>(Terms.Integer().WhenNotFollowedBy(Literals.Char('.'))),
        ["separated"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer())),
        ["list-eof"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).Eof()),
        ["elseerror"] = new Grammar<long>(Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"))),
        ["comments"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"))),
    };

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();

        void Add(string grammar, params string[] inputs)
        {
            foreach (var input in inputs)
            {
                data.Add(grammar, input);
            }
        }

        string[] calc =
        [
            "", " ", "1", "1+2*3", " 1 + 2 ", "-(3.5*2)", "1.", "1e", "1e5", "12.5e-3", "(1+2", "1+", "1+2)x", "((1))/(2-3)*4", "1 +\n 2",
            "3 - 1 / 2 + 1", "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2", "-(3 + 2) * -4 + --6",
        ];
        Add("calc", calc);
        Add("calc-eof", calc);

        string[] json =
        [
            "", "{", "{}", "[]", "\"a\"", "{\"property\":\"value\"}", " { \"a\" : [ \"b\" , { } ] } ",
            "{\"property\":[\"value\",\"value\",\"value\"]}", "{\"a\":\"b\\\"c\"}", "{\"a\":\"b", "[\"a\",]", "[\"a\" \"b\"]", "{} {}", "[\"a\"]x",
        ];
        Add("json", json);
        Add("json-eof", json);

        Add("sql",
            "SELECT * FROM users",
            "SELECT id, name FROM users WHERE id = 1",
            "SELECT * FROM users WHERE name = 'John'",
            "SELECT * -- comment \n FROM users",
            "SELECT id, name /* multiline\n comment\n */ FROM users",
            "/* some documentation */ SELECT id, name FROM users WHERE id = 1",
            "SELECT * FROM users /* unterminated",
            "SELECT * FROM users WHERE id BETWEEN 1 AND 100 ORDER BY id ASC LIMIT 10",
            "SELECT u.id, o.amount FROM users AS u JOIN orders AS o ON u.id = o.user_id",
            "SELECT category, COUNT(*) FROM products GROUP BY category HAVING COUNT(*) > 10",
            "WITH cte(id, name) AS (SELECT id, name FROM users) SELECT * FROM cte",
            "SELECT * FROM users UNION ALL SELECT * FROM customers",
            "SELECT * FROM users WHERE id >= 1; SELECT 1",
            "SELECT FROM",
            "SELEC * FROM users");

        Add("identifier", "", "a", "abc", " abc def", "1a", "a1_b ");
        Add("integer", "", "1", "123", " 42x", "-5", "+", "123456789012");
        Add("decimal", "", "1", "1.5", "1.", "1.e", "12.5e-3", "1e", "1e+", ".5", "-0.25 ");
        Add("double", "", "1", "1.5e10", "1e", "2.", "-3.25e-2x");
        Add("text", "", "h", "hel", "hello", "hello!", " hello", "help");
        Add("text-ci", "", "HEL", "HeLLo", "HELP", "hellO world");
        Add("keyword", "", "f", "for", "for ", "fore", "for(", "fo");
        Add("choice", "", "f", "for", "fore", "foreach", "forever", "foreve", "if", "i", "x");
        Add("keywords", "", "for", "fore", "foreach", "foreachx", "if", "ifx", "x");
        Add("anyof", "", "a", "ab", "abc", "abca", "abx", "x");
        Add("anyof-max", "", "a", "abc", "abcab", "abxa");
        Add("noneof", "", "a", "ab", "ab,c", "a,", ";");
        Add("pattern", "", "1", "12", "1234", "123456", "1a");
        Add("string", "", "'", "'a", "'abc'", "\"a\\\"b\"", "'a\\", "'a\\'", "'\\x'", "x");
        Add("whitespace", "", " ", "  \n\t x", "x");
        Add("nonwhitespace", "", "a", "abc def", " x");
        Add("textbefore", "", "a", "abc", "abc-", "abc--", "a-b--c", "--");
        Add("textbefore-eof", "", "a", "abc", "abc-", "abc--", "--");
        Add("capture", "", "a", "a=", "a = 12", "a = 12 x", "a=x");
        Add("not", "", "a", "ab", "abc", "ac", "xab");
        Add("followed", "", "f", "f(", "f (", "f x", "f");
        Add("notfollowed", "", "1", "12", "12.", "12.5", "12 .", "12x");
        Add("separated", "", "1", "1,", "1,2", "1 , 2 ,3", "1,,2", "1,x");
        Add("list-eof", "", "a", "a b c", "a b 1", "a b ");
        Add("elseerror", "", "(", "(1", "(1)", "(1 x", "( 12 )", "x");
        Add("comments", "", "a", "a // b\n c", "a /* b */ c", "a /* b", "a / b", "// only");

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ConclusivePrefixesMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);

        for (var length = 0; length <= input.Length; length++)
        {
            var actual = g.Parse(input.Substring(0, length), isFinal: false);

            if (actual != null)
            {
                Assert.True(expected == actual, $"Prefix '{input.Substring(0, length)}' of '{input}': expected {expected}, actual {actual}");
            }
        }

        // The whole text is conclusive once it is final
        Assert.NotNull(expected);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StreamsMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var random = new Random(input.Length * 31 + grammar.Length);

        (Func<int> Chunk, int BufferSize, bool Yield)[] configurations =
        [
            (() => 1, 1, false),
            (() => 1, 4096, false),
            (() => 3, 2, true),
            (() => 7, 5, false),
            (() => random.Next(1, 6), 1, false),
            (() => int.MaxValue, 1, false),
        ];

        foreach (var (chunk, bufferSize, yield) in configurations)
        {
            var options = new StreamParseOptions { BufferSize = bufferSize, ContextFactory = g.ContextFactory };
            var actual = await g.ParseAsync(() => new ChunkedReader(input, chunk, yield), options);

            Assert.True(expected == actual, $"'{input}' with buffer size {bufferSize}: expected {expected}, actual {actual}");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StreamsMatchTheWholeTextOnStringReaders(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var actual = await g.ParseAsync(() => new StringReader(input), new StreamParseOptions { ContextFactory = g.ContextFactory });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CompactingStreamsMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var random = new Random(input.Length * 17 + grammar.Length);

        (Func<int> Chunk, int BufferSize)[] configurations =
        [
            (() => 1, 1),
            (() => 1, 4096),
            (() => 3, 2),
            (() => 7, 5),
            (() => random.Next(1, 6), 1),
            (() => random.Next(1, 6), 3),
            (() => int.MaxValue, 1),
        ];

        foreach (var (chunk, bufferSize) in configurations)
        {
            var options = new StreamParseOptions { BufferSize = bufferSize, ContextFactory = g.ContextFactory };
            var actual = g.ParseCompacting(() => new ChunkedReader(input, chunk, yield: false), options);

            Assert.True(expected == actual, $"'{input}' with buffer size {bufferSize}: expected {expected}, actual {actual}");
        }

        // Splits the first token at every position
        for (var split = 1; split <= input.Length; split++)
        {
            var options = new StreamParseOptions { BufferSize = split, ContextFactory = g.ContextFactory };
            var actual = g.ParseCompacting(() => new ChunkedReader(input, () => int.MaxValue, yield: false), options);

            Assert.True(expected == actual, $"'{input}' split at {split}: expected {expected}, actual {actual}");
        }
    }

    [Fact]
    public async Task ParseAsyncReadsOnlyWhatIsNeeded()
    {
        var reader = new ChunkedReader("[\"a\"]" + new string(' ', 100_000) + "x", () => 1, yield: false);
        var result = await JsonParser.Json.ParseAsync(reader, new StreamParseOptions { BufferSize = 1 });

        Assert.Equal("[\"a\"]", result!.ToString());
        Assert.True(reader.Position < 64, $"Read {reader.Position} characters");
    }

    [Fact]
    public async Task TryParseAsyncReturnsFalseOnParseException()
    {
        var parser = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"));

        var (success, _) = await parser.TryParseAsync(new ChunkedReader("(1 x", () => 1, yield: false));
        Assert.False(success);

        var exception = await Assert.ThrowsAsync<ParseException>(async () => await parser.ParseAsync(new ChunkedReader("(1 x", () => 1, yield: false)));
        Assert.Equal("Expected ')'", exception.Message);
        Assert.Equal(2, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseAsyncLimitsBufferedCharacters()
    {
        var options = new StreamParseOptions { BufferSize = 4, MaxBufferedCharacters = 16 };

        Assert.Equal("[\"abcdefghij\"]", (await JsonParser.Json.ParseAsync(new ChunkedReader("[\"abcdefghij\"]", () => 1, false), options))!.ToString());

        var exception = await Assert.ThrowsAsync<ParseException>(async () => await JsonParser.Json.ParseAsync(new ChunkedReader("[\"abcdefghijklmnopqrstuvwxyz\"]", () => 1, false), options));
        // The start of the token which doesn't fit
        Assert.Equal(1, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseAsyncSupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var reader = new ChunkedReader("[\"a\",\"b\",\"c\"]", () => 1, yield: true) { OnRead = position => { if (position == 3) cts.Cancel(); } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await JsonParser.Json.ParseAsync(reader, new StreamParseOptions { BufferSize = 1 }, cts.Token));
    }

    [Fact]
    public async Task CustomParsersCanMarkHitEnd()
    {
        // Succeeds on "ab" but needs to see what follows the "a" to decide
        var parser = new LookaheadParser().Then(static x => x);

        Assert.True(await TryParseAsync("ab"));
        Assert.True(await TryParseAsync("a"));
        Assert.False(await TryParseAsync("ac"));

        async Task<bool> TryParseAsync(string text)
        {
            var expected = parser.TryParse(text, out _);
            var (actual, _) = await parser.TryParseAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = 1 });
            Assert.Equal(expected, actual);
            return actual;
        }
    }

    public static TheoryData<int, int> ChunkSizes() => new() { { 1, 1 }, { 1, 4096 }, { 3, 2 }, { 7, 16 }, { int.MaxValue, 1 } };

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseManyAsyncParsesSuccessiveValues(int chunk, int bufferSize)
    {
        var lines = Enumerable.Range(0, 50).Select(static i => $"{{\"id\":\"{i}\",\"tags\":[\"{new string('x', i % 7)}\"]}}").ToArray();
        var text = string.Join("\n", lines) + "\n";

        var items = await ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => chunk, yield: chunk == 3), new StreamParseOptions { BufferSize = bufferSize }));

        Assert.Equal(lines, items.Select(static json => json.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    [InlineData("1 2 3")]
    [InlineData(" 1\n22\n 333 \n")]
    public async Task ParseManyAsyncParsesValuesSeparatedByWhiteSpace(string text)
    {
        var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = 1 }));

        Assert.Equal(text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(long.Parse), values);
    }

    [Theory]
    [InlineData("1,2,3", 3)]
    [InlineData("1 , 2 ,3 ,", 3)]
    [InlineData("", 0)]
    [InlineData("1", 1)]
    public async Task ParseManyAsyncParsesSeparatedValues(string text, int expected)
    {
        foreach (var bufferSize in new[] { 1, 3, 4096 })
        {
            var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), Terms.Char(','), new StreamParseOptions { BufferSize = bufferSize }));
            Assert.Equal(Enumerable.Range(1, expected).Select(static i => (long)i), values);
        }
    }

    [Theory]
    [InlineData("1 2", 2)]
    [InlineData("1,2 3", 4)]
    [InlineData("1,,2", 2)]
    public async Task ParseManyAsyncRequiresSeparators(string text, int offset)
    {
        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), Terms.Char(','), new StreamParseOptions { BufferSize = 1 })));
        Assert.Equal(offset, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseManyAsyncReportsAbsolutePositions()
    {
        var text = "{\"a\":\"b\"}\n{\"c\":\"d\"}\n {\"e\" \"f\"}\n";
        var items = new List<IJson>();

        foreach (var bufferSize in new[] { 1, 4096 })
        {
            items.Clear();
            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = bufferSize }), items));

            Assert.Equal(2, items.Count);
            Assert.Equal(21, exception.Position.Offset);
            Assert.Equal(3, exception.Position.Line);
            Assert.Equal(2, exception.Position.Column);
        }
    }

    [Fact]
    public async Task ParseManyAsyncPropagatesParseExceptionsWithAbsolutePositions()
    {
        var parser = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"));

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(parser.ParseManyAsync(new ChunkedReader("(1)\n(2)\n(3 x", () => 1, yield: false), new StreamParseOptions { BufferSize = 1 })));

        Assert.Equal("Expected ')'", exception.Message);
        Assert.Equal(10, exception.Position.Offset);
        Assert.Equal(3, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);
    }

    [Fact]
    public async Task ParseManyAsyncRejectsEmptyValues()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ToListAsync(Terms.Integer().Optional().ParseManyAsync(new ChunkedReader("1 x", () => 1, yield: false))));
    }

    [Fact]
    public async Task ParseManyAsyncKeepsMemoryBounded()
    {
        const int Count = 10_000;
        var reader = new GeneratedReader(Count);
        var options = new StreamParseOptions { BufferSize = 64, MaxBufferedCharacters = 256 };
        var count = 0;

        await foreach (var _ in JsonParser.Json.ParseManyAsync(reader, options))
        {
            count++;
        }

        Assert.Equal(Count, count);
    }

    [Fact]
    public async Task ParseManyAsyncLimitsValueSize()
    {
        var text = "[\"a\"]\n[\"" + new string('b', 100) + "\"]";
        var items = new List<IJson>();

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => 5, false), new StreamParseOptions { BufferSize = 8, MaxBufferedCharacters = 32 }), items));

        Assert.Single(items);
        Assert.Equal(6, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseManyAsyncSupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var count = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Terms.Integer().ParseManyAsync(new ChunkedReader("1 2 3 4 5 6", () => 1, yield: true), new StreamParseOptions { BufferSize = 1 }, cts.Token))
            {
                if (++count == 2)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ParseManyAsyncUsesTheContextFactory()
    {
        var options = new StreamParseOptions
        {
            BufferSize = 1,
            ContextFactory = static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = Capture(ZeroOrMany(Literals.Char('#').Or(Literals.Char(' ')))) },
        };

        var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1#2 ##3", () => 1, false), options));

        Assert.Equal([1L, 2, 3], values);
    }

    [Fact]
    public async Task TextSpansRemainValid()
    {
        var spans = await ToListAsync(Terms.Identifier().ParseManyAsync(new ChunkedReader("alpha beta gamma delta", () => 1, false), new StreamParseOptions { BufferSize = 1 }));

        Assert.Equal(["alpha", "beta", "gamma", "delta"], spans.Select(static s => s.ToString()));
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseManyAsyncParsesDelimitedValues(int chunk, int bufferSize)
    {
        var text = "{\"a\": \"1\"}\n\n  \r\n[\"b\", \"c\"]\r\n\"d\"\n \"e\" ";
        var reader = new ChunkedReader(text, () => chunk, yield: chunk == 3);

        var items = await ToListAsync(JsonParser.Json.ParseManyAsync(reader, '\n', new StreamParseOptions { BufferSize = bufferSize }));

        Assert.Equal(["{\"a\":\"1\"}", "[\"b\",\"c\"]", "\"d\"", "\"e\""], items.Select(static json => json.ToString()));
    }

    [Theory]
    [InlineData("1\n2\nx", '\n')]
    [InlineData("1\r\n2\r\n x", '\n')]
    [InlineData("1\n\n\n2\n\n x\n", '\n')]
    [InlineData("1, 2,\n 3,\r\n x", ',')]
    [InlineData("1,\r,\r 2, x", ',')]
    [InlineData("1\r2\r\r x", '\r')]
    [InlineData("1\r\n2\r\n x", '\r')]
    [InlineData("1;2;;x", ';')]
    [InlineData("x", ';')]
    public async Task ParseManyAsyncReportsAbsolutePositionsOfDelimitedValues(string text, char delimiter)
    {
        var expected = new Cursor(text);

        while (expected.Current != 'x')
        {
            expected.Advance();
        }

        foreach (var chunk in new[] { 1, 2, 4096 })
        {
            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => chunk, yield: false), delimiter, new StreamParseOptions { BufferSize = 1 })));

            Assert.Equal(expected.Position, exception.Position);
        }
    }

    [Fact]
    public async Task ParseManyAsyncRequiresDelimitedValuesToBeMatchedEntirely()
    {
        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1\n2 3\n", () => 1, false), '\n')));

        Assert.Equal(4, exception.Position.Offset);
        Assert.Equal(2, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);

        var options = new StreamParseOptions { SkipWhiteSpace = false };

        Assert.Equal([1L, 2], await ToListAsync(Literals.Integer().ParseManyAsync(new ChunkedReader("1\n\n2", () => 1, false), '\n', options)));

        await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Literals.Integer().ParseManyAsync(new ChunkedReader("1 \n2", () => 1, false), '\n', options)));
    }

    [Fact]
    public async Task ParseManyAsyncLimitsDelimitedValueSize()
    {
        var items = new List<long>();

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1\n22\n" + new string('3', 20) + "\n4", () => 3, false), '\n', new StreamParseOptions { BufferSize = 2, MaxBufferedCharacters = 8 }), items));

        Assert.Equal(2, items.Count);
        Assert.Equal(5, exception.Position.Offset);
        Assert.Equal(3, exception.Position.Line);
        Assert.Equal(1, exception.Position.Column);
    }

    [Fact]
    public async Task ParseManyAsyncParsesDelimitedValuesAsSoonAsTheyAreReceived()
    {
        var reader = new InteractiveReader();
        var values = new List<long>();

        reader.Write("1\n2");

        var task = Task.Run(async () =>
        {
            await foreach (var value in Terms.Integer().ParseManyAsync(reader, '\n'))
            {
                values.Add(value);

                // More text is only available once a value is parsed, waiting for a full buffer would never end
                if (value == 1)
                {
                    reader.Write("\n3");
                }
                else if (value == 2)
                {
                    reader.Write("\n");
                }
                else
                {
                    reader.Complete();
                }
            }
        });

        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))));
        await task;
        Assert.Equal([1L, 2, 3], values);
    }

    [Fact]
    public async Task ParseManyAsyncEnumerationCanStopEarly()
    {
        var reader = new ChunkedReader("1 2 3 4 5", () => 1, false);

        await foreach (var value in Terms.Integer().ParseManyAsync(reader, new StreamParseOptions { BufferSize = 1 }))
        {
            if (value == 2)
            {
                break;
            }
        }

        Assert.True(reader.Position < 9);
    }

    private static async Task<List<TValue>> ToListAsync<TValue>(IAsyncEnumerable<TValue> source, List<TValue>? items = null)
    {
        items ??= [];

        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private static Parser<StatementList> CreateSqlParser()
    {
        var method = typeof(SqlParser).GetMethod("CreateRuntimeParser", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Parser<StatementList>)method.Invoke(null, null)!;
    }

    private interface IGrammar
    {
        Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; }

        /// <summary>
        /// Returns the outcome of the parse, or <see langword="null"/> when it is inconclusive.
        /// </summary>
        string? Parse(string text, bool isFinal);

        Task<string> ParseAsync(Func<TextReader> reader, StreamParseOptions options);

        string ParseCompacting(Func<TextReader> reader, StreamParseOptions options);
    }

    private sealed class Grammar<T> : IGrammar
    {
        private readonly Parser<T> _parser;
        private readonly Func<Scanner, ParseContext>? _contextFactory;

        public Grammar(Parser<T> parser, Func<Scanner, ParseContext>? contextFactory = null)
        {
            _parser = parser;
            _contextFactory = contextFactory;
            ContextFactory = contextFactory == null ? null : (scanner, _) => contextFactory(scanner);
        }

        public Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; }

        public string? Parse(string text, bool isFinal)
        {
            var scanner = new Scanner(text, isFinal);
            var context = _contextFactory?.Invoke(scanner) ?? new ParseContext(scanner);
            var result = new ParseResult<T>();

            try
            {
                var success = _parser.Parse(context, ref result);

                if (scanner.Cursor.HitEnd)
                {
                    return null;
                }

                return success ? "OK " + Dump(result.Value) : "FAIL";
            }
            catch (ParseException e)
            {
                return scanner.Cursor.HitEnd ? null : Error(e);
            }
        }

        public async Task<string> ParseAsync(Func<TextReader> reader, StreamParseOptions options)
        {
            var (success, value) = await _parser.TryParseAsync(reader(), options);

            if (success)
            {
                return "OK " + Dump(value);
            }

            try
            {
                await _parser.ParseAsync(reader(), options);
                return "FAIL";
            }
            catch (ParseException e)
            {
                return Error(e);
            }
        }

        public string ParseCompacting(Func<TextReader> reader, StreamParseOptions options)
        {
            var success = _parser.TryParse(reader(), out var value, out var error, options);

            if (success)
            {
                return "OK " + Dump(value);
            }

            try
            {
                _parser.Parse(reader(), options);
            }
            catch (ParseException e)
            {
                var message = Error(e);
                Assert.Equal(message, $"ERROR {error!.Message} at {error.Position.Offset}{error.Position}");
                return message;
            }

            Assert.Null(error);
            return "FAIL";
        }

        private static string Error(ParseException e) => $"ERROR {e.Message} at {e.Position.Offset}{e.Position}";
    }

    private static string Dump(object? value, int depth = 0)
    {
        if (value == null)
        {
            return "null";
        }

        if (depth > 64)
        {
            return "...";
        }

        var type = value.GetType();

        switch (value)
        {
            case string s:
                return "\"" + s + "\"";
            case TextSpan span:
                return "'" + span.ToString() + "'";
            case IFormattable formattable when type.IsPrimitive || type.IsEnum || value is decimal:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            case IEnumerable enumerable:
                return "[" + string.Join(",", enumerable.Cast<object?>().Select(x => Dump(x, depth + 1))) + "]";
        }

        if (type.IsPrimitive)
        {
            return value.ToString()!;
        }

        var members = new List<string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string dump;

            try
            {
                dump = Dump(property.GetValue(value), depth + 1);
            }
            catch (TargetInvocationException)
            {
                dump = "!";
            }

            members.Add(property.Name + "=" + dump);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            members.Add(field.Name + "=" + Dump(field.GetValue(value), depth + 1));
        }

        return type.Name + "{" + string.Join(",", members) + "}";
    }

    /// <summary>
    /// Returns the text in chunks, optionally completing asynchronously.
    /// </summary>
    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly Func<int> _chunk;
        private readonly bool _yield;

        public ChunkedReader(string text, Func<int> chunk, bool yield)
        {
            _text = text;
            _chunk = chunk;
            _yield = yield;
        }

        public int Position { get; private set; }

        public Action<int>? OnRead { get; set; }

        public override int Peek() => Position < _text.Length ? _text[Position] : -1;

        public override int Read() => Position < _text.Length ? _text[Position++] : -1;

        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

#if NET8_0_OR_GREATER
        public override int Read(Span<char> buffer)
#else
        private int Read(Span<char> buffer)
#endif
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk()), _text.Length - Position);
            _text.AsSpan(Position, length).CopyTo(buffer);
            Position += length;
            OnRead?.Invoke(Position);
            return length;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (_yield)
            {
                await Task.Yield();
            }

            return Read(buffer, index, count);
        }

#if NET8_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_yield)
            {
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }
#endif
    }

    /// <summary>
    /// Generates an endless-looking NDJSON input without retaining it.
    /// </summary>
    private sealed class GeneratedReader : TextReader
    {
        private readonly int _count;
        private int _index;
        private string _current = "";
        private int _position;

        public GeneratedReader(int count)
        {
            _count = count;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _current.Length)
            {
                if (_index == _count)
                {
                    return 0;
                }

                _current = $"{{\"id\":\"{_index++}\",\"value\":[\"{new string('v', _index % 50)}\"]}}\n";
                _position = 0;
            }

            var length = Math.Min(count, _current.Length - _position);
            _current.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count) => Task.FromResult(Read(buffer, index, count));

#if NET8_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var chars = new char[buffer.Length];
            var read = Read(chars, 0, chars.Length);
            chars.AsSpan(0, read).CopyTo(buffer.Span);
            return new ValueTask<int>(read);
        }
#endif
    }

    /// <summary>
    /// Returns the text written so far, waiting for more until it is completed.
    /// </summary>
    private sealed class InteractiveReader : TextReader
    {
        private readonly object _lock = new();
        private readonly System.Text.StringBuilder _pending = new();
        private TaskCompletionSource<bool> _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _completed;

        public void Write(string text)
        {
            lock (_lock)
            {
                _pending.Append(text);
                _available.TrySetResult(true);
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _completed = true;
                _available.TrySetResult(true);
            }
        }

        public override int Read(char[] buffer, int index, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            while (true)
            {
                Task wait;

                lock (_lock)
                {
                    if (_pending.Length > 0)
                    {
                        var length = Math.Min(count, _pending.Length);
                        _pending.CopyTo(0, buffer, index, length);
                        _pending.Remove(0, length);
                        return length;
                    }

                    if (_completed)
                    {
                        return 0;
                    }

                    _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    wait = _available.Task;
                }

                await wait.ConfigureAwait(false);
            }
        }

#if NET8_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var chars = new char[buffer.Length];
            var read = await ReadAsync(chars, 0, chars.Length).ConfigureAwait(false);
            chars.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
#endif
    }

    /// <summary>
    /// Matches 'a' when it is followed by 'b' or ends the text, reading <see cref="Cursor.Span"/> directly.
    /// </summary>
    private sealed class LookaheadParser : Parser<char>
    {
        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            // A token is parsed again on a larger buffer when it reads the end of the buffer
            if (context.IsCompacting)
            {
                return context.ParseToken(this, ref result);
            }

            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;
            var span = cursor.Span;

            if (span.Length > 0 && span[0] == 'a')
            {
                if (span.Length == 1)
                {
                    // The decision depends on the next character
                    cursor.MarkHitEnd();
                }

                if (span.Length == 1 || span[1] == 'b')
                {
                    var start = cursor.Offset;
                    cursor.Advance();
                    result.Set(start, cursor.Offset, 'a');
                    context.ExitParser(this);
                    return true;
                }
            }

            context.ExitParser(this);
            return false;
        }
    }
}
