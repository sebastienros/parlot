using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Parlot.SourceGenerator;

internal static class FactoryParameterUsage
{
    internal sealed record InvalidUse(string ParameterName, Location Location);

    public static IEnumerable<InvalidUse> FindInvalidUses(
        IMethodSymbol factory,
        MethodDeclarationSyntax syntax,
        SemanticModel semanticModel)
    {
        foreach (var identifier in syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (semanticModel.GetSymbolInfo(identifier).Symbol is not IParameterSymbol parameter
                || !SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, factory))
            {
                continue;
            }

            if (identifier.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(invocation => semanticModel.GetOperation(invocation) is INameOfOperation))
            {
                continue;
            }

            var callback = identifier.Ancestors()
                .OfType<LambdaExpressionSyntax>()
                .FirstOrDefault(lambda => IsDeferredCallback(lambda, semanticModel));

            if (callback is null
                || IsWrittenOrPassedByReference(identifier, parameter, semanticModel))
            {
                yield return new InvalidUse(parameter.Name, identifier.GetLocation());
            }
        }
    }

    public static bool IsDeferredCallback(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        SyntaxNode node = expression;
        while (node.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            node = node.Parent;
        }

        if (node.Parent is not ArgumentSyntax argument
            || semanticModel.GetOperation(argument) is not IArgumentOperation { Parameter: { } parameter }
            || parameter.Type.TypeKind != TypeKind.Delegate
            || parameter.ContainingSymbol is not IMethodSymbol method
            || method.ContainingAssembly.Name != "Parlot"
            || method.ContainingNamespace.ToDisplayString() != "Parlot.Fluent")
        {
            return false;
        }

        // Only callbacks known to run during Parse may depend on runtime factory arguments.
        // In particular, Recursive's callback builds the graph and must still run at build time.
        return method.ContainingType.MetadataName switch
        {
            "Parsers" => method.Name is "If" or "Select",
            "Parser`1" => method.Name is "Then" or "ThenElse" or "When" or "Switch" or "Else",
            _ => false
        };
    }

    public static string GetFieldName(IParameterSymbol parameter) => $"_argument{parameter.Ordinal}";

    private static bool IsWrittenOrPassedByReference(
        IdentifierNameSyntax identifier,
        IParameterSymbol parameter,
        SemanticModel semanticModel)
    {
        var operation = semanticModel.GetOperation(identifier);
        if (operation is null)
        {
            return false;
        }

        // Reference-type members belong to the supplied object; value-type members belong to
        // the captured variable itself. Neither a captured variable nor its storage may escape.
        while (operation.Parent is IParenthesizedOperation or ITupleOperation
            || (parameter.Type.IsValueType && operation.Parent is IFieldReferenceOperation or IPropertyReferenceOperation))
        {
            operation = operation.Parent;
        }

        return operation.Parent switch
        {
            IAssignmentOperation assignment => ReferenceEquals(assignment.Target, operation),
            IIncrementOrDecrementOperation => true,
            IArgumentOperation argument => argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out or RefKind.In,
            _ => false
        };
    }
}
