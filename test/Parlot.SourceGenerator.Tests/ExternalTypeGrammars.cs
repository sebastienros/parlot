namespace Parlot.SourceGenerator.Tests;

public static partial class ExternalTypeGrammars
{
    public static partial bool TryParseSimpleValue(string text, out SimpleValue value);
    public static partial bool TryParseSimpleNumber(string text, out SimpleNumber value);
}
