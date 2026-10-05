using System.IO;

namespace Parlot.Standalone.Tests;

public static partial class DiagnosticsGrammars
{
    public static partial bool Choice(string text, out char value);
    public static partial bool Choice(TextReader text, out char value);
    public static partial bool Optional(string text, out char value);
    public static partial bool Throws(string text, out char value);
    public static partial bool Configured(string text, int factor, out long value);
    public static partial bool Packed(string text, out string value);
    public static partial bool Silent(string text, out char value);
}
