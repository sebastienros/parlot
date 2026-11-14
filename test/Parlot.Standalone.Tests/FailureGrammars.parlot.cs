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

    [GenerateParser(nameof(ErrorNesting))]
    public static Parser<long> BuildErrorNesting() =>
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

    // A value of two words: the white space before it isn't the last one that was skipped when it is rejected, and the
    // other alternative reads it
    [GenerateParser(nameof(RejectedPair))]
    public static Parser<string> BuildRejectedPair() =>
        Terms.Identifier().And(Terms.Identifier()).When(static (_, value) => value.Item2.ToString() != "from")
            .Then(static value => value.Item1.ToString() + " " + value.Item2.ToString())
            .Or(Literals.WhiteSpace(includeNewLines: true).SkipAnd(Literals.Identifier()).AndSkip(Terms.Text("from")).Then(static value => value.ToString() + " from"))
            .AndSkip(Terms.Char(';')).Eof();

    // The same word after comments, which a white space parser skips
    [GenerateParser(nameof(RejectedAfterComments))]
    public static Parser<string> BuildRejectedAfterComments() =>
        Terms.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from").AndSkip(Terms.Char(';')).Eof()
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    // White space which is read as a value, by a parser which doesn't skip any, where a lookahead skipped it first
    [GenerateParser(nameof(RejectedWhiteSpace))]
    public static Parser<string> BuildRejectedWhiteSpace() =>
        Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().When(static (_, _) => false).Then(static value => value.ToString())).Eof();

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

    // A value of two words, read by moving the cursor forward over the white space between them, that the condition rejects
    [GenerateParser(nameof(RejectedWords))]
    public static Parser<string> BuildRejectedWords() =>
        Terms.Identifier().And(Terms.Identifier()).When(static (_, value) => value.Item2.ToString() != "from")
            .Then(static value => value.Item1.ToString()).AndSkip(Terms.Char(';')).Eof();

    // The same words, rejected by a conversion
    [GenerateParser(nameof(ConversionRejectedWords))]
    public static Parser<string> BuildConversionRejectedWords() =>
        Terms.Identifier().And(Terms.Identifier()).ThenTry(static value => (value.Item2.ToString() != "from", value.Item1.ToString()))
            .AndSkip(Terms.Char(';')).Eof();

    // A word and the longest of two texts, where the cursor is moved forward to the end of the one a selector keeps
    [GenerateParser(nameof(RejectedLongest))]
    public static Parser<string> BuildRejectedLongest() =>
        Terms.Identifier().And(Longest(static matches => matches.IndexOfLongest(), Terms.Text("fr"), Terms.Text("from")))
            .When(static (_, value) => value.Item2 != "from").Then(static value => value.Item1.ToString()).AndSkip(Terms.Char(';')).Eof();

    // The same, without a selector: the cursor is moved back from each text which matched, to read the next one
    [GenerateParser(nameof(RejectedLongestText))]
    public static Parser<string> BuildRejectedLongestText() =>
        Terms.Identifier().And(Longest(Terms.Text("fr"), Terms.Text("from")))
            .When(static (_, value) => value.Item2 != "from").Then(static value => value.Item1.ToString()).AndSkip(Terms.Char(';')).Eof();

    // The same, with an alternative which isn't a text
    [GenerateParser(nameof(RejectedLongestWord))]
    public static Parser<string> BuildRejectedLongestWord() =>
        Terms.Identifier().And(Longest(Terms.Text("fr"), Terms.Identifier().Then(static value => value.ToString())))
            .When(static (_, value) => value.Item2 != "from").Then(static value => value.Item1.ToString()).AndSkip(Terms.Char(';')).Eof();

    // Span code which moves the cursor to skip white space, doesn't find an optional text after it, and reads on or ends
    // before the white space: the cursor is moved back
    [GenerateParser(nameof(SpanBacktracking))]
    public static Parser<string> BuildSpanBacktracking() =>
        Terms.Text("a").AndSkip(Terms.Text("b").AndSkip(Terms.Text("c")).Optional()).AndSkip(Terms.Text("d").Optional()).Eof();

    // A text before a delimiter which isn't consumed, then the delimiter, that the condition rejects: the cursor is moved
    // back from the delimiter which matched
    [GenerateParser(nameof(RejectedTextBefore))]
    public static Parser<string> BuildRejectedTextBefore() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;")).AndSkip(Literals.Text(";;"))
            .Then(static value => value.ToString()).When(static (_, value) => !value.StartsWith("from", System.StringComparison.Ordinal)))
            .AndSkip(Terms.Char('!')).Eof();

    // The same, with a delimiter which skips white space and isn't searched for by its first char
    [GenerateParser(nameof(RejectedTextBeforeTerm))]
    public static Parser<string> BuildRejectedTextBeforeTerm() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;")).AndSkip(Terms.Text(";;"))
            .Then(static value => value.ToString()).When(static (_, value) => !value.StartsWith("from", System.StringComparison.Ordinal)))
            .AndSkip(Terms.Char('!')).Eof();

    // A text whose delimiter is consumed
    [GenerateParser(nameof(TextBeforeConsumed))]
    public static Parser<string> BuildTextBeforeConsumed() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), consumeDelimiter: true))
            .Then(static value => value.ToString()).AndSkip(Terms.Char('!')).Eof();

    // A text which can be empty, followed by a text which isn't its delimiter
    [GenerateParser(nameof(TextBeforeThenOther))]
    public static Parser<string> BuildTextBeforeThenOther() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), canBeEmpty: true))
            .Then(static value => value.ToString()).AndSkip(Literals.Text(";;!")).Eof();

    // A text whose delimiter must be found
    [GenerateParser(nameof(TextBeforeRequired))]
    public static Parser<string> BuildTextBeforeRequired() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), failOnEof: true))
            .Then(static value => value.ToString()).AndSkip(Literals.Text(";;")).Eof();

    // A delimiter whose first alternative reads further than the one which matches, and which isn't consumed: a lookahead
    [GenerateParser(nameof(RejectedTextBeforeChoice))]
    public static Parser<string> BuildRejectedTextBeforeChoice() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;")))
            .Then(static value => value.ToString()).When(static (_, value) => !value.StartsWith("from", System.StringComparison.Ordinal)))
            .AndSkip(Literals.Text(";;")).Eof();

    // A delimiter of two parsers, which reads a `;` wherever there is one in the text, and is consumed
    [GenerateParser(nameof(RejectedTextBeforeConsumed))]
    public static Parser<string> BuildRejectedTextBeforeConsumed() =>
        Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Char(';').And(Terms.Char(';')), consumeDelimiter: true)
            .Then(static value => value.ToString()).When(static (_, value) => !value.StartsWith("from", System.StringComparison.Ordinal)))
            .AndSkip(Terms.Char('!')).Eof();

    // An expression whose factories receive the operators, that the condition rejects: the cursor is moved back over the
    // white space skipped to look for an operator, for the level which parses it
    [GenerateParser(nameof(RejectedValuedOperators))]
    public static Parser<long> BuildRejectedValuedOperators() =>
        Terms.Char('>').SkipAnd(Terms.Integer().Operators()
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => op == "*" ? a * b : a / b)
            .LeftAssociative(new[] { Terms.Text("+"), Terms.Text("-") }, static (a, b, op) => op == "+" ? a + b : a - b)
            .When(static (_, value) => value != 3)).AndSkip(Terms.Char(';')).Eof();

    // Span code: an alternative which reads a word and fails, before one which matches. The position it read up to is
    // only in a local, which is moved back for the next alternative.
    [GenerateParser(nameof(SpanChoice))]
    public static Parser<string> BuildSpanChoice() =>
        Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;")).AndSkip(Literals.Char('x')).Eof();

    // The same choice, in a value that the condition rejects
    [GenerateParser(nameof(RejectedSpanChoice))]
    public static Parser<string> BuildRejectedSpanChoice() =>
        Terms.Char('>').SkipAnd(Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;"))
            .When(static (_, _) => false)).Eof();

    // Span code: an optional sequence, and the element of a loop, which read a text and fail, without white space
    [GenerateParser(nameof(SpanOptional))]
    public static Parser<string> BuildSpanOptional() =>
        Literals.Text("a").AndSkip(Literals.Text("b").AndSkip(Literals.Text("c")).Optional()).AndSkip(Literals.Text("d").Optional()).Eof();

    [GenerateParser(nameof(SpanLoop))]
    public static Parser<string> BuildSpanLoop() =>
        Literals.Text("a").AndSkip(ZeroOrMany(Literals.Text("b").AndSkip(Literals.Text("c")))).AndSkip(Literals.Text("d").Optional()).Eof();

    // Span code: the longest of two alternatives, where the one which fails read further than the one which is kept
    [GenerateParser(nameof(SpanLongest))]
    public static Parser<string> BuildSpanLongest() =>
        Literals.Text("a").AndSkip(Longest(Literals.Text("fr"), Literals.Text("from").AndSkip(Literals.Char('!')))).AndSkip(Literals.Char('x').Optional()).Eof();

    // A value read by span code, without white space, that the condition rejects
    [GenerateParser(nameof(RejectedSpan))]
    public static Parser<string> BuildRejectedSpan() =>
        Literals.Identifier().And(Literals.Char(':')).And(Literals.Identifier())
            .When(static (_, value) => value.Item3.ToString() != "from").Then(static value => value.Item1.ToString()).AndSkip(Terms.Char(';')).Eof();

    // Span code: a token which isn't found after the white space it skipped, in a value which goes on over that white space
    [GenerateParser(nameof(RejectedSpanWhiteSpace))]
    public static Parser<string> BuildRejectedSpanWhiteSpace() =>
        Terms.Text("a").AndSkip(Terms.Text("b").Optional()).AndSkip(Literals.Text(" c").Optional()).When(static (_, _) => false).Eof();
}
