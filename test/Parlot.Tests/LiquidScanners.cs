using System;
using Parlot.Fluent;

namespace Parlot.Tests;

/// <summary>
/// The identifier rules of Liquid templates (Fluid's IdentifierParser and VariableSignatureParser), as span scanners.
/// Linked into the source generator tests and the benchmarks.
/// </summary>
internal static class LiquidScanners
{
    public const string StartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

    /// <summary>
    /// <c>[A-Za-z0-9_][A-Za-z0-9_-]*</c> without a trailing <c>-</c> followed by <c>%</c> or <c>}</c>, containing a non-digit.
    /// </summary>
    public static int Identifier(ReadOnlySpan<char> span)
    {
        var nonDigits = 0;
        var size = ScanSignature(span, ref nonDigits);

        if (nonDigits == 0)
        {
            // Only digits, more text could add a letter
            return size == span.Length ? -1 : 0;
        }

        return size;
    }

    /// <summary>
    /// <c>[A-Za-z0-9_][A-Za-z0-9_-]*</c> without a trailing <c>-</c> followed by <c>%</c> or <c>}</c>.
    /// </summary>
    public static int VariableSignature(ReadOnlySpan<char> span)
    {
        var nonDigits = 0;
        return ScanSignature(span, ref nonDigits);
    }

    private static int ScanSignature(ReadOnlySpan<char> span, ref int nonDigits)
    {
        var size = 0;

        while (size < span.Length)
        {
            var c = span[size];

            if (IsLetterOrUnderscore(c) || (c == '-' && size > 0))
            {
                nonDigits++;
            }
            else if (!IsDigit(c))
            {
                break;
            }

            size++;
        }

        // A trailing '-' next to an end tag is not part of the identifier, c.f. https://github.com/sebastienros/fluid/issues/347
        // At the end of the span the dash is kept, which marks the parse such that it's scanned again with more text.
        if (size > 1 && size < span.Length && span[size - 1] == '-' && (span[size] == '%' || span[size] == '}'))
        {
            size--;
            nonDigits--;
        }

        return size;
    }

    private static bool IsLetterOrUnderscore(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a' || c == '_';

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;
}
