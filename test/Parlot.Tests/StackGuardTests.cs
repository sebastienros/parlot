using Parlot.Fluent;
using System;
using System.Threading;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class StackGuardTests
{
    private const int Depth = 100_000;
    private const string Message = "The input is nested too deeply to be parsed on the remaining stack.";

    private static readonly string DeepList = new string('[', Depth) + "item" + new string(']', Depth);
    private static readonly string DeepNegation = new string('-', Depth) + "a";

    private static Parser<int> List() =>
        Recursive<int>(static list => Between(Literals.Char('['), list, Literals.Char(']')).Then(static depth => depth + 1)
            .Or(Literals.Text("item").Then(0)));

    [Fact]
    public void DeferredShouldFailOnDeepInputInsteadOfOverflowingTheStack()
    {
        var list = List();

        OnSmallStack(() =>
        {
            Assert.False(list.TryParse(DeepList, out _, out var error));
            Assert.Equal(Message, error.Message);
            Assert.InRange(error.Position.Offset, 1, Depth - 1);

            // Loop detection and the recursion limit take another path
            Assert.False(list.TryParse(new ParseContext(new Scanner(DeepList), disableLoopDetection: true), out _, out error));
            Assert.Equal(Message, error.Message);
            Assert.False(list.TryParse(new ParseContext(new Scanner(DeepList), maxRecursionDepth: int.MaxValue), out _, out error));
            Assert.Equal(Message, error.Message);

            Assert.Throws<ParseException>(() => list.Parse(DeepList));

            Assert.True(list.TryParse("[[[item]]]", out var depth));
            Assert.Equal(3, depth);
        });
    }

    [Fact]
    public void DeferredShouldFailOnDeepInputWithLeftRecursionDetection()
    {
        // A grammar that LoopAnalysis can't prove tracks its positions
        var expression = Deferred<int>();
        expression.Parser = expression.AndSkip(Literals.Char('+')).Then(static value => value)
            .Or(Between(Literals.Char('['), expression, Literals.Char(']')).Then(static depth => depth + 1))
            .Or(Literals.Text("item").Then(0));

        OnSmallStack(() =>
        {
            Assert.False(expression.TryParse(DeepList, out _, out var error));
            Assert.Equal(Message, error.Message);
        });
    }

    [Fact]
    public void UnaryShouldParseDeepInputWithoutTheStack()
    {
        var negation = Literals.Char('a').Then(0).Unary((Literals.Char('-'), static count => count + 1));
        var contextual = Literals.Char('a').Then(0).Unary((Literals.Char('-'), static (_, count) => count + 1));

        // The operators are read in a loop, the chain doesn't recurse
        OnSmallStack(() =>
        {
            Assert.True(negation.TryParse(DeepNegation, out var count));
            Assert.Equal(Depth, count);
            Assert.True(contextual.TryParse(DeepNegation, out count));
            Assert.Equal(Depth, count);

            Assert.True(negation.TryParse("---a", out count));
            Assert.Equal(3, count);
        });
    }

    [Fact]
    public void PrefixOperatorsShouldParseDeepInputWithoutTheStack()
    {
        var table = Literals.Char('a').Then(0).Operators().Unary((Literals.Char('-'), static count => count + 1));

        // The operators are read in a loop, the chain doesn't recurse
        OnSmallStack(() =>
        {
            Assert.True(table.TryParse(DeepNegation, out var count));
            Assert.Equal(Depth, count);

            Assert.True(table.TryParse("---a", out count));
            Assert.Equal(3, count);
        });
    }

    [Fact]
    public void StackGuardShouldFailTheWholeParse()
    {
        // The alternative isn't tried: the guard throws rather than failing the Deferred parser
        var list = List().Or(Literals.Char('[').Then(-1));

        OnSmallStack(() => Assert.False(list.TryParse(DeepList, out _)));
    }

    private static void OnSmallStack(Action action)
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
