using Parlot;
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Globalization;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

public sealed class TextLiteral : Parser<string>, ICompilable, ISeekable, ISourceable
{
    private readonly StringComparison _comparisonType;
    private readonly bool _hasNewLines;

    public TextLiteral(string text, StringComparison comparisonType)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _comparisonType = comparisonType;
        _hasNewLines = text.Any(Character.IsNewLine);

        if (CanSeek = Text.Length > 0)
        {
            var ignoreCase = comparisonType switch
            {
                StringComparison.OrdinalIgnoreCase => true,
                StringComparison.CurrentCultureIgnoreCase => true,
                StringComparison.InvariantCultureIgnoreCase => true,
                _ => false
            };

            var invariant = comparisonType switch
            {
                StringComparison.InvariantCulture => true,
                StringComparison.InvariantCultureIgnoreCase => true,
                _ => false
            };

            if (invariant)
            {
                ExpectedChars = ignoreCase ? [Text.ToUpperInvariant()[0], Text.ToLowerInvariant()[0]] : [Text[0]];
            }
            else
            {
                ExpectedChars = ignoreCase ? [Text.ToUpper(CultureInfo.CurrentCulture)[0], Text.ToLower(CultureInfo.CurrentCulture)[0]] : [Text[0]];
            }
        }
    }

    public string Text { get; }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var span = Text.AsSpan();

        if (cursor.Match(span, _comparisonType))
        {
            var start = cursor.Offset;

            if (_hasNewLines)
            {
                cursor.Advance(Text.Length);
            }
            else
            {
                cursor.AdvanceNoNewLines(Text.Length);
            }

            var end = cursor.Offset;
            var parsedText = context.Scanner.Buffer.AsSpan(start, end - start);

            // Prevent an allocation if the text matches exactly
            result.Set(start, end, parsedText.Equals(Text, StringComparison.Ordinal) 
                ? Text 
                : parsedText.ToString());

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<string>();

        var start = context.DeclareOffsetVariable(result);
        var resultSpan = Expression.Variable(typeof(ReadOnlySpan<char>), $"result{context.NextNumber}");
        result.Variables.Add(resultSpan);

        var readTextMethod = typeof(Scanner).GetMethod(nameof(Scanner.ReadText), 
            [typeof(ReadOnlySpan<char>), typeof(StringComparison), typeof(ReadOnlySpan<char>).MakeByRefType()])!;

        var ifReadText = Expression.IfThen(
            Expression.Call(
                Expression.Field(context.ParseContext, "Scanner"),
                readTextMethod,
                Expression.Call(ExpressionHelper.MemoryExtensions_AsSpan, Expression.Constant(Text)),
                Expression.Constant(_comparisonType, typeof(StringComparison)),
                resultSpan
            ),
            Expression.Block(
                Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                context.DiscardResult
                ? Expression.Empty()
                : Expression.Assign(result.Value, Expression.Call(resultSpan, ExpressionHelper.ReadOnlySpan_ToString))
            )
        );

        result.Body.Add(ifReadText);

        return result;
    }

    
    public Parlot.SourceGeneration.SourceResult GenerateSource(Parlot.SourceGeneration.SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(string));

        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var startName = $"start{context.NextNumber()}";

        var textLiteral = ToLiteral(Text);
        var lengthLiteral = Text.Length.ToString(CultureInfo.InvariantCulture);
        var comparison = $"global::System.StringComparison.{_comparisonType}";
        var newLines = CountNewLines(Text);
        var trailingSegmentLength = TrailingSegmentLength(Text);
        var isNotOrdinal = _comparisonType != StringComparison.Ordinal;

        if (isNotOrdinal) 
        {
            result.Body.Add($"int {startName};");
        }

        result.Body.Add($"{result.SuccessVariable} = {cursorName}.Match({textLiteral}, {comparison});");
        
        result.Body.Add($"if ({result.SuccessVariable})");
        result.Body.Add("{");
        if (isNotOrdinal)
        {
            result.Body.Add($"    {startName} = {cursorName}.Offset;");
        }
        result.Body.Add($"    {cursorName}.AdvanceBy({lengthLiteral}, {newLines}, {trailingSegmentLength});");
        
        // Reuse the literal string for Ordinal comparison to avoid allocation (matches Fluent Parse behavior)
        if (isNotOrdinal)
        {
            // For case-insensitive comparisons, we need to extract the actual matched text
            result.Body.Add($"    {result.ValueVariable} = new string({scannerName}.Buffer.AsSpan({startName}, {lengthLiteral}));");
        }
        else
        {
            result.Body.Add($"    {result.ValueVariable} = {textLiteral};");
        }
        
        result.Body.Add("}");

        return result;
    }

public override string ToString() => $"Text(\"{Text}\")";

    private static string ToLiteral(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static int CountNewLines(string value)
    {
        var count = 0;

        foreach (var c in value)
        {
            if (Character.IsNewLine(c))
            {
                count++;
            }
        }

        return count;
    }

    private static int TrailingSegmentLength(string value)
    {
        var lastNewLine = value.LastIndexOf('\n');

        if (lastNewLine < 0)
        {
            return value.Length;
        }

        return value.Length - lastNewLine - 1;
    }
}
