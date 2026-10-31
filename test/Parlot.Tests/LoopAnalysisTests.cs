using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using Parlot.Tests.Sql;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A Deferred parser that can't be invoked again at the position where it is active doesn't look for itself in the
/// active parsers, which is proved when its grammar is first parsed.
/// </summary>
public class LoopAnalysisTests
{
    private static readonly PropertyInfo _isLoopDetectionElided = typeof(Deferred<string>).GetProperty("IsLoopDetectionElided", BindingFlags.NonPublic | BindingFlags.Instance)!;

    // The property the benchmark harness reads too
    private static bool IsElided(object deferred) => (bool)deferred.GetType().GetProperty(_isLoopDetectionElided.Name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(deferred)!;

    private static object GetActivePositions(ParseContext context) => typeof(ParseContext).GetField("_activePositions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(context);

    [Fact]
    public void DirectLeftRecursionKeepsDetection()
    {
        var loop = Deferred<string>();
        loop.Parser = loop;

        Assert.False(IsElided(loop));
        Assert.False(loop.TryParse("x", out _));
    }

    [Fact]
    public void LeftRecursiveExpressionKeepsDetection()
    {
        var expression = Deferred<string>();
        var term = Terms.Text("1");

        expression.Parser = expression.AndSkip(Terms.Char('+')).And(term).Then(static x => x.Item1 + "+" + x.Item2).Or(term);

        Assert.False(IsElided(expression));

        // The left recursive alternative fails where it is entered again
        Assert.True(expression.TryParse("1+1", out var result));
        Assert.Equal("1", result);
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("optional")]
    [InlineData("zeroOrMany")]
    [InlineData("zeroOrOne")]
    [InlineData("oneOrMany")]
    [InlineData("not")]
    [InlineData("whenFollowedBy")]
    [InlineData("whenNotFollowedBy")]
    [InlineData("else")]
    [InlineData("nested")]
    [InlineData("select")]
    [InlineData("switch")]
    [InlineData("separated")]
    [InlineData("between")]
    [InlineData("unary")]
    [InlineData("leftAssociative")]
    [InlineData("rightAssociative")]
    [InlineData("textBefore")]
    [InlineData("capture")]
    [InlineData("emptyText")]
    [InlineData("emptyPattern")]
    [InlineData("if")]
    [InlineData("eof")]
    [InlineData("always")]
    [InlineData("skipWhiteSpace")]
    [InlineData("withWhiteSpaceParser")]
    [InlineData("customEmptyLeaf")]
    [InlineData("customWrapper")]
    [InlineData("delegate")]
    public void LeftRecursionKeepsDetection(string shape)
    {
        var a = Deferred<string>();
        var b = Deferred<string>();
        var c = Deferred<string>();
        var z = Literals.Text("z");

        Parser<string> recursive = shape switch
        {
            "choice" => Literals.Text("y").Or(a.AndSkip(z)),
            "optional" => Literals.Char('-').Optional().SkipAnd(a),
            "zeroOrMany" => ZeroOrMany(Literals.Char(' ')).SkipAnd(a),
            "zeroOrOne" => ZeroOrOne(Literals.Char(' ')).SkipAnd(a),
            "oneOrMany" => OneOrMany(a.AndSkip(z)).Then(static x => string.Concat(x)),
            "not" => Not(Literals.Char('y')).SkipAnd(a),
            "whenFollowedBy" => Always("w").WhenFollowedBy(a),
            "whenNotFollowedBy" => Always("w").WhenNotFollowedBy(a).AndSkip(z),
            "else" => a.AndSkip(z).Else("e").AndSkip(z),
            "nested" => b,
            "select" => Select<ParseContext, string>(static _ => 0, a.AndSkip(z)),
            "switch" => Literals.Char('-').Optional().Switch(static (_, _) => 0, a.AndSkip(z)),
            "separated" => Separated(Literals.Char(','), a.AndSkip(z)).Then(static x => string.Concat(x)),
            "between" => Between(Literals.Char('(').Optional(), a, z),
            "unary" => a.AndSkip(z).Unary((Literals.Char('-'), static x => "-" + x)),
            "leftAssociative" => a.AndSkip(z).LeftAssociative((Literals.Char('+'), static (x, y) => x + y)),
            "rightAssociative" => a.AndSkip(z).RightAssociative((Literals.Char('^'), static (x, y) => x + y)),
            "textBefore" => AnyCharBefore(a).Then(static x => x.ToString()),
            "capture" => Capture(a.AndSkip(z)).Then(static x => x.ToString()),
            "emptyText" => Literals.Text("").SkipAnd(a),
            "emptyPattern" => Literals.Pattern(static ch => ch == ' ', minSize: 0).SkipAnd(a),
            "if" => If(static _ => false, Literals.Text("never"), a.AndSkip(z)),
            "eof" => Eof().SkipAnd(a),
            "always" => Always().SkipAnd(a),
            "skipWhiteSpace" => SkipWhiteSpace(a.AndSkip(z)),
            "withWhiteSpaceParser" => a.AndSkip(z).WithWhiteSpaceParser(Literals.WhiteSpace()),

            // A custom parser that holds no parser is assumed to succeed without reading anything
            "customEmptyLeaf" => new EmptyTextParser().SkipAnd(a),

            // A custom parser that holds a parser can invoke any parser
            "customWrapper" => new ForwardingParser<string>(Literals.Text("w")),
            "delegate" => new DelegateParser<string>(static (ParseContext context, out string value) => { value = "d"; return false; }),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        b.Parser = c.Or(Literals.Text("b"));
        c.Parser = Literals.Char(' ').Optional().SkipAnd(a);

        a.Parser = recursive.Or(Literals.Text("x"));

        Assert.False(IsElided(a));

        if (shape == "nested")
        {
            Assert.False(IsElided(b));
            Assert.False(IsElided(c));
        }

        // The recursion ends where it would loop
        _ = a.TryParse("x", out _);
        _ = a.TryParse("-x", out _);
        _ = a.TryParse("", out _);
    }

    [Fact]
    public void NonLeftRecursiveGrammarElidesDetection()
    {
        var list = Deferred<string>();
        var element = Literals.Text("item").Or(list);

        list.Parser = Between(Literals.Char('['), ZeroOrMany(element.AndSkip(ZeroOrOne(Literals.Char(',')))), Literals.Char(']')).Then(static _ => "list");

        Assert.True(IsElided(list));
        Assert.True(list.TryParse("[item,[item,[]]]", out var result));
        Assert.Equal("list", result);
        Assert.False(list.TryParse("[item", out _));
    }

    [Fact]
    public void OperatorTableElidesDetection()
    {
        var expression = Deferred<string>();
        var primary = Terms.Text("1").Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-'), static x => "-" + x))
            .LeftAssociative((Terms.Char('+'), static (a, b) => "(" + a + "+" + b + ")"));

        Assert.True(IsElided(expression));
        Assert.True(expression.TryParse("-(1+1)+1", out var result));
        Assert.Equal("(-(1+1)+1)", result);
    }

    [Fact]
    public void LeftRecursionThroughOperatorTableKeepsDetection()
    {
        // The operand is the expression itself, at the position of the table
        var operand = Deferred<string>();
        operand.Parser = operand.Operators().LeftAssociative((Terms.Char('+'), static (a, b) => a + "+" + b)).Or(Terms.Text("1"));

        Assert.False(IsElided(operand));
        Assert.True(operand.TryParse("1", out var result));
        Assert.Equal("1", result);

        // A prefix operator which can read nothing invokes the operand at the position of the table
        var prefixed = Deferred<string>();
        prefixed.Parser = Terms.Text("1").Operators().Unary((ZeroOrOne(Terms.Char('-')).Then(static _ => 0).Or(prefixed.Then(static _ => 0)), static x => x));

        Assert.False(IsElided(prefixed));
    }

    [Fact]
    public void ParserAfterInputIsReadDoesNotPreventElision()
    {
        // The custom parser holds a parser, so it can invoke anything, but only after a char is read
        var a = Deferred<string>();
        a.Parser = Literals.Char('(').SkipAnd(new ForwardingParser<string>(a)).Or(Literals.Text("x"));

        Assert.True(IsElided(a));
        Assert.True(a.TryParse("((x", out var result));
        Assert.Equal("x", result);
    }

    [Fact]
    public void CustomLeafParserDoesNotPreventElision()
    {
        // Like a custom string literal parser of an application, which only holds plain data. It is not nested in a type
        // holding parsers, like a grammar.
        var a = Deferred<string>();
        var quoted = new QuotedTextParser();

        a.Parser = quoted.Or(Literals.Char('[').SkipAnd(a).AndSkip(Literals.Char(']')));

        Assert.True(IsElided(a));
        Assert.True(a.TryParse("[['q']]", out var result));
        Assert.Equal("q", result);
    }

    [Fact]
    public void CustomParserNestedInGrammarKeepsDetection()
    {
        // The custom parser holds no parser, but its grammar does
        var a = Deferred<string>();
        a.Parser = new GrammarWithNestedParser.NestedParser().Or(Literals.Char('[').SkipAnd(a));

        Assert.False(IsElided(a));
        Assert.True(a.TryParse("[[t", out var result));
        Assert.Equal("t", result);
    }

    [Fact]
    public void SamplesElideDetection()
    {
        var calc = Assert.IsType<Deferred<Calc.Expression>>(FluentParser.Expression);
        Assert.True(IsElided(calc));

        var json = FindDeferreds(JsonParser.Json);
        Assert.Single(json);
        Assert.True(IsElided(json[0]));

        var sql = FindDeferreds(typeof(SqlParser).GetMethod("CreateRuntimeParser", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!);
        Assert.Equal(5, sql.Count);
        Assert.All(sql, static deferred => Assert.True(IsElided(deferred)));
    }

    [Fact]
    public void ReassigningParserAnalyzesAgain()
    {
        var a = Deferred<string>();
        var b = Deferred<string>();

        a.Parser = b.Or(Literals.Text("x"));
        b.Parser = Literals.Text("y");

        Assert.True(a.TryParse("y", out var result));
        Assert.Equal("y", result);
        Assert.True(IsElided(a));
        Assert.True(IsElided(b));

        // a invokes b which invokes a, at the same position
        b.Parser = a.AndSkip(Literals.Text("z"));

        Assert.False(IsElided(a));
        Assert.False(IsElided(b));
        Assert.True(a.TryParse("x", out result));
        Assert.Equal("x", result);

        b.Parser = Literals.Text("y");

        Assert.True(IsElided(a));
        Assert.True(IsElided(b));
        Assert.True(a.TryParse("y", out result));
        Assert.Equal("y", result);
    }

    [Fact]
    public void ReassigningParserOfAnotherGrammarAnalyzesSharedParsersAgain()
    {
        // 'shared' is analyzed with 'a', then reached by 'b' at its start, which can then reach it again
        var shared = Deferred<string>();
        var a = Deferred<string>();
        var b = Deferred<string>();

        shared.Parser = Literals.Text("s");
        a.Parser = Literals.Char('(').SkipAnd(shared);
        b.Parser = shared.Or(Literals.Text("b"));

        Assert.True(IsElided(a));
        Assert.True(IsElided(shared));
        Assert.True(IsElided(b));

        shared.Parser = b.AndSkip(Literals.Text("z"));

        Assert.True(IsElided(a));
        Assert.False(IsElided(shared));
        Assert.False(IsElided(b));
        Assert.True(a.TryParse("(bz", out var result));
        Assert.Equal("b", result);
    }

    [Fact]
    public void ElidedDeferredDoesNotAllocateActiveParsers()
    {
        var context = new ParseContext(new Scanner("""{"a": ["b", {"c": "d"}, ["e"]]}"""));

        Assert.NotNull(JsonParser.Json.Parse(context));
        Assert.Null(GetActivePositions(context));

        // A deferred parser that keeps the detection still allocates them
        var loop = Deferred<string>();
        loop.Parser = Literals.Char('-').Optional().SkipAnd(loop).Or(Literals.Text("x"));

        context = new ParseContext(new Scanner("-x"));

        Assert.True(loop.TryParse(context, out var result, out _));
        Assert.Equal("x", result);
        Assert.NotNull(GetActivePositions(context));
    }

    [Fact]
    public void CursorIsRestoredWhenElidedDeferredFails()
    {
        var a = Deferred<string>();
        a.Parser = Literals.Text("a").And(Literals.Text("b")).Then(static x => x.Item1 + x.Item2);

        Assert.True(IsElided(a));

        var context = new ParseContext(new Scanner("ac"));
        var result = new ParseResult<string>();

        Assert.False(a.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ElidedDeferredEnforcesMaxRecursionDepth()
    {
        var list = Deferred<string>();
        list.Parser = Between(Literals.Char('['), list, Literals.Char(']')).Or(Literals.Text("item"));

        Assert.True(IsElided(list));

        var context = new ParseContext(new Scanner("[[[item]]]"), maxRecursionDepth: 3);

        Assert.False(list.TryParse(context, out _, out var error));
        Assert.Equal("The maximum parser recursion depth of 3 was exceeded.", error!.Message);
    }

    [Fact]
    public void WhiteSpaceParserReachingDeferredKeepsDetection()
    {
        // The comment skips white space with Terms, so it invokes the white space parser, which invokes the comment again
        // at the same position. The comment can't tell from its own grammar.
        var comment = Deferred<TextSpan>();
        comment.Parser = Capture(Terms.Text("/*").And(AnyCharBefore(Literals.Text("*/"))).And(Literals.Text("*/")));

        var whiteSpace = Capture(ZeroOrMany(Literals.WhiteSpace(includeNewLines: true).Or(comment)));

        Assert.True(IsElided(comment));

        var grammar = Terms.Text("a").And(Terms.Text("b"));

        Assert.True(grammar.WithWhiteSpaceParser(whiteSpace).TryParse("a /* c */ b", out var result));
        Assert.Equal(("a", "b"), result);

        var context = new ParseContext(new Scanner("a /* c */ b")) { WhiteSpaceParser = whiteSpace };

        Assert.True(grammar.TryParse(context, out result, out _));
        Assert.Equal(("a", "b"), result);
    }

    [Fact]
    public void WhiteSpaceParserNotReachingDeferredDoesNotPreventElision()
    {
        var list = Deferred<string>();
        list.Parser = Between(Terms.Char('['), list, Terms.Char(']')).Or(Terms.Text("item"));

        var grammar = list.WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithSingleLine("//"));
        var context = new ParseContext(new Scanner("[ // comment\n [ item ] ]"));

        Assert.True(grammar.TryParse(context, out var result, out _));
        Assert.Equal("item", result);
        Assert.Null(GetActivePositions(context));
    }

    [Fact]
    public void ConcurrentFirstParsesAgree()
    {
        var json = CreateJsonGrammar(out var deferred);

        Parallel.For(0, 64, _ =>
        {
            Assert.True(json.TryParse("[1, [2, [3]], [4]]", out var result));
            Assert.Equal(4, result);
            Assert.True(IsElided(deferred));
        });
    }

    [Fact]
    public void EveryParlotParserIsAnalyzable()
    {
        // A parser type the analysis doesn't know is treated as invoking any parser, which keeps the detection
        var parserTypes = typeof(Parser<>).Assembly.GetTypes()
            .Where(static type => type.IsClass && DerivesFromParser(type))
            .ToArray();

        Assert.NotEmpty(parserTypes);

        foreach (var type in parserTypes)
        {
            var analyzable = type.GetInterfaces().Any(static i => i.Name == "ILoopAnalyzable");

            Assert.True(analyzable || type.Name.StartsWith("DelegateParser", StringComparison.Ordinal), $"{type} is not analyzed");
        }

        static bool DerivesFromParser(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Parser<>))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static Parser<int> CreateJsonGrammar(out Deferred<int> deferred)
    {
        var value = Deferred<int>();
        var number = Terms.Integer().Then(static _ => 1);
        var array = Between(Terms.Char('['), Separated(Terms.Char(','), value), Terms.Char(']')).Then(static x => x.Sum());

        value.Parser = number.Or(array);
        deferred = value;

        return value;
    }

    /// <summary>
    /// Finds the deferred parsers of a grammar through the fields of its parsers, as a benchmark harness can.
    /// </summary>
    internal static List<object> FindDeferreds(object root)
    {
        var seen = new HashSet<object>(ReferenceComparer.Instance);
        var pending = new Stack<object>();
        var deferreds = new List<object>();

        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!seen.Add(current))
            {
                continue;
            }

            if (current.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(Deferred<>))
            {
                deferreds.Add(current);
            }

            if (current is IEnumerable items and not string)
            {
                foreach (var item in items)
                {
                    if (item is not null)
                    {
                        pending.Push(item);
                    }
                }

                continue;
            }

            for (var t = current.GetType(); t is not null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.GetValue(current) is { } value && value is not Delegate and not string && !field.FieldType.IsPrimitive
                        && (value.GetType().Namespace?.StartsWith("Parlot", StringComparison.Ordinal) == true || value is IEnumerable || value is ITuple))
                    {
                        pending.Push(value);
                    }
                }
            }
        }

        return deferreds;
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Invokes another parser.
    /// </summary>
    private sealed class ForwardingParser<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public ForwardingParser(Parser<T> parser) => _parser = parser;

        public override bool Parse(ParseContext context, ref ParseResult<T> result) => _parser.Parse(context, ref result);
    }
}

/// <summary>
/// Reads a single quoted text, like the custom string parsers of applications.
/// </summary>
internal sealed class QuotedTextParser : Parser<string>, ISeekable
{
    public bool CanSeek => true;

    public char[] ExpectedChars { get; } = ['\''];

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (cursor.Current != '\'')
        {
            return false;
        }

        var end = context.Scanner.Buffer.IndexOf('\'', start + 1);

        if (end < 0)
        {
            return false;
        }

        cursor.Advance(end + 1 - start);
        result.Set(start, end + 1, context.Scanner.Buffer.Substring(start + 1, end - start - 1));
        return true;
    }
}

/// <summary>
/// Succeeds without reading anything.
/// </summary>
internal sealed class EmptyTextParser : Parser<string>
{
    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        result.Set(context.Scanner.Cursor.Offset, context.Scanner.Cursor.Offset, "");
        return true;
    }
}

/// <summary>
/// A grammar holding a custom parser, which can reach the parsers of the grammar.
/// </summary>
internal static class GrammarWithNestedParser
{
    public static readonly Parser<string> Text = Literals.Text("t");

    public sealed class NestedParser : Parser<string>
    {
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => Text.Parse(context, ref result);
    }
}
