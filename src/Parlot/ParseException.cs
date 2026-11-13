using System;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
#endif

namespace Parlot;

#if !NET8_0_OR_GREATER
[Serializable]
#endif
public class ParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class, without a position.
    /// </summary>
    public ParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class with a message, without a position.
    /// </summary>
    public ParseException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class with a message and the exception which caused it, without a position.
    /// </summary>
    public ParseException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

#if !PARLOT_STANDALONE
    // An untyped second argument, like default or new(), keeps selecting this constructor rather than being ambiguous with
    // the one which takes an inner exception. The embedded runtime compiles with C# 10, which doesn't know the attribute.
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
#endif
    public ParseException(string message, TextPosition position) : base(message)
    {
        Position = position;
    }

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class from serialized data.
    /// </summary>
    protected ParseException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        Position = new TextPosition(info.GetInt32(nameof(TextPosition.Offset)), info.GetInt32(nameof(TextPosition.Line)), info.GetInt32(nameof(TextPosition.Column)));
    }

    /// <summary>
    /// Adds the position to the serialized data of the exception.
    /// </summary>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(TextPosition.Offset), Position.Offset);
        info.AddValue(nameof(TextPosition.Line), Position.Line);
        info.AddValue(nameof(TextPosition.Column), Position.Column);
    }
#endif

    public TextPosition Position { get; set; }
}
