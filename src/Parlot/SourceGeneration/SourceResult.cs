using System.Collections.Generic;

namespace Parlot.SourceGeneration;

/// <summary>
/// Represents the source-generation shape of a parser: its locals, body statements,
/// and the names of the success and value variables it uses.
/// </summary>
public sealed class SourceResult
{
    public SourceResult(string successVariable, string valueVariable, string? valueTypeName)
    {
        SuccessVariable = successVariable;
        ValueVariable = valueVariable;
        ValueTypeName = valueTypeName;
    }

    /// <summary>
    /// Name of the boolean variable indicating parse success.
    /// </summary>
    public string SuccessVariable { get; }

    /// <summary>
    /// Name of the variable holding the parsed value.
    /// </summary>
    public string ValueVariable { get; }

    /// <summary>
    /// Optional CLR type name of the value variable.
    /// </summary>
    public string? ValueTypeName { get; }

    /// <summary>
    /// Local declarations (including initialization) that should appear in the generated method.
    /// </summary>
    public IList<string> Locals { get; } = new List<string>();

    /// <summary>
    /// Statements forming the body of the parser.
    /// </summary>
    public IList<string> Body { get; } = new List<string>();
}
