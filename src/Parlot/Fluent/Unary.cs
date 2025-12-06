using FastExpressionCompiler;
using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a unary operation structure.
/// Handles prefix operators that can be applied recursively.
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class Unary<T, TInput> : Parser<T>, ICompilable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<T, T> Factory)[] _operators;

    public Unary(Parser<T> parser, (Parser<TInput> op, Func<T, T> factory)[] operators)
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

        // Try each unary operator
        foreach (var (op, factory) in _operators)
        {
            var operatorResult = new ParseResult<TInput>();
            if (op.Parse(context, ref operatorResult))
            {
                // Recursively parse the operand (which may have more unary operators)
                if (Parse(context, ref result))
                {
                    result = new ParseResult<T>(result.Start, result.End, factory(result.Value));
                    context.ExitParser(this);
                    return true;
                }
                else
                {
                    // Operator matched but no operand - fail
                    context.ExitParser(this);
                    return false;
                }
            }
        }

        // No operator matched, try the base parser
        var success = _parser.Parse(context, ref result);

        context.ExitParser(this);
        return success;
    }

    private bool _initialized;
    private readonly Closure _closure = new();

    private sealed class Closure
    {
        public object? Func;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // Create the body of this parser only once (similar to Deferred<T>)
        if (!_initialized)
        {
            _initialized = true;

            // Build the inner body that will be compiled into a function
            var innerContext = context;
            var innerResult = innerContext.CreateCompilationResult<T>();

            var nextNum = innerContext.NextNumber;
            var matchedFactory = innerResult.DeclareVariable<Func<T, T>>($"unaryFactory{nextNum}");
            var operatorMatched = innerResult.DeclareVariable<bool>($"unaryOpMatched{nextNum}");

            // Compile the base parser
            var baseParserResult = _parser.Build(innerContext);

            // Build operator checks
            var operatorCheckExpressions = new List<Expression>();
            var allOperatorVariables = new List<ParameterExpression>();

            for (int i = 0; i < _operators.Length; i++)
            {
                var (op, factory) = _operators[i];
                var opCompileResult = op.Build(innerContext);

                foreach (var variable in opCompileResult.Variables)
                {
                    allOperatorVariables.Add(variable);
                }

                var factoryConst = Expression.Constant(factory);

                if (i == 0)
                {
                    // First operator - always try it
                    operatorCheckExpressions.AddRange(opCompileResult.Body);
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            opCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(operatorMatched, Expression.Constant(true)),
                                Expression.Assign(matchedFactory, factoryConst)
                            )
                        )
                    );
                }
                else
                {
                    // Subsequent operators - only try if no operator matched yet
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            Expression.Not(operatorMatched),
                            Expression.Block(
                                opCompileResult.Body.Concat([
                                    Expression.IfThen(
                                        opCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(operatorMatched, Expression.Constant(true)),
                                            Expression.Assign(matchedFactory, factoryConst)
                                        )
                                    )
                                ])
                            )
                        )
                    );
                }
            }

            // Build the recursive call - we'll use the closure to call ourselves
            var closureConst = Expression.Constant(_closure);
            var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
            var funcReturnType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
            var funcsAccess = Expression.MakeMemberAccess(closureConst, getFuncs);
            var castFunc = Expression.Convert(funcsAccess, funcReturnType);
            
            var recursiveResult = Expression.Variable(typeof(ValueTuple<bool, T>), $"recursiveUnary{nextNum}");
            innerResult.Variables.Add(recursiveResult);

            // Build the full expression
            var innerBody = Expression.Block(
                allOperatorVariables.Concat(baseParserResult.Variables),
                new Expression[] {
                    // Reset operator matched
                    Expression.Assign(operatorMatched, Expression.Constant(false)),
                    Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<T, T>)))
                }
                .Concat(operatorCheckExpressions)
                .Concat([
                    Expression.IfThenElse(
                        operatorMatched,
                        // Operator matched - try recursive parse
                        Expression.Block(
                            Expression.Assign(recursiveResult, Expression.Invoke(castFunc, innerContext.ParseContext)),
                            Expression.IfThen(
                                Expression.Field(recursiveResult, "Item1"),
                                Expression.Block(
                                    Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                    Expression.Assign(innerResult.Value, Expression.Invoke(matchedFactory, Expression.Field(recursiveResult, "Item2")))
                                )
                            )
                        ),
                        // No operator - try base parser
                        Expression.Block(
                            baseParserResult.Body.Concat([
                                Expression.IfThen(
                                    baseParserResult.Success,
                                    Expression.Block(
                                        Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                        Expression.Assign(innerResult.Value, baseParserResult.Value)
                                    )
                                )
                            ])
                        )
                    )
                ])
            );

            // Create the lambda for the inner body
            var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryResult{context.NextNumber}");
            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

            var lambda =
                Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        type: typeof(ValueTuple<bool, T>),
                        variables: innerResult.Variables.Append(resultExpression),
                        innerBody,
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                            innerResult.Success,
                            context.DiscardResult ? Expression.Default(innerResult.Value.Type) : innerResult.Value)),
                        returnExpression,
                        returnLabel),
                    true,
                    context.ParseContext
                );

#if DEBUG
            context.Lambdas.Add(lambda);
#endif

            _closure.Func = lambda.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);
        }

        // Call the compiled function
        var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryDef{context.NextNumber}");
        result.Variables.Add(deferred);

        var closureScope = Expression.Constant(_closure);
        var getFunc = typeof(Closure).GetMember(nameof(Closure.Func))[0];
        var funcType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
        var funcAccess = Expression.MakeMemberAccess(closureScope, getFunc);
        var cast = Expression.Convert(funcAccess, funcType);
        
        result.Body.Add(Expression.Assign(deferred, Expression.Invoke(cast, context.ParseContext)));
        result.Body.Add(Expression.Assign(result.Success, Expression.Field(deferred, "Item1")));
        result.Body.Add(
            context.DiscardResult
                ? Expression.Empty()
                : Expression.Assign(result.Value, Expression.Field(deferred, "Item2"))
        );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("Unary requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var operatorMatchedName = $"unaryOpMatched{context.NextNumber()}";
        
        // Register this unary parser as a deferred method for recursive calls
        var helperMethodName = context.Deferred.GetOrCreateMethodName(this, "Unary");

        result.Body.Add($"bool {operatorMatchedName} = false;");

        // Generate operator matching for each operator
        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException($"Unary requires all operator parsers to be source-generatable.");
            }

            // Register the factory lambda
            var factoryFieldName = context.RegisterLambda(factory);

            var opResult = opSourceable.GenerateSource(context);

            var indent = "";
            if (i == 0)
            {
                foreach (var local in opResult.Locals)
                {
                    result.Body.Add($"{indent}{local}");
                }

                foreach (var stmt in opResult.Body)
                {
                    result.Body.Add($"{indent}{stmt}");
                }
                result.Body.Add($"{indent}if ({opResult.SuccessVariable})");
            }
            else
            {
                result.Body.Add($"{indent}if (!{operatorMatchedName})");
                result.Body.Add($"{indent}{{");
                foreach (var local in opResult.Locals)
                {
                    result.Body.Add($"{indent}    {local}");
                }

                foreach (var stmt in opResult.Body)
                {
                    result.Body.Add($"{indent}    {stmt}");
                }
                result.Body.Add($"{indent}    if ({opResult.SuccessVariable})");
            }

            var innerIndent = i == 0 ? indent : $"{indent}    ";
            result.Body.Add($"{innerIndent}{{");
            result.Body.Add($"{innerIndent}    {operatorMatchedName} = true;");
            
            // Recursive call via helper method (returns ValueTuple<bool, T>)
            var recursiveResultName = $"recursiveResult{context.NextNumber()}";
            result.Body.Add($"{innerIndent}    global::System.ValueTuple<bool, {valueTypeName}> {recursiveResultName} = default;");
            result.Body.Add($"{innerIndent}    {recursiveResultName} = {helperMethodName}({ctx});");
            result.Body.Add($"{innerIndent}    if ({recursiveResultName}.Item1)");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {result.SuccessVariable} = true;");
            result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}.Invoke({recursiveResultName}.Item2);");
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}}}");

            if (i > 0)
            {
                result.Body.Add($"{indent}}}");
            }
        }

        // If no operator matched, try base parser
        result.Body.Add($"if (!{operatorMatchedName})");
        result.Body.Add("{");

        var baseResult = parserSourceable.GenerateSource(context);
        foreach (var local in baseResult.Locals)
        {
            result.Body.Add($"    {local}");
        }
        foreach (var stmt in baseResult.Body)
        {
            result.Body.Add($"    {stmt}");
        }
        result.Body.Add($"    if ({baseResult.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = {baseResult.ValueVariable};");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}
