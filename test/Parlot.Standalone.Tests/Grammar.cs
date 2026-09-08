using System.Collections.Generic;

namespace Parlot.Standalone.Tests;

public static partial class Grammar
{
    public static partial bool TryParseNumber(string text, out int value);
    public static partial bool TryParsePrefix(string text, out string value);
    public static partial bool TryParseAlternative(string text, out char value);
    public static partial bool TryParseString(string text, out string value);
    public static partial bool TryParseNumbers(string text, out IReadOnlyList<int> value);
    public static partial bool TryParseOptional(string text, out int value);
    public static partial bool TryParseRecursive(string text, out int value);
    public static partial bool TryParseCustomWhitespace(string text, out string value);
    public static partial bool TryParseConfigured(string text, GrammarOptions options, out string value);
    public static partial bool TryParseConfiguredWhitespace(string text, GrammarOptions options, out string value);
    public static partial bool TryParseError(string text, out char value);
    public static partial bool TryParseThrowingCallback(string text, out char value);
    public static partial bool TryParseExplicitLambda(string text, out string value);
}

public sealed class GrammarOptions
{
    public bool Formal { get; set; }
    public string Prefix { get; set; }
    public int Evaluations { get; set; }
}
