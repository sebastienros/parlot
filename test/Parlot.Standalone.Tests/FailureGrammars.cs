using System.IO;
using System.Threading;

namespace Parlot.Standalone.Tests;

public static partial class FailureGrammars
{
    public static partial bool Select(string text, out string value);
    public static partial bool Select(string text, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool Select(TextReader reader, out string value);
    public static partial bool Select(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Expression(string text, CancellationToken cancellationToken, out long value);
    public static partial bool Expression(string text, CancellationToken cancellationToken, out long value, out int failureOffset);
    public static partial bool Expression(string text, CancellationToken cancellationToken, out long value, out (int Offset, int Line, int Column) failure);
    public static partial bool Expression(TextReader reader, CancellationToken cancellationToken, out long value, out (int Offset, int Line, int Column) failure);

    public static partial bool Pair(string text, out (long, long) value, out int failureOffset);

    public static partial bool Error(string text, out long value, out (int Offset, int Line, int Column) failure);

    public static partial bool Span(string text, out string value, out int failureOffset);

    public static partial bool Configured(string text, long factor, out long value, out (int Offset, int Line, int Column) failure);
}
