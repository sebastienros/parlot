using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a unary operation structure.
/// Handles prefix operators that can be applied recursively.
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class Unary<T, TInput> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
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

    // The hints of the operators and of the operand, any of which can read first. They are read when they are asked for since
    // the operand can be a Deferred set later.
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => Hints.CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => Hints.ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, this, static self =>
    {
        var first = new object[self._operators.Length + 1];

        for (var i = 0; i < self._operators.Length; i++)
        {
            first[i] = self._operators[i].Op;
        }

        first[first.Length - 1] = self._parser;

        return SeekableHints.Union(first);
    });

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        foreach (var (op, _) in _operators)
        {
            analysis.Invoke(op);
        }

        // An operator is followed by an operand only when it moved the cursor
        return analysis.Invoke(_parser);
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

        var start = context.Scanner.Cursor.Offset;
        var operatorResult = new ParseResult<TInput>();
        var op = MatchOperator(context, ref operatorResult);

        var success = op < 0
            ? _parser.Parse(context, ref result)
            : ParseOperators(context, ref result, op, start, operatorResult.Start);

        context.ExitParser(this);
        return success;
    }

    /// <summary>
    /// Returns the index of the first operator which reads some text, or -1.
    /// </summary>
    private int MatchOperator(ParseContext context, ref ParseResult<TInput> operatorResult)
    {
        var cursor = context.Scanner.Cursor;
        var position = cursor.Offset;

        // The operators are tried from the same position
        var pin = context.Pin();

        for (var i = 0; i < _operators.Length; i++)
        {
            // An operator that didn't read anything would be matched again before the same operand, forever. It is
            // treated as no operator, and the next ones are tried at the same position.
            if (_operators[i].Op.Parse(context, ref operatorResult) && cursor.Offset != position)
            {
                context.Unpin(pin);
                return i;
            }
        }

        context.Unpin(pin);
        return -1;
    }

    /// <summary>
    /// Parses the operators following the first one, which matched at <paramref name="start"/>, then their operand, and applies
    /// them from the last one. The operators are read in a loop rather than recursively, so a long chain doesn't use the stack.
    /// </summary>
    private bool ParseOperators(ParseContext context, ref ParseResult<T> result, int first, int start, int resultStart)
    {
        // Each operator counts as one level of recursion, as when the operand of each one was parsed by this parser
        var limitRecursion = context.MaxRecursionDepth > 0;
        var operators = new OperatorList(stackalloc int[OperatorList.InlineCapacity]);

        try
        {
            var operatorResult = new ParseResult<TInput>();
            var op = first;

            do
            {
                if (limitRecursion)
                {
                    context.EnterRecursion();
                }

                operators.Add(op);
                op = MatchOperator(context, ref operatorResult);
            }
            while (op >= 0);

            if (!_parser.Parse(context, ref result))
            {
                // An operator not followed by an operand fails the parser
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            // The result spans the first operator and the operand
            for (var i = operators.Count - 1; i >= 0; i--)
            {
                result = new ParseResult<T>(resultStart, result.End, _operators[operators[i]].Factory(result.Value));
            }

            return true;
        }
        finally
        {
            if (limitRecursion)
            {
                context.ExitRecursion(operators.Count);
            }

            operators.Release();
        }
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // The callbacks receive the value produced by the inner operator.
        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("Unary requires the base parser to be source-generatable.");
        }

        var operators = new ISourceable[_operators.Length];
        var factories = new string[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException("Unary requires all operator parsers to be source-generatable.");
            }

            operators[i] = opSourceable;
            factories[i] = context.RegisterLambda(factory);
        }

        return UnarySource.Generate(context, typeof(T), typeof(TInput), parserSourceable, operators, factories, false, "_Unary");
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}

public sealed class UnaryWithContext<T, TInput> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
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

    // The hints of the operators and of the operand, any of which can read first. They are read when they are asked for since
    // the operand can be a Deferred set later.
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => Hints.CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => Hints.ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, this, static self =>
    {
        var first = new object[self._operators.Length + 1];

        for (var i = 0; i < self._operators.Length; i++)
        {
            first[i] = self._operators[i].Op;
        }

        first[first.Length - 1] = self._parser;

        return SeekableHints.Union(first);
    });

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        foreach (var (op, _) in _operators)
        {
            analysis.Invoke(op);
        }

        // An operator is followed by an operand only when it moved the cursor
        return analysis.Invoke(_parser);
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

        var start = context.Scanner.Cursor.Offset;
        var operatorResult = new ParseResult<TInput>();
        var op = MatchOperator(context, ref operatorResult);

        var success = op < 0
            ? _parser.Parse(context, ref result)
            : ParseOperators(context, ref result, op, start, operatorResult.Start);

        context.ExitParser(this);
        return success;
    }

    /// <summary>
    /// Returns the index of the first operator which reads some text, or -1.
    /// </summary>
    private int MatchOperator(ParseContext context, ref ParseResult<TInput> operatorResult)
    {
        var cursor = context.Scanner.Cursor;
        var position = cursor.Offset;

        // The operators are tried from the same position
        var pin = context.Pin();

        for (var i = 0; i < _operators.Length; i++)
        {
            // An operator that didn't read anything would be matched again before the same operand, forever. It is
            // treated as no operator, and the next ones are tried at the same position.
            if (_operators[i].Op.Parse(context, ref operatorResult) && cursor.Offset != position)
            {
                context.Unpin(pin);
                return i;
            }
        }

        context.Unpin(pin);
        return -1;
    }

    /// <summary>
    /// Parses the operators following the first one, which matched at <paramref name="start"/>, then their operand, and applies
    /// them from the last one. The operators are read in a loop rather than recursively, so a long chain doesn't use the stack.
    /// </summary>
    private bool ParseOperators(ParseContext context, ref ParseResult<T> result, int first, int start, int resultStart)
    {
        // Each operator counts as one level of recursion, as when the operand of each one was parsed by this parser
        var limitRecursion = context.MaxRecursionDepth > 0;
        var operators = new OperatorList(stackalloc int[OperatorList.InlineCapacity]);

        try
        {
            var operatorResult = new ParseResult<TInput>();
            var op = first;

            do
            {
                if (limitRecursion)
                {
                    context.EnterRecursion();
                }

                operators.Add(op);
                op = MatchOperator(context, ref operatorResult);
            }
            while (op >= 0);

            if (!_parser.Parse(context, ref result))
            {
                // An operator not followed by an operand fails the parser
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            // The result spans the first operator and the operand
            for (var i = operators.Count - 1; i >= 0; i--)
            {
                result = new ParseResult<T>(resultStart, result.End, _operators[operators[i]].Factory(context, result.Value));
            }

            return true;
        }
        finally
        {
            if (limitRecursion)
            {
                context.ExitRecursion(operators.Count);
            }

            operators.Release();
        }
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // The callbacks receive the value produced by the inner operator.
        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_parser is not ISourceable parserSourceable)
        {
            throw new NotSupportedException("Unary requires the base parser to be source-generatable.");
        }

        var operators = new ISourceable[_operators.Length];
        var factories = new string[_operators.Length];

        for (var i = 0; i < _operators.Length; i++)
        {
            var (op, factory) = _operators[i];

            if (op is not ISourceable opSourceable)
            {
                throw new NotSupportedException("Unary requires all operator parsers to be source-generatable.");
            }

            operators[i] = opSourceable;
            factories[i] = context.RegisterLambda(factory);
        }

        return UnarySource.Generate(context, typeof(T), typeof(TInput), parserSourceable, operators, factories, true, "_UnaryCtx");
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}

/// <summary>
/// Emits the code of the unary parsers, which read their operators in a loop like at runtime.
/// </summary>
internal static class UnarySource
{
    public static SourceResult Generate(
        SourceGenerationContext context,
        Type valueType,
        Type operatorType,
        ISourceable parser,
        ISourceable[] operators,
        string[] factories,
        bool withContext,
        string helperPrefix)
    {
        var result = context.CreateResult(valueType);
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
        var operatorTypeName = SourceGenerationContext.GetTypeName(operatorType);
        var id = context.NextNumber();
        var chain = $"unaryChain{id}";
        var startName = $"unaryStart{id}";
        var opName = $"unaryOp{id}";

        var operatorHelpers = new string[operators.Length];

        for (var i = 0; i < operators.Length; i++)
        {
            var op = operators[i];
            operatorHelpers[i] = context.WithDiscardResult(true, () => context.Helpers
                .GetOrCreate(op, $"{context.MethodNamePrefix}{helperPrefix}", operatorTypeName, () => op.GenerateSource(context))
                .MethodName);
        }

        var baseHelper = context.Helpers
            .GetOrCreate(parser, $"{context.MethodNamePrefix}{helperPrefix}", valueTypeName, () => parser.GenerateSource(context))
            .MethodName;

        // Like at runtime, the parser counts as one level of recursion, and each operator as one more
        var limitRecursion = context.MaxRecursionDepth > 0;

        if (limitRecursion)
        {
            result.Body.Add($"{ctx}.EnterRecursion();");
        }

        // The first operator is matched inline: most operands have none, and their code then has no call to set up
        result.Body.Add($"var {startName} = {cursor}.Offset;");
        result.Body.Add($"var {opName} = -1;");
        EmitMatch(context, result.Body, "", operatorHelpers, ctx, cursor, opName, startName);
        result.Body.Add($"if ({opName} < 0)");
        result.Body.Add("{");
        result.Body.Add($"    if ({baseHelper}({ctx}, out {result.ValueVariable}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {chain}({ctx}, {opName}, {startName}, out {result.ValueVariable});");
        result.Body.Add("}");

        // An exception fails the whole parse, and the context isn't used again, so the levels are only left on a return
        if (limitRecursion)
        {
            result.Body.Add($"{ctx}.ExitRecursion();");
        }

        // The operators following the first one, then their operand, applied from the last operator. The local function receives
        // the context rather than capturing it, which would move the locals of the parser's method to a closure. It is a single
        // statement, so that its returns are not taken for the parser's own.
        var lines = new List<string>
        {
            $"bool {chain}(global::Parlot.Fluent.ParseContext {ctx}, int op, int start, out {valueTypeName} operand)",
            "{",
            $"    var {cursor} = {ctx}.Scanner.Cursor;",
            $"    var operators = new global::Parlot.Fluent.OperatorList(stackalloc int[{OperatorList.InlineCapacity.ToString(CultureInfo.InvariantCulture)}]);",
            "    try",
            "    {",
            "        do",
            "        {",
        };

        if (limitRecursion)
        {
            lines.Add($"            {ctx}.EnterRecursion();");
        }

        lines.Add("            operators.Add(op);");
        lines.Add($"            var position = {cursor}.Offset;");
        lines.Add("            op = -1;");

        EmitMatch(context, lines, "            ", operatorHelpers, ctx, cursor, "op", "position");

        lines.Add("        }");
        lines.Add("        while (op >= 0);");
        lines.Add($"        if (!{baseHelper}({ctx}, out operand))");
        lines.Add("        {");
        lines.Add($"            {cursor}.ResetPosition(start);");
        lines.Add("            return false;");
        lines.Add("        }");
        lines.Add("        for (var i = operators.Count - 1; i >= 0; i--)");
        lines.Add("        {");
        lines.Add("            switch (operators[i])");
        lines.Add("            {");

        for (var i = 0; i < operators.Length; i++)
        {
            var arguments = withContext ? $"{ctx}, operand" : "operand";
            lines.Add($"                case {i.ToString(CultureInfo.InvariantCulture)}: operand = {factories[i]}({arguments}); break;");
        }

        lines.Add("            }");
        lines.Add("        }");
        lines.Add("        return true;");
        lines.Add("    }");
        lines.Add("    finally");
        lines.Add("    {");

        if (limitRecursion)
        {
            lines.Add($"        {ctx}.ExitRecursion(operators.Count);");
        }

        lines.Add("        operators.Release();");
        lines.Add("    }");
        lines.Add("}");
        result.Body.Add(string.Join("\n", lines));

        return result;
    }

    // Sets opName to the index of the first operator which reads some text from the position in positionName. An operator
    // that didn't read anything would be matched again before the same operand, forever: it is treated as no operator, like
    // at runtime.
    private static void EmitMatch(SourceGenerationContext context, IList<string> lines, string indent, string[] operatorHelpers, string ctx, string cursor, string opName, string positionName)
    {
        string? pin = null;

        if (context.IsCompacting)
        {
            // The operators are tried from the same position
            pin = $"unaryPin{context.NextNumber()}";
            lines.Add($"{indent}var {pin} = {ctx}.Pin();");
        }

        for (var i = 0; i < operatorHelpers.Length; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            lines.Add($"{indent}{(i == 0 ? "" : "else ")}if ({operatorHelpers[i]}({ctx}, out _) && {cursor}.Offset != {positionName}) {opName} = {index};");
        }

        if (pin is not null)
        {
            lines.Add($"{indent}{ctx}.Unpin({pin});");
        }
    }
}
