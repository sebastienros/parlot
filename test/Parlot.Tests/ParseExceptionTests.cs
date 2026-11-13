using Parlot.Fluent;
using System;
using System.Linq.Expressions;
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

        Assert.Null(empty.InnerException);
        Assert.Equal(default, empty.Position);
        Assert.Equal("message", withMessage.Message);
        Assert.Null(withMessage.InnerException);
        Assert.Equal(default, withMessage.Position);
        Assert.Equal("message", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(default, withInner.Position);
    }

    [Fact]
    public void ShouldKeepItsPosition()
    {
        var exception = new ParseException("message", new TextPosition(4, 2, 3));

        Assert.Equal("message", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(new TextPosition(4, 2, 3), exception.Position);
    }

    [Fact]
    public void UntypedPositionShouldStillSelectThePositionConstructor()
    {
        // These calls compiled before the inner exception constructor existed, and must not become ambiguous with it
        Expression<Func<ParseException>> withDefault = () => new ParseException("message", default);
        Expression<Func<ParseException>> withNew = () => new ParseException("message", new());
        Expression<Func<ParseException>> withNull = () => new ParseException("message", null);

        Assert.Equal(typeof(TextPosition), SecondParameter(withDefault));
        Assert.Equal(typeof(TextPosition), SecondParameter(withNew));
        Assert.Equal(typeof(Exception), SecondParameter(withNull));

        static Type SecondParameter(Expression<Func<ParseException>> expression)
        {
            return ((NewExpression)expression.Body).Constructor!.GetParameters()[1].ParameterType;
        }
    }

    [Fact]
    public void ExceptionWithoutPositionShouldNotReportOne()
    {
        var parser = Parsers.Literals.Text("a").Then<string>(static _ => throw new ParseException("no position"));

        Assert.False(parser.TryParse("a", out _, out var error));
        Assert.Equal("no position", error!.Message);
        Assert.Equal(default, error.Position);
    }

#if NETFRAMEWORK
    [Fact]
    public void ShouldSerializeWithItsPosition()
    {
        var exception = new ParseException("message", new TextPosition(4, 2, 3));

        var copy = RoundTrip(exception);

        Assert.Equal("message", copy.Message);
        Assert.Null(copy.InnerException);
        Assert.Equal(new TextPosition(4, 2, 3), copy.Position);
    }

    [Fact]
    public void ShouldSerializeWithItsInnerException()
    {
        var exception = new ParseException("message", new InvalidOperationException("inner"));

        var copy = RoundTrip(exception);

        Assert.Equal("message", copy.Message);
        Assert.Equal("inner", Assert.IsType<InvalidOperationException>(copy.InnerException).Message);
        Assert.Equal(default, copy.Position);
    }

    private static ParseException RoundTrip(ParseException exception)
    {
        using var stream = new System.IO.MemoryStream();
        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        formatter.Serialize(stream, exception);
        stream.Position = 0;
        return (ParseException)formatter.Deserialize(stream);
    }
#endif
}
