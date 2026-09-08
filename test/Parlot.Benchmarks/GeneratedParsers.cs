using System.Collections.Generic;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks;

/// <summary>
/// Dependency-free source-generated entry points used by the benchmarks.
/// </summary>
public static partial class GeneratedParsers
{
    public static partial bool TryParseExpression(string input, out Expression value);
    public static partial bool TryParseJson(string input, out IJson value);
    public static partial bool TryParseText(string input, out string value);
    public static partial bool TryParseDecimal(string input, out decimal value);
    public static partial bool TryParseInteger(string input, out long value);
    public static partial bool TryParseOneOf(string input, out string value);
    public static partial bool TryParseLiteralOneOf(string input, out string value);
    public static partial bool TryParseAnd(string input, out (string, decimal) value);
    public static partial bool TryParseZeroOrMany(string input, out IReadOnlyList<decimal> value);
    public static partial bool TryParseSkipWhiteSpace(string input, out decimal value);
}
