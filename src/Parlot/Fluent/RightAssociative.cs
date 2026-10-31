using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a right-associative structure from a base parser and a list of operators.
/// c.f. https://en.wikipedia.org/wiki/Operator_associativity
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class RightAssociative<T, TInput> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<T, T, T> Factory)[] _operators;

    public RightAssociative(Parser<T> parser, (Parser<TInput> op, Func<T, T, T> factory)[] operators)
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

        // a (op1) b (op2) c is applied as op1(a, op2(b, c)), so the pairs are kept until the end
        var operations = new ListBuilder<(Func<T, T, T> Factory, T Operand)>();

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin();

        try
        {
            while (true)
            {
                var operatorPosition = context.Scanner.Cursor.Position;
                context.MovePin(pin);
                var operatorResult = new ParseResult<TInput>();
                Func<T, T, T>? matchedFactory = null;

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
                    // Operator matched but no right operand - rollback operator consumption.
                    context.Scanner.Cursor.ResetPosition(operatorPosition);
                    break;
                }

                // Neither the operator nor the operand read anything, so they would match forever
                if (context.Scanner.Cursor.Offset == operatorPosition.Offset)
                {
                    break;
                }

                operations.Add((matchedFactory, rightResult.Value));
                end = rightResult.End;
            }

            context.Unpin(pin);

            // The pairs are read from the builder: the factories can parse, and grow the scratch stack
            var count = operations.Count;
            if (count > 0)
            {
                var accumulated = operations[count - 1].Operand;

                for (var i = count - 1; i > 0; i--)
                {
                    accumulated = operations[i].Factory(operations[i - 1].Operand, accumulated);
                }

                value = operations[0].Factory(value, accumulated);
            }
        }
        finally
        {
            operations.Release();
        }

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        return RightAssociativeSource.Generate(
            context,
            this,
            _parser,
            _operators.Select(static operation => (operation.Op, (Delegate)operation.Factory)).ToArray(),
            withContext: false,
            "RightAssoc");
    }

    public override string ToString() => Name ?? $"RightAssociative({_parser})";
}

/// <summary>
/// A parser that creates a right-associative structure from a base parser and a list of operators whose
/// factories receive the <see cref="ParseContext"/>.
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class RightAssociativeWithContext<T, TInput> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<ParseContext, T, T, T> Factory)[] _operators;

    public RightAssociativeWithContext(Parser<T> parser, (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] operators)
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

        var operations = new ListBuilder<(Func<ParseContext, T, T, T> Factory, T Operand)>();

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin();

        try
        {
            while (true)
            {
                var operatorPosition = context.Scanner.Cursor.Position;
                context.MovePin(pin);
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
                    // Operator matched but no right operand - rollback operator consumption.
                    context.Scanner.Cursor.ResetPosition(operatorPosition);
                    break;
                }

                // Neither the operator nor the operand read anything, so they would match forever
                if (context.Scanner.Cursor.Offset == operatorPosition.Offset)
                {
                    break;
                }

                operations.Add((matchedFactory, rightResult.Value));
                end = rightResult.End;
            }

            context.Unpin(pin);

            // The pairs are read from the builder: the factories can parse, and grow the scratch stack
            var count = operations.Count;
            if (count > 0)
            {
                var accumulated = operations[count - 1].Operand;

                for (var i = count - 1; i > 0; i--)
                {
                    accumulated = operations[i].Factory(context, operations[i - 1].Operand, accumulated);
                }

                value = operations[0].Factory(context, value, accumulated);
            }
        }
        finally
        {
            operations.Release();
        }

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        return RightAssociativeSource.Generate(
            context,
            this,
            _parser,
            _operators.Select(static operation => (operation.Op, (Delegate)operation.Factory)).ToArray(),
            withContext: true,
            "RightAssocCtx");
    }

    public override string ToString() => Name ?? $"RightAssociative({_parser})";
}

/// <summary>
/// Emits the source of the tuple-form right-associative parsers. The operator that matched is recorded by
/// index next to its operand, and the fold calls the registered factory method for that index.
/// </summary>
internal static class RightAssociativeSource
{
    public static SourceResult Generate<T, TInput>(
        SourceGenerationContext context,
        Parser<T> owner,
        Parser<T> parser,
        (Parser<TInput> Op, Delegate Factory)[] operators,
        bool withContext,
        string helperPrefix)
    {
        // Each callback needs the operand values, even if the final value is discarded
        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => ((ISourceable)owner).GenerateSource(context));
        }

        if (parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("RightAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var inputTypeName = SourceGenerationContext.GetTypeName(typeof(TInput));
        var uniqueId = context.NextNumber();
        var prefix = $"{context.MethodNamePrefix}_{helperPrefix}{uniqueId}";

        var baseHelperName = context.Helpers
            .GetOrCreate(parserSourceable, prefix, valueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        var factoryNames = new string[operators.Length];
        var operatorHelperNames = new string[operators.Length];

        for (var i = 0; i < operators.Length; i++)
        {
            var (op, factory) = operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException("RightAssociative requires all operator parsers to be source-generatable.");
            }

            factoryNames[i] = context.RegisterLambda(factory);
            operatorHelperNames[i] = context.WithDiscardResult(true, () => context.Helpers
                .GetOrCreate(opSourceable, prefix, inputTypeName, () => opSourceable.GenerateSource(context))
                .MethodName);
        }

        var operationsName = $"operations{uniqueId}";
        var matchedName = $"matched{uniqueId}";
        var positionName = $"opPos{uniqueId}";
        var rightName = $"right{uniqueId}";
        var listTypeName = $"global::Parlot.Fluent.HybridList<(int, {valueTypeName})>";

        string Apply(string index, string left, string right)
        {
            var arms = new List<string>();

            for (var i = 0; i < factoryNames.Length; i++)
            {
                var call = withContext
                    ? $"{factoryNames[i]}({ctx}, {left}, {right})"
                    : $"{factoryNames[i]}({left}, {right})";
                arms.Add($"{i} => {call}");
            }

            arms.Add("_ => throw new global::System.InvalidOperationException()");

            return $"({index} switch {{ {string.Join(", ", arms)} }})";
        }

        result.Body.Add($"if ({baseHelperName}({ctx}, out {result.ValueVariable}))");
        result.Body.Add("{");
        result.Body.Add($"    {listTypeName}? {operationsName} = null;");

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin(result);
        result.Body.Add("    while (true)");
        result.Body.Add("    {");
        result.Body.Add($"        var {positionName} = {cursorName}.Position;");
        context.MovePin(result, pin, "        ");
        result.Body.Add($"        var {matchedName} = -1;");

        for (var i = 0; i < operatorHelperNames.Length; i++)
        {
            var keyword = i == 0 ? "if" : "else if";
            result.Body.Add($"        {keyword} ({operatorHelperNames[i]}({ctx}, out _)) {matchedName} = {i};");
        }

        result.Body.Add($"        if ({matchedName} < 0) break;");
        result.Body.Add($"        if (!{baseHelperName}({ctx}, out var {rightName}))");
        result.Body.Add("        {");
        result.Body.Add($"            {cursorName}.ResetPosition({positionName});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        if ({cursorName}.Offset == {positionName}.Offset) break;");
        result.Body.Add($"        {operationsName} ??= new {listTypeName}();");
        result.Body.Add($"        {operationsName}.Add(({matchedName}, {rightName}));");
        result.Body.Add("    }");
        context.Unpin(result, pin, "    ");

        var accumulatedName = $"accumulated{uniqueId}";
        var indexName = $"index{uniqueId}";
        result.Body.Add($"    if ({operationsName} is {{ Count: > 0 }})");
        result.Body.Add("    {");
        result.Body.Add($"        var {accumulatedName} = {operationsName}[{operationsName}.Count - 1].Item2;");
        result.Body.Add($"        for (var {indexName} = {operationsName}.Count - 1; {indexName} > 0; {indexName}--)");
        result.Body.Add("        {");
        result.Body.Add($"            {accumulatedName} = {Apply($"{operationsName}[{indexName}].Item1", $"{operationsName}[{indexName} - 1].Item2", accumulatedName)};");
        result.Body.Add("        }");
        result.Body.Add($"        {result.ValueVariable} = {Apply($"{operationsName}[0].Item1", result.ValueVariable, accumulatedName)};");
        result.Body.Add("    }");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }
}
