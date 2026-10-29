using Parlot.Fluent;
using Parlot.Tests.Json;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using static Parlot.Fluent.Parsers;


namespace Parlot.Tests;

public class OperatorsTests
{
    [Fact]
    public void Sequence_Operator_Plus_Works()
    {
        var parser = Literals.Char('a') + Literals.Char('b') + Literals.Char('c');
        var success = parser.TryParse("abc", out var result);
        Assert.True(success);
        Assert.Equal(('a', 'b', 'c'), result);
    }

    [Fact]
    public void Choice_Operator_Pipe_Works()
    {
        var parser = Literals.Char('a') | Literals.Char('b') | Literals.Char('c');
        var successA = parser.TryParse("a", out var resultA);
        var successB = parser.TryParse("b", out var resultB);
        var successC = parser.TryParse("c", out var resultC);
        var successD = parser.TryParse("d", out _);
        Assert.True(successA);
        Assert.Equal('a', resultA);
        Assert.True(successB);
        Assert.Equal('b', resultB);
        Assert.True(successC);
        Assert.Equal('c', resultC);
        Assert.False(successD);
    }

    [Fact]
    public void Choice_Operator_Pipe_Works_With_Mixed_Parsers()
    {
        IParser<char> parser1 = Literals.Char('a');
        Parser<char> parser2 = Literals.Char('b');
        var parser = parser1 | parser2 | Literals.Char('c');
        var successA = parser.TryParse("a", out var resultA);
        var successB = parser.TryParse("b", out var resultB);
        var successC = parser.TryParse("c", out var resultC);
        var successD = parser.TryParse("d", out _);
        Assert.True(successA);
        Assert.Equal('a', resultA);
        Assert.True(successB);
        Assert.Equal('b', resultB);
        Assert.True(successC);
        Assert.Equal('c', resultC);
        Assert.False(successD);
    }

    [Fact]
    public void CanMix_Operator_Pipe_And_Plus()
    {
        var choiceParser = Literals.Char('x') | Literals.Char('y');
        var parser = Literals.Char('a') + Literals.Char('b') + choiceParser;
        var successX = parser.TryParse("abx", out var resultX);
        var successY = parser.TryParse("aby", out var resultY);
        var successZ = parser.TryParse("abz", out _);
        Assert.True(successX);
        Assert.Equal(('a', 'b', 'x'), resultX);
        Assert.True(successY);
        Assert.Equal(('a', 'b', 'y'), resultY);
        Assert.False(successZ);
    }

    [Fact]
    public void Operator_Plus_Supports_Covariance()
    {
        var animalParser = Literals.Char('a').Then(c => new Animal());
        var dogParser = Literals.Char('b').Then(c => new Dog());
        var parser = animalParser + dogParser;
        var success = parser.TryParse("ab", out var result);
        Assert.True(success);
        Assert.IsType<Animal>(result.Item1);
        Assert.IsType<Dog>(result.Item2);
    }

    private class Animal;
    private class Dog : Animal;

    [Fact]
    public void Operator_Pipe_Passes_Typed_Parsers_Without_Adapter()
    {
        var a = Terms.Integer();
        var b = Terms.Integer();

        var parser = a | b;

        // The operands are the parsers themselves, not adapters, so OneOf could strip their SkipWhiteSpace wrappers
        Assert.Same(a, parser.OriginalParsers[0]);
        Assert.Same(b, parser.OriginalParsers[1]);
        Assert.True(parser.SkipWhitespace);
        Assert.All(parser.Parsers, static p => Assert.IsNotType<SkipWhiteSpace<long>>(p));

        var or = Assert.IsType<OneOf<long>>(a.Or(b));
        Assert.Equal(or.SkipWhitespace, parser.SkipWhitespace);
        Assert.Equal(or.Parsers.Count, parser.Parsers.Count);

        Assert.Equal(123, parser.Parse("  123"));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Operator_Pipe_Allocates_Like_Or()
    {
        var pipe = Terms.Integer() | Terms.Integer();
        var or = Terms.Integer().Or(Terms.Integer());

        Assert.Equal(Allocated(or), Allocated(pipe));

        static long Allocated(Parser<long> parser)
        {
            var context = new ParseContext(new Scanner("  123"));
            var result = new ParseResult<long>();

            // Warm up, such that the tiering and the lazy initializations are not measured
            for (var i = 0; i < 100; i++)
            {
                context.Scanner.Cursor.ResetPosition(TextPosition.Start);
                Assert.True(parser.Parse(context, ref result));
            }

            var before = System.GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < 100; i++)
            {
                context.Scanner.Cursor.ResetPosition(TextPosition.Start);
                parser.Parse(context, ref result);
            }

            return System.GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
#endif

    [Fact]
    public void Operator_Pipe_Supports_Covariance()
    {
        var animalParser = Literals.Char('a').Then(c => new Animal());
        var dogParser = Literals.Char('b').Then(c => new Dog());
        var parser = animalParser | dogParser;
        var successA = parser.TryParse("a", out var resultA);
        var successB = parser.TryParse("b", out var resultB);
        Assert.True(successA);
        Assert.IsType<Animal>(resultA);
        Assert.True(successB);
        Assert.IsType<Dog>(resultB);

        var parser2 = dogParser | animalParser;
        successA = parser.TryParse("a", out resultA);
        successB = parser.TryParse("b", out resultB);
        Assert.True(successA);
        Assert.IsType<Animal>(resultA);
        Assert.True(successB);
        Assert.IsType<Dog>(resultB);
    }

    [Fact]
    public void Can_Parse_Json_With_Operators()
    {
        var LBrace = Terms.Char('{');
        var RBrace = Terms.Char('}');
        var LBracket = Terms.Char('[');
        var RBracket = Terms.Char(']');
        var Colon = Terms.Char(':');
        var Comma = Terms.Char(',');

        var String = Terms.String(StringLiteralQuotes.Double);

        var jsonString =
            String
                .Then(static s => new JsonString(s.ToString()));

        var json = Deferred<IJson>();

        var jsonArray =
            Between(LBracket, Separated(Comma, json), RBracket)
                .Then(static els => new JsonArray(els));

        var jsonMember =
            (String + Colon + json).Then(static member => new KeyValuePair<string, IJson>(member.Item1.ToString(), member.Item3));

        var jsonObject =
            (LBrace + Separated(Comma, jsonMember) + RBrace)
                // Dictionary<,>(IEnumerable<KeyValuePair<,>>) is .NET Core 2.0+; on .NET Framework it
                // binds to the (int capacity) overload instead, so build the dictionary explicitly.
                .Then(static kvps => new JsonObject(kvps.Item2.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)));

        var Json = json.Parser = jsonString.Then<IJson>() | jsonArray |jsonObject;

        var input = "{\"name\":\"John\",\"age\":\"30\",\"cars\":[\"Ford\",\"BMW\",\"Fiat\"]}";
        var success = Json.TryParse(input, out var result);
        Assert.True(success);

    }

    [Fact]
    public void Operator_ShiftRight_Skips_Left_Result()
    {
        var parser = Terms.Char('(') >> Terms.Integer();

        Assert.IsType<SequenceSkipAnd<char, long>>(parser);
        Assert.True(parser.TryParse("( 42", out var result));
        Assert.Equal(42, result);
    }

    [Fact]
    public void Operator_ShiftLeft_Skips_Right_Result()
    {
        var parser = Terms.Integer() << Terms.Char(')');

        Assert.IsType<SequenceAndSkip<long, char>>(parser);
        Assert.True(parser.TryParse("42 )", out var result));
        Assert.Equal(42, result);
    }

    [Fact]
    public void Operator_Shifts_Chain_Like_Between()
    {
        var parser = Terms.Char('(') >> Terms.Integer() << Terms.Char(')');

        Assert.True(parser.TryParse(" ( 42 ) ", out var result));
        Assert.Equal(42, result);
        Assert.False(parser.TryParse("( 42", out _));
        Assert.False(parser.TryParse("42 )", out _));
    }

    [Fact]
    public void Operator_Shifts_Bind_Looser_Than_Plus()
    {
        // + is additive and binds tighter than the shift operators, so each side of a shift is one sequence
        var parser = Terms.Identifier() + Terms.Char('=') >> Terms.Integer() + Terms.Char(';');

        Assert.True(parser.TryParse("age = 12;", out var result));
        Assert.Equal((12L, ';'), result);

        var kept = Terms.Identifier() + Terms.Char('=') << Terms.Integer() + Terms.Char(';');

        Assert.True(kept.TryParse("age = 12;", out var left));
        Assert.Equal("age", left.Item1.ToString());
        Assert.Equal('=', left.Item2);
    }

    [Fact]
    public void Operator_Minus_Excludes_Right_Parser()
    {
        // Keyword checks the word boundary; Text("if") would also exclude "iffy", like Not(Text("if")) does
        var keyword = Terms.Keyword("if") | Terms.Keyword("else");
        var parser = Terms.Identifier() - keyword;

        Assert.True(parser.TryParse("iffy", out var result));
        Assert.Equal("iffy", result.ToString());
        Assert.False(parser.TryParse("if", out _));
        Assert.False(parser.TryParse("else", out _));

        var fluent = Not(keyword).SkipAnd(Terms.Identifier());
        Assert.Equal(fluent.TryParse("if", out _), parser.TryParse("if", out _));
    }

    [Fact]
    public void Operator_Minus_Restores_Position_On_Failure()
    {
        var parser = Terms.Identifier() - Terms.Text("if");
        var context = new ParseContext(new Scanner("  if"));
        var result = new ParseResult<TextSpan>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void Operator_Modulo_Builds_Separated_List()
    {
        var parser = Terms.Integer() % Terms.Char(',');

        Assert.IsType<Separated<char, long>>(parser);
        Assert.True(parser.TryParse("1, 2, 3", out var result));
        Assert.Equal([1, 2, 3], result);
        Assert.False(parser.TryParse("", out _));

        var fluent = Separated(Terms.Char(','), Terms.Integer());
        Assert.Equal(fluent.Parse("1,2"), parser.Parse("1,2"));
    }

    [Fact]
    public void Operator_Modulo_Binds_Tighter_Than_Plus()
    {
        // % is multiplicative, so the list is built before the sequence
        var parser = Terms.Char('[') >> Terms.Integer() % Terms.Char(',') << Terms.Char(']');

        Assert.True(parser.TryParse("[1, 2]", out var result));
        Assert.Equal([1, 2], result);
    }

    [Fact]
    public void Operator_Not_Succeeds_When_Parser_Fails()
    {
        var parser = !Terms.Char('a');

        Assert.IsType<Not<char>>(parser);
        Assert.True(parser.TryParse("b", out _));
        Assert.False(parser.TryParse("a", out _));

        var context = new ParseContext(new Scanner("a"));
        var result = new ParseResult<char>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void Operator_Unary_Minus_Is_Optional()
    {
        var parser = -Terms.Integer();

        Assert.IsType<Optional<long>>(parser);
        Assert.True(parser.TryParse("42", out var some));
        Assert.True(some.HasValue);
        Assert.Equal(42, some.Value);
        Assert.True(parser.TryParse("x", out var none));
        Assert.False(none.HasValue);
    }

    [Fact]
    public void Operator_Unary_Plus_Is_OneOrMany()
    {
        var parser = +Terms.Integer();

        Assert.IsType<OneOrMany<long>>(parser);
        Assert.True(parser.TryParse("1 2 3", out var result));
        Assert.Equal([1, 2, 3], result);
        Assert.False(parser.TryParse("x", out _));
    }

    [Fact]
    public void Operator_Tilde_Is_ZeroOrMany()
    {
        var parser = ~Terms.Integer();

        Assert.IsType<ZeroOrMany<long>>(parser);
        Assert.True(parser.TryParse("1 2 3", out var result));
        Assert.Equal([1, 2, 3], result);
        Assert.True(parser.TryParse("x", out var empty));
        Assert.Empty(empty);
    }

    [Fact]
    public void Unary_Operators_Compose_With_Binary_Ones()
    {
        // sign? digits (',' digits)*  ->  (Option<char>, long, list)
        var parser = -Terms.Char('-') + Terms.Integer() + ~(Terms.Char(',') >> Terms.Integer());

        Assert.True(parser.TryParse("-1, 2, 3", out var result));
        Assert.True(result.Item1.HasValue);
        Assert.Equal(1, result.Item2);
        Assert.Equal([2, 3], result.Item3);

        Assert.True(parser.TryParse("7", out result));
        Assert.False(result.Item1.HasValue);
        Assert.Equal(7, result.Item2);
        Assert.Empty(result.Item3);
    }

    [Fact]
    public void Operators_Are_Usable_In_Choices()
    {
        // The parsers built by the operators keep the ISeekable hints of their operands, so OneOf can still build its lookup
        var parser = (Terms.Char('(') >> Terms.Integer() << Terms.Char(')')) | Terms.Integer();

        Assert.True(parser.TryParse("(1)", out var result));
        Assert.Equal(1, result);
        Assert.True(parser.TryParse("2", out result));
        Assert.Equal(2, result);
        Assert.False(parser.TryParse("x", out _));
    }
}
