using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Parlot.SourceGeneration;

namespace Parlot.SourceGenerator;

/// <summary>
/// Rewrites lambda expressions and method groups in a syntax tree to wrapped versions
/// that register their pointer ID when invoked. This allows the source generator to match
/// runtime-registered lambdas back to their original source code.
/// 
/// Deferred parser callbacks register their pointer without executing the user body.
/// Other lambdas still execute normally, since they may participate in building the graph.
/// 
/// Example transformation:
///   Original: x => x.ToLower()
///   Rewritten: x => { Parlot.SourceGeneration.LambdaPointer.CurrentPointer = 0; return default(string); }
/// 
/// <para>
/// Factory parameter captures are bound to generated instance fields. Other captures are reported
/// only when the callback is actually used by the generated graph.
/// </para>
/// </summary>
internal sealed class LambdaRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly IMethodSymbol _factory;
    private readonly Dictionary<int, LambdaInfo> _lambdas = new();
    private int _nextPointer;

    public LambdaRewriter(SemanticModel semanticModel, IMethodSymbol factory)
    {
        _semanticModel = semanticModel;
        _factory = factory;
    }

    /// <summary>
    /// Gets all recorded lambdas with their pointers and source code.
    /// </summary>
    public IReadOnlyDictionary<int, LambdaInfo> Lambdas => _lambdas;

    /// <summary>
    /// Information about a captured variable in a lambda (closure).
    /// </summary>
    public sealed record CapturedVariableInfo(
        string VariableName,
        string LambdaSource,
        Location Location);

    /// <summary>
    /// Information about a rewritten lambda.
    /// </summary>
    public sealed record LambdaInfo(
        string OriginalSource,
        bool IsMethodGroup,
        string? InferredReturnType,
        int ParameterCount,
        IReadOnlyList<string> ParameterTypes,
        string? FilePath,
        int StartLine,
        int StartColumn,
        IReadOnlyList<CapturedVariableInfo> CapturedVariables);

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        return RewriteLambda(node, new[] { node.Parameter }, node.ExpressionBody, node.Block);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        return RewriteLambda(node, node.ParameterList.Parameters.ToArray(), node.ExpressionBody, node.Block);
    }

    private ParenthesizedLambdaExpressionSyntax RewriteLambda(
        LambdaExpressionSyntax originalLambda,
        ParameterSyntax[] parameters,
        ExpressionSyntax? expressionBody,
        BlockSyntax? blockBody)
    {
        var pointer = _nextPointer++;
        var originalSource = new ParameterBindingRewriter(_semanticModel, _factory)
            .Visit(originalLambda)!.ToFullString().Trim();
        var capturedVariables = DetectCapturedVariables(originalLambda, originalSource);

        // Determine return type and parameter types from semantic model
        var paramTypes = new List<string>();
        string? returnType = null;
        IMethodSymbol? delegateInvokeMethod = null;

        var typeInfo = _semanticModel.GetTypeInfo(originalLambda);
        if (typeInfo.ConvertedType is INamedTypeSymbol namedType && namedType.DelegateInvokeMethod is { } invokeMethod)
        {
            delegateInvokeMethod = invokeMethod;
            returnType = invokeMethod.ReturnType.Name.ToLowerInvariant();
            foreach (var param in invokeMethod.Parameters)
            {
                paramTypes.Add(param.Type.ToDisplayString());
            }
        }

        // Get location information for debugging support
        var location = originalLambda.GetLocation();
        var lineSpan = location.GetLineSpan();
        var filePath = lineSpan.Path;
        var startLine = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
        var startColumn = lineSpan.StartLinePosition.Character + 1; // Convert to 1-based

        _lambdas[pointer] = new LambdaInfo(
            originalSource,
            IsMethodGroup: false,
            returnType,
            parameters.Length,
            paramTypes,
            filePath,
            startLine,
            startColumn,
            capturedVariables);

        // Create the pointer registration statement
        // global::Parlot.SourceGeneration.LambdaPointer.CurrentPointer = {pointer};
        var registrationStatement = CreatePointerRegistrationStatement(pointer);

        BlockSyntax newBody;
        if (delegateInvokeMethod is not null)
        {
            var hasCaptures = _semanticModel.AnalyzeDataFlow(originalLambda)?.CapturedInside.Any(symbol =>
                !symbol.Locations.Any(location =>
                    location.SourceTree == originalLambda.SyntaxTree && originalLambda.Span.Contains(location.SourceSpan))) == true;
            var executionBody = blockBody is not null
                ? (BlockSyntax)Visit(blockBody)!
                : SyntaxFactory.Block(delegateInvokeMethod.ReturnsVoid
                    ? SyntaxFactory.ExpressionStatement((ExpressionSyntax)Visit(expressionBody)!)
                    : SyntaxFactory.ReturnStatement((ExpressionSyntax)Visit(expressionBody)!));
            if (hasCaptures && FactoryParameterUsage.IsDeferredCallback(originalLambda, _semanticModel))
            {
                executionBody = SyntaxFactory.Block(
                    SyntaxFactory.ThrowStatement(SyntaxFactory.ParseExpression(
                        "global::Parlot.SourceGeneration.LambdaPointer.CreateEagerCaptureException()")));
            }
            newBody = CreateDeferredBody(registrationStatement, delegateInvokeMethod, executionBody);
        }
        else if (blockBody != null)
        {
            // Already a block body - prepend the registration
            var visitedBody = (BlockSyntax)Visit(blockBody)!;
            newBody = visitedBody.WithStatements(
                visitedBody.Statements.Insert(0, registrationStatement));
        }
        else if (expressionBody != null)
        {
            // Expression body - convert to block with registration + return
            newBody = SyntaxFactory.Block(
                registrationStatement,
                SyntaxFactory.ReturnStatement((ExpressionSyntax)Visit(expressionBody)!));
        }
        else
        {
            // Shouldn't happen, but handle gracefully
            newBody = SyntaxFactory.Block(registrationStatement);
        }

        // Build parameter list - keep original parameters
        var parameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(parameters));

        // Create the new lambda with the same modifiers (static, etc.)
        var newLambda = SyntaxFactory.ParenthesizedLambdaExpression(parameterList, newBody)
            .WithModifiers(originalLambda.Modifiers);

        return newLambda;
    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        // Check if the argument is a method group (identifier or member access without invocation)
        if (node.Expression is IdentifierNameSyntax or MemberAccessExpressionSyntax)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
            
            // Check if it resolves to a method (method group)
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol ||
                (symbolInfo.CandidateSymbols.Length > 0 && symbolInfo.CandidateSymbols[0] is IMethodSymbol))
            {
                var method = (IMethodSymbol)(symbolInfo.Symbol ?? symbolInfo.CandidateSymbols[0]);
                
                var pointer = _nextPointer++;
                var originalSource = new ParameterBindingRewriter(_semanticModel, _factory)
                    .Visit(node.Expression)!.ToFullString().Trim();

                var paramTypes = method.Parameters.Select(p => p.Type.ToDisplayString()).ToList();
                var returnType = method.ReturnType.Name.ToLowerInvariant();

                // Get location information for debugging support
                var location = node.Expression.GetLocation();
                var lineSpan = location.GetLineSpan();
                var filePath = lineSpan.Path;
                var startLine = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
                var startColumn = lineSpan.StartLinePosition.Character + 1; // Convert to 1-based

                _lambdas[pointer] = new LambdaInfo(
                    originalSource,
                    IsMethodGroup: true,
                    returnType,
                    method.Parameters.Length,
                    paramTypes,
                    filePath,
                    startLine,
                    startColumn,
                    Array.Empty<CapturedVariableInfo>());

                // Replace method group with a lambda that sets the pointer and calls the method
                // e.g., char.IsLetter becomes (char arg0) => { LambdaPointer.CurrentPointer = N; return char.IsLetter(arg0); }
                
                var parameters = method.Parameters.Select((p, i) =>
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier($"arg{i}"))
                        .WithType(SyntaxFactory.ParseTypeName(p.Type.ToDisplayString() + " ")))
                    .ToArray();

                var parameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters));

                // Build registration statement
                var registrationStatement = CreatePointerRegistrationStatement(pointer);

                // Build the method invocation
                var arguments = parameters.Select((_, i) =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName($"arg{i}")));
                
                var methodCall = SyntaxFactory.InvocationExpression(
                    node.Expression,
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

                var body = CreateDeferredBody(registrationStatement, method, SyntaxFactory.Block(
                    method.ReturnsVoid ? SyntaxFactory.ExpressionStatement(methodCall) : SyntaxFactory.ReturnStatement(methodCall)));

                var stubLambda = SyntaxFactory.ParenthesizedLambdaExpression(parameterList, body);

                return node.WithExpression(stubLambda);
            }
        }

        return base.VisitArgument(node);
    }

    /// <summary>
    /// Detects captured variables (closures) in a lambda expression.
    /// Captured variables are local variables or parameters from the enclosing scope
    /// that are referenced inside the lambda but are not lambda parameters themselves.
    /// </summary>
    private List<CapturedVariableInfo> DetectCapturedVariables(
        LambdaExpressionSyntax lambda,
        string lambdaSource)
    {
        var captures = new List<CapturedVariableInfo>();
        var capturedSymbols = _semanticModel.AnalyzeDataFlow(lambda)?.CapturedInside;
        foreach (var identifier in lambda.Body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var name = identifier.Identifier.Text;
            // Get symbol info to determine what this identifier refers to
            var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
            var symbol = symbolInfo.Symbol;

            if (symbol is null || capturedSymbols?.Contains(symbol, SymbolEqualityComparer.Default) != true)
            {
                continue;
            }

            // Check if this is a captured variable (local variable or parameter from enclosing scope)
            if (symbol is ILocalSymbol localSymbol)
            {
                // This is a local variable - check if it's from outside the lambda
                if (!IsDefinedWithinLambda(localSymbol, lambda))
                {
                    captures.Add(new CapturedVariableInfo(
                        name,
                        lambdaSource,
                        identifier.GetLocation()));
                }
            }
            else if (symbol is IParameterSymbol paramSymbol)
            {
                if (!SymbolEqualityComparer.Default.Equals(paramSymbol.ContainingSymbol, _factory)
                    && !paramSymbol.Locations.Any(location =>
                        location.SourceTree == lambda.SyntaxTree && lambda.Span.Contains(location.SourceSpan)))
                {
                    captures.Add(new CapturedVariableInfo(
                        name,
                        lambdaSource,
                        identifier.GetLocation()));
                }
            }

        }

        return captures;
    }

    /// <summary>
    /// Checks if a local variable is defined within the lambda expression.
    /// </summary>
    private static bool IsDefinedWithinLambda(ILocalSymbol local, LambdaExpressionSyntax lambda)
    {
        foreach (var location in local.Locations)
        {
            if (location.SourceTree == lambda.SyntaxTree)
            {
                var span = location.SourceSpan;
                if (lambda.Span.Contains(span))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static BlockSyntax CreateStubBody(StatementSyntax registration, IMethodSymbol method)
    {
        return method.ReturnsVoid
            ? SyntaxFactory.Block(registration)
            : SyntaxFactory.Block(
                registration,
                SyntaxFactory.ReturnStatement(SyntaxFactory.PostfixUnaryExpression(
                    SyntaxKind.SuppressNullableWarningExpression,
                    SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(
                        method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))));
    }

    private static BlockSyntax CreateDeferredBody(StatementSyntax registration, IMethodSymbol method, BlockSyntax executionBody)
    {
        var registrationBody = CreateStubBody(registration, method);
        if (method.ReturnsVoid)
        {
            registrationBody = registrationBody.AddStatements(SyntaxFactory.ReturnStatement());
        }

        return executionBody.WithStatements(executionBody.Statements.Insert(0, SyntaxFactory.IfStatement(
            SyntaxFactory.ParseExpression("global::Parlot.SourceGeneration.LambdaPointer.IsRegistering"),
            registrationBody)));
    }

    private sealed class ParameterBindingRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly IMethodSymbol _factory;

        public ParameterBindingRewriter(SemanticModel semanticModel, IMethodSymbol factory)
        {
            _semanticModel = semanticModel;
            _factory = factory;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_semanticModel.GetSymbolInfo(node).Symbol is IParameterSymbol parameter
                && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, _factory))
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(FactoryParameterUsage.GetFieldName(parameter)))
                    .WithTriviaFrom(node);
            }

            return QualifyMember(node) ?? base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
            => QualifyMember(node) ?? base.VisitGenericName(node);

        private MemberAccessExpressionSyntax? QualifyMember(SimpleNameSyntax node)
        {
            if (node.Parent is MemberAccessExpressionSyntax member && member.Name == node
                || node.Parent is MemberBindingExpressionSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
                || node.Parent is NameColonSyntax or NameEqualsSyntax)
            {
                return null;
            }

            var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol is IMethodSymbol { IsStatic: true, MethodKind: MethodKind.Ordinary }
                or IFieldSymbol { IsStatic: true }
                or IPropertySymbol { IsStatic: true }
                or IEventSymbol { IsStatic: true })
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseName(symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    node.WithoutTrivia()).WithTriviaFrom(node);
            }

            return null;
        }

        public override SyntaxNode? VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            var rewritten = (AnonymousObjectMemberDeclaratorSyntax)base.VisitAnonymousObjectMemberDeclarator(node)!;
            if (node.NameEquals is null && IsFactoryParameter(node.Expression))
            {
                rewritten = rewritten.WithNameEquals(SyntaxFactory.NameEquals(
                    SyntaxFactory.IdentifierName(((IdentifierNameSyntax)node.Expression).Identifier)));
            }

            return rewritten;
        }

        public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
        {
            var rewritten = (TupleExpressionSyntax)base.VisitTupleExpression(node)!;
            for (var index = 0; index < node.Arguments.Count; index++)
            {
                var argument = node.Arguments[index];
                if (argument.NameColon is null && IsFactoryParameter(argument.Expression))
                {
                    rewritten = rewritten.WithArguments(rewritten.Arguments.Replace(rewritten.Arguments[index],
                        rewritten.Arguments[index].WithNameColon(SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName(((IdentifierNameSyntax)argument.Expression).Identifier)))));
                }
            }

            return rewritten;
        }

        private bool IsFactoryParameter(ExpressionSyntax expression)
            => expression is IdentifierNameSyntax
                && _semanticModel.GetSymbolInfo(expression).Symbol is IParameterSymbol parameter
                && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, _factory);

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                && _semanticModel.GetConstantValue(node) is { HasValue: true, Value: string name })
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(name))
                    .WithTriviaFrom(node);
            }

            return base.VisitInvocationExpression(node);
        }
    }

    /// <summary>
    /// Creates a statement that sets LambdaPointer.CurrentPointer to the given pointer value.
    /// </summary>
    private static ExpressionStatementSyntax CreatePointerRegistrationStatement(int pointer)
    {
        // global::Parlot.SourceGeneration.LambdaPointer.CurrentPointer = {pointer};
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.AliasQualifiedName(
                                SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                                SyntaxFactory.IdentifierName("Parlot")),
                            SyntaxFactory.IdentifierName("SourceGeneration")),
                        SyntaxFactory.IdentifierName("LambdaPointer")),
                    SyntaxFactory.IdentifierName("CurrentPointer")),
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(pointer))));
    }
}

/// <summary>
/// Result of rewriting a syntax tree with lambda stubs.
/// </summary>
internal sealed class LambdaRewriteResult
{
    public LambdaRewriteResult(
        SyntaxTree rewrittenTree,
        IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> lambdas)
    {
        RewrittenTree = rewrittenTree;
        Lambdas = lambdas;
    }

    /// <summary>
    /// The rewritten syntax tree with stub lambdas.
    /// </summary>
    public SyntaxTree RewrittenTree { get; }

    /// <summary>
    /// Map from lambda pointer to original lambda information.
    /// </summary>
    public IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> Lambdas { get; }

    /// <summary>
    /// Gets a dictionary mapping pointers to their original source code.
    /// </summary>
    public Dictionary<int, string> GetSourceCodeMap()
    {
        return Lambdas.ToDictionary(kv => kv.Key, kv => kv.Value.OriginalSource);
    }
}
