using System;
using System.Reflection;
using Xunit;

namespace Parlot.Standalone.Tests;

public class ParseExceptionTests
{
    // The embedded runtime's exception, which consumers find in their own assembly
    private static readonly Type EmbeddedType = typeof(Grammar).Assembly.GetType("Parlot.Generated.ParseException", throwOnError: true)!;

    private static Exception Create(params object[] arguments)
    {
        return (Exception)Activator.CreateInstance(EmbeddedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, arguments, culture: null)!;
    }

    [Fact]
    public void EmbeddedParseExceptionShouldHaveTheStandardExceptionConstructors()
    {
        // Libraries which check their exception types create them through reflection (#548)
        var inner = new InvalidOperationException("inner");

        var empty = Create();
        var withMessage = Create("message");
        var withInner = Create("message", inner);

        Assert.Null(empty.InnerException);
        Assert.Equal("message", withMessage.Message);
        Assert.Null(withMessage.InnerException);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }

#if NETFRAMEWORK
    [Fact]
    public void EmbeddedParseExceptionShouldSerializeWithItsPosition()
    {
        var positionType = typeof(Grammar).Assembly.GetType("Parlot.Generated.TextPosition", throwOnError: true)!;
        var position = Activator.CreateInstance(positionType, 4, 2, 3)!;
        var exception = Create("message", position);

        using var stream = new System.IO.MemoryStream();
        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        formatter.Serialize(stream, exception);
        stream.Position = 0;
        var copy = (Exception)formatter.Deserialize(stream);

        Assert.IsType(EmbeddedType, copy);
        Assert.Equal("message", copy.Message);
        Assert.Equal(position, EmbeddedType.GetProperty("Position")!.GetValue(copy));
    }
#endif
}
