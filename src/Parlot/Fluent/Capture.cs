using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class Capture<T> : Parser<TextSpan>, ICompilable, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }


    public Capture(Parser<T> parser)
    {
        _parser = parser;

        if (parser is ISeekable seekable && seekable.CanSeek)
        {
            CanSeek = true;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        ParseResult<T> _ = new();

        // Did parser succeed.
        if (_parser.Parse(context, ref _))
        {
            var end = context.Scanner.Cursor.Offset;
            var length = end - start.Offset;

            result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, length));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var start = context.Scanner.Cursor.Position;
        var start = context.DeclarePositionVariable(result);

        var ignoreResults = context.DiscardResult;
        context.DiscardResult = true;

        var parserCompileResult = _parser.Build(context);

        context.DiscardResult = ignoreResults;

        // parse1 instructions
        //
        // if (parser1.Success)
        // {
        //     var end = context.Scanner.Cursor.Offset;
        //     var length = end - start.Offset;
        //   
        //     value = new TextSpan(context.Scanner.Buffer, start.Offset, length);
        //   
        //     success = true;
        // }

        var startOffset = result.DeclareVariable<int>($"startOffset{context.NextNumber}", context.Offset(start));

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.IfThen(
                    test: parserCompileResult.Success,
                    ifTrue: Expression.Block(
                        // Never discard result here, that would nullify this parser
                        Expression.Assign(result.Value,
                            context.NewTextSpan(
                                context.Buffer(),
                                startOffset,
                                Expression.Subtract(context.Offset(), startOffset)
                                )),
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                        )
                )
            )
        );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Capture requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var innerValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        
        var startName = $"start{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var lengthName = $"length{context.NextNumber()}";
        
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Capture", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out _))
        // {
        //     var end = cursor.Offset;
        //     var length = end - start.Offset;
        //     value = new TextSpan(scanner.Buffer, start.Offset, length);
        //     success = true;
        // }
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        result.Body.Add($"    var {lengthName} = {endName} - {startName}.Offset;");
        result.Body.Add($"    {result.ValueVariable} = new global::Parlot.TextSpan({scannerName}.Buffer, {startName}.Offset, {lengthName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Capture)";
}
