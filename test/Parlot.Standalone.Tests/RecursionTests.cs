using System;
using System.IO;
using System.Threading;
using Xunit;

namespace Parlot.Standalone.Tests;

public class RecursionTests
{
    private const int Depth = 100_000;

    private static readonly string DeepList = new string('[', Depth) + "item" + new string(']', Depth);
    private static readonly string DeepNegation = new string('-', Depth) + "a";

    [Fact]
    public void Deep_Input_Fails_Instead_Of_Overflowing_The_Stack()
    {
        OnSmallStack(static () =>
        {
            Assert.False(RecursionGrammars.List(DeepList, out _));

            using var reader = new StringReader(DeepList);
            Assert.False(RecursionGrammars.List(reader, out _));

            // A parse that fits the stack is unaffected
            Assert.True(RecursionGrammars.List("[[[item]]]", out var depth));
            Assert.Equal(3, depth);
        });
    }

    [Fact]
    public void Deep_Prefix_Operator_Chains_Are_Read_In_A_Loop()
    {
        // The operators of Unary and of an operator table don't recurse, a chain longer than the stack allows is parsed
        OnSmallStack(static () =>
        {
            Assert.True(RecursionGrammars.Negation(DeepNegation, out var count));
            Assert.Equal(Depth, count);
            Assert.True(RecursionGrammars.Prefix(DeepNegation, out count));
            Assert.Equal(Depth, count);
        });
    }

    [Fact]
    public void Deep_Input_Is_Reported_By_The_Diagnostics_Overload()
    {
        OnSmallStack(static () =>
        {
            Assert.False(RecursionGrammars.List(DeepList, out _, out var diagnostics));
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("The input is nested too deeply to be parsed on the remaining stack.", diagnostic.Message);
            Assert.False(diagnostic.IsWarning);
            Assert.InRange(diagnostic.Offset, 1, Depth - 1);
        });
    }

    [Fact]
    public void Max_Recursion_Depth_Counts_Like_The_Runtime()
    {
        // Each entry into the Deferred parser is a level, as with ParseContext.MaxRecursionDepth
        Assert.True(RecursionGrammars.LimitedList("[[item]]", out var depth));
        Assert.Equal(2, depth);
        Assert.False(RecursionGrammars.LimitedList("[[[item]]]", out _));

        Assert.False(RecursionGrammars.LimitedList("[[[item]]]", out _, out var diagnostics));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("The maximum parser recursion depth of 3 was exceeded.", diagnostic.Message);
        Assert.Equal(3, diagnostic.Offset);

        // Each invocation of Unary is a level, and so is each of its operators
        Assert.True(RecursionGrammars.LimitedNegation("--a", out var count));
        Assert.Equal(2, count);
        Assert.False(RecursionGrammars.LimitedNegation("---a", out _));

        // Each prefix operator of an operator table is a level
        Assert.True(RecursionGrammars.LimitedPrefix("--a", out count));
        Assert.Equal(2, count);
        Assert.False(RecursionGrammars.LimitedPrefix("---a", out _));

        // Without a limit
        Assert.True(RecursionGrammars.List("[[[[[item]]]]]", out depth));
        Assert.Equal(5, depth);
        Assert.True(RecursionGrammars.Negation("-----a", out count));
        Assert.Equal(5, count);
        Assert.True(RecursionGrammars.Prefix("-----a", out count));
        Assert.Equal(5, count);
    }

    [Fact]
    public void Max_Recursion_Depth_Fails_The_Whole_Parse()
    {
        // The limit isn't a mismatch that a choice could recover from: the alternative "item" is never tried
        Assert.False(RecursionGrammars.LimitedList("[[[item]]]", out _));
        Assert.False(RecursionGrammars.LimitedList(new string('[', Depth), out _));
    }

    [Fact]
    public void Max_Recursion_Depth_Is_Read_By_The_Callbacks()
    {
        Assert.True(RecursionGrammars.LimitOfContext("x", out var limit));
        Assert.Equal(7, limit);
    }

#if NET
    [Fact]
    public void Max_Recursion_Depth_Allocates_Nothing_More()
    {
        // The levels are counted in a field of the context, the limit doesn't allocate the cold state of the context
        Assert.Equal(Allocated(static () => RecursionGrammars.List("[[item]]", out _)), Allocated(static () => RecursionGrammars.LimitedList("[[item]]", out _)));
        Assert.Equal(Allocated(static () => RecursionGrammars.Negation("--a", out _)), Allocated(static () => RecursionGrammars.LimitedNegation("--a", out _)));
        Assert.Equal(Allocated(static () => RecursionGrammars.Prefix("--a", out _)), Allocated(static () => RecursionGrammars.LimitedPrefix("--a", out _)));

        static long Allocated(Func<bool> parse)
        {
            Assert.True(parse());
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(parse());
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
#endif

    internal static void OnSmallStack(Action action)
    {
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, maxStackSize: 1024 * 1024);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("The parse failed on a 1 MB stack.", failure);
        }
    }
}
