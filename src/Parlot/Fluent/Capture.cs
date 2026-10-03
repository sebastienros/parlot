using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Capture<T> : Parser<TextSpan>, ISeekable, ISourceable
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

        // The captured text must still be buffered once the parser is done
        var pin = context.Pin();
        var success = _parser.Parse(context, ref _);
        context.Unpin(pin);

        if (success)
        {
            var end = context.Scanner.Cursor.Offset;
            var length = end - start.Offset;

            result.Set(start.Offset, end, context.Scanner.Cursor.CreateSpan(start.Offset, length));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
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
        var helperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Capture", innerValueTypeName, () => sourceable.GenerateSource(context))
            .MethodName);

        // if (Helper(context, out _))
        // {
        //     var end = cursor.Offset;
        //     var length = end - start.Offset;
        //     value = new TextSpan(scanner.Buffer, start.Offset, length);
        //     success = true;
        // }
        var success = context.PinnedCall(result, $"{helperName}({context.ParseContextName}, out _)");
        result.Body.Add($"if ({success})");
        result.Body.Add("{");
        result.Body.Add($"    var {endName} = {cursorName}.Offset;");
        result.Body.Add($"    var {lengthName} = {endName} - {startName}.Offset;");
        result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}.Offset, {lengthName});");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Capture)";
}
