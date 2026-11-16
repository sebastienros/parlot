using System.IO;

namespace Parlot.Standalone.Tests;

public static partial class LineGrammars
{
    public static partial bool Locate(string text, out string value);
    public static partial bool Locate(string text, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool Locate(TextReader reader, out string value);
    public static partial bool Locate(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool LocateDefault(string text, out string value, out (int Offset, int Line, int Column) failure);
}
