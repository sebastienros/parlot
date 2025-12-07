using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

/// <summary>
/// Runtime helpers for source-generated parsers. These methods are called by generated code
/// to reduce the amount of inline code that needs to be emitted.
/// </summary>
public static class SourceGenerationHelpers
{
    /// <summary>
    /// Tries to match and advance past a single character, optionally skipping whitespace first.
    /// </summary>
    /// <param name="context">The parse context.</param>
    /// <param name="c">The character to match.</param>
    /// <param name="skipWhiteSpace">Whether to skip whitespace before matching.</param>
    /// <returns>True if the character was matched and consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryMatchChar(ParseContext context, char c, bool skipWhiteSpace)
    {
        var cursor = context.Scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        if (cursor.Match(c))
        {
            cursor.Advance();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to match a text literal with the specified comparison.
    /// </summary>
    /// <param name="context">The parse context.</param>
    /// <param name="text">The text to match.</param>
    /// <param name="comparison">The string comparison mode.</param>
    /// <param name="skipWhiteSpace">Whether to skip whitespace before matching.</param>
    /// <returns>True if the text was matched and consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryMatchText(ParseContext context, string text, StringComparison comparison, bool skipWhiteSpace)
    {
        var cursor = context.Scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        if (context.Scanner.ReadText(text, comparison))
        {
            return true;
        }

        return false;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Tries to parse a decimal number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDecimal(
        ParseContext context,
        out decimal value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (decimal.TryParse(numberSpan, numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a long integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseLong(
        ParseContext context,
        out long value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (long.TryParse(numberSpan, numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a double.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDouble(
        ParseContext context,
        out double value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (double.TryParse(numberSpan, numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a float.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseFloat(
        ParseContext context,
        out float value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (float.TryParse(numberSpan, numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse an int.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseInt(
        ParseContext context,
        out int value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (int.TryParse(numberSpan, numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }
#else
    /// <summary>
    /// Tries to parse a decimal number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDecimal(
        ParseContext context,
        out decimal value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (decimal.TryParse(numberSpan.ToString(), numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a long integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseLong(
        ParseContext context,
        out long value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (long.TryParse(numberSpan.ToString(), numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a double.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseDouble(
        ParseContext context,
        out double value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (double.TryParse(numberSpan.ToString(), numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse a float.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseFloat(
        ParseContext context,
        out float value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (float.TryParse(numberSpan.ToString(), numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }

    /// <summary>
    /// Tries to parse an int.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseInt(
        ParseContext context,
        out int value,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        NumberStyles numberStyles,
        CultureInfo cultureInfo,
        char decimalSeparator,
        char groupSeparator,
        bool skipWhiteSpace)
    {
        value = default;
        var scanner = context.Scanner;
        var cursor = scanner.Cursor;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var reset = cursor.Position;

        if (scanner.ReadDecimal(allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, out var numberSpan, decimalSeparator, groupSeparator))
        {
            if (int.TryParse(numberSpan.ToString(), numberStyles, cultureInfo, out value))
            {
                return true;
            }
        }

        cursor.ResetPosition(reset);
        return false;
    }
#endif
}
