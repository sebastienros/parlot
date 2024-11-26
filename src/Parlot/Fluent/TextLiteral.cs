using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Globalization;

namespace Parlot.Fluent;

public sealed class TextLiteral : Parser<string>, ICompilable, ISeekable
{
    private readonly StringComparison _comparisonType;
    private readonly bool _hasNewLines;

    public TextLiteral(string text, StringComparison comparisonType)
    {
        Name = $"TextLiteral(\"{text}\")";

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

        if (cursor.Match(Text.AsSpan(), _comparisonType))
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

            result.Set(start, cursor.Offset, Text);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<string>();

        // if (context.Scanner.ReadText(Text, _comparer, null))
        // {
        //      success = true;
        //      value = Text;
        // }
        //
        // [if skipWhiteSpace]
        // if (!success)
        // {
        //      resetPosition(beginning);
        // }

        var ifReadText = Expression.IfThen(
            Expression.Call(
                Expression.Field(context.ParseContext, "Scanner"),
                ExpressionHelper.Scanner_ReadText_NoResult,
                Expression.Call(ExpressionHelper.MemoryExtensions_AsSpan, Expression.Constant(Text)),
                Expression.Constant(_comparisonType, typeof(StringComparison))
                ),
            Expression.Block(
                Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                context.DiscardResult
                ? Expression.Empty()
                : Expression.Assign(result.Value, Expression.Constant(Text, typeof(string)))
                )
            );

        result.Body.Add(ifReadText);

        return result;
    }
}
