namespace Parlot.Explorer.Sample;

public static partial class Grammars
{
    private static partial bool Json(string text, out JsonValue value);
    private static partial bool Choice(string text, out char value);
    private static partial bool Assignment(string text, out Assignment value);
    private static partial bool Optional(string text, out char value);
    private static partial bool Recursive(string text, out char value);
    private static partial bool Throws(string text, out char value);
}
