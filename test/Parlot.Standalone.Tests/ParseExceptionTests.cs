using System;
using System.Reflection;
using Xunit;

namespace Parlot.Standalone.Tests;

public class ParseExceptionTests
{
    // The embedded runtime's exception, which consumers find in their own assembly
    private static readonly Type EmbeddedType = typeof(Grammar).Assembly.GetType("Parlot.Generated.ParseException", throwOnError: true)!;
    private static readonly Type PositionType = typeof(Grammar).Assembly.GetType("Parlot.Generated.TextPosition", throwOnError: true)!;

    private static Exception Create(params object[] arguments)
    {
        return (Exception)Activator.CreateInstance(EmbeddedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, arguments, culture: null)!;
    }

    private static object Position(Exception exception)
    {
        return EmbeddedType.GetProperty("Position")!.GetValue(exception)!;
    }

    private static bool IsNestedTooDeeply(Exception exception)
    {
        return (bool)EmbeddedType.GetProperty("IsNestedTooDeeply")!.GetValue(exception)!;
    }

    [Fact]
    public void EmbeddedParseExceptionShouldHaveTheStandardExceptionConstructors()
    {
        // Libraries which check their exception types create them through reflection (#548)
        var inner = new InvalidOperationException("inner");
        var noPosition = Activator.CreateInstance(PositionType)!;

        var empty = Create();
        var withMessage = Create("message");
        var withInner = Create("message", inner);

        Assert.Null(empty.InnerException);
        Assert.Equal(noPosition, Position(empty));
        Assert.Equal("message", withMessage.Message);
        Assert.Null(withMessage.InnerException);
        Assert.Equal(noPosition, Position(withMessage));
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(noPosition, Position(withInner));
    }

    [Fact]
    public void EmbeddedParseExceptionShouldKeepItsPosition()
    {
        var position = Activator.CreateInstance(PositionType, 4, 2, 3)!;

        var exception = Create("message", position);

        Assert.Equal("message", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(position, Position(exception));
        Assert.Equal("(2:3)", Position(exception).ToString());
    }

#if NETFRAMEWORK
    [Fact]
    public void EmbeddedParseExceptionShouldSerializeWithItsPosition()
    {
        var position = Activator.CreateInstance(PositionType, 4, 2, 3)!;

        var copy = RoundTrip(Create("message", position));

        Assert.IsType(EmbeddedType, copy);
        Assert.Equal("message", copy.Message);
        Assert.Null(copy.InnerException);
        Assert.Equal(position, Position(copy));
        Assert.False(IsNestedTooDeeply(copy));
    }

    [Fact]
    public void EmbeddedParseExceptionShouldSerializeWhetherTheInputIsNestedTooDeeply()
    {
        var position = Activator.CreateInstance(PositionType, 4, 2, 3)!;
        var exception = Create("message", position);

        // What the context sets when it throws for the recursion limit or the stack of the thread
        EmbeddedType.GetProperty("IsNestedTooDeeply")!.SetValue(exception, true);

        var copy = RoundTrip(exception);

        Assert.True(IsNestedTooDeeply(exception));
        Assert.True(IsNestedTooDeeply(copy));
        Assert.Equal(position, Position(copy));
    }

    [Fact]
    public void EmbeddedParseExceptionShouldSerializeWithItsInnerException()
    {
        var copy = RoundTrip(Create("message", new InvalidOperationException("inner")));

        Assert.IsType(EmbeddedType, copy);
        Assert.Equal("message", copy.Message);
        Assert.Equal("inner", Assert.IsType<InvalidOperationException>(copy.InnerException).Message);
        Assert.Equal(Activator.CreateInstance(PositionType), Position(copy));
    }

    [Fact]
    public void EmbeddedGetObjectDataShouldBeSecurityCritical()
    {
        // The embedded exception lands in assemblies which can allow partially trusted callers, where a transparent
        // override of the security critical Exception.GetObjectData prevents the type from loading
        var method = EmbeddedType.GetMethod("GetObjectData")!;

        Assert.NotNull(Attribute.GetCustomAttribute(method, typeof(System.Security.SecurityCriticalAttribute), inherit: false));
    }

    private static Exception RoundTrip(Exception exception)
    {
        using var stream = new System.IO.MemoryStream();
        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        formatter.Serialize(stream, exception);
        stream.Position = 0;
        return (Exception)formatter.Deserialize(stream);
    }
#endif
}
