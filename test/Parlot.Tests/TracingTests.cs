using Parlot.Fluent;
using Parlot.Tracing;
using System;
using System.Text.Json;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class TracingTests
{
    [Fact]
    public void TracingShouldCaptureParserExecution()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("123"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetFirefoxProfilerJson();
        Assert.NotEmpty(json);
        
        // Verify it's valid JSON
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public void TracingShouldIncludeParserNames()
    {
        // Arrange
        var parser = Terms.Integer().Named("MyInteger");
        var context = new ParseContext(new Scanner("456"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetFirefoxProfilerJson();
        
        // Verify parser name is in the string table
        Assert.Contains("MyInteger", json);
    }

    [Fact]
    public void TracingShouldIncludeSuccessFlag()
    {
        // Arrange
        var parser = Terms.Text("hello");
        var successContext = new ParseContext(new Scanner("hello"));
        var failContext = new ParseContext(new Scanner("world"));

        // Act - Success case
        using (var tracing = ParserTracing.Start(successContext))
        {
            var success = parser.TryParse(successContext, out var result, out _);
            Assert.True(success);
            var json = tracing.GetFirefoxProfilerJson();
            
            // Parse JSON and check string table for success marker
            using var doc = JsonDocument.Parse(json);
            var stringTable = doc.RootElement.GetProperty("threads")[0].GetProperty("stringTable");
            var hasSuccessMarker = false;
            for (int i = 0; i < stringTable.GetArrayLength(); i++)
            {
                var entry = stringTable[i].GetString();
                if (entry != null && entry.Contains("✓"))
                {
                    hasSuccessMarker = true;
                    break;
                }
            }
            Assert.True(hasSuccessMarker, "Should contain success marker ✓");
        }

        // Act - Failure case (parser will be called but fail)
        using (var tracing = ParserTracing.Start(failContext))
        {
            var success = parser.TryParse(failContext, out var result, out _);
            Assert.False(success);
            var json = tracing.GetFirefoxProfilerJson();
            
            // Parse JSON and check string table for failure marker
            using var doc = JsonDocument.Parse(json);
            var stringTable = doc.RootElement.GetProperty("threads")[0].GetProperty("stringTable");
            var hasFailureMarker = false;
            for (int i = 0; i < stringTable.GetArrayLength(); i++)
            {
                var entry = stringTable[i].GetString();
                if (entry != null && entry.Contains("✗"))
                {
                    hasFailureMarker = true;
                    break;
                }
            }
            Assert.True(hasFailureMarker, "Should contain failure marker ✗");
        }
    }

    [Fact]
    public void TracingShouldIncludeInputPreview()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("789abc"));

        // Act
        using var tracing = ParserTracing.Start(context, new TracingOptions { PreviewLength = 5 });
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetFirefoxProfilerJson();
        
        // Verify input preview is present (should contain "789ab" or similar)
        Assert.Contains("789", json);
    }

    [Fact]
    public void TracingShouldHandleNestedParsers()
    {
        // Arrange
        var innerParser = Terms.Integer().Named("Inner");
        var outerParser = innerParser.And(Terms.Text(",")).Named("Outer");
        var context = new ParseContext(new Scanner("123,"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = outerParser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetFirefoxProfilerJson();
        
        // Both parser names should be in the trace
        Assert.Contains("Inner", json);
        Assert.Contains("Outer", json);
    }

    [Fact]
    public void TracingShouldProduceValidFirefoxProfilerSchema()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("42"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetFirefoxProfilerJson();
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Verify required Firefox Profiler schema elements
        Assert.True(root.TryGetProperty("meta", out _));
        Assert.True(root.TryGetProperty("threads", out var threads));
        Assert.True(threads.GetArrayLength() > 0);
        
        var thread = threads[0];
        Assert.True(thread.TryGetProperty("stringTable", out _));
        Assert.True(thread.TryGetProperty("frameTable", out _));
        Assert.True(thread.TryGetProperty("stackTable", out _));
        Assert.True(thread.TryGetProperty("samples", out _));
        Assert.True(thread.TryGetProperty("markers", out _));
    }

    [Fact]
    public void TracingCanBeDisabledAndReEnabled()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("123"));

        // Act - Parse without tracing
        var success1 = parser.TryParse(context, out var result1, out _);
        Assert.True(success1);
        Assert.Null(context.Tracer);

        // Enable tracing
        using (var tracing = ParserTracing.Start(context))
        {
            Assert.NotNull(context.Tracer);
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            var success2 = parser.TryParse(context, out var result2, out _);
            Assert.True(success2);
        }

        // Tracer should be removed after disposal
        Assert.Null(context.Tracer);
    }

    [Fact]
    public void TracingShouldHandleEmptyInput()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner(""));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.False(success);
        var json = tracing.GetFirefoxProfilerJson();
        
        // Should still produce valid JSON even with failed parse
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public void TracingOptionsCanCustomizePreviewLength()
    {
        // Arrange
        var parser = Terms.Integer();
        var shortContext = new ParseContext(new Scanner("123456789"));
        var longContext = new ParseContext(new Scanner("123456789"));

        // Act - Short preview
        using (var tracing = ParserTracing.Start(shortContext, new TracingOptions { PreviewLength = 3 }))
        {
            parser.TryParse(shortContext, out var result1, out _);
            var json = tracing.GetFirefoxProfilerJson();
            // With preview length 3, we should see "123" but not much more
            Assert.Contains("123", json);
        }

        // Act - Long preview
        using (var tracing = ParserTracing.Start(longContext, new TracingOptions { PreviewLength = 20 }))
        {
            parser.TryParse(longContext, out var result2, out _);
            var json = tracing.GetFirefoxProfilerJson();
            // With longer preview, we should see more digits
            Assert.Contains("123456789", json);
        }
    }
}
