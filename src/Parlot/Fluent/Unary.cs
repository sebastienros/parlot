using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a unary operation structure.
/// Handles prefix operators that can be applied recursively.
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class Unary<T, TInput> : Parser<T>, ISourceable
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.MaxRecursionDepth > 0)
        {
            context.EnterRecursion();

            try
            {
                return ParseCore(context, ref result);
            }
            finally
            {
                context.ExitRecursion();
            }
        }

        return ParseCore(context, ref result);
    }

    private bool ParseCore(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // Try each unary operator
        foreach (var (op, factory) in _operators)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
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
                    context.Scanner.Cursor.ResetPosition(operatorPosition);
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

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("Unary requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var operatorMatchedName = $"unaryOpMatched{context.NextNumber()}";
        
        // Register this unary parser as a deferred method for recursive calls
        var helperMethodName = context.Deferred.GetOrCreateMethodName(this, "Unary");

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

            // Register helper for the operator parser
            var opValueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(opSourceable));
            var opHelperName = context.Helpers
                .GetOrCreate(opSourceable, $"{context.MethodNamePrefix}_Unary", opValueTypeName, () => opSourceable.GenerateSource(context))
                .MethodName;

            var opResultName = $"opResult{context.NextNumber()}";
            var opPositionName = $"unaryPos{context.NextNumber()}";

            var indent = "";
            if (i == 0)
            {
                result.Body.Add($"{indent}var {opPositionName} = {cursorName}.Position;");
                result.Body.Add($"{indent}if ({opHelperName}({ctx}, out _))");
            }
            else
            {
                result.Body.Add($"{indent}if (!{operatorMatchedName})");
                result.Body.Add($"{indent}{{");
                result.Body.Add($"{indent}    var {opPositionName} = {cursorName}.Position;");
                result.Body.Add($"{indent}    if ({opHelperName}({ctx}, out _))");
            }

            var innerIndent = i == 0 ? indent : $"{indent}    ";
            result.Body.Add($"{innerIndent}{{");
            result.Body.Add($"{innerIndent}    {operatorMatchedName} = true;");
            
            // Recursive call via helper method
            result.Body.Add($"{innerIndent}    if ({helperMethodName}({ctx}, out var {opResultName}RecursiveValue))");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {result.SuccessVariable} = true;");
            if (!context.DiscardResult)
            {
                result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}({opResultName}RecursiveValue);");
            }
            else
            {
                result.Body.Add($"{innerIndent}        {factoryFieldName}({opResultName}RecursiveValue);");
            }
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}    else");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {cursorName}.ResetPosition({opPositionName});");
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}}}");

            if (i > 0)
            {
                result.Body.Add($"{indent}}}");
            }
        }

        // If no operator matched, try base parser using helper
        result.Body.Add($"if (!{operatorMatchedName})");
        result.Body.Add("{");

        var baseHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_Unary", valueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        if (context.DiscardResult)
        {
            result.Body.Add($"    if ({baseHelperName}({ctx}, out _))");
        }
        else
        {
            result.Body.Add($"    if ({baseHelperName}({ctx}, out {result.ValueVariable}))");
        }
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}

public sealed class UnaryWithContext<T, TInput> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<ParseContext, T, T> Factory)[] _operators;

    public UnaryWithContext(Parser<T> parser, (Parser<TInput> op, Func<ParseContext, T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (context.MaxRecursionDepth > 0)
        {
            context.EnterRecursion();

            try
            {
                return ParseCore(context, ref result);
            }
            finally
            {
                context.ExitRecursion();
            }
        }

        return ParseCore(context, ref result);
    }

    private bool ParseCore(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        foreach (var (op, factory) in _operators)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            var operatorResult = new ParseResult<TInput>();

            if (op.Parse(context, ref operatorResult))
            {
                if (Parse(context, ref result))
                {
                    result = new ParseResult<T>(result.Start, result.End, factory(context, result.Value));
                    context.ExitParser(this);
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(operatorPosition);
                context.ExitParser(this);
                return false;
            }
        }

        var success = _parser.Parse(context, ref result);

        context.ExitParser(this);
        return success;
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
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var operatorMatchedName = $"unaryOpMatched{context.NextNumber()}";
        var helperMethodName = context.Deferred.GetOrCreateMethodName(this, "UnaryCtx");

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

        for (int i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException("Unary requires all operator parsers to be source-generatable.");
            }

            var factoryFieldName = context.RegisterLambda(factory);

            var opValueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(opSourceable));
            var opHelperName = context.Helpers
                .GetOrCreate(opSourceable, $"{context.MethodNamePrefix}_UnaryCtx", opValueTypeName, () => opSourceable.GenerateSource(context))
                .MethodName;

            var opResultName = $"opResult{context.NextNumber()}";
            var opPositionName = $"unaryPos{context.NextNumber()}";

            var indent = "";
            if (i == 0)
            {
                result.Body.Add($"{indent}var {opPositionName} = {cursorName}.Position;");
                result.Body.Add($"{indent}if ({opHelperName}({ctx}, out _))");
            }
            else
            {
                result.Body.Add($"{indent}if (!{operatorMatchedName})");
                result.Body.Add($"{indent}{{");
                result.Body.Add($"{indent}    var {opPositionName} = {cursorName}.Position;");
                result.Body.Add($"{indent}    if ({opHelperName}({ctx}, out _))");
            }

            var innerIndent = i == 0 ? indent : $"{indent}    ";
            result.Body.Add($"{innerIndent}{{");
            result.Body.Add($"{innerIndent}    {operatorMatchedName} = true;");

            result.Body.Add($"{innerIndent}    if ({helperMethodName}({ctx}, out var {opResultName}RecursiveValue))");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {result.SuccessVariable} = true;");

            if (!context.DiscardResult)
            {
                result.Body.Add($"{innerIndent}        {result.ValueVariable} = {factoryFieldName}({ctx}, {opResultName}RecursiveValue);");
            }
            else
            {
                result.Body.Add($"{innerIndent}        {factoryFieldName}({ctx}, {opResultName}RecursiveValue);");
            }

            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}    else");
            result.Body.Add($"{innerIndent}    {{");
            result.Body.Add($"{innerIndent}        {cursorName}.ResetPosition({opPositionName});");
            result.Body.Add($"{innerIndent}    }}");
            result.Body.Add($"{innerIndent}}}");

            if (i > 0)
            {
                result.Body.Add($"{indent}}}");
            }
        }

        result.Body.Add($"if (!{operatorMatchedName})");
        result.Body.Add("{");

        var baseHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_UnaryCtx", valueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        if (context.DiscardResult)
        {
            result.Body.Add($"    if ({baseHelperName}({ctx}, out _))");
        }
        else
        {
            result.Body.Add($"    if ({baseHelperName}({ctx}, out {result.ValueVariable}))");
        }

        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}
