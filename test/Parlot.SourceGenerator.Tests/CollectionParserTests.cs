using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class CollectionParserTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void ZeroOrMany_Uses_Embedded_Collection_Storage(int count)
    {
        var input = new string('x', count);

        Assert.True(CollectionGrammars.TryParseZeroStrings(input, out var values));
        Assert.Equal(count, values.Count);

        if (count == 0)
        {
            Assert.Same(Array.Empty<string>(), values);
        }
        else
        {
            // One array of the exact size, without the support types
            Assert.Equal(count, Assert.IsType<string[]>(values).Length);
        }

        Assert.Equal(0, ScratchCount<string>());
    }

    // The number of items on the scratch stack of the generated list parsers, on the current thread
    private static int ScratchCount<T>()
    {
        var type = typeof(CollectionGrammars).Assembly.GetType("Parlot.Generated.Fluent.ListScratch`1", throwOnError: true)!.MakeGenericType(typeof(T));
        var scratch = type.GetField("t_current", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        return scratch == null ? 0 : (int)type.GetField("Count", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scratch)!;
    }

    // name(child ...), name[child, ...] and name{child ...}, with the expected TreeNode.ToString()
    private static (string Input, string Expected) BuildTree(int seed, int depth, int maxWidth)
    {
        var random = new Random(seed);
        var input = new StringBuilder();
        var expected = new StringBuilder();
        var counter = 0;

        void Build(int level)
        {
            var name = "n" + counter++;
            input.Append(name);
            expected.Append(name);

            if (level == depth)
            {
                return;
            }

            var kind = random.Next(3);
            var width = random.Next(kind == 2 ? 1 : 0, maxWidth + 1);
            var (open, separator, close) = kind switch { 0 => ("(", " ", ")"), 1 => ("[", ",", "]"), _ => ("{", " ", "}") };

            input.Append(open);
            expected.Append('(');

            for (var i = 0; i < width; i++)
            {
                if (i > 0)
                {
                    input.Append(separator);
                    expected.Append(' ');
                }

                Build(level + 1);
            }

            input.Append(close);
            expected.Append(')');
        }

        Build(0);
        return (input.ToString(), expected.ToString());
    }

    [Theory]
    [InlineData(1, 3, 4)]
    [InlineData(2, 5, 3)]
    [InlineData(3, 2, 40)]
    [InlineData(4, 1, 2000)]
    public void Nested_Lists_Of_The_Same_Type_Keep_Their_Items(int seed, int depth, int maxWidth)
    {
        var (input, expected) = BuildTree(seed, depth, maxWidth);

        Assert.True(CollectionGrammars.TryParseTree(input, out var tree));
        Assert.Equal(expected, tree.ToString());
        Assert.Equal(0, ScratchCount<TreeNode>());

        // The compacting reader, refilled one char at a time, parses the same lists
        Assert.True(input.Length > 4096 || maxWidth < 2000);
        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 7) })
        {
            Assert.True(CollectionGrammars.TryParseTree(reader, out var streamed));
            Assert.Equal(expected, streamed.ToString());
            Assert.Equal(0, ScratchCount<TreeNode>());
        }
    }

    [Fact]
    public void Exception_In_A_List_Does_Not_Corrupt_Later_Parses()
    {
        var (input, expected) = BuildTree(5, 4, 5);

        for (var i = 0; i < 3; i++)
        {
            Assert.Throws<InvalidOperationException>(() => CollectionGrammars.TryParseTree("a(b c d[e, f, g{h i j(k l boom m)}] n)", out _));
            Assert.Equal(0, ScratchCount<TreeNode>());

            Assert.True(CollectionGrammars.TryParseTree(input, out var tree));
            Assert.Equal(expected, tree.ToString());
            Assert.Equal(0, ScratchCount<TreeNode>());
        }
    }

    [Fact]
    public void Reentrant_Parse_From_A_Callback_Keeps_The_Open_Lists()
    {
        Assert.True(CollectionGrammars.TryParseTree(TreeNode.SubTree, out var sub));
        var nested = sub.ToString();

        Assert.True(CollectionGrammars.TryParseTree("a(b c d[e, f, g{h i sub j}, sub, k] l sub)", out var tree));
        Assert.Equal($"a(b c d(e f g(h i {nested} j) {nested} k) l {nested})", tree.ToString());
        Assert.Equal(0, ScratchCount<TreeNode>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(1500)]
    public void Right_Associative_Folds_The_Operations_In_Order(int operators)
    {
        var values = new long[operators + 1];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = i + 1;
        }

        var expected = values[values.Length - 1];
        for (var i = values.Length - 2; i >= 0; i--)
        {
            expected = TreeNode.Fold(values[i], expected);
        }

        var input = string.Join(" ^ ", values);

        Assert.True(CollectionGrammars.TryParseRightFold(input, out var value));
        Assert.Equal(expected, value);
        Assert.True(CollectionGrammars.TryParseRightFoldWithOperator(input, out value));
        Assert.Equal(expected, value);
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

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void OneOrMany_And_Separated_Preserve_All_Items(int count)
    {
        var adjacent = new string('x', count);
        var separated = string.Join(",", new string('x', count).ToCharArray());

        Assert.True(CollectionGrammars.TryParseOneStrings(adjacent, out var oneOrMany));
        Assert.True(CollectionGrammars.TryParseSeparatedStrings(separated, out var separatedValues));
        Assert.Equal(count, oneOrMany.Count);
        Assert.Equal(count, separatedValues.Count);
        Assert.All(oneOrMany, static value => Assert.Equal("x", value));
        Assert.All(separatedValues, static value => Assert.Equal("x", value));
    }

    [Fact]
    public void OneOrMany_And_Separated_Reject_Empty_Input()
    {
        Assert.False(CollectionGrammars.TryParseOneStrings("", out var oneOrMany));
        Assert.Null(oneOrMany);
        Assert.False(CollectionGrammars.TryParseSeparatedStrings("", out var separated));
        Assert.Null(separated);
    }

    [Fact]
    public void Tuple_Collections_Remain_Bcl_Shaped()
    {
        Assert.True(CollectionGrammars.TryParseTuples("xxx", out var values));
        Assert.Equal(new[] { (1, "x"), (1, "x"), (1, "x") }, values);
    }

    [Theory]
    [InlineData("x,?", 1)]
    [InlineData("x,x,?", 2)]
    public void Separated_Rolls_Back_A_Trailing_Separator(string input, int expectedCount)
    {
        Assert.True(CollectionGrammars.TryParseSeparatedThenQuestion(input, out var count));
        Assert.Equal(expectedCount, count);
    }

    [Theory]
    [InlineData("x", true, true, true, true)]
    [InlineData("x,x", true, true, true, true)]
    [InlineData("x,,x", true, false, false, true)]
    [InlineData(",x", false, true, false, true)]
    [InlineData("x,", false, false, true, true)]
    [InlineData(",,x,,x,,", false, false, false, true)]
    [InlineData(",,x", false, false, false, true)]
    [InlineData("x,,", false, false, false, true)]
    [InlineData("", false, false, false, false)]
    [InlineData(",,", false, false, false, false)]
    public void Separated_Options_Match_Runtime(
        string input, bool interior, bool leading, bool trailing, bool all)
    {
        var expected = new[]
        {
            SeparatedResult(input, removeEmptyEntries: true),
            SeparatedResult(input, allowLeadingSeparator: true),
            SeparatedResult(input, allowTrailingSeparator: true),
            SeparatedResult(input, removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true)
        };

        Assert.Equal(expected[0].Success, CollectionGrammars.TryParseSeparatedInterior(input, out var interiorValues));
        Assert.Equal(expected[1].Success, CollectionGrammars.TryParseSeparatedLeading(input, out var leadingValues));
        Assert.Equal(expected[2].Success, CollectionGrammars.TryParseSeparatedTrailing(input, out var trailingValues));
        Assert.Equal(expected[3].Success, CollectionGrammars.TryParseSeparatedAllOptions(input, out var allValues));
        Assert.Equal(interior, expected[0].Success);
        Assert.Equal(leading, expected[1].Success);
        Assert.Equal(trailing, expected[2].Success);
        Assert.Equal(all, expected[3].Success);
        if (all)
        {
            Assert.Equal(expected[3].Values, allValues);
        }
        if (interior)
        {
            Assert.Equal(expected[0].Values, interiorValues);
        }
        if (leading)
        {
            Assert.Equal(expected[1].Values, leadingValues);
        }
        if (trailing)
        {
            Assert.Equal(expected[2].Values, trailingValues);
        }
    }

    [Theory]
    [InlineData(",,x,,?", true)]
    [InlineData(",,?", false)]
    public void Separated_Options_Work_When_Result_Is_Discarded(string input, bool expected)
    {
        Assert.Equal(expected, CollectionGrammars.TryParseSeparatedDiscarded(input, out var value));
        if (expected)
        {
            Assert.Equal('?', value);
        }
    }

    [Fact]
    public void Separated_Options_Restore_Cursor_For_Following_Parsers()
    {
        Assert.True(CollectionGrammars.TryParseSeparatedUnmatchedRun("x,,?", out var count));
        Assert.Equal(1, count);
        Assert.True(CollectionGrammars.TryParseSeparatedLeadingFallback(",", out var fallback));
        Assert.Equal(-1, fallback);
    }

    [Fact]
    public void Separated_Options_Stop_At_Zero_Progress()
    {
        Assert.True(CollectionGrammars.TryParseSeparatedOptionalOptions("aaa", out var values));
        Assert.Equal(3, values.Count);
        Assert.False(CollectionGrammars.TryParseSeparatedOptionalOptions("", out _));
        Assert.False(CollectionGrammars.TryParseSeparatedOptionalOptions(",", out _));
    }

    private static (bool Success, IReadOnlyList<string> Values) SeparatedResult(
        string input, bool removeEmptyEntries = false, bool allowLeadingSeparator = false, bool allowTrailingSeparator = false)
    {
        var parser = Fluent.Parsers.Separated(Fluent.Parsers.Literals.Char(','),
            Fluent.Parsers.Literals.Text("x"), removeEmptyEntries, allowLeadingSeparator, allowTrailingSeparator).Eof();
        var success = parser.TryParse(input, out var values);
        return (success, values);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("x", "x")]
    [InlineData("xxxx", "xxxx")]
    public void Discarded_Collections_Can_Be_Captured_As_String(string input, string expected)
    {
        Assert.True(CollectionGrammars.TryParseCapturedZero(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void BoundedManyLeavesNextItemUnconsumed()
    {
        Assert.True(CollectionGrammars.TryParseBoundedZero("xxx", out var zeroCount));
        Assert.Equal(2, zeroCount);
        Assert.True(CollectionGrammars.TryParseBoundedOne("xxx", out var oneCount));
        Assert.Equal(2, oneCount);
        Assert.False(CollectionGrammars.TryParseBoundedZero("xx", out _));
        Assert.False(CollectionGrammars.TryParseBoundedOne("x", out _));
    }

    [Fact]
    public void BoundedSeparatedLeavesNextItemUnconsumed()
    {
        Assert.True(CollectionGrammars.TryParseBoundedSeparated("x,x,x,x", out var count));
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("x,x")]
    public void OptionalSeparatedAcceptsUpToMaximum(string input)
    {
        Assert.True(CollectionGrammars.TryParseOptionalSeparated(input, out var values));
        Assert.Equal(input.Length == 0 ? 0 : (input.Length + 1) / 2, values.Count);
        Assert.False(CollectionGrammars.TryParseOptionalSeparated("x,x,x", out _));
        Assert.True(CollectionGrammars.TryParseOptionalSeparatedChoice("z", out var choice));
        Assert.Equal('x', choice);
    }

    [Fact]
    public void BoundedCollectionsWorkWhenValuesAreDiscarded()
    {
        Assert.True(CollectionGrammars.TryParseCapturedBoundedZero("xxx", out var zero));
        Assert.Equal("xx", zero);
        Assert.True(CollectionGrammars.TryParseCapturedBoundedOne("xxx", out var one));
        Assert.Equal("xx", one);
        Assert.True(CollectionGrammars.TryParseCapturedBoundedSeparated("x,x,x,x", out var separated));
        Assert.Equal("x,x,x", separated);
        Assert.False(CollectionGrammars.TryParseCapturedBoundedSeparated("x,x", out _));
    }

    [Fact]
    public void BoundedSeparatedRejectsTooFewItemsAndRollsBackTrailingSeparator()
    {
        Assert.False(CollectionGrammars.TryParseBoundedSeparated("x,x", out _));
        Assert.False(CollectionGrammars.TryParseBoundedSeparated("x,x,", out _));
        Assert.False(CollectionGrammars.TryParseBoundedSeparated("x,x,x,", out _));
        Assert.True(CollectionGrammars.TryParseSeparatedMinimumFallback("x,x", out var fallbackCount));
        Assert.Equal(1, fallbackCount);
    }

    [Fact]
    public void BoundedSeparatedOptionsMatchRuntimeAndDiscardResults()
    {
        Assert.True(CollectionGrammars.TryParseBoundedSeparatedOptions(",,x,,x,,x", out var count));
        Assert.Equal(2, count);
        Assert.True(CollectionGrammars.TryParseDiscardedBoundedSeparatedOptions(",,x,,x,,x", out var discarded));
        Assert.Equal('x', discarded);
        Assert.False(CollectionGrammars.TryParseBoundedSeparatedOptions(",,x,,x", out _));
        Assert.True(CollectionGrammars.TryParseOptionalSeparatedOptions(",,,", out var emptyCount));
        Assert.Equal(0, emptyCount);
        Assert.False(CollectionGrammars.TryParseOptionalSeparatedOptions(",,x,,x,,x", out _));
        Assert.True(CollectionGrammars.TryParseBoundedSeparatedOptionsFallback(",", out var fallback));
        Assert.Equal(',', fallback);
    }
}

public static partial class CollectionGrammars
{
    public static partial bool TryParseZeroStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseOneStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedStrings(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedInterior(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedLeading(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedTrailing(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedAllOptions(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSeparatedDiscarded(string text, out char value);
    public static partial bool TryParseSeparatedUnmatchedRun(string text, out int value);
    public static partial bool TryParseSeparatedLeadingFallback(string text, out int value);
    public static partial bool TryParseSeparatedOptionalOptions(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseTuples(string text, out IReadOnlyList<(int Number, string Text)> value);
    public static partial bool TryParseSeparatedThenQuestion(string text, out int value);
    public static partial bool TryParseCapturedZero(string text, out string value);
    public static partial bool TryParseBoundedZero(string text, out int value);
    public static partial bool TryParseBoundedOne(string text, out int value);
    public static partial bool TryParseBoundedSeparated(string text, out int value);
    public static partial bool TryParseOptionalSeparated(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseCapturedBoundedZero(string text, out string value);
    public static partial bool TryParseCapturedBoundedOne(string text, out string value);
    public static partial bool TryParseCapturedBoundedSeparated(string text, out string value);
    public static partial bool TryParseSeparatedMinimumFallback(string text, out int value);
    public static partial bool TryParseOptionalSeparatedChoice(string text, out char value);
    public static partial bool TryParseBoundedSeparatedOptions(string text, out int value);
    public static partial bool TryParseBoundedSeparatedOptionsFallback(string text, out char value);
    public static partial bool TryParseOptionalSeparatedOptions(string text, out int value);
    public static partial bool TryParseDiscardedBoundedSeparatedOptions(string text, out char value);
    public static partial bool TryParseTree(string text, out TreeNode value);
    public static partial bool TryParseTree(TextReader reader, out TreeNode value);
    public static partial bool TryParseRightFold(string text, out long value);
    public static partial bool TryParseRightFoldWithOperator(string text, out long value);
}

public sealed class TreeNode
{
    // Parsed by the "sub" leaves: more items than the initial capacity of the scratch stack
    public static readonly string SubTree = "n(" + string.Join(" ", Enumerable.Range(0, 100).Select(static i => "r" + i)) + " m[s, t{u v}])";

    public TreeNode(string name, IReadOnlyList<TreeNode> children = null)
    {
        Name = name;
        Children = children;
    }

    public string Name { get; }

    public IReadOnlyList<TreeNode> Children { get; }

    public static TreeNode Leaf(string name) => name switch
    {
        "boom" => throw new InvalidOperationException(name),
        "sub" => CollectionGrammars.TryParseTree(SubTree, out var sub) ? sub : throw new InvalidOperationException(name),
        _ => new TreeNode(name),
    };

    public static long Fold(long left, long right) => (left * 31 + right) % 1_000_003;

    public override string ToString() => Children == null ? Name : $"{Name}({string.Join(" ", Children)})";
}
