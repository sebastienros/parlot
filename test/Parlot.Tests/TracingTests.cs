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
        var json = tracing.GetSpeedscopeJson();
        Assert.NotEmpty(json);
        
        // Verify it's valid JSON
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public void SpeedscopeShouldHaveCorrectStructure()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("123"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetSpeedscopeJson();
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Verify Speedscope schema
        Assert.True(root.TryGetProperty("$schema", out var schema));
        Assert.Equal("https://www.speedscope.app/file-format-schema.json", schema.GetString());
        
        Assert.True(root.TryGetProperty("shared", out var shared));
        Assert.True(shared.TryGetProperty("frames", out var frames));
        Assert.True(frames.GetArrayLength() > 0);
        
        Assert.True(root.TryGetProperty("profiles", out var profiles));
        Assert.True(profiles.GetArrayLength() > 0);
        
        var profile = profiles[0];
        Assert.Equal("evented", profile.GetProperty("type").GetString());
        Assert.True(profile.TryGetProperty("events", out var events));
        Assert.True(events.GetArrayLength() > 0);
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
        var json = tracing.GetSpeedscopeJson();
        
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
            var json = tracing.GetSpeedscopeJson();
            
            // Parse and check frame names for success marker
            using var doc = JsonDocument.Parse(json);
            var frames = doc.RootElement.GetProperty("shared").GetProperty("frames");
            var hasSuccessMarker = false;
            for (int i = 0; i < frames.GetArrayLength(); i++)
            {
                var frameName = frames[i].GetProperty("name").GetString();
                if (frameName != null && frameName.Contains("✓"))
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
            var json = tracing.GetSpeedscopeJson();
            
            // Parse and check frame names for failure marker
            using var doc = JsonDocument.Parse(json);
            var frames = doc.RootElement.GetProperty("shared").GetProperty("frames");
            var hasFailureMarker = false;
            for (int i = 0; i < frames.GetArrayLength(); i++)
            {
                var frameName = frames[i].GetProperty("name").GetString();
                if (frameName != null && frameName.Contains("✗"))
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
        var json = tracing.GetSpeedscopeJson();
        
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
        var json = tracing.GetSpeedscopeJson();
        
        // Both parser names should be in the trace
        Assert.Contains("Inner", json);
        Assert.Contains("Outer", json);
    }

    [Fact]
    public void TracingShouldProduceValidSpeedscopeSchema()
    {
        // Arrange
        var parser = Terms.Integer();
        var context = new ParseContext(new Scanner("42"));

        // Act
        using var tracing = ParserTracing.Start(context);
        var success = parser.TryParse(context, out var result, out _);

        // Assert
        Assert.True(success);
        var json = tracing.GetSpeedscopeJson();
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        // Verify required Speedscope schema elements
        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.True(root.TryGetProperty("shared", out var shared));
        Assert.True(shared.TryGetProperty("frames", out _));
        Assert.True(root.TryGetProperty("profiles", out var profiles));
        Assert.True(profiles.GetArrayLength() > 0);
        
        var profile = profiles[0];
        Assert.True(profile.TryGetProperty("type", out _));
        Assert.True(profile.TryGetProperty("name", out _));
        Assert.True(profile.TryGetProperty("unit", out _));
        Assert.True(profile.TryGetProperty("events", out _));
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
        var json = tracing.GetSpeedscopeJson();
        
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
            var json = tracing.GetSpeedscopeJson();
            // With preview length 3, we should see "123" but not much more
            Assert.Contains("123", json);
        }

        // Act - Long preview
        using (var tracing = ParserTracing.Start(longContext, new TracingOptions { PreviewLength = 20 }))
        {
            parser.TryParse(longContext, out var result2, out _);
            var json = tracing.GetSpeedscopeJson();
            // With longer preview, we should see more digits
            Assert.Contains("123456789", json);
        }
    }
}
