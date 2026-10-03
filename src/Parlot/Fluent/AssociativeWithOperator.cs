using System;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

internal sealed class AssociativeWithOperator<T, TOperator> : Parser<T>, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly OneOf<TOperator> _operators;
    private readonly Func<T, T, TOperator, T>? _factory;
    private readonly Func<ParseContext, T, T, TOperator, T>? _contextFactory;
    private readonly bool _rightAssociative;

    public AssociativeWithOperator(Parser<T> parser, Parser<TOperator>[] operators, Func<T, T, TOperator, T> factory, bool rightAssociative)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = CreateOperators(operators);
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _rightAssociative = rightAssociative;
    }

    public AssociativeWithOperator(Parser<T> parser, Parser<TOperator>[] operators, Func<ParseContext, T, T, TOperator, T> factory, bool rightAssociative)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = CreateOperators(operators);
        _contextFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        _rightAssociative = rightAssociative;
    }

    private static OneOf<TOperator> CreateOperators(Parser<TOperator>[] operators)
    {
        ThrowHelper.ThrowIfNull(operators, nameof(operators));

        if (operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }

        return new OneOf<TOperator>(operators);
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
        HybridList<(TOperator Operator, T Operand)>? operations = null;

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin();

        while (true)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            context.MovePin(pin);
            var operatorResult = new ParseResult<TOperator>();
            if (!_operators.Parse(context, ref operatorResult))
            {
                break;
            }

            var rightResult = new ParseResult<T>();
            if (!_parser.Parse(context, ref rightResult))
            {
                context.Scanner.Cursor.ResetPosition(operatorPosition);
                break;
            }

            if (context.Scanner.Cursor.Offset == operatorPosition.Offset)
            {
                break;
            }

            if (_rightAssociative)
            {
                operations ??= [];
                operations.Add((operatorResult.Value, rightResult.Value));
            }
            else
            {
                value = Apply(context, value, rightResult.Value, operatorResult.Value);
            }

            end = rightResult.End;
        }

        context.Unpin(pin);

        if (operations is { Count: > 0 })
        {
            var accumulated = operations[operations.Count - 1].Operand;
            for (var i = operations.Count - 1; i > 0; i--)
            {
                accumulated = Apply(context, operations[i - 1].Operand, accumulated, operations[i].Operator);
            }

            value = Apply(context, value, accumulated, operations[0].Operator);
        }

        result.Set(result.Start, end, value);
        context.ExitParser(this);
        return true;
    }

    private T Apply(ParseContext context, T left, T right, TOperator operation) =>
        _factory is not null
            ? _factory(left, right, operation)
            : _contextFactory!(context, left, right, operation);

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("Associative parsing requires a source-generatable operand parser.");
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var id = context.NextNumber();
        var valueType = SourceGenerationContext.GetTypeName(typeof(T));
        var operatorType = SourceGenerationContext.GetTypeName(typeof(TOperator));
        var prefix = $"{context.MethodNamePrefix}_Associative{id}";
        var baseHelper = context.Helpers
            .GetOrCreate(parserSourceable, prefix, valueType, () => parserSourceable.GenerateSource(context))
            .MethodName;
        var operatorHelper = context.Helpers
            .GetOrCreate(_operators, prefix, operatorType, () => _operators.GenerateSource(context))
            .MethodName;
        var factoryName = context.RegisterLambda((Delegate?)_factory ?? _contextFactory!);

        string Call(string left, string right, string operation) =>
            _factory is not null
                ? $"{factoryName}({left}, {right}, {operation})"
                : $"{factoryName}({ctx}, {left}, {right}, {operation})";

        var position = $"operatorPosition{id}";
        var operationValue = $"operation{id}";
        var rightValue = $"right{id}";
        var operations = $"operations{id}";

        result.Body.Add($"if ({baseHelper}({ctx}, out {result.ValueVariable}))");
        result.Body.Add("{");
        if (_rightAssociative)
        {
            result.Body.Add($"    global::Parlot.Fluent.HybridList<({operatorType}, {valueType})>? {operations} = null;");
        }

        // The operator is read again by the caller when it isn't followed by an operand
        var pin = context.Pin(result);
        result.Body.Add("    while (true)");
        result.Body.Add("    {");
        result.Body.Add($"        var {position} = {cursor}.Position;");
        context.MovePin(result, pin, "        ");
        result.Body.Add($"        if (!{operatorHelper}({ctx}, out var {operationValue})) break;");
        result.Body.Add($"        if (!{baseHelper}({ctx}, out var {rightValue}))");
        result.Body.Add("        {");
        result.Body.Add($"            {cursor}.ResetPosition({position});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        if ({cursor}.Offset == {position}.Offset) break;");

        if (_rightAssociative)
        {
            result.Body.Add($"        {operations} ??= new global::Parlot.Fluent.HybridList<({operatorType}, {valueType})>();");
            result.Body.Add($"        {operations}.Add(({operationValue}, {rightValue}));");
        }
        else
        {
            result.Body.Add($"        {result.ValueVariable} = {Call(result.ValueVariable, rightValue, operationValue)};");
        }

        result.Body.Add("    }");
        context.Unpin(result, pin, "    ");

        if (_rightAssociative)
        {
            var accumulated = $"accumulated{id}";
            var index = $"index{id}";
            result.Body.Add($"    if ({operations} is {{ Count: > 0 }})");
            result.Body.Add("    {");
            result.Body.Add($"        var {accumulated} = {operations}[{operations}.Count - 1].Item2;");
            result.Body.Add($"        for (var {index} = {operations}.Count - 1; {index} > 0; {index}--)");
            result.Body.Add("        {");
            result.Body.Add($"            {accumulated} = {Call($"{operations}[{index} - 1].Item2", accumulated, $"{operations}[{index}].Item1")};");
            result.Body.Add("        }");
            result.Body.Add($"        {result.ValueVariable} = {Call(result.ValueVariable, accumulated, $"{operations}[0].Item1")};");
            result.Body.Add("    }");
        }

        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        return result;
    }

    public override string ToString() => Name ?? $"{(_rightAssociative ? "Right" : "Left")}Associative({_parser})";
}
