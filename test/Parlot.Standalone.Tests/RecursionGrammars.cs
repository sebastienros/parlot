using System.Collections.Generic;
using System.IO;

namespace Parlot.Standalone.Tests;

public static partial class RecursionGrammars
{
    public static partial bool List(string text, out int value);
    public static partial bool List(TextReader text, out int value);
    public static partial bool List(string text, out int value, out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
    public static partial bool LimitedList(string text, out int value);
    public static partial bool LimitedList(string text, out int value, out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> diagnostics);
    public static partial bool Negation(string text, out int value);
    public static partial bool LimitedNegation(string text, out int value);
    public static partial bool Prefix(string text, out int value);
    public static partial bool LimitedPrefix(string text, out int value);
}
