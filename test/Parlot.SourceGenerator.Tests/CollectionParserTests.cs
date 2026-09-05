using System;
using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public partial class CollectionParserTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void Collections_MatchRuntimeStorageAndAllocations(int count)
    {
        Check(ZeroStrings(), ZeroStrings, count, separated: false, allowsEmpty: true);
        Check(OneStrings(), OneStrings, count, separated: false, allowsEmpty: false);
        Check(SeparatedStrings(), SeparatedStrings, count, separated: true, allowsEmpty: false);
        Check(ZeroTuples(), ZeroTuples, count, separated: false, allowsEmpty: true);
        Check(OneTuples(), OneTuples, count, separated: false, allowsEmpty: false);
        Check(SeparatedTuples(), SeparatedTuples, count, separated: true, allowsEmpty: false);
    }

    private static void Check<T>(Parser<IReadOnlyList<T>> generated,
        Func<Parser<IReadOnlyList<T>>> factory, int count, bool separated, bool allowsEmpty)
    {
        // Method-group invocation bypasses factory interception.
        var runtime = factory();
        Assert.NotEqual(runtime.GetType(), generated.GetType());
        var input = CreateInput(count, separated);
        var generatedContext = new ParseContext(new Scanner(input));
        var runtimeContext = new ParseContext(new Scanner(input));
        var generatedResult = new ParseResult<IReadOnlyList<T>>();
        var runtimeResult = new ParseResult<IReadOnlyList<T>>();
        var expectedSuccess = count > 0 || allowsEmpty;

        Assert.Equal(expectedSuccess, runtime.Parse(runtimeContext, ref runtimeResult));
        Assert.Equal(expectedSuccess, generated.Parse(generatedContext, ref generatedResult));
        Assert.Equal(runtimeContext.Scanner.Cursor.Position, generatedContext.Scanner.Cursor.Position);
        Assert.Equal(input.Length, generatedContext.Scanner.Cursor.Offset);

        if (expectedSuccess)
        {
            Assert.Equal(runtimeResult.Value, generatedResult.Value);
            Assert.Equal(runtimeResult.Value.GetType(), generatedResult.Value.GetType());
            Assert.Equal(count, generatedResult.Value.Count);

            if (count == 0)
            {
                Assert.Same(Array.Empty<T>(), generatedResult.Value);
            }
            else if (count <= 4)
            {
                Assert.IsType<HybridList<T>>(generatedResult.Value);
            }
            else
            {
                Assert.IsType<List<T>>(generatedResult.Value);
            }

            var collection = Assert.IsAssignableFrom<ICollection<T>>(generatedResult.Value);
            var runtimeCollection = Assert.IsAssignableFrom<ICollection<T>>(runtimeResult.Value);
            Assert.Equal(runtimeCollection.IsReadOnly, collection.IsReadOnly);
            var copy = new T[count + 2];
            collection.CopyTo(copy, 1);
            Assert.Equal(count, new List<T>(generatedResult.Value).Count);
            for (var i = 0; i < count; i++)
            {
                Assert.Equal(runtimeResult.Value[i], copy[i + 1]);
                Assert.True(collection.Contains(copy[i + 1]));
            }

            if (count > 0 && count <= 4)
            {
                var item = generatedResult.Value[0];
                Assert.Throws<NotSupportedException>(() => collection.Add(item));
                Assert.Throws<NotSupportedException>(() => collection.Clear());
                Assert.Throws<NotSupportedException>(() => collection.Remove(item));
            }
        }

        var runtimeBytes = AllocatedBytes(runtime, runtimeContext);
        var generatedBytes = AllocatedBytes(generated, generatedContext);
        Assert.Equal(runtimeBytes, generatedBytes);
        if (count == 0)
        {
            Assert.Equal(0, generatedBytes);
        }
    }

    private static long AllocatedBytes<T>(Parser<T> parser, ParseContext context)
    {
        var result = new ParseResult<T>();
        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.Parse(context, ref result);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.Parse(context, ref result);
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(result.Value);
        return bytes;
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("x,x,x,x")]
    [InlineData("x,x,x,x,x")]
    [InlineData("x,?")]
    [InlineData("  ?")]
    public void Separated_PreservesBacktracking(string input)
    {
        var generated = SeparatedStrings();
        Func<Parser<IReadOnlyList<string>>> factory = SeparatedStrings;
        var runtime = factory();
        var generatedContext = new ParseContext(new Scanner(input));
        var runtimeContext = new ParseContext(new Scanner(input));
        var generatedResult = new ParseResult<IReadOnlyList<string>>();
        var runtimeResult = new ParseResult<IReadOnlyList<string>>();
        Assert.Equal(runtime.Parse(runtimeContext, ref runtimeResult),
            generated.Parse(generatedContext, ref generatedResult));
        Assert.Equal(runtimeContext.Scanner.Cursor.Position, generatedContext.Scanner.Cursor.Position);
        Assert.Equal(input == "x,?" ? 1 : input == "  ?" ? 0 : input.Length,
            generatedContext.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(32)]
    public void DiscardedCollections_DoNotAllocate(int count)
    {
        CheckDiscarded(CaptureZero(), CaptureZero, CreateInput(count, false), expectedSuccess: true);
        CheckDiscarded(CaptureOne(), CaptureOne, CreateInput(count, false), count > 0);
        CheckDiscarded(CaptureSeparated(), CaptureSeparated, CreateInput(count, true), count > 0);
    }

    private static void CheckDiscarded(Parser<TextSpan> generated, Func<Parser<TextSpan>> factory,
        string input, bool expectedSuccess)
    {
        Assert.NotEqual(factory().GetType(), generated.GetType());
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<TextSpan>();
        Assert.Equal(expectedSuccess, generated.Parse(context, ref result));
        Assert.Equal(input.Length, context.Scanner.Cursor.Offset);
        if (expectedSuccess)
        {
            Assert.Equal(input, result.Value.ToString());
        }
        Assert.Equal(0, AllocatedBytes(generated, context));
    }

    private static string CreateInput(int count, bool separated) =>
        separated ? string.Join(",", new string('x', count).ToCharArray()) : new string('x', count);

    [GenerateParser]
    public static Parser<IReadOnlyList<string>> ZeroStrings() => ZeroOrMany(Literals.Text("x"));

    [GenerateParser]
    public static Parser<IReadOnlyList<string>> OneStrings() => OneOrMany(Literals.Text("x"));

    [GenerateParser]
    public static Parser<IReadOnlyList<string>> SeparatedStrings() => Separated(Terms.Char(','), Terms.Text("x"));

    [GenerateParser]
    public static Parser<IReadOnlyList<(int, string)>> ZeroTuples() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser]
    public static Parser<IReadOnlyList<(int, string)>> OneTuples() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser]
    public static Parser<IReadOnlyList<(int, string)>> SeparatedTuples() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser]
    public static Parser<TextSpan> CaptureZero() => Capture(ZeroOrMany(Literals.Text("x")));

    [GenerateParser]
    public static Parser<TextSpan> CaptureOne() => Capture(OneOrMany(Literals.Text("x")));

    [GenerateParser]
    public static Parser<TextSpan> CaptureSeparated() => Capture(Separated(Literals.Char(','), Literals.Text("x")));
}
