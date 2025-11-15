using Parlot.Fluent;
using Parlot.Tests.Json;
using System.Collections.Generic;
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

    private class Animal { }
    private class Dog : Animal { }

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
                .Then(static kvps => new JsonObject(new Dictionary<string, IJson>(kvps.Item2)));

        var Json = json.Parser = jsonString.Then<IJson>() | jsonArray |jsonObject;

        var input = "{\"name\":\"John\",\"age\":\"30\",\"cars\":[\"Ford\",\"BMW\",\"Fiat\"]}";
        var success = Json.TryParse(input, out var result);
        Assert.True(success);

    }
}
