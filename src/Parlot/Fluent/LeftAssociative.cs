using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a left-associative structure from a base parser and a list of operators.
/// c.f. https://en.wikipedia.org/wiki/Operator_associativity
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class LeftAssociative<T, TInput> : Parser<T>, ICompilable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<T, T, T> Factory)[] _operators;

    public LeftAssociative(Parser<T> parser, (Parser<TInput> op, Func<T, T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // Parse the first operand (e.g., multiplicative)
        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return false;
        }

        var value = result.Value;

        // Parse zero or more (operator operand) pairs
        while (true)
        {
            var operatorResult = new ParseResult<TInput>();
            Func<T, T, T>? matchedFactory = null;

            // Try each operator
            foreach (var (op, factory) in _operators)
            {
                if (op.Parse(context, ref operatorResult))
                {
                    matchedFactory = factory;
                    break;
                }
            }

            if (matchedFactory == null)
            {
                // No operator matched, we're done
                break;
            }

            // Parse the right operand
            var rightResult = new ParseResult<T>();
            if (!_parser.Parse(context, ref rightResult))
            {
                // Operator matched but no right operand - this is an error
                // For now we just stop parsing here
                break;
            }

            // Apply the operator
            value = matchedFactory(value, rightResult.Value);
        }

        result = new ParseResult<T>(result.Start, result.End, value);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // Create variables for the loop
        var nextNum = context.NextNumber;
        var currentValue = result.DeclareVariable<T>($"leftAssocValue{nextNum}");
        var matchedFactory = result.DeclareVariable<Func<T, T, T>>($"matchedFactory{nextNum}");

        var breakLabel = Expression.Label($"leftAssocBreak{nextNum}");

        // Compile the base parser for the first operand
        var firstParserResult = _parser.Build(context);

        // Compile the base parser for the right operand in the loop
        var rightParserResult = _parser.Build(context);

        // Build operator matching expressions - each operator sets matchedFactory if it matches
        var operatorChecks = new List<Expression>();
        var allOperatorVariables = new List<ParameterExpression>();

        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];
            var opCompileResult = op.Build(context);

            foreach (var variable in opCompileResult.Variables)
            {
                allOperatorVariables.Add(variable);
            }

            var factoryConst = Expression.Constant(factory);

            // Reset success if not already matched (for subsequent operators)
            var checkBlock = new List<Expression>();
            
            // Only try this operator if we haven't matched yet
            if (i > 0)
            {
                checkBlock.Add(
                    Expression.IfThen(
                        Expression.Equal(matchedFactory, Expression.Constant(null, typeof(Func<T, T, T>))),
                        Expression.Block(
                            opCompileResult.Body.Concat([
                                Expression.IfThen(
                                    opCompileResult.Success,
                                    Expression.Assign(matchedFactory, factoryConst)
                                )
                            ])
                        )
                    )
                );
            }
            else
            {
                // First operator - always try it
                checkBlock.AddRange(opCompileResult.Body);
                checkBlock.Add(
                    Expression.IfThen(
                        opCompileResult.Success,
                        Expression.Assign(matchedFactory, factoryConst)
                    )
                );
            }

            operatorChecks.AddRange(checkBlock);
        }

        // Build the loop body with its own variable scope
        var loopBody = Expression.Block(
            // Include operator variables and right parser variables in the loop scope
            allOperatorVariables.Concat(rightParserResult.Variables),
            new Expression[] {
                // Reset matchedFactory
                Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<T, T, T>)))
            }
            .Concat(operatorChecks)
            .Concat([
                // If no operator matched, break
                Expression.IfThen(
                    Expression.Equal(matchedFactory, Expression.Constant(null, typeof(Func<T, T, T>))),
                    Expression.Break(breakLabel)
                )
            ])
            .Concat(rightParserResult.Body)
            .Concat([
                // If right operand failed, break
                Expression.IfThen(
                    Expression.Not(rightParserResult.Success),
                    Expression.Break(breakLabel)
                ),

                // Apply operator: currentValue = matchedFactory(currentValue, rightValue)
                Expression.Assign(currentValue,
                    Expression.Invoke(matchedFactory, currentValue, rightParserResult.Value))
            ])
        );

        var loopExpr = Expression.Loop(loopBody, breakLabel);

        // Build the full expression
        result.Body.Add(
            Expression.Block(
                firstParserResult.Variables,
                new Expression[] { Expression.Block(firstParserResult.Body) }.Concat([
                    Expression.IfThenElse(
                        firstParserResult.Success,
                        Expression.Block(
                            // Store first value
                            Expression.Assign(currentValue, firstParserResult.Value),

                            // Loop for additional operands
                            loopExpr,

                            // Success
                            Expression.Assign(result.Success, Expression.Constant(true)),
                            Expression.Assign(result.Value, currentValue)
                        ),
                        Expression.Assign(result.Success, Expression.Constant(false))
                    )
                ])
            )
        );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("LeftAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var inputTypeName = SourceGenerationContext.GetTypeName(typeof(TInput));

        // Generate a unique ID for this LeftAssociative instance to avoid collisions
        var uniqueId = context.NextNumber();
        
        var operatorMatchedName = $"opMatched{context.NextNumber()}";

        result.Body.Add($"bool {operatorMatchedName} = false;");

        // Helper function to get parser value type
        static Type GetParserValueType(object parser)
        {
            var type = parser.GetType();
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Parlot.Fluent.Parser`1")
                {
                    return type.GetGenericArguments()[0];
                }
                type = type.BaseType!;
            }
            throw new InvalidOperationException("Unable to determine parser value type.");
        }

        // Register helper for the base parser with unique prefix
        var baseHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_LeftAssoc{uniqueId}", valueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        // Generate first operand parsing using helper - use output parameter directly
        if (context.DiscardResult)
        {
            result.Body.Add($"if ({baseHelperName}({ctx}, out _))");
        }
        else
        {
            result.Body.Add($"if ({baseHelperName}({ctx}, out {result.ValueVariable}))");
        }
        result.Body.Add("{");
        result.Body.Add("    while (true)");
        result.Body.Add("    {");
        result.Body.Add($"        {operatorMatchedName} = false;");

        // Generate operator matching for each operator
        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException($"LeftAssociative requires all operator parsers to be source-generatable.");
            }

            // Register the factory lambda
            var factoryFieldName = context.RegisterLambda(factory);

            // Register helper for the operator parser with unique prefix
            var opValueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(opSourceable));
            var opHelperName = context.Helpers
                .GetOrCreate(opSourceable, $"{context.MethodNamePrefix}_LeftAssoc{uniqueId}", opValueTypeName, () => opSourceable.GenerateSource(context))
                .MethodName;

            var opResultName = $"opResult{context.NextNumber()}";

            var indent = "        ";
            if (i == 0)
            {
                result.Body.Add($"{indent}if ({opHelperName}({ctx}, out _))");
            }
            else
            {
                result.Body.Add($"{indent}if (!{operatorMatchedName})");
                result.Body.Add($"{indent}{{");
                result.Body.Add($"{indent}    if ({opHelperName}({ctx}, out _))");
            }

            var innerIndent = i == 0 ? indent : $"{indent}    ";
            result.Body.Add($"{innerIndent}{{");
            result.Body.Add($"{innerIndent}    {operatorMatchedName} = true;");

            // Parse right operand using helper
            result.Body.Add($"{innerIndent}    if ({baseHelperName}({ctx}, out var {opResultName}RightValue))");
            result.Body.Add($"{innerIndent}    {{");
            if (!context.DiscardResult)
            {
                result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}({result.ValueVariable}, {opResultName}RightValue);");
            }
            else
            {
                result.Body.Add($"{innerIndent}        {factoryFieldName}({result.ValueVariable}, {opResultName}RightValue);");
            }
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}}}");

            if (i > 0)
            {
                result.Body.Add($"{indent}}}");
            }
        }

        result.Body.Add($"        if (!{operatorMatchedName}) break;");
        result.Body.Add("    }");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"LeftAssociative({_parser})";
}
