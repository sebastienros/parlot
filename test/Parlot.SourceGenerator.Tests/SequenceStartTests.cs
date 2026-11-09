using System;
using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// A generated callback receives the start the runtime one does when the first parts of a sequence match nothing: the
/// start of the first part which matched something.
/// </summary>
public class SequenceStartTests
{
    private delegate bool TryParse(string text, out (int Start, int End) value);

    private static readonly Parser<IReadOnlyList<string>> Decorators = ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier().Then(static x => x.ToString()!)));
    private static readonly Parser<Option<char>> OptionalStar = Terms.Char('*').Optional();
    private static readonly Parser<char> StarElse = Terms.Char('*').Else('-');
    private static readonly Parser<string> Class = Terms.Text("class");
    private static readonly Parser<string> Name = Terms.Identifier().Then(static x => x.ToString()!);

    private static Parser<(int, int)> Offsets<T>(Parser<T> parser) => parser.Then(static (_, start, end, _) => (start, end));

    private static readonly Dictionary<string, (Parser<(int, int)> Runtime, TryParse Generated)> Grammars = new()
    {
        ["zeroormany"] = (Offsets(Decorators.And(Class)), SequenceStartGrammars.TryParseZeroOrMany),
        ["optional"] = (Offsets(OptionalStar.And(Class)), SequenceStartGrammars.TryParseOptional),
        ["always"] = (Offsets(Always().And(Class)), SequenceStartGrammars.TryParseAlways),
        ["else"] = (Offsets(StarElse.And(Class)), SequenceStartGrammars.TryParseElse),
        ["not"] = (Offsets(Not(Terms.Char('!')).And(Class)), SequenceStartGrammars.TryParseNot),
        ["zeroormany-3"] = (Offsets(Decorators.And(Class).And(Name)), SequenceStartGrammars.TryParseZeroOrMany3),
        ["optional-3"] = (Offsets(OptionalStar.And(Class).And(Name)), SequenceStartGrammars.TryParseOptional3),
        ["always-3"] = (Offsets(Always().And(Class).And(Name)), SequenceStartGrammars.TryParseAlways3),
        ["else-3"] = (Offsets(StarElse.And(Class).And(Name)), SequenceStartGrammars.TryParseElse3),
        ["two-empty-3"] = (Offsets(Decorators.And(OptionalStar).And(Class)), SequenceStartGrammars.TryParseTwoEmpty3),
        ["two-empty-7"] = (Offsets(Decorators.And(OptionalStar).And(Always()).And(Class).And(Name).And(OptionalStar).And(Name)), SequenceStartGrammars.TryParseTwoEmpty7),
        ["nested"] = (Offsets(Decorators.And(OptionalStar.And(Class))), SequenceStartGrammars.TryParseNested),
        ["skipand"] = (Offsets(Decorators.SkipAnd(Class)), SequenceStartGrammars.TryParseSkipAnd),
        ["skipand-3"] = (Offsets(Decorators.And(OptionalStar).SkipAnd(Class)), SequenceStartGrammars.TryParseSkipAnd3),
        ["andskip"] = (Offsets(Decorators.AndSkip(Class)), SequenceStartGrammars.TryParseAndSkip),
        ["andskip-3"] = (Offsets(Decorators.And(OptionalStar).AndSkip(Class)), SequenceStartGrammars.TryParseAndSkip3),
        ["all-empty"] = (Offsets(Decorators.And(OptionalStar)), SequenceStartGrammars.TryParseAllEmpty),
        ["all-empty-3"] = (Offsets(Decorators.And(OptionalStar).And(Always())), SequenceStartGrammars.TryParseAllEmpty3),
        ["literal-first"] = (Offsets(Literals.WhiteSpace().And(Class)), SequenceStartGrammars.TryParseLiteralFirst),
    };

    [Theory]
    [InlineData("zeroormany", "class", 0, 5)]
    [InlineData("zeroormany", "   class", 3, 8)]
    [InlineData("zeroormany", "   @a class", 3, 11)]
    [InlineData("zeroormany", "  @a  @b class", 2, 14)]
    [InlineData("optional", "   class", 3, 8)]
    [InlineData("optional", "   * class", 3, 10)]
    [InlineData("always", "   class", 3, 8)]
    [InlineData("else", "   class", 3, 8)]
    [InlineData("else", "  *class", 2, 8)]
    [InlineData("not", "   class", 3, 8)]
    [InlineData("zeroormany-3", "   class a", 3, 10)]
    [InlineData("zeroormany-3", "   @a class b", 3, 13)]
    [InlineData("optional-3", "   class a", 3, 10)]
    [InlineData("optional-3", " * class a", 1, 10)]
    [InlineData("always-3", "   class a", 3, 10)]
    [InlineData("else-3", "   class a", 3, 10)]
    [InlineData("two-empty-3", "   class", 3, 8)]
    [InlineData("two-empty-3", "   *  class", 3, 11)]
    [InlineData("two-empty-3", "  @a  * class", 2, 13)]
    [InlineData("two-empty-7", "   class a b", 3, 12)]
    [InlineData("two-empty-7", "  * class a * b", 2, 15)]
    [InlineData("nested", "   class", 3, 8)]
    [InlineData("nested", "   *  class", 3, 11)]
    [InlineData("skipand", "   class", 3, 8)]
    [InlineData("skipand", "  @a class", 2, 10)]
    [InlineData("skipand-3", "   class", 3, 8)]
    [InlineData("andskip", "   class", 3, 8)]
    [InlineData("andskip-3", "   class", 3, 8)]
    [InlineData("andskip-3", " * class", 1, 8)]
    [InlineData("all-empty", "   x", 0, 0)]
    [InlineData("all-empty-3", "   x", 0, 0)]
    [InlineData("literal-first", "  class", 0, 7)]
    // Failures, which give the white space skipped before a part back
    [InlineData("zeroormany", "   struct", -1, 0)]
    [InlineData("zeroormany", "   @a struct", -1, 0)]
    [InlineData("optional", "   * struct", -1, 0)]
    [InlineData("always", "   struct", -1, 0)]
    [InlineData("else", "   * struct", -1, 0)]
    [InlineData("zeroormany-3", "   @a class ", -1, 0)]
    [InlineData("optional-3", "   class 1", -1, 0)]
    [InlineData("two-empty-7", "  * class a * ", -1, 0)]
    [InlineData("nested", "   * struct", -1, 0)]
    [InlineData("skipand-3", "   * struct", -1, 0)]
    [InlineData("andskip-3", "   * struct", -1, 0)]
    public void Generated_Callback_Receives_The_Start_Of_The_First_Part_That_Matched(string grammar, string input, int start, int end)
    {
        var (runtime, generated) = Grammars[grammar];

        if (start < 0)
        {
            Assert.False(runtime.TryParse(input, out _));
            Assert.False(generated(input, out _));
            return;
        }

        Assert.True(runtime.TryParse(input, out var expected));
        Assert.Equal((start, end), expected);
        Assert.True(generated(input, out var value));
        Assert.Equal((start, end), value);
    }

    [Theory]
    [InlineData("   class a", "3-10:class:a")]
    [InlineData("  @x * class a *", "2-16:class:a")]
    [InlineData("  * class a", "2-11:class:a")]
    public void Generated_Sequence_Keeps_Its_Value(string input, string expected)
    {
        var runtime = Decorators.And(OptionalStar).SkipAnd(Class).And(Name).AndSkip(OptionalStar)
            .Then(static (_, start, end, value) => $"{start}-{end}:{value.Item2}:{value.Item3}");

        Assert.True(runtime.TryParse(input, out var runtimeValue));
        Assert.Equal(expected, runtimeValue);
        Assert.True(SequenceStartGrammars.TryParseValues(input, out var value));
        Assert.Equal(expected, value);
    }
    private delegate bool TryParseReader(TextReader reader, out (int Start, int End) value);

    private static readonly Dictionary<string, TryParseReader> Readers = new()
    {
        ["zeroormany"] = SequenceStartGrammars.TryParseZeroOrMany,
        ["optional"] = SequenceStartGrammars.TryParseOptional,
        ["two-empty-3"] = SequenceStartGrammars.TryParseTwoEmpty3,
    };

    [Theory]
    [InlineData("zeroormany", "   class", 3, 8)]
    [InlineData("zeroormany", "   @a class", 3, 11)]
    [InlineData("zeroormany", "   @a struct", -1, 0)]
    [InlineData("optional", "   class", 3, 8)]
    [InlineData("optional", "   * class", 3, 10)]
    [InlineData("optional", "   * struct", -1, 0)]
    [InlineData("two-empty-3", "   class", 3, 8)]
    [InlineData("two-empty-3", "  @a  * class", 2, 13)]
    [InlineData("two-empty-3", "  @a  * struct", -1, 0)]
    public void Generated_Reader_Callback_Receives_The_Same_Start(string grammar, string input, int start, int end)
    {
        // The white space given back when a part matches nothing is read again, even when the reader returns one char
        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 2) })
        {
            if (start < 0)
            {
                Assert.False(Readers[grammar](reader, out _));
                continue;
            }

            Assert.True(Readers[grammar](reader, out var value));
            Assert.Equal((start, end), value);
        }
    }

    // A reader returning at most a fixed number of chars per read
    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private int _position;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override int Read(Span<char> buffer)
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk), _text.Length - _position);
            _text.AsSpan(_position, length).CopyTo(buffer);
            _position += length;
            return length;
        }
    }
}

public static partial class SequenceStartGrammars
{
    public static partial bool TryParseZeroOrMany(TextReader reader, out (int Start, int End) value);
    public static partial bool TryParseOptional(TextReader reader, out (int Start, int End) value);
    public static partial bool TryParseTwoEmpty3(TextReader reader, out (int Start, int End) value);
    public static partial bool TryParseZeroOrMany(string text, out (int Start, int End) value);
    public static partial bool TryParseOptional(string text, out (int Start, int End) value);
    public static partial bool TryParseAlways(string text, out (int Start, int End) value);
    public static partial bool TryParseElse(string text, out (int Start, int End) value);
    public static partial bool TryParseNot(string text, out (int Start, int End) value);
    public static partial bool TryParseZeroOrMany3(string text, out (int Start, int End) value);
    public static partial bool TryParseOptional3(string text, out (int Start, int End) value);
    public static partial bool TryParseAlways3(string text, out (int Start, int End) value);
    public static partial bool TryParseElse3(string text, out (int Start, int End) value);
    public static partial bool TryParseTwoEmpty3(string text, out (int Start, int End) value);
    public static partial bool TryParseTwoEmpty7(string text, out (int Start, int End) value);
    public static partial bool TryParseNested(string text, out (int Start, int End) value);
    public static partial bool TryParseSkipAnd(string text, out (int Start, int End) value);
    public static partial bool TryParseSkipAnd3(string text, out (int Start, int End) value);
    public static partial bool TryParseAndSkip(string text, out (int Start, int End) value);
    public static partial bool TryParseAndSkip3(string text, out (int Start, int End) value);
    public static partial bool TryParseAllEmpty(string text, out (int Start, int End) value);
    public static partial bool TryParseAllEmpty3(string text, out (int Start, int End) value);
    public static partial bool TryParseLiteralFirst(string text, out (int Start, int End) value);
    public static partial bool TryParseValues(string text, out string value);
}
