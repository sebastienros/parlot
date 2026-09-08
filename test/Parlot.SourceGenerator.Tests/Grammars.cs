#nullable enable

using System;
using System.Collections.Generic;
using Parlot.Tests.Calc;

namespace Parlot.SourceGenerator.Tests;

public static partial class Grammars
{
    public static partial bool TryParseHello(string text, out string value);
    public static partial bool TryParseExpression(string text, out double value);
    public static partial bool TryParseLeftAssociative(string text, out double value);
    public static partial bool TryParseNestedLeftAssociative(string text, out double value);
    public static partial bool TryParseCalculator(string text, out Expression value);
    public static partial bool TryParseTermsChar(string text, out char value);
    public static partial bool TryParseTermsString(string text, out string value);
    public static partial bool TryParseTermsPattern(string text, out string value);
    public static partial bool TryParseTermsIdentifier(string text, out string value);
    public static partial bool TryParseTermsWhiteSpace(string text, out string value);
    public static partial bool TryParseTermsNonWhiteSpace(string text, out string value);
    public static partial bool TryParseTermsDecimal(string text, out decimal value);
    public static partial bool TryParseTermsKeyword(string text, out string value);
    public static partial bool TryParseLiteralsText(string text, out string value);
    public static partial bool TryParseLiteralsChar(string text, out char value);
    public static partial bool TryParseLiteralsQuote(string text, out char value);
    public static partial bool TryParseLiteralsBackslash(string text, out char value);
    public static partial bool TryParseLiteralsNewLine(string text, out char value);
    public static partial bool TryParseSequence(string text, out (string Text, char Character) value);
    public static partial bool TryParseSkipAnd(string text, out char value);
    public static partial bool TryParseAndSkip(string text, out char value);
    public static partial bool TryParseOptionalText(string text, out string? value);
    public static partial bool TryParseZeroOrManyChars(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseZeroOrOneChar(string text, out char value);
    public static partial bool TryParseEofText(string text, out string value);
    public static partial bool TryParseCaptureChar(string text, out string value);
    public static partial bool TryParseOneOfChar(string text, out char value);
    public static partial bool TryParseBetweenIdentifier(string text, out string value);
    public static partial bool TryParseSeparatedDecimals(string text, out IReadOnlyList<decimal> value);
    public static partial bool TryParseUnaryDecimal(string text, out decimal value);
    public static partial bool TryParseLeftAssociativeDecimal(string text, out decimal value);
    public static partial bool TryParseLeftAssociativeThenPlus(string text, out decimal value);
    public static partial bool TryParseUnaryFallback(string text, out decimal value);
    public static partial bool TryParseAnyOfDigits(string text, out string value);
    public static partial bool TryParseAnyOfLetters(string text, out string value);
    public static partial bool TryParseNoneOfWhitespace(string text, out string value);
    public static partial bool TryParseBlockLambda(string text, out string value);
    public static partial bool TryParseNotX(string text, out char value);
    public static partial bool TryParseHelloNotBang(string text, out string value);
    public static partial bool TryParseHelloBang(string text, out string value);
    public static partial bool TryParseCountingOneOf(string text, out char value);
    public static partial bool TryParseCustomSwitch(string text, out char value);
    public static partial bool TryParseCustomSelect(string text, bool preferX, out char value);
    public static partial bool TryParseZeroOrManyOptional(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseOneOrManyOptional(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseSeparatedOptional(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseGenericProperty(string text, out NodeBase value);

    public static partial bool TryParseInteger(string text, out long value);
    public static partial bool TryParseDecimal(string text, out decimal value);
    public static partial bool TryParseDoubleExponent(string text, out double value);
    public static partial bool TryParseCommaDecimal(string text, out decimal value);
    public static partial bool TryParseUnderscoreInteger(string text, out long value);
    public static partial bool TryParseUnsignedInteger(string text, out long value);
    public static partial bool TryParseIntegralDecimal(string text, out decimal value);
    public static partial bool TryParseFloat(string text, out float value);
    public static partial bool TryParseLong(string text, out long value);
    public static partial bool TryParseByte(string text, out byte value);
    public static partial bool TryParseCustomCultureDecimal(string text, out decimal value);
    public static partial bool TryParseHexadecimal(string text, out int value);
    public static partial bool TryParseOctal(string text, out long value);
    public static partial bool TryParseBinary(string text, out byte value);
}

public abstract class NodeBase
{
}

public sealed class BasicNode : NodeBase
{
    public BasicNode(object value)
    {
        Value = value;
    }

    public object Value { get; }
}

public static class GeneratedParserCounters
{
    private static readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public static void Reset()
    {
        lock (_lock)
        {
            _counts.Clear();
        }
    }

    public static int GetCount(string name)
    {
        lock (_lock)
        {
            return _counts.TryGetValue(name, out var count) ? count : 0;
        }
    }

    public static void Increment(string name)
    {
        lock (_lock)
        {
            _counts[name] = _counts.TryGetValue(name, out var count) ? count + 1 : 1;
        }
    }
}
