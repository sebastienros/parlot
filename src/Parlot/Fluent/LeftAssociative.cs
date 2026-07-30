using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a left-associative structure from a base parser and a list of operators.
/// c.f. https://en.wikipedia.org/wiki/Operator_associativity
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class LeftAssociative<T, TInput> : Parser<T>, ISourceable
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


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("LeftAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
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
        var operatorPositionName = $"opPos{context.NextNumber()}";
        result.Body.Add($"        var {operatorPositionName} = {cursorName}.Position;");

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
            // Parse right operand using helper
            result.Body.Add($"{innerIndent}    if ({baseHelperName}({ctx}, out var {opResultName}RightValue))");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {operatorMatchedName} = true;");
            if (!context.DiscardResult)
            {
                result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}({result.ValueVariable}, {opResultName}RightValue);");
            }
            else
            {
                result.Body.Add($"{innerIndent}        {factoryFieldName}({result.ValueVariable}, {opResultName}RightValue);");
            }
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}    else");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {cursorName}.ResetPosition({operatorPositionName});");
            result.Body.Add($"{innerIndent}        break;");
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


public sealed class LeftAssociativeWithContext<T, TInput> : Parser<T>, ISourceable
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


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("LeftAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var uniqueId = context.NextNumber();
        var operatorMatchedName = $"opMatched{context.NextNumber()}";

        result.Body.Add($"bool {operatorMatchedName} = false;");

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

        var baseHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_LeftAssocCtx{uniqueId}", valueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

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

        var operatorPositionName = $"opPos{context.NextNumber()}";
        result.Body.Add($"        var {operatorPositionName} = {cursorName}.Position;");

        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException("LeftAssociative requires all operator parsers to be source-generatable.");
            }

            var factoryFieldName = context.RegisterLambda(factory);

            var opValueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(opSourceable));
            var opHelperName = context.Helpers
                .GetOrCreate(opSourceable, $"{context.MethodNamePrefix}_LeftAssocCtx{uniqueId}", opValueTypeName, () => opSourceable.GenerateSource(context))
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
            result.Body.Add($"{innerIndent}    if ({baseHelperName}({ctx}, out var {opResultName}RightValue))");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {operatorMatchedName} = true;");

            if (!context.DiscardResult)
            {
                result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}({ctx}, {result.ValueVariable}, {opResultName}RightValue);");
            }
            else
            {
                result.Body.Add($"{innerIndent}        {factoryFieldName}({ctx}, {result.ValueVariable}, {opResultName}RightValue);");
            }

            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}    else");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {cursorName}.ResetPosition({operatorPositionName});");
            result.Body.Add($"{innerIndent}        break;");
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