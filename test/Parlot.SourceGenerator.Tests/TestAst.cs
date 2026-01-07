namespace Parlot.SourceGenerator.Tests;

// Simple AST types defined in a separate file for testing [IncludeFiles] attribute
public sealed record SimpleValue(string Text);

public sealed record SimpleNumber(decimal Value);

public sealed record SimpleExpression(SimpleValue Left, string Operator, SimpleValue Right);
