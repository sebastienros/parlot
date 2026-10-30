using Parlot.SourceGeneration;
using System;

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
    private readonly OneOf<int> _choice;

    public LeftAssociative(Parser<T> parser, (Parser<TInput> op, Func<T, T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }

        // The operators are tried through a choice, which only tries the ones that can start with the next
        // char and skips white space once when they all do
        var choices = new Parser<int>[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            choices[i] = new OperatorIndex<TInput>(_operators[i].Op ?? throw new ArgumentException("Operators must not contain null parsers.", nameof(operators)), i);
        }

        _choice = new OneOf<int>(choices);
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

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin();

        // Parse zero or more (operator operand) pairs
        while (true)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            context.MovePin(pin);
            var operatorResult = new ParseResult<int>();

            // The choice returns the index of the operator it matched
            if (!_choice.Parse(context, ref operatorResult))
            {
                // No operator matched, we're done
                break;
            }

            var matchedFactory = _operators[operatorResult.Value].Factory;

            // Parse the right operand
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

            // Apply the operator
            value = matchedFactory(value, rightResult.Value);
            end = rightResult.End;
        }

        context.Unpin(pin);

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Each callback needs the accumulated value, even if the final value is discarded.
        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("LeftAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Generate a unique ID for this LeftAssociative instance to avoid collisions
        var uniqueId = context.NextNumber();

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

        // The operators are tried through the choice, which returns the index of the one it matched
        var operatorHelperName = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(_choice, $"{context.MethodNamePrefix}_LeftAssoc{uniqueId}", "int", () => _choice.GenerateSource(context))
            .MethodName);

        var factoryNames = new string[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            factoryNames[i] = context.RegisterLambda(_operators[i].Factory);
        }

        var operatorPositionName = $"opPos{context.NextNumber()}";
        var operatorName = $"op{context.NextNumber()}";
        var rightName = $"right{context.NextNumber()}";

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin(result);
        result.Body.Add("    while (true)");
        result.Body.Add("    {");
        result.Body.Add($"        var {operatorPositionName} = {cursorName}.Position;");
        context.MovePin(result, pin, "        ");
        result.Body.Add($"        if (!{operatorHelperName}({ctx}, out var {operatorName})) break;");
        result.Body.Add($"        if (!{baseHelperName}({ctx}, out var {rightName}))");
        result.Body.Add("        {");
        result.Body.Add($"            {cursorName}.ResetPosition({operatorPositionName});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        if ({cursorName}.Offset == {operatorPositionName}.Offset) break;");
        result.Body.Add($"        switch ({operatorName})");
        result.Body.Add("        {");

        for (var i = 0; i < _operators.Length; i++)
        {
            result.Body.Add($"            case {i}:");
            result.Body.Add($"                {result.ValueVariable} = {factoryNames[i]}({result.ValueVariable}, {rightName});");
            result.Body.Add("                break;");
        }

        result.Body.Add("        }");
        result.Body.Add("    }");
        context.Unpin(result, pin, "    ");
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
    private readonly OneOf<int> _choice;

    public LeftAssociativeWithContext(Parser<T> parser, (Parser<TInput> op, Func<ParseContext, T, T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }

        // The operators are tried through a choice, which only tries the ones that can start with the next
        // char and skips white space once when they all do
        var choices = new Parser<int>[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            choices[i] = new OperatorIndex<TInput>(_operators[i].Op ?? throw new ArgumentException("Operators must not contain null parsers.", nameof(operators)), i);
        }

        _choice = new OneOf<int>(choices);
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

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin();

        while (true)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            context.MovePin(pin);
            var operatorResult = new ParseResult<int>();

            // The choice returns the index of the operator it matched
            if (!_choice.Parse(context, ref operatorResult))
            {
                break;
            }

            var matchedFactory = _operators[operatorResult.Value].Factory;

            var rightResult = new ParseResult<T>();
            if (!_parser.Parse(context, ref rightResult))
            {
                context.Scanner.Cursor.ResetPosition(operatorPosition);
                break;
            }

            // Neither the operator nor the operand read anything, so they would match forever
            if (context.Scanner.Cursor.Offset == operatorPosition.Offset)
            {
                break;
            }

            value = matchedFactory(context, value, rightResult.Value);
            end = rightResult.End;
        }

        context.Unpin(pin);

        result = new ParseResult<T>(result.Start, end, value);

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("LeftAssociative requires the base parser to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        var uniqueId = context.NextNumber();
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

        // The operators are tried through the choice, which returns the index of the one it matched
        var operatorHelperName = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(_choice, $"{context.MethodNamePrefix}_LeftAssocCtx{uniqueId}", "int", () => _choice.GenerateSource(context))
            .MethodName);

        var factoryNames = new string[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            factoryNames[i] = context.RegisterLambda(_operators[i].Factory);
        }

        var operatorPositionName = $"opPos{context.NextNumber()}";
        var operatorName = $"op{context.NextNumber()}";
        var rightName = $"right{context.NextNumber()}";

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin(result);
        result.Body.Add("    while (true)");
        result.Body.Add("    {");
        result.Body.Add($"        var {operatorPositionName} = {cursorName}.Position;");
        context.MovePin(result, pin, "        ");
        result.Body.Add($"        if (!{operatorHelperName}({ctx}, out var {operatorName})) break;");
        result.Body.Add($"        if (!{baseHelperName}({ctx}, out var {rightName}))");
        result.Body.Add("        {");
        result.Body.Add($"            {cursorName}.ResetPosition({operatorPositionName});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        if ({cursorName}.Offset == {operatorPositionName}.Offset) break;");
        result.Body.Add($"        switch ({operatorName})");
        result.Body.Add("        {");

        for (var i = 0; i < _operators.Length; i++)
        {
            result.Body.Add($"            case {i}:");
            result.Body.Add($"                {result.ValueVariable} = {factoryNames[i]}({ctx}, {result.ValueVariable}, {rightName});");
            result.Body.Add("                break;");
        }

        result.Body.Add("        }");
        result.Body.Add("    }");
        context.Unpin(result, pin, "    ");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"LeftAssociative({_parser})";
}