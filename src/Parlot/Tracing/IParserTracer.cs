namespace Parlot.Tracing;

/// <summary>
/// Interface for tracing parser execution.
/// </summary>
public interface IParserTracer
{
    /// <summary>
    /// Called when a parser is entered.
    /// </summary>
    /// <param name="parser">The parser being entered.</param>
    /// <param name="context">The parse context.</param>
    void EnterParser(object parser, Fluent.ParseContext context);

    /// <summary>
    /// Called when a parser exits.
    /// </summary>
    /// <param name="parser">The parser being exited.</param>
    /// <param name="context">The parse context.</param>
    /// <param name="success">Whether the parser succeeded.</param>
    void ExitParser(object parser, Fluent.ParseContext context, bool success);

    /// <summary>
    /// Called when a parser exits (legacy version that infers success from context).
    /// </summary>
    /// <param name="parser">The parser being exited.</param>
    /// <param name="context">The parse context.</param>
    void ExitParserLegacy(object parser, Fluent.ParseContext context);
}
