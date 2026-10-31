namespace Parlot;

/// <summary>
/// The severity of a <see cref="ParseDiagnostic"/>.
/// </summary>
public enum ParseDiagnosticSeverity
{
    /// <summary>
    /// The input is invalid. The parser that reported it failed.
    /// </summary>
    Error,

    /// <summary>
    /// The input is accepted but questionable. The parser that reported it succeeded.
    /// </summary>
    Warning,
}

/// <summary>
/// A diagnostic reported by a grammar while parsing.
/// </summary>
public readonly struct ParseDiagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    public ParseDiagnostic(string message, TextPosition position, ParseDiagnosticSeverity severity)
    {
        Message = message;
        Position = position;
        Severity = severity;
    }

    /// <summary>
    /// The diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The location of the diagnostic in the input.
    /// </summary>
    public TextPosition Position { get; }

    /// <summary>
    /// Whether the diagnostic is an error or a warning.
    /// </summary>
    public ParseDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Whether the diagnostic is a warning.
    /// </summary>
    public bool IsWarning => Severity == ParseDiagnosticSeverity.Warning;

    /// <inheritdoc />
    public override string ToString() => $"{Severity} {Position}: {Message}";
}
