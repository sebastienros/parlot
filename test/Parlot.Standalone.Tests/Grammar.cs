using System.Collections.Generic;
using System.Threading;

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
    public static partial bool TryParseCancelableNumber(string text, CancellationToken cancellationToken, out int value);
    public static partial bool TryParseCancelableRecursive(string text, CancellationToken cancellationToken, out int value);
    public static partial bool TryParseCancelableSequence(string text, CancellationOptions options, CancellationToken cancellationToken, out int value);
    public static partial bool TryParseCancelableWhitespace(string text, CancellationOptions options, CancellationToken cancellationToken, out string value);
    public static partial bool TryParseTokenConfiguration(string text, CancellationToken applicationToken, out bool value);
}

public sealed class CancellationOptions
{
    public CancellationTokenSource Source { get; set; }
    public int Evaluations { get; set; }
}

public sealed class GrammarOptions
{
    public bool Formal { get; set; }
    public string Prefix { get; set; }
    public int Evaluations { get; set; }
}
