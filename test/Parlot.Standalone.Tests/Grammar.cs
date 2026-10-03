using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Parlot.Standalone.Tests;

public static partial class Grammar
{
    public static partial bool TryParseNumber(string text, out int value);
    public static partial bool TryParsePrefix(string text, out string value);
    public static partial bool TryParseMatchedText(string text, out string value);
    public static partial bool TryParseSkippedValues(string text, out string value);
    public static partial bool TryParseIdentifier(string text, out string value);
    public static partial bool TryParseAlternative(string text, out char value);
    public static partial bool TryParseString(string text, out string value);
    public static partial bool TryParseNumbers(string text, out IReadOnlyList<int> value);
    public static partial bool TryParseOptional(string text, out int value);
    public static partial bool TryParseZeroOrOne(string text, out IReadOnlyList<char> value);
    public static partial bool TryParseZeroOrOneDiscarded(string text, out char value);
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
    public static partial bool TryParseKeyword(string text, out string value);
    public static partial bool TryParseKeywordFallback(string text, out string value);
    public static partial bool TryParseKeywordCapture(string text, out string value);
    public static partial bool TryParseKeywordCustomWhitespace(string text, out string value);
    public static partial bool TryParseTextChoice(string text, out string value);
    public static partial bool TryParseTextChoiceFallback(string text, out string value);
    public static partial bool TryParseTextChoiceCapture(string text, out string value);
    public static partial bool TryParseTextChoiceCustomWhitespace(string text, out string value);
    public static partial bool TryParseTextChoiceDiscarded(string text, out char value);
    public static partial bool TryParseTextChoicePosition(string text, out (int Offset, int Line, int Column) value);

    // TextReader entry points parse with a compacting buffer.
    public static partial bool TryParseNumber(TextReader reader, out int value);
    public static partial bool TryParseSkippedValues(TextReader reader, out string value);
    public static partial bool TryParseAlternative(TextReader reader, out char value);
    public static partial bool TryParseString(TextReader reader, out string value);
    public static partial bool TryParseNumbers(TextReader reader, out IReadOnlyList<int> value);
    public static partial bool TryParseOptional(TextReader reader, out int value);
    public static partial bool TryParseRecursive(TextReader reader, out int value);
    public static partial bool TryParseCustomWhitespace(TextReader reader, out string value);
    public static partial bool TryParseConfiguredWhitespace(TextReader reader, GrammarOptions options, out string value);
    public static partial bool TryParseError(TextReader reader, out char value);
    public static partial bool TryParseCancelableNumber(TextReader reader, CancellationToken cancellationToken, out int value);
    public static partial bool TryParseCancelableSequence(TextReader reader, CancellationOptions options, CancellationToken cancellationToken, out int value);
    public static partial bool TryParseKeyword(TextReader reader, out string value);
    public static partial bool TryParseKeywordFallback(TextReader reader, out string value);
    public static partial bool TryParseKeywordCapture(TextReader reader, out string value);
    public static partial bool TryParseTextChoice(TextReader reader, out string value);
    public static partial bool TryParseTextChoiceFallback(TextReader reader, out string value);
    public static partial bool TryParseTextChoiceCapture(TextReader reader, out string value);
    public static partial bool TryParseTextChoicePosition(TextReader reader, out (int Offset, int Line, int Column) value);
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
