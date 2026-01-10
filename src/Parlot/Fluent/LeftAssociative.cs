using Parlot.Compilation;
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
public sealed class LeftAssociative<T, TInput> : Parser<T>, ICompilable
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
        var end = result.End;

        // Parse zero or more (operator operand) pairs
        while (true)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
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
                // Operator matched but no right operand - rollback operator consumption.
                context.Scanner.Cursor.ResetPosition(operatorPosition);
                break;
            }

            // Apply the operator
            value = matchedFactory(value, rightResult.Value);
            end = rightResult.End;
        }

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var nextNum = context.NextNumber;
        var currentValue = result.DeclareVariable<T>($"leftAssocValue{nextNum}");
        var matchedFactory = result.DeclareVariable<Func<T, T, T>>($"matchedFactory{nextNum}");
        var operatorPosition = result.DeclareVariable<TextPosition>($"leftAssocPos{nextNum}");

        var breakLabel = Expression.Label($"leftAssocBreak{nextNum}");

        var firstParserResult = _parser.Build(context);
        var rightParserResult = _parser.Build(context);

        // Build operator matching expressions - each operator sets matchedFactory if it matches
        var operatorChecks = new List<Expression>();
        var allOperatorVariables = new List<ParameterExpression>();

        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];
            var opCompileResult = op.Build(context);

            allOperatorVariables.AddRange(opCompileResult.Variables);

            var factoryConst = Expression.Constant(factory);

            if (i > 0)
            {
                operatorChecks.Add(
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
                operatorChecks.AddRange(opCompileResult.Body);
                operatorChecks.Add(
                    Expression.IfThen(
                        opCompileResult.Success,
                        Expression.Assign(matchedFactory, factoryConst)
                    )
                );
            }
        }

        var scanner = Expression.Field(context.ParseContext, nameof(ParseContext.Scanner));
        var cursor = Expression.Field(scanner, nameof(Scanner.Cursor));
        var cursorPosition = Expression.Property(cursor, nameof(Cursor.Position));
        var resetPosition = typeof(Cursor).GetMethod(nameof(Cursor.ResetPosition), [typeof(TextPosition).MakeByRefType()])!;

        // Build the loop body with its own variable scope
        var loopBody = Expression.Block(
            allOperatorVariables.Concat(rightParserResult.Variables),
            new Expression[]
            {
                Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<T, T, T>))),
                Expression.Assign(operatorPosition, cursorPosition)
            }
            .Concat(operatorChecks)
            .Concat([
                Expression.IfThen(
                    Expression.Equal(matchedFactory, Expression.Constant(null, typeof(Func<T, T, T>))),
                    Expression.Break(breakLabel)
                )
            ])
            .Concat(rightParserResult.Body)
            .Concat([
                Expression.IfThen(
                    Expression.Not(rightParserResult.Success),
                    Expression.Block(
                        Expression.Call(cursor, resetPosition, operatorPosition),
                        Expression.Break(breakLabel)
                    )
                ),
                Expression.Assign(currentValue,
                    Expression.Invoke(matchedFactory, currentValue, rightParserResult.Value))
            ])
        );

        var loopExpr = Expression.Loop(loopBody, breakLabel);

        result.Body.Add(
            Expression.Block(
                firstParserResult.Variables,
                new Expression[] { Expression.Block(firstParserResult.Body) }.Concat([
                    Expression.IfThenElse(
                        firstParserResult.Success,
                        Expression.Block(
                            Expression.Assign(currentValue, firstParserResult.Value),
                            loopExpr,
                            Expression.Assign(result.Success, Expression.Constant(true)),
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, currentValue)
                        ),
                        Expression.Assign(result.Success, Expression.Constant(false))
                    )
                ])
            )
        );

        return result;
    }

    public override string ToString() => Name ?? $"LeftAssociative({_parser})";
}

public sealed class LeftAssociativeWithContext<T, TInput> : Parser<T>, ICompilable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<ParseContext, T, T, T> Factory)[] _operators;

    public LeftAssociativeWithContext(Parser<T> parser, (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] operators)
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

        if (!_parser.Parse(context, ref result))
        {
            context.ExitParser(this);
            return false;
        }

        var value = result.Value;
        var end = result.End;

        while (true)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            var operatorResult = new ParseResult<TInput>();
            Func<ParseContext, T, T, T>? matchedFactory = null;

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
                break;
            }

            var rightResult = new ParseResult<T>();
            if (!_parser.Parse(context, ref rightResult))
            {
                context.Scanner.Cursor.ResetPosition(operatorPosition);
                break;
            }

            value = matchedFactory(context, value, rightResult.Value);
            end = rightResult.End;
        }

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var nextNum = context.NextNumber;
        var currentValue = result.DeclareVariable<T>($"leftAssocCtxValue{nextNum}");
        var matchedFactory = result.DeclareVariable<Func<ParseContext, T, T, T>>($"matchedFactoryCtx{nextNum}");
        var operatorPosition = result.DeclareVariable<TextPosition>($"leftAssocCtxPos{nextNum}");

        var breakLabel = Expression.Label($"leftAssocCtxBreak{nextNum}");

        var firstParserResult = _parser.Build(context);
        var rightParserResult = _parser.Build(context);

        var operatorChecks = new List<Expression>();
        var allOperatorVariables = new List<ParameterExpression>();

        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];
            var opCompileResult = op.Build(context);

            allOperatorVariables.AddRange(opCompileResult.Variables);

            var factoryConst = Expression.Constant(factory);

            if (i > 0)
            {
                operatorChecks.Add(
                    Expression.IfThen(
                        Expression.Equal(matchedFactory, Expression.Constant(null, typeof(Func<ParseContext, T, T, T>))),
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
                operatorChecks.AddRange(opCompileResult.Body);
                operatorChecks.Add(
                    Expression.IfThen(
                        opCompileResult.Success,
                        Expression.Assign(matchedFactory, factoryConst)
                    )
                );
            }
        }

        var scanner = Expression.Field(context.ParseContext, nameof(ParseContext.Scanner));
        var cursor = Expression.Field(scanner, nameof(Scanner.Cursor));
        var cursorPosition = Expression.Property(cursor, nameof(Cursor.Position));
        var resetPosition = typeof(Cursor).GetMethod(nameof(Cursor.ResetPosition), [typeof(TextPosition).MakeByRefType()])!;

        var loopBody = Expression.Block(
            allOperatorVariables.Concat(rightParserResult.Variables),
            new Expression[]
            {
                Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<ParseContext, T, T, T>))),
                Expression.Assign(operatorPosition, cursorPosition)
            }
            .Concat(operatorChecks)
            .Concat([
                Expression.IfThen(
                    Expression.Equal(matchedFactory, Expression.Constant(null, typeof(Func<ParseContext, T, T, T>))),
                    Expression.Break(breakLabel)
                )
            ])
            .Concat(rightParserResult.Body)
            .Concat([
                Expression.IfThen(
                    Expression.Not(rightParserResult.Success),
                    Expression.Block(
                        Expression.Call(cursor, resetPosition, operatorPosition),
                        Expression.Break(breakLabel)
                    )
                ),
                Expression.Assign(currentValue,
                    Expression.Invoke(matchedFactory, context.ParseContext, currentValue, rightParserResult.Value))
            ])
        );

        var loopExpr = Expression.Loop(loopBody, breakLabel);

        result.Body.Add(
            Expression.Block(
                firstParserResult.Variables,
                new Expression[] { Expression.Block(firstParserResult.Body) }.Concat([
                    Expression.IfThenElse(
                        firstParserResult.Success,
                        Expression.Block(
                            Expression.Assign(currentValue, firstParserResult.Value),
                            loopExpr,
                            Expression.Assign(result.Success, Expression.Constant(true)),
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, currentValue)
                        ),
                        Expression.Assign(result.Success, Expression.Constant(false))
                    )
                ])
            )
        );

        return result;
    }

    public override string ToString() => Name ?? $"LeftAssociative({_parser})";
}
