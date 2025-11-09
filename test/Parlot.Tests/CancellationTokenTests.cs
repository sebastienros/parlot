using Parlot.Fluent;
using System;
using System.Linq;
using System.Threading;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CancellationTokenTests
{
    [Fact]
    public void ShouldCancelParsing()
    {
        // Create a parser that will try to parse multiple integers
        var parser = Separated(Terms.Char(','), Terms.Integer());

        // Create a cancellation token that is already cancelled
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Parsing should throw OperationCanceledException
        Assert.Throws<OperationCanceledException>(() => 
            parser.Parse("1,2,3,4,5", cts.Token));
    }

    [Fact]
    public void ShouldNotCancelWhenTokenIsNotCancelled()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());

        // Create a cancellation token that is not cancelled
        var cts = new CancellationTokenSource();

        // Parsing should succeed
        var result = parser.Parse("1,2,3", cts.Token);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
    }

    [Fact]
    public void TryParseShouldReturnFalseWhenCancelled()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());

        // Create a cancellation token that is already cancelled
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // TryParse should return false and provide an error
        var success = parser.TryParse("1,2,3,4,5", cts.Token, out var result, out var error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("canceled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseShouldSucceedWhenTokenIsNotCancelled()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());

        // Create a cancellation token that is not cancelled
        var cts = new CancellationTokenSource();

        // TryParse should succeed
        var success = parser.TryParse("1,2,3", cts.Token, out var result, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
    }

    [Fact]
    public void ShouldCancelDuringLongParsing()
    {
        // Create a parser that will parse many items
        var parser = ZeroOrMany(Terms.Integer());

        // Create many numbers to parse
        var input = string.Join(" ", Enumerable.Range(1, 10000));

        // Create a cancellation token that we'll cancel after a delay
        var cts = new CancellationTokenSource();

        // Cancel immediately
        cts.Cancel();

        // Parsing should be cancelled
        Assert.Throws<OperationCanceledException>(() =>
            parser.Parse(input, cts.Token));
    }

    [Fact]
    public void ParseContextShouldStoreTokenCorrectly()
    {
        var cts = new CancellationTokenSource();
        var scanner = new Scanner("test");
        var context = new ParseContext(scanner, cts.Token);

        // Verify the token is stored
        Assert.Equal(cts.Token, context.CancellationToken);
    }

    [Fact]
    public void ParseContextDefaultConstructorShouldUseNoneToken()
    {
        var scanner = new Scanner("test");
        var context = new ParseContext(scanner);

        // Verify the token is CancellationToken.None
        Assert.Equal(CancellationToken.None, context.CancellationToken);
    }

    [Fact]
    public void ShouldCancelWithTimeoutExample()
    {
        // This demonstrates a timeout scenario as mentioned in the issue
        var parser = ZeroOrMany(Terms.Integer());
        var input = string.Join(" ", Enumerable.Range(1, 1000));

        // Create a cancellation token with a very short timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // This might or might not throw depending on timing, but demonstrates the usage
        try
        {
            var result = parser.Parse(input, cts.Token);
            // If we get here, parsing completed before timeout
        }
        catch (OperationCanceledException)
        {
            // Expected if timeout occurred
        }
    }

    [Fact]
    public void DeeplyNestedParsersShouldRespectCancellation()
    {
        // Create a deeply nested parser structure
        var parser = Deferred<TextSpan>();
        parser.Parser = OneOf(
            Terms.Integer().Then(x => new TextSpan()),
            Between(Terms.Char('('), parser, Terms.Char(')'))
        );

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should throw when cancelled
        Assert.Throws<OperationCanceledException>(() =>
            parser.Parse("((((1))))", cts.Token));
    }

    [Fact]
    public void RecursiveParserShouldRespectCancellation()
    {
        // Create a simple parser that can be cancelled
        var parser = ZeroOrMany(Terms.Integer());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should throw when cancelled
        Assert.Throws<OperationCanceledException>(() =>
            parser.Parse("1 2 3 4 5", cts.Token));
    }

    [Fact]
    public void TryParseWithoutCancellationTokenShouldWork()
    {
        var parser = Terms.Integer();

        // Should work as before - backward compatibility
        var success = parser.TryParse("123", out var result);

        Assert.True(success);
        Assert.Equal(123, result);
    }

    [Fact]
    public void ParseWithoutCancellationTokenShouldWork()
    {
        var parser = Terms.Integer();

        // Should work as before - backward compatibility
        var result = parser.Parse("456");

        Assert.Equal(456, result);
    }
}
