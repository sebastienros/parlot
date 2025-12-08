using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class ElseError<T> : Parser<T>, ICompilable, ISourceable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true);

        // parse1 instructions
        // success = true
        // 
        // if (parser1.Success)
        // {
        //   value = parser1.Value
        // }
        // else
        // {
        //    throw new ParseException(_message, context.Scanner.Cursor.Position);
        // }
        //

        var parserCompileResult = _parser.Build(context, requireResult: true);

        var block = Expression.Block(
            parserCompileResult.Variables,
            parserCompileResult.Body
            .Append(
                context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, parserCompileResult.Value))
                .Append(
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(result.Value, parserCompileResult.Value),
                        context.ThrowParseException(Expression.Constant(_message))


                ))
        );

        result.Body.Add(block);

        return result;
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

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // if (inner.success)
        // {
        //     value = inner.value;
        //     success = true;
        // }
        // else
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        
        result.Body.Add($"if ({inner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {inner.ValueVariable};");
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

public sealed class Error<T> : Parser<T>, ICompilable, ISourceable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // parse1 instructions
        // success = false;
        //
        // if (parser1.Success)
        // {
        //    value = parser1.Value;
        //    throw new ParseException(_message, context.Scanner.Cursor.Position);
        // }

        var parserCompileResult = _parser.Build(context, requireResult: false);

        var block = Expression.Block(
            parserCompileResult.Variables,
            parserCompileResult.Body
                .Append(
                    Expression.IfThen(
                        parserCompileResult.Success,
                        context.ThrowParseException(Expression.Constant(_message))
                    )
                )
        );

        result.Body.Add(block);

        return result;
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

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // if (inner.success)
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;
        
        result.Body.Add($"if ({inner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    throw new global::Parlot.ParseException(\"{_message.Replace("\"", "\\\"")}\", {cursorName}.Position);");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}

public sealed class Error<T, U> : Parser<U>, ICompilable, ISeekable, ISourceable
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<U>();

        // parse1 instructions
        // success = false;
        // 
        // if (parser1.Success)
        // {
        //    throw new ParseException(_message, context.Scanner.Cursor.Position);
        // }

        var parserCompileResult = _parser.Build(context, requireResult: false);

        var block = Expression.Block(
            parserCompileResult.Variables,
            parserCompileResult.Body
                .Append(
                    Expression.IfThen(
                        parserCompileResult.Success,
                        context.ThrowParseException(Expression.Constant(_message))
                    )
                )
        );

        result.Body.Add(block);

        return result;
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

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // if (inner.success)
        // {
        //     throw new ParseException(_message, cursor.Position);
        // }
        // success = false;
        
        result.Body.Add($"if ({inner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    throw new global::Parlot.ParseException(\"{_message.Replace("\"", "\\\"")}\", {cursorName}.Position);");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Error)";
}
