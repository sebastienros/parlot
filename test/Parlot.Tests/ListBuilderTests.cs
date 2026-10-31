#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The list parsers accumulate their items on a stack shared by the lists of the same type on a thread. These tests
/// check that nested, large, reentrant and aborted lists don't interfere, and leave the stack empty.
/// </summary>
public class ListBuilderTests
{
    private sealed class Node
    {
        public Node(string name, IReadOnlyList<Node>? children = null)
        {
            Name = name;
            Children = children;
        }

        public string Name { get; }

        public IReadOnlyList<Node>? Children { get; }

        public override string ToString()
        {
            return Children == null ? Name : $"{Name}({string.Join(" ", Children)})";
        }
    }

    // name(child child ...), name[child, child, ...] or name{child child ...}: nested ZeroOrMany, Separated and
    // OneOrMany lists of the same element type, which share the scratch stack
    private static Parser<Node> CreateTree(Func<string, Node>? leaf = null, Func<Parser<char>, Parser<char>>? close = null)
    {
        leaf ??= static name => new Node(name);
        close ??= static parser => parser;

        var node = Deferred<Node>();
        var name = Terms.Identifier().Then(static span => span.ToString());

        var zeroOrMany = name.AndSkip(Terms.Char('(')).And(ZeroOrMany(node)).AndSkip(close(Terms.Char(')')))
            .Then(static x => new Node(x.Item1, x.Item2));
        var separated = name.AndSkip(Terms.Char('[')).And(Separated(Terms.Char(','), node, min: 0)).AndSkip(close(Terms.Char(']')))
            .Then(static x => new Node(x.Item1, x.Item2));
        var oneOrMany = name.AndSkip(Terms.Char('{')).And(OneOrMany(node)).AndSkip(close(Terms.Char('}')))
            .Then(static x => new Node(x.Item1, x.Item2));

        node.Parser = OneOf(zeroOrMany, separated, oneOrMany, name.Then(leaf));

        return node;
    }

    private static readonly Parser<Node> Tree = CreateTree();

    // Renders a random tree in the syntax of CreateTree, and in the syntax of Node.ToString()
    private static (string Input, string Expected) BuildTree(Random random, int depth, int maxWidth)
    {
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

    private static object? Scratch<T>(out Type type)
    {
        type = typeof(Parser<>).Assembly.GetType("Parlot.Fluent.ListScratch`1", throwOnError: true)!.MakeGenericType(typeof(T));
        return type.GetField("t_current", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
    }

    // The number of items on the scratch stack of the current thread
    private static int ScratchCount<T>()
    {
        var scratch = Scratch<T>(out var type);
        return scratch == null ? 0 : (int)type.GetField("Count", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scratch)!;
    }

    private static void AssertScratchIsEmptyAndCleared<T>() where T : class
    {
        Assert.Equal(0, ScratchCount<T>());

        var scratch = Scratch<T>(out var type);

        if (scratch != null)
        {
            var items = (T?[])type.GetField("Items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scratch)!;

            // The released slots don't retain the items, and an idle stack doesn't keep a large array
            Assert.All(items, static item => Assert.Null(item));
            Assert.True(items.Length <= 1024, $"The idle scratch stack has {items.Length} slots.");
        }
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(2, 3, 2)]
    [InlineData(3, 4, 5)]
    [InlineData(4, 6, 3)]
    [InlineData(5, 2, 40)]
    [InlineData(6, 12, 2)]
    public void NestedListsOfTheSameTypeKeepTheirItems(int seed, int depth, int maxWidth)
    {
        var random = new Random(seed);

        for (var i = 0; i < 20; i++)
        {
            var (input, expected) = BuildTree(random, depth, maxWidth);

            Assert.True(Tree.TryParse(input, out var node), input);
            Assert.Equal(expected, node!.ToString());
            AssertScratchIsEmptyAndCleared<Node>();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(5000)]
    public void ListsOfAnySizeAreArraysOfTheirExactSize(int count)
    {
        var items = string.Join(" ", Enumerable.Range(0, count).Select(static i => "a" + i));

        var zeroOrMany = ZeroOrMany(Terms.Identifier().Then(static x => x.ToString())).Parse(items)!;
        var separated = Separated(Terms.Char(','), Terms.Identifier().Then(static x => x.ToString()), min: 0).Parse(items.Replace(' ', ','))!;

        foreach (var list in new[] { zeroOrMany, separated })
        {
            Assert.Equal(Enumerable.Range(0, count).Select(static i => "a" + i), list);

            if (count == 0)
            {
                Assert.Same(Array.Empty<string>(), list);
            }
            else
            {
                Assert.Equal(count, Assert.IsType<string[]>(list).Length);
            }
        }

        if (count > 0)
        {
            var oneOrMany = OneOrMany(Terms.Identifier().Then(static x => x.ToString())).Parse(items)!;
            Assert.Equal(count, Assert.IsType<string[]>(oneOrMany).Length);
            Assert.Equal(zeroOrMany, oneOrMany);
        }

        AssertScratchIsEmptyAndCleared<string>();
    }

    [Fact]
    public void NestedListsCanExceedTheRetainedCapacity()
    {
        // An open list of 600 items, then a nested list of 2000 items, then 600 more items: the stack is rented above
        // 1024 items, and the retained array is restored when the outer list completes
        var input = "root(" + string.Join(" ", Enumerable.Range(0, 600).Select(static i => "a" + i))
            + " big[" + string.Join(",", Enumerable.Range(0, 2000).Select(static i => "b" + i)) + "] "
            + string.Join(" ", Enumerable.Range(0, 600).Select(static i => "c" + i)) + ")";

        for (var i = 0; i < 3; i++)
        {
            var root = Tree.Parse(input)!;

            Assert.Equal(1201, root.Children!.Count);
            Assert.Equal("a599", root.Children[599].Name);
            Assert.Equal("big", root.Children[600].Name);
            Assert.Equal(2000, root.Children[600].Children!.Count);
            Assert.Equal("b1999", root.Children[600].Children![1999].Name);
            Assert.Equal("c599", root.Children[1200].Name);
            AssertScratchIsEmptyAndCleared<Node>();
        }
    }

    [Fact]
    public void ExceptionInCallbackDoesNotCorruptLaterParses()
    {
        var tree = CreateTree(static name => name == "boom" ? throw new InvalidOperationException(name) : new Node(name));
        var (input, expected) = BuildTree(new Random(42), 4, 6);

        for (var i = 0; i < 3; i++)
        {
            Assert.Throws<InvalidOperationException>(() => tree.Parse("a(b c d[e, f, g{h i j(k l boom m)}] n)"));
            AssertScratchIsEmptyAndCleared<Node>();

            Assert.Equal(expected, tree.Parse(input)!.ToString());
            AssertScratchIsEmptyAndCleared<Node>();
        }
    }

    [Fact]
    public void ParseExceptionDoesNotCorruptLaterParses()
    {
        var tree = CreateTree(close: static parser => parser.ElseError("Expected the end of the list"));
        var (input, expected) = BuildTree(new Random(7), 4, 6);

        for (var i = 0; i < 3; i++)
        {
            var exception = Assert.Throws<ParseException>(() => tree.Parse("a(b c d[e, f, g{h i j(k l m) n o"));
            Assert.Equal("Expected the end of the list", exception.Message);
            AssertScratchIsEmptyAndCleared<Node>();

            Assert.False(tree.TryParse("a(b c d[e, f, g{h i j(k l m) n o", out _, out var error));
            Assert.NotNull(error);
            AssertScratchIsEmptyAndCleared<Node>();

            Assert.Equal(expected, tree.Parse(input)!.ToString());
            AssertScratchIsEmptyAndCleared<Node>();
        }
    }

    [Fact]
    public void CancellationDoesNotCorruptLaterParses()
    {
        using var cts = new CancellationTokenSource();
        var tree = CreateTree(name =>
        {
            if (name == "cancel")
            {
                cts.Cancel();
            }

            return new Node(name);
        });

        // The cancellation is observed within the next parsers, while the lists are open
        var input = "a(b c d[e, f, g{h i cancel " + string.Join(" ", Enumerable.Range(0, 200).Select(static i => "x" + i)) + "}] z)";

        Assert.Throws<OperationCanceledException>(() => tree.Parse(input, cts.Token));
        AssertScratchIsEmptyAndCleared<Node>();

        Assert.Equal("a(b c d(e f g(h i x0 x1)) z)", tree.Parse("a(b c d[e, f, g{h i x0 x1}] z)")!.ToString());
        AssertScratchIsEmptyAndCleared<Node>();
    }

    [Fact]
    public void ReentrantParseFromCallbackKeepsTheOpenLists()
    {
        // The nested parse pushes more items than the initial capacity of the stack while the outer lists are open,
        // so the items of the outer lists are moved to a larger array
        var nested = "n(" + string.Join(" ", Enumerable.Range(0, 100).Select(static i => "r" + i)) + " m[s, t{u v}])";
        var nestedExpected = Tree.Parse(nested)!.ToString();

        var tree = CreateTree(static name => name == "sub" ? Tree.Parse("n(" + string.Join(" ", Enumerable.Range(0, 100).Select(static i => "r" + i)) + " m[s, t{u v}])")! : new Node(name));

        var result = tree.Parse("a(b c d[e, f, g{h i sub j}, sub, k] l sub)")!;

        Assert.Equal($"a(b c d(e f g(h i {nestedExpected} j) {nestedExpected} k) l {nestedExpected})", result.ToString());
        AssertScratchIsEmptyAndCleared<Node>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(1500)]
    public void RightAssociativeFoldsTheOperationsInOrder(int operators)
    {
        static long Fold(long left, long right) => (left * 31 + right) % 1_000_003;

        var parsers = new Parser<long>[]
        {
            Terms.Integer().RightAssociative((Terms.Char('^'), static (a, b) => Fold(a, b))),
            Terms.Integer().RightAssociative((Terms.Char('^'), static (_, a, b) => Fold(a, b))),
            Terms.Integer().RightAssociative(new[] { Terms.Char('^') }, static (a, b, _) => Fold(a, b)),
        };

        var values = Enumerable.Range(1, operators + 1).Select(static i => (long)i).ToArray();
        var expected = values[values.Length - 1];
        for (var i = values.Length - 2; i >= 0; i--)
        {
            expected = Fold(values[i], expected);
        }

        var input = string.Join(" ^ ", values);

        foreach (var parser in parsers)
        {
            Assert.Equal(expected, parser.Parse(input));
        }
    }

    [Fact]
    public void RightAssociativeFactoriesCanParseAndThrow()
    {
        Parser<long>? parser = null;

        // A factory which parses with the same parser, growing the stack while the outer operations are on it, and
        // one which throws
        parser = Terms.Integer().RightAssociative((Terms.Char('^'), (a, b) => a switch
        {
            7 => parser!.Parse(string.Join("^", Enumerable.Range(20, 40))) + b,
            13 => throw new InvalidOperationException(),
            _ => a * 2 + b,
        }));

        var inner = Enumerable.Range(20, 40).Reverse().Aggregate(0L, static (accumulated, value) => accumulated == 0 ? value : value * 2 + accumulated);

        // 1 ^ (7 ^ (2 ^ 3)) with 7 ^ x = inner + x
        Assert.Equal(1 * 2 + (inner + (2 * 2 + 3)), parser.Parse("1 ^ 7 ^ 2 ^ 3"));
        Assert.Equal(0, ScratchCount<(Func<long, long, long>, long)>());

        Assert.Throws<InvalidOperationException>(() => parser.Parse("1 ^ 2 ^ 13 ^ 4 ^ 5"));
        Assert.Equal(0, ScratchCount<(Func<long, long, long>, long)>());

        Assert.Equal(1 * 2 + (2 * 2 + 3), parser.Parse("1 ^ 2 ^ 3"));
        Assert.Equal(0, ScratchCount<(Func<long, long, long>, long)>());
    }

    [Fact]
    public void FailedListParsersReleaseTheirItems()
    {
        // The minimum isn't reached: the items are released without a result
        var parser = Separated(Terms.Char(','), Terms.Identifier().Then(static x => x.ToString()), min: 5)
            .Or(OneOrMany(Terms.Identifier().Then(static x => x.ToString())).AndSkip(Terms.Char(',')));

        Assert.True(parser.TryParse("a,b,c,d", out var result));
        Assert.Equal(new[] { "a" }, result);
        AssertScratchIsEmptyAndCleared<string>();
    }
}
