#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// A value which is read and then rejected fails at its start, after the white space its parser skips, whichever way it is
/// rejected (a <c>When</c> condition, a <c>ThenTry</c> conversion, one returning a tuple), whoever skipped that white space
/// first, and whether the parser is the runtime one or the generated one, reading a string or a <see cref="TextReader"/>.
/// </summary>
public class RejectedValueParityTests
{
    private delegate bool StringParser(string text, out string value, out int failure);
    private delegate bool ReaderParser(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    private static readonly string[] Forms = ["When", "Conversion", "Tuple"];

    // The runtime copies of RejectedValueGrammars
    private static Parser<string> When(Parser<TextSpan> word) =>
        word.When(static (_, value) => value.ToString() != "from").Then(static value => value.ToString());

    private static Parser<string> Conversion(Parser<TextSpan> word) =>
        word.ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
        {
            result = value.ToString();
            return result != "from";
        });

    private static Parser<string> Tuple(Parser<TextSpan> word) =>
        word.ThenTry(static value => (value.ToString() != "from", value.ToString()));

    private static Parser<string> Token(Func<Parser<TextSpan>, Parser<string>> reject) =>
        reject(Terms.Identifier()).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Literal(Func<Parser<TextSpan>, Parser<string>> reject) =>
        reject(Literals.Identifier()).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Alternative(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Lookahead(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Not(Terms.Char('x')).SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Peeked(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Peek(Terms.Identifier()).SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Second(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Identifier().SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> Comments(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Token(reject).WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    private static Parser<string> InnerComments(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier()).WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/")))
            .AndSkip(Terms.Char(';')).Eof();

    private static Parser<string> InnerSet(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier()).WithWhiteSpaceSet(WhiteSpaceSet.Default))
            .AndSkip(Terms.Char(';')).Eof().WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

    private static readonly Dictionary<string, (Parser<string> Runtime, StringParser Generated, ReaderParser GeneratedReader)> Grammars = new()
    {
        ["TokenWhen"] = (Token(When), RejectedValueGrammars.TryParseTokenWhen, RejectedValueGrammars.TryParseTokenWhen),
        ["TokenConversion"] = (Token(Conversion), RejectedValueGrammars.TryParseTokenConversion, RejectedValueGrammars.TryParseTokenConversion),
        ["TokenTuple"] = (Token(Tuple), RejectedValueGrammars.TryParseTokenTuple, RejectedValueGrammars.TryParseTokenTuple),
        ["LiteralWhen"] = (Literal(When), RejectedValueGrammars.TryParseLiteralWhen, RejectedValueGrammars.TryParseLiteralWhen),
        ["LiteralConversion"] = (Literal(Conversion), RejectedValueGrammars.TryParseLiteralConversion, RejectedValueGrammars.TryParseLiteralConversion),
        ["LiteralTuple"] = (Literal(Tuple), RejectedValueGrammars.TryParseLiteralTuple, RejectedValueGrammars.TryParseLiteralTuple),
        ["AlternativeWhen"] = (Alternative(When), RejectedValueGrammars.TryParseAlternativeWhen, RejectedValueGrammars.TryParseAlternativeWhen),
        ["AlternativeConversion"] = (Alternative(Conversion), RejectedValueGrammars.TryParseAlternativeConversion, RejectedValueGrammars.TryParseAlternativeConversion),
        ["AlternativeTuple"] = (Alternative(Tuple), RejectedValueGrammars.TryParseAlternativeTuple, RejectedValueGrammars.TryParseAlternativeTuple),
        ["LookaheadWhen"] = (Lookahead(When), RejectedValueGrammars.TryParseLookaheadWhen, RejectedValueGrammars.TryParseLookaheadWhen),
        ["LookaheadConversion"] = (Lookahead(Conversion), RejectedValueGrammars.TryParseLookaheadConversion, RejectedValueGrammars.TryParseLookaheadConversion),
        ["LookaheadTuple"] = (Lookahead(Tuple), RejectedValueGrammars.TryParseLookaheadTuple, RejectedValueGrammars.TryParseLookaheadTuple),
        ["PeekedWhen"] = (Peeked(When), RejectedValueGrammars.TryParsePeekedWhen, RejectedValueGrammars.TryParsePeekedWhen),
        ["PeekedConversion"] = (Peeked(Conversion), RejectedValueGrammars.TryParsePeekedConversion, RejectedValueGrammars.TryParsePeekedConversion),
        ["PeekedTuple"] = (Peeked(Tuple), RejectedValueGrammars.TryParsePeekedTuple, RejectedValueGrammars.TryParsePeekedTuple),
        ["SecondWhen"] = (Second(When), RejectedValueGrammars.TryParseSecondWhen, RejectedValueGrammars.TryParseSecondWhen),
        ["SecondConversion"] = (Second(Conversion), RejectedValueGrammars.TryParseSecondConversion, RejectedValueGrammars.TryParseSecondConversion),
        ["SecondTuple"] = (Second(Tuple), RejectedValueGrammars.TryParseSecondTuple, RejectedValueGrammars.TryParseSecondTuple),
        ["CommentsWhen"] = (Comments(When), RejectedValueGrammars.TryParseCommentsWhen, RejectedValueGrammars.TryParseCommentsWhen),
        ["CommentsConversion"] = (Comments(Conversion), RejectedValueGrammars.TryParseCommentsConversion, RejectedValueGrammars.TryParseCommentsConversion),
        ["CommentsTuple"] = (Comments(Tuple), RejectedValueGrammars.TryParseCommentsTuple, RejectedValueGrammars.TryParseCommentsTuple),
        ["InnerCommentsWhen"] = (InnerComments(When), RejectedValueGrammars.TryParseInnerCommentsWhen, RejectedValueGrammars.TryParseInnerCommentsWhen),
        ["InnerCommentsConversion"] = (InnerComments(Conversion), RejectedValueGrammars.TryParseInnerCommentsConversion, RejectedValueGrammars.TryParseInnerCommentsConversion),
        ["InnerCommentsTuple"] = (InnerComments(Tuple), RejectedValueGrammars.TryParseInnerCommentsTuple, RejectedValueGrammars.TryParseInnerCommentsTuple),
        ["InnerSetWhen"] = (InnerSet(When), RejectedValueGrammars.TryParseInnerSetWhen, RejectedValueGrammars.TryParseInnerSetWhen),
        ["InnerSetConversion"] = (InnerSet(Conversion), RejectedValueGrammars.TryParseInnerSetConversion, RejectedValueGrammars.TryParseInnerSetConversion),
        ["InnerSetTuple"] = (InnerSet(Tuple), RejectedValueGrammars.TryParseInnerSetTuple, RejectedValueGrammars.TryParseInnerSetTuple),
        ["WordsWhen"] = (
            Terms.Identifier().And(Terms.Identifier()).When(static (_, value) => value.Item2.ToString() != "from").Then(static value => value.Item1.ToString())
                .AndSkip(Terms.Char(';')).Eof(),
            RejectedValueGrammars.TryParseWordsWhen, RejectedValueGrammars.TryParseWordsWhen),
        ["WordsConversion"] = (
            Terms.Identifier().And(Terms.Identifier()).ThenTry(static (ParseContext _, int _, int _, (TextSpan, TextSpan) value, out string result) =>
            {
                result = value.Item1.ToString();
                return value.Item2.ToString() != "from";
            }).AndSkip(Terms.Char(';')).Eof(),
            RejectedValueGrammars.TryParseWordsConversion, RejectedValueGrammars.TryParseWordsConversion),
        ["WordsTuple"] = (
            Terms.Identifier().And(Terms.Identifier()).ThenTry(static value => (value.Item2.ToString() != "from", value.Item1.ToString()))
                .AndSkip(Terms.Char(';')).Eof(),
            RejectedValueGrammars.TryParseWordsTuple, RejectedValueGrammars.TryParseWordsTuple),
        ["WhiteSpaceWhen"] = (
            Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().When(static (_, _) => false).Then(static value => value.ToString())).Eof(),
            RejectedValueGrammars.TryParseWhiteSpaceWhen, RejectedValueGrammars.TryParseWhiteSpaceWhen),
        ["WhiteSpaceConversion"] = (
            Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
            {
                result = value.ToString();
                return false;
            })).Eof(),
            RejectedValueGrammars.TryParseWhiteSpaceConversion, RejectedValueGrammars.TryParseWhiteSpaceConversion),
        ["WhiteSpaceTuple"] = (
            Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().ThenTry(static value => (false, value.ToString()))).Eof(),
            RejectedValueGrammars.TryParseWhiteSpaceTuple, RejectedValueGrammars.TryParseWhiteSpaceTuple),
    };

    // shape, input, the furthest failure
    public static TheoryData<string, string, int> Failures => new()
    {
        // The word skips the white space before it: it is the last one which was skipped when the word is rejected
        { "Token", "from;", 0 },
        { "Token", "  from;", 2 },
        { "Token", "\n\t from;", 3 },
        { "Token", "  from;", 2 },
        { "Token", "  fromage,", 9 },
        { "Token", "  ,", 2 },
        { "Token", "  ", 2 },
        { "Token", "", 0 },

        // The word doesn't skip any
        { "Literal", "from;", 0 },
        { "Literal", "  from;", 0 },
        { "Literal", "fromage,", 7 },

        // Another alternative skipped it first, from the same offset
        { "Alternative", "from;", 0 },
        { "Alternative", "  from;", 2 },
        { "Alternative", "\n from ;", 2 },
        { "Alternative", "  fromage,", 9 },
        { "Alternative", "  select,", 8 },

        // A lookahead did, and moved the cursor back
        { "Lookahead", "from;", 0 },
        { "Lookahead", "  from;", 2 },
        { "Lookahead", "  fromage,", 9 },
        { "Lookahead", "  x", 2 },

        // A lookahead which matched read the word, and moved the cursor back to before its white space
        { "Peeked", "from;", 0 },
        { "Peeked", "  from;", 2 },
        { "Peeked", "\n\t from;", 3 },
        { "Peeked", "  fromage,", 9 },
        { "Peeked", "  ,", 2 },
        { "Peeked", "  ", 2 },

        // White space was skipped at an earlier offset, before another word
        { "Second", "a from;", 2 },
        { "Second", "  a  from;", 5 },
        { "Second", "a\nfrom;", 2 },
        { "Second", "  from  from;", 8 },
        { "Second", "  a  b,", 6 },
        { "Second", "  a  ", 5 },

        // A white space parser skips comments too
        { "Comments", "from;", 0 },
        { "Comments", "  from;", 2 },
        { "Comments", "/* a */ from;", 8 },
        { "Comments", "/* a */from;", 7 },
        { "Comments", "  /* a */\n  from;", 12 },
        { "Comments", "/* a */ fromage,", 15 },

        // The white space which another alternative skipped is not what the word's white space parser skips
        { "InnerComments", "from;", 0 },
        { "InnerComments", "  from;", 2 },
        { "InnerComments", "  /* a */ from;", 10 },
        { "InnerComments", "/* a */from;", 7 },

        // Nor is the white space of another set: a no-break space is not ASCII white space
        { "InnerSet", "from;", 0 },
        { "InnerSet", "  from;", 2 },
        { "InnerSet", "  from;", 2 },
        { "InnerSet", "  from;", 2 },
        { "InnerSet", " from;", 1 },

        // A value of two words: the last white space which was skipped is the one between them
        { "Words", "a from;", 0 },
        { "Words", "  a  from;", 2 },
        { "Words", "\n a\n from;", 2 },
        { "Words", "  a  b,", 6 },
        { "Words", "  a  ,", 5 },

        // White space read as a value by a parser which doesn't skip any, after a lookahead skipped it: the value starts
        // where its parser is invoked
        { "WhiteSpace", "  y", 0 },
        { "WhiteSpace", "  ", 0 },
        { "WhiteSpace", "y", 0 },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Rejected_Value_Fails_At_The_Same_Offset_However_It_Is_Rejected(string shape, string text, int offset)
    {
        foreach (var form in Forms)
        {
            AssertFailure(shape + form, text, offset);
        }
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void Rejected_Value_Fails_At_The_Same_Offset_Where_A_Reader_Buffer_Ends(string shape, string text, int offset)
    {
        // These read their first char as it is
        if (shape is "Literal" or "WhiteSpace")
        {
            return;
        }

        // A generated TextReader parser reads 4096 chars at a time: the white space before the value, or the value, is read
        // across two buffers, and what was skipped in the first one is not known any more
        foreach (var padding in new[] { 4094, 4095, 4096, 4097 })
        {
            foreach (var form in Forms)
            {
                AssertFailure(shape + form, new string(' ', padding) + text, padding + offset);
            }
        }
    }

    [Theory]
    [InlineData("Token", "  word ;", "word")]
    [InlineData("Token", "fromage;", "fromage")]
    [InlineData("Literal", "word;", "word")]
    [InlineData("Alternative", "  select ;", "select")]
    [InlineData("Alternative", "  word;", "word")]
    [InlineData("Lookahead", "  word;", "word")]
    [InlineData("Peeked", "  word;", "word")]
    [InlineData("Second", " from  word;", "word")]
    [InlineData("Comments", "/* a */ word /* b */;", "word")]
    [InlineData("InnerComments", "  /* a */ word;", "word")]
    [InlineData("InnerSet", "  word;", "word")]
    [InlineData("Words", "  from  word;", "from")]
    public void Accepted_Value_Is_The_Same_However_It_Is_Converted(string shape, string text, string expected)
    {
        foreach (var form in Forms)
        {
            var (runtime, generated, generatedReader) = Grammars[shape + form];

            Assert.True(runtime.TryParse(text, out var value), shape + form);
            Assert.Equal(expected, value);

            Assert.True(runtime.TryParse(new OneCharReader(text), out value, out _, new StreamParseOptions { BufferSize = 1 }), shape + form);
            Assert.Equal(expected, value);

            Assert.True(generated(text, out value, out var failure), shape + form);
            Assert.Equal(expected, value);
            Assert.Equal(0, failure);

            Assert.True(generatedReader(new OneCharReader(text), out value, out var readerFailure), shape + form);
            Assert.Equal(expected, value);
            Assert.Equal(default, readerFailure);
        }
    }

    private static void AssertFailure(string name, string text, int offset)
    {
        var (runtime, generated, generatedReader) = Grammars[name];

        var context = new ParseContext(new Scanner(text));
        Assert.False(runtime.TryParse(context, out _, out _), name);
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        AssertOffset(offset, context.Scanner.Cursor.FurthestOffset, name, "runtime, string");

        ParseContext readerContext = null!;
        var options = new StreamParseOptions
        {
            BufferSize = 1,
            ContextFactory = (scanner, token) => readerContext = new ParseContext(scanner, token),
        };

        Assert.False(runtime.TryParse(new OneCharReader(text), out _, out _, options), name);
        AssertOffset(offset, readerContext.Scanner.Cursor.FurthestOffset, name, "runtime, reader");

        Assert.False(generated(text, out _, out var failure), name);
        AssertOffset(offset, failure, name, "generated, string");

        Assert.False(generatedReader(new OneCharReader(text), out _, out var readerFailure), name);
        AssertOffset(offset, readerFailure.Offset, name, "generated, reader");
    }

    private static void AssertOffset(int expected, int actual, string name, string parser) =>
        Assert.True(expected == actual, $"{name} ({parser}): the furthest failure is {actual}, expected {expected}");

    // Returns one char per read, so that a TextReader parser refills its buffer as often as possible
    private sealed class OneCharReader : TextReader
    {
        private readonly string _text;
        private int _position;

        public OneCharReader(string text) => _text = text;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _text.Length || count == 0)
            {
                return 0;
            }

            buffer[index] = _text[_position++];
            return 1;
        }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;
    }
}

public static partial class RejectedValueGrammars
{
    public static partial bool TryParseTokenWhen(string text, out string value, out int failure);
    public static partial bool TryParseTokenWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenConversion(string text, out string value, out int failure);
    public static partial bool TryParseTokenConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenTuple(string text, out string value, out int failure);
    public static partial bool TryParseTokenTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLiteralWhen(string text, out string value, out int failure);
    public static partial bool TryParseLiteralWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLiteralConversion(string text, out string value, out int failure);
    public static partial bool TryParseLiteralConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLiteralTuple(string text, out string value, out int failure);
    public static partial bool TryParseLiteralTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseAlternativeWhen(string text, out string value, out int failure);
    public static partial bool TryParseAlternativeWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseAlternativeConversion(string text, out string value, out int failure);
    public static partial bool TryParseAlternativeConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseAlternativeTuple(string text, out string value, out int failure);
    public static partial bool TryParseAlternativeTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLookaheadWhen(string text, out string value, out int failure);
    public static partial bool TryParseLookaheadWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLookaheadConversion(string text, out string value, out int failure);
    public static partial bool TryParseLookaheadConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLookaheadTuple(string text, out string value, out int failure);
    public static partial bool TryParseLookaheadTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekedWhen(string text, out string value, out int failure);
    public static partial bool TryParsePeekedWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekedConversion(string text, out string value, out int failure);
    public static partial bool TryParsePeekedConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekedTuple(string text, out string value, out int failure);
    public static partial bool TryParsePeekedTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseSecondWhen(string text, out string value, out int failure);
    public static partial bool TryParseSecondWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseSecondConversion(string text, out string value, out int failure);
    public static partial bool TryParseSecondConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseSecondTuple(string text, out string value, out int failure);
    public static partial bool TryParseSecondTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCommentsWhen(string text, out string value, out int failure);
    public static partial bool TryParseCommentsWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCommentsConversion(string text, out string value, out int failure);
    public static partial bool TryParseCommentsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCommentsTuple(string text, out string value, out int failure);
    public static partial bool TryParseCommentsTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerCommentsWhen(string text, out string value, out int failure);
    public static partial bool TryParseInnerCommentsWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerCommentsConversion(string text, out string value, out int failure);
    public static partial bool TryParseInnerCommentsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerCommentsTuple(string text, out string value, out int failure);
    public static partial bool TryParseInnerCommentsTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerSetWhen(string text, out string value, out int failure);
    public static partial bool TryParseInnerSetWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerSetConversion(string text, out string value, out int failure);
    public static partial bool TryParseInnerSetConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseInnerSetTuple(string text, out string value, out int failure);
    public static partial bool TryParseInnerSetTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWordsWhen(string text, out string value, out int failure);
    public static partial bool TryParseWordsWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWordsConversion(string text, out string value, out int failure);
    public static partial bool TryParseWordsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWordsTuple(string text, out string value, out int failure);
    public static partial bool TryParseWordsTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWhiteSpaceWhen(string text, out string value, out int failure);
    public static partial bool TryParseWhiteSpaceWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWhiteSpaceConversion(string text, out string value, out int failure);
    public static partial bool TryParseWhiteSpaceConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWhiteSpaceTuple(string text, out string value, out int failure);
    public static partial bool TryParseWhiteSpaceTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}
