using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Globalization;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class TextLiteral : Parser<string>, ICompilable, ISeekable
{
    private readonly StringComparison _comparisonType;
    private readonly bool _hasNewLines;

    private static readonly MethodInfo _parseTextMethodInfo = typeof(TextLiteral).GetMethod(nameof(ParseText), BindingFlags.NonPublic | BindingFlags.Static)!;

    // The invariant upper case of each char of the BMP, at the index of the char. It is only built for a
    // case-insensitive text which doesn't start with an ASCII char.
    private static string? _invariantUpperCase;

    public TextLiteral(string text, StringComparison comparisonType)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _comparisonType = comparisonType;
        // The cursor only ends a line on '\n'; '\r' and '\v' are ordinary chars for it
        _hasNewLines = text.AsSpan().IndexOf('\n') >= 0;

        // A culture comparison ignores some chars, like the soft hyphen, and the current culture is the one of the
        // thread that parses, not the one of this thread. The chars such a text can start with are not known, only
        // an ordinal comparison starts with the first char of the text, or with its other cases.
        if (CanSeek = Text.Length > 0 && comparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase)
        {
            ExpectedChars = comparisonType == StringComparison.OrdinalIgnoreCase
                ? OrdinalIgnoreCaseChars(Text)
                : [Text[0]];
        }
    }

    // The chars an ordinal case-insensitive comparison takes for the first one of a text. These are not the upper
    // and lower cases a culture gives: under a Turkish culture the upper case of 'i' is the dotted 'I' (U+0130),
    // which the comparison doesn't take for 'i', while it takes 'I'.
    private static char[] OrdinalIgnoreCaseChars(string text)
    {
        var first = text[0];

        if (first < 128)
        {
            // An ASCII letter only has its other case, on every runtime
            if (Character.IsInRange(first, 'a', 'z'))
            {
                return [(char)(first - 32), first];
            }

            if (Character.IsInRange(first, 'A', 'Z'))
            {
                return [first, (char)(first + 32)];
            }

            return [first];
        }

        var chars = new List<char>();

        if (char.IsSurrogate(first))
        {
            // A char beyond the BMP is compared as a whole. What is expected is the first half of each of its cases.
            var cased = char.IsSurrogatePair(text, 0) ? text.Substring(0, 2) : text.Substring(0, 1);

            chars.Add(first);

            foreach (var other in new[] { cased.ToUpperInvariant()[0], cased.ToLowerInvariant()[0] })
            {
                if (!chars.Contains(other))
                {
                    chars.Add(other);
                }
            }

            return [.. chars];
        }

        // Other chars can have more than one other case: the Greek sigma (U+03C3), final sigma (U+03C2) and capital
        // sigma (U+03A3) are the same to the comparison, and so are the micro sign (U+00B5) and the Greek mu (U+03BC).
        // The candidates are the chars with the same upper case, and the comparison decides.

        var upperCase = _invariantUpperCase;

        if (upperCase == null)
        {
            var all = new char[char.MaxValue + 1];

            for (var i = 0; i < all.Length; i++)
            {
                // Two surrogates in a row would be read as a single char
                all[i] = char.IsSurrogate((char)i) ? '\0' : (char)i;
            }

            // A single call for all the chars, since each call goes through the globalization library
            _invariantUpperCase = upperCase = new string(all).ToUpperInvariant();
        }

        var expected = first.ToString();
        var firstUpperCase = upperCase[first];

        for (var i = 0; i < upperCase.Length; i++)
        {
            if (upperCase[i] == firstUpperCase && string.Equals(((char)i).ToString(), expected, StringComparison.OrdinalIgnoreCase))
            {
                chars.Add((char)i);
            }
        }

        return [.. chars];
    }

    public string Text { get; }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (ParseText(cursor, Text, _comparisonType, _hasNewLines, out var value))
        {
            result.Set(start, cursor.Offset, value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseText(Cursor cursor, string text, StringComparison comparisonType, bool hasNewLines, out string value)
    {
        if (cursor.Match(text.AsSpan(), comparisonType))
        {
            var start = cursor.Offset;

            if (hasNewLines)
            {
                cursor.Advance(text.Length);
            }
            else
            {
                cursor.AdvanceNoNewLines(text.Length);
            }

            var end = cursor.Offset;

            if (comparisonType == StringComparison.Ordinal)
            {
                value = text;
            }
            else
            {
                var buffer = cursor.Buffer;
                var parsedText = buffer.AsSpan(start, end - start);

                // Prevent an allocation if the text matches exactly or spans the full buffer
                value = parsedText.Equals(text.AsSpan(), StringComparison.Ordinal)
                    ? text
                    : start == 0 && end == buffer.Length
                        ? buffer
                        : parsedText.ToString();
            }

            return true;
        }

        value = null!;
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<string>();

        var value = Expression.Variable(typeof(string), $"value{context.NextNumber}");
        result.Variables.Add(value);

        var selfConstant = Expression.Constant(this);
        var textProperty = Expression.Property(selfConstant, nameof(Text));

        result.Body.Add(
            Expression.IfThen(
                Expression.Call(
                    _parseTextMethodInfo,
                    context.Cursor(),
                    textProperty,
                    Expression.Constant(_comparisonType, typeof(StringComparison)),
                    Expression.Constant(_hasNewLines),
                    value
                ),
                Expression.Block(
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, value)
                )
            )
        );

        return result;
    }

    public override string ToString() => $"Text(\"{Text}\")";
}
