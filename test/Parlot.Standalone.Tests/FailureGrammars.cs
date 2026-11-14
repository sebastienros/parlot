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
    public static partial bool ErrorNesting(string text, out long value, out (int Offset, bool NestedTooDeeply) failure);

    public static partial bool Span(string text, out string value, out int failureOffset);
    public static partial bool Span(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Configured(string text, long factor, out long value, out (int Offset, int Line, int Column) failure);

    public static partial bool Rejected(string text, out string value, out int failureOffset);
    public static partial bool Rejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedLiteral(string text, out string value, out int failureOffset);

    public static partial bool ConversionRejected(string text, out string value, out int failureOffset);
    public static partial bool ConversionRejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool ConversionRejectedLiteral(string text, out string value, out int failureOffset);

    public static partial bool ConversionSecond(string text, out string value, out int failureOffset);
    public static partial bool ConversionSecond(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool ConversionFurther(string text, out string value, out int failureOffset);
    public static partial bool ConversionFurther(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool ConversionRejectedAfterComments(string text, out string value, out int failureOffset);
    public static partial bool ConversionRejectedAfterComments(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool ConversionRejectedAlternative(string text, out string value, out int failureOffset);
    public static partial bool ConversionRejectedAlternative(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedPair(string text, out string value, out int failureOffset);
    public static partial bool RejectedPair(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedAfterComments(string text, out string value, out int failureOffset);
    public static partial bool RejectedAfterComments(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedWhiteSpace(string text, out string value, out int failureOffset);
    public static partial bool RejectedWhiteSpace(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Second(string text, out string value, out int failureOffset);

    public static partial bool NotFollowed(string text, out string value, out int failureOffset);
    public static partial bool NotFollowed(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Followed(string text, out string value, out int failureOffset);
    public static partial bool Followed(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Peeked(string text, out string value, out int failureOffset);
    public static partial bool Peeked(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool CapturedRejected(string text, out string value, out int failureOffset);
    public static partial bool CapturedRejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool CapturedConversionRejected(string text, out string value, out int failureOffset);
    public static partial bool CapturedConversionRejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool CapturedOffsetsRejected(string text, out string value, out int failureOffset);
    public static partial bool CapturedOffsetsRejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool PeekedCondition(string text, out string value, out int failureOffset);
    public static partial bool PeekedCondition(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool PeekedConversion(string text, out string value, out int failureOffset);
    public static partial bool PeekedConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool PeekedRejected(string text, out string value, out int failureOffset);
    public static partial bool PeekedRejected(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool PeekedFurther(string text, out string value, out int failureOffset);
    public static partial bool PeekedFurther(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool PeekedAlternatives(string text, out string value, out int failureOffset);
    public static partial bool PeekedAlternatives(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool Negative(string text, out string value, out int failureOffset);
    public static partial bool Negative(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedWords(string text, out string value, out int failureOffset);
    public static partial bool RejectedWords(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool ConversionRejectedWords(string text, out string value, out int failureOffset);
    public static partial bool ConversionRejectedWords(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedLongest(string text, out string value, out int failureOffset);
    public static partial bool RejectedLongest(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedLongestText(string text, out string value, out int failureOffset);
    public static partial bool RejectedLongestText(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedLongestWord(string text, out string value, out int failureOffset);
    public static partial bool RejectedLongestWord(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool SpanBacktracking(string text, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool SpanBacktracking(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedTextBefore(string text, out string value, out int failureOffset);
    public static partial bool RejectedTextBefore(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedTextBeforeTerm(string text, out string value, out int failureOffset);
    public static partial bool RejectedTextBeforeTerm(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool TextBeforeConsumed(string text, out string value, out int failureOffset);
    public static partial bool TextBeforeConsumed(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool TextBeforeThenOther(string text, out string value, out int failureOffset);
    public static partial bool TextBeforeThenOther(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool TextBeforeRequired(string text, out string value, out int failureOffset);
    public static partial bool TextBeforeRequired(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedTextBeforeChoice(string text, out string value, out int failureOffset);
    public static partial bool RejectedTextBeforeChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedTextBeforeConsumed(string text, out string value, out int failureOffset);
    public static partial bool RejectedTextBeforeConsumed(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedValuedOperators(string text, out long value, out int failureOffset);
    public static partial bool RejectedValuedOperators(TextReader reader, out long value, out (int Offset, int Line, int Column) failure);

    public static partial bool SpanChoice(string text, out string value, out int failureOffset);
    public static partial bool SpanChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedSpanChoice(string text, out string value, out int failureOffset);
    public static partial bool RejectedSpanChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool SpanOptional(string text, out string value, out int failureOffset);
    public static partial bool SpanOptional(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool SpanLoop(string text, out string value, out int failureOffset);
    public static partial bool SpanLoop(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool SpanLongest(string text, out string value, out int failureOffset);
    public static partial bool SpanLongest(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedSpanLongest(string text, out string value, out int failureOffset);
    public static partial bool RejectedSpanLongest(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedSpan(string text, out string value, out int failureOffset);
    public static partial bool RejectedSpan(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    public static partial bool RejectedSpanWhiteSpace(string text, out string value, out int failureOffset);
    public static partial bool RejectedSpanWhiteSpace(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}
