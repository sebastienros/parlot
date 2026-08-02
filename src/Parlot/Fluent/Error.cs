using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class ElseError<T> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

    public ElseError(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return true;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ElseError requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T), defaultSuccess: true);
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_ElseError", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out value))
        // {
        //     success = true;
        // }
        // else
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        
        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        }
        else
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out {result.ValueVariable}))");
        }
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    throw new global::Parlot.ParseException(\"{_message.Replace("\"", "\\\"")}\", {cursorName}.Position);");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (ElseError)";
}

public sealed class Error<T> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

    public Error(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Error requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Error", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;
        
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        result.Body.Add($"    throw new global::Parlot.ParseException(\"{_message.Replace("\"", "\\\"")}\", {cursorName}.Position);");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}

public sealed class Error<T, U> : Parser<U>, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly string _message;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Error(Parser<T> parser, string message)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _message = message;

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            context.ExitParser(this);
            throw new ParseException(_message, context.Scanner.Cursor.Position);
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Error requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(U));
        var cursorName = context.CursorName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Error", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;
        
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        result.Body.Add($"    throw new global::Parlot.ParseException(\"{_message.Replace("\"", "\\\"")}\", {cursorName}.Position);");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}
