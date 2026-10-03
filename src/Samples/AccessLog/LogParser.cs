using Parlot.Fluent;
using System.Collections.Generic;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.AccessLog;

/// <summary>
/// Counts the failed requests of an access log without building a model, so parsing it measures the parsers
/// rather than the allocations of the results.
/// </summary>
/// <remarks>
/// A record is a line like <c>2026-10-02T12:34:56.789Z ERROR [http.server] GET /api/items/42 503 15.25ms "timeout"</c>.
/// It failed when its level is <c>ERROR</c> or its status is 500 or more.
/// </remarks>
public static class LogParser
{
    /// <summary>
    /// Parses a record, and returns 1 when it failed, 0 otherwise.
    /// </summary>
    public static readonly Parser<int> Record;

    /// <summary>
    /// Parses all the records of a log, and returns how many failed.
    /// </summary>
    public static readonly Parser<int> Log;

    static LogParser()
    {
        var timestamp = Terms.Pattern(static c => Character.IsDecimalDigit(c) || c is '-' or ':' or '.' or 'T' or 'Z');
        var level = OneOf(Terms.Text("DEBUG"), Terms.Text("INFO"), Terms.Text("WARN"), Terms.Text("ERROR"));
        var component = Between(Terms.Char('['), Literals.Pattern(static c => Character.IsIdentifierPart(c) || c == '.'), Literals.Char(']'));
        var method = Terms.Identifier();
        var path = Terms.NonWhiteSpace();
        var status = Terms.Integer();
        var duration = Terms.Decimal().AndSkip(Literals.Text("ms"));
        var message = Terms.String(StringLiteralQuotes.Double);

        Record = timestamp
            .SkipAnd(level)
            .AndSkip(component)
            .AndSkip(method)
            .AndSkip(path)
            .And(status)
            .AndSkip(duration)
            .AndSkip(message)
            .Then(static record => record.Item1 == "ERROR" || record.Item2 >= 500 ? 1 : 0);

        Log = ZeroOrMany(Record)
            .AndSkip(Terms.WhiteSpace().Optional())
            .Eof()
            .Then(Sum);
    }

    private static int Sum(IReadOnlyList<int> values)
    {
        var sum = 0;

        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum;
    }
}
