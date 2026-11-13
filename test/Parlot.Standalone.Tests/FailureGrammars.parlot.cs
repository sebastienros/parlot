using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class FailureGrammars
{
    // SELECT a, b FROM t
    [GenerateParser(nameof(Select))]
    public static Parser<string> BuildSelect() =>
        Terms.Keyword("SELECT", caseInsensitive: true)
            .SkipAnd(Separated(Terms.Char(','), Terms.Identifier()))
            .AndSkip(Terms.Keyword("FROM", caseInsensitive: true))
            .And(Terms.Identifier())
            .Then(static value => value.Item2.ToString() + ":" + value.Item1.Count)
            .Eof();

    // Nested parenthesized sums: 1 + (2 + 3)
    [GenerateParser(nameof(Expression))]
    public static Parser<long> BuildExpression()
    {
        var expression = Deferred<long>();
        var primary = Terms.Integer().Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        expression.Parser = primary.And(ZeroOrMany(Terms.Char('+').SkipAnd(primary)))
            .Then(static value =>
            {
                var sum = value.Item1;
                foreach (var item in value.Item2)
                {
                    sum += item;
                }
                return sum;
            });
        return expression.Eof();
    }

    // Only the overload returning the failure is declared
    [GenerateParser(nameof(Pair))]
    public static Parser<(long, long)> BuildPair() =>
        Terms.Integer().AndSkip(Terms.Char(',')).And(Terms.Integer()).Eof();

    // An Error reports its own position
    [GenerateParser(nameof(Error))]
    public static Parser<long> BuildError() =>
        Terms.Char('[').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(']').ElseError("Expected ']'"));

    // Span code: the cursor doesn't move until the whole sequence matched
    [GenerateParser(nameof(Span))]
    public static Parser<string> BuildSpan() =>
        Literals.Text("abc").SkipAnd(Literals.Char(':')).SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2))
            .Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(Configured))]
    public static Parser<long> BuildConfigured(long factor) =>
        Terms.Integer().Then(value => value * factor).AndSkip(Terms.Char(';')).Eof();

    // A word that the condition rejects, like a keyword read as a word
    [GenerateParser(nameof(Rejected))]
    public static Parser<string> BuildRejected() =>
        Terms.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from").AndSkip(Terms.Char(';')).Eof();

    // The same word, rejected by a conversion
    [GenerateParser(nameof(ConversionRejected))]
    public static Parser<string> BuildConversionRejected() =>
        Terms.Identifier().ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
        {
            result = value.ToString();
            return result != "from";
        }).AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(ConversionRejectedLiteral))]
    public static Parser<string> BuildConversionRejectedLiteral() =>
        Literals.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString())).AndSkip(Terms.Char(';')).Eof();

    // A value that a conversion rejects after another word
    [GenerateParser(nameof(ConversionSecond))]
    public static Parser<string> BuildConversionSecond() =>
        Terms.Identifier().SkipAnd(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString()))).Eof();

    // A longer attempt fails before the value is rejected
    [GenerateParser(nameof(ConversionFurther))]
    public static Parser<string> BuildConversionFurther() =>
        Terms.Text("from").And(Terms.Char('(')).And(Terms.Char(')')).Then(static _ => "call")
            .Or(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString()))).Eof();

    // The word after comments, which a parser skips instead of the white space set
    [GenerateParser(nameof(ConversionRejectedAfterComments))]
    public static Parser<string> BuildConversionRejectedAfterComments() =>
        BuildConversionRejected().WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    // The word after a text which isn't there: its white space was skipped before the word is read
    [GenerateParser(nameof(ConversionRejectedAlternative))]
    public static Parser<string> BuildConversionRejectedAlternative() =>
        Terms.Text("select").Or(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString())))
            .AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(RejectedLiteral))]
    public static Parser<string> BuildRejectedLiteral() =>
        Literals.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from").AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(Second))]
    public static Parser<string> BuildSecond() =>
        Terms.Identifier().SkipAnd(Terms.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from")).Eof();

    [GenerateParser(nameof(NotFollowed))]
    public static Parser<string> BuildNotFollowed() =>
        Terms.Identifier().WhenNotFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(Followed))]
    public static Parser<string> BuildFollowed() =>
        Terms.Identifier().WhenFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char('x')).Eof();

    [GenerateParser(nameof(Peeked))]
    public static Parser<string> BuildPeeked() =>
        Peek(Terms.Text("((")).SkipAnd(Terms.Char('x')).Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(Negative))]
    public static Parser<string> BuildNegative() =>
        Not(Terms.Text("abc")).SkipAnd(Terms.Identifier()).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();
}
