using Parlot.Fluent;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

#nullable enable

public class ThenIssueTests
{
    [Theory]
    [InlineData("""
        // A Comment
        var a = 'test'
        """)]
    [InlineData("""
        var a = 'test'
        """)]
    public void ThenWithNonIConvertibleTypeShouldWork(string testString)
    {
        var commentParser = Terms.Text("//").And(AnyCharBefore(OneOf(Literals.Char('\n'), Literals.Char('\r'))));
        var varKeyword = Terms.Text("var");
        var identifier = Terms.Identifier();
        var equal = Terms.Char('=');
        var str = Terms.String();

        var expressionParser = varKeyword.And(identifier).And(equal).And(str).Then<BaseExp?>(a => new AssignExpression(a.Item2.ToString()!, a.Item4.ToString()!));

        var simpleParser = OneOf(commentParser.Then<BaseExp>(), expressionParser);

        var success = simpleParser.TryParse(testString, out var result);
        
        Assert.True(success);
    }

    public abstract record BaseExp;

    public record AssignExpression(string Name, string Value) : BaseExp;
}
