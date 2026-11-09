using System;
using Xunit;

namespace Parlot.Tests;

public class ParseExceptionTests
{
    [Fact]
    public void ShouldHaveTheStandardExceptionConstructors()
    {
        // Libraries which check their exception types create them through reflection (#548)
        var inner = new InvalidOperationException("inner");

        var empty = (ParseException)Activator.CreateInstance(typeof(ParseException))!;
        var withMessage = (ParseException)Activator.CreateInstance(typeof(ParseException), "message")!;
        var withInner = (ParseException)Activator.CreateInstance(typeof(ParseException), "message", inner)!;

        Assert.Equal(default, empty.Position);
        Assert.Equal("message", withMessage.Message);
        Assert.Null(withMessage.InnerException);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(default, withInner.Position);
    }

    [Fact]
    public void ShouldKeepItsPosition()
    {
        var exception = new ParseException("message", new TextPosition(4, 2, 3));

        Assert.Equal("message", exception.Message);
        Assert.Equal(new TextPosition(4, 2, 3), exception.Position);
    }

#if NETFRAMEWORK
    [Fact]
    public void ShouldSerializeWithItsPosition()
    {
        var exception = new ParseException("message", new TextPosition(4, 2, 3));

        using var stream = new System.IO.MemoryStream();
        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        formatter.Serialize(stream, exception);
        stream.Position = 0;
        var copy = (ParseException)formatter.Deserialize(stream);

        Assert.Equal("message", copy.Message);
        Assert.Equal(new TextPosition(4, 2, 3), copy.Position);
    }
#endif
}
