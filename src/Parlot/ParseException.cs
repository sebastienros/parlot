using System;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
#endif

namespace Parlot;

/// <summary>
/// The exception thrown when a text can't be parsed, with the position of the failure in the text.
/// </summary>
#if !NET8_0_OR_GREATER
[Serializable]
#endif
public class ParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class, without a position.
    /// </summary>
    /// <remarks><see cref="Position"/> is left at its default value.</remarks>
    public ParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class with a message, without a position.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <remarks><see cref="Position"/> is left at its default value.</remarks>
    public ParseException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class with a message and the exception which caused it, without a position.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="innerException">The exception which caused the failure, or <see langword="null"/>.</param>
    /// <remarks><see cref="Position"/> is left at its default value.</remarks>
    public ParseException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class with a message and the position of the failure.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="position">The position of the failure in the text.</param>
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
    /// <param name="info">The serialized data of the exception.</param>
    /// <param name="context">The source of the serialized data.</param>
    protected ParseException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        Position = new TextPosition(info.GetInt32(nameof(TextPosition.Offset)), info.GetInt32(nameof(TextPosition.Line)), info.GetInt32(nameof(TextPosition.Column)));
        IsNestedTooDeeply = info.GetBoolean(nameof(IsNestedTooDeeply));
    }

    /// <summary>
    /// Adds the position, and whether the input is nested too deeply, to the serialized data of the exception.
    /// </summary>
    /// <param name="info">The serialized data of the exception.</param>
    /// <param name="context">The destination of the serialized data.</param>
    // Exception.GetObjectData is security critical on .NET Framework, where an override must be too: a transparent one,
    // which it is in an assembly that allows partially trusted callers, prevents the type from loading.
    [System.Security.SecurityCritical]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(TextPosition.Offset), Position.Offset);
        info.AddValue(nameof(TextPosition.Line), Position.Line);
        info.AddValue(nameof(TextPosition.Column), Position.Column);
        info.AddValue(nameof(IsNestedTooDeeply), IsNestedTooDeeply);
    }
#endif

    /// <summary>
    /// The position of the failure in the text. An exception created without a position has the default
    /// <see cref="TextPosition"/>, whose line is 0, which no position in a text has.
    /// </summary>
    public TextPosition Position { get; set; }

    /// <summary>
    /// Whether the parse failed because the input is nested too deeply: for the recursion limit of the context, or for the
    /// stack of the thread.
    /// </summary>
    public bool IsNestedTooDeeply { get; internal set; }
}
