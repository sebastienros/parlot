using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Parlot.Fluent;

/// <summary>
/// A parser applying a table of prefix, postfix and binary operators to the values of another parser, with precedence
/// climbing (c.f. https://en.wikipedia.org/wiki/Operator-precedence_parser).
/// </summary>
/// <remarks>
/// <para>
/// The table is a list of levels, from the one which binds the tightest. It parses the same values as the chain of
/// <see cref="Parsers.Unary{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T}}[])"/>,
/// <see cref="Parsers.LeftAssociative{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/> and
/// <see cref="Parsers.RightAssociative{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/> parsers built
/// with the same levels in the same order, each one reading its operands with the previous one.
/// </para>
/// <para>
/// The chain tries the operators of each of its levels after each operand, from the tightest one, and every operand goes
/// through every level. This parser instead looks for the operator following an operand once, through a choice of all the
/// operators, such that its cost doesn't depend on the number of levels.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the operands and of the result.</typeparam>
public sealed class PrecedenceParser<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _operand;
    private readonly OperatorLevel<T>[] _levels;

    // The level of each operator, and its factory as a unary or a binary function
    private readonly int[] _operatorLevels;
    private readonly Func<T, T>?[] _unary;
    private readonly Func<T, T, T>?[] _binary;

    // The prefix levels, from the tightest one, which are tried before an operand
    private readonly int[] _prefixLevels;

    // The tightest level whose operators follow an operand, int.MaxValue when there is none. A lookup from it, or from a
    // tighter level, tries all these operators at once through _choice.
    private readonly int _lowestLookupLevel;
    private OneOf<int>? _choice;

    // The parsers that can read first, a prefix operator or an operand, whose hints are read when they are asked for since
    // they can be Deferred parsers set later
    private readonly object[] _first;
    private ForwardedHints? _hints;

    /// <summary>
    /// Creates a parser with no operators, which parses the values of <paramref name="operand"/>. Levels are added with
    /// <see cref="Unary{TInput}(ValueTuple{Parser{TInput}, Func{T, T}}[])"/>,
    /// <see cref="Postfix{TInput}(ValueTuple{Parser{TInput}, Func{T, T}}[])"/>,
    /// <see cref="LeftAssociative{TInput}(ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/> and
    /// <see cref="RightAssociative{TInput}(ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/>.
    /// </summary>
    /// <param name="operand">The parser of the operands.</param>
    public PrecedenceParser(Parser<T> operand) : this(operand ?? throw new ArgumentNullException(nameof(operand)), [])
    {
    }

    internal PrecedenceParser(Parser<T> operand, OperatorLevel<T>[] levels)
    {
        _operand = operand;
        _levels = levels;

        var count = levels.Length == 0 ? 0 : levels[levels.Length - 1].First + levels[levels.Length - 1].Operators.Length;
        _operatorLevels = new int[count];
        _unary = new Func<T, T>?[count];
        _binary = new Func<T, T, T>?[count];

        var prefixLevels = new List<int>();
        _lowestLookupLevel = int.MaxValue;

        // The parser starts with a prefix operator or an operand
        var first = new List<object> { operand };

        for (var level = 0; level < levels.Length; level++)
        {
            var operatorLevel = levels[level];

            for (var i = 0; i < operatorLevel.Operators.Length; i++)
            {
                var index = operatorLevel.First + i;
                _operatorLevels[index] = level;

                if (operatorLevel.Kind is OperatorKind.Prefix or OperatorKind.Postfix)
                {
                    _unary[index] = (Func<T, T>)operatorLevel.Factories[i];
                }
                else
                {
                    _binary[index] = (Func<T, T, T>)operatorLevel.Factories[i];
                }
            }

            if (operatorLevel.Kind == OperatorKind.Prefix)
            {
                prefixLevels.Add(level);
                first.AddRange(operatorLevel.Operators);
            }
            else if (_lowestLookupLevel == int.MaxValue)
            {
                _lowestLookupLevel = level;
            }
        }

        _prefixLevels = prefixLevels.ToArray();
        _first = first.ToArray();
    }

    /// <inheritdoc />
    public bool CanSeek => Hints.CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => Hints.ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, _first, static first => SeekableHints.Union(first));

    /// <summary>
    /// Returns a parser with an additional level of prefix operators, which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Parsers.Unary{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T}}[])"/>, the first operator
    /// which reads some text applies, and the operand that follows can start with another operator of the level. An
    /// operator that isn't followed by an operand fails the parser.
    /// </remarks>
    /// <typeparam name="TInput">The type of the operator parsers.</typeparam>
    /// <param name="list">The operators of the level and the functions creating their result from the operand.</param>
    /// <returns>A new parser with the additional level.</returns>
    /// <example>
    /// <code>
    /// var expression = primary.Operators()
    ///     .Unary((Terms.Char('-'), static x => -x))
    ///     .LeftAssociative((Terms.Char('*'), static (a, b) => a * b), (Terms.Char('/'), static (a, b) => a / b))
    ///     .LeftAssociative((Terms.Char('+'), static (a, b) => a + b), (Terms.Char('-'), static (a, b) => a - b));
    /// </code>
    /// </example>
    public PrecedenceParser<T> Unary<TInput>(params (Parser<TInput> op, Func<T, T> factory)[] list) =>
        Add(OperatorLevel<T>.Create(OperatorKind.Prefix, OperatorCount, list, nameof(list)));

    /// <summary>
    /// Returns a parser with an additional level of postfix operators, which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// The operators of the level are applied in turn to the value of the tighter levels, as long as one of them reads some text.
    /// </remarks>
    /// <typeparam name="TInput">The type of the operator parsers.</typeparam>
    /// <param name="list">The operators of the level and the functions creating their result from the operand.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> Postfix<TInput>(params (Parser<TInput> op, Func<T, T> factory)[] list) =>
        Add(OperatorLevel<T>.Create(OperatorKind.Postfix, OperatorCount, list, nameof(list)));

    /// <summary>
    /// Returns a parser with an additional level of left-associative binary operators, which binds less tightly than the
    /// existing levels.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Parsers.LeftAssociative{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/>,
    /// <c>a - b - c</c> is <c>(a - b) - c</c>. An operator that isn't followed by an operand is not read, and the looser
    /// levels are tried at its position.
    /// </remarks>
    /// <typeparam name="TInput">The type of the operator parsers.</typeparam>
    /// <param name="list">The operators of the level and the functions creating their result from the operands.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> LeftAssociative<TInput>(params (Parser<TInput> op, Func<T, T, T> factory)[] list) =>
        Add(OperatorLevel<T>.Create(OperatorKind.LeftAssociative, OperatorCount, list, nameof(list)));

    /// <summary>
    /// Returns a parser with an additional level of right-associative binary operators, which binds less tightly than the
    /// existing levels.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Parsers.RightAssociative{T, TInput}(Parser{T}, ValueTuple{Parser{TInput}, Func{T, T, T}}[])"/>,
    /// <c>a ^ b ^ c</c> is <c>a ^ (b ^ c)</c>. An operator that isn't followed by an operand is not read, and the looser
    /// levels are tried at its position.
    /// </remarks>
    /// <typeparam name="TInput">The type of the operator parsers.</typeparam>
    /// <param name="list">The operators of the level and the functions creating their result from the operands.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> RightAssociative<TInput>(params (Parser<TInput> op, Func<T, T, T> factory)[] list) =>
        Add(OperatorLevel<T>.Create(OperatorKind.RightAssociative, OperatorCount, list, nameof(list)));

    private int OperatorCount => _operatorLevels.Length;

    private PrecedenceParser<T> Add(OperatorLevel<T> level)
    {
        var levels = new OperatorLevel<T>[_levels.Length + 1];
        _levels.CopyTo(levels, 0);
        levels[_levels.Length] = level;

        return new PrecedenceParser<T>(_operand, levels);
    }

    // All the operators which can follow an operand, built when it is first used since a table is usually built
    // level by level
    private OneOf<int>? GetChoice()
    {
        var choice = Volatile.Read(ref _choice);

        if (choice is null && _lowestLookupLevel != int.MaxValue)
        {
            var operators = new List<Parser<int>>();

            foreach (var level in _levels)
            {
                if (level.Kind != OperatorKind.Prefix)
                {
                    operators.AddRange(level.Operators);
                }
            }

            choice = new OneOf<int>(operators.ToArray());
            choice = Interlocked.CompareExchange(ref _choice, choice, null) ?? choice;
        }

        return choice;
    }

    /// <inheritdoc />
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        // The prefix operators are tried first, one is followed by an operand only when it moved the cursor
        foreach (var level in _levels)
        {
            if (level.Kind == OperatorKind.Prefix)
            {
                analysis.Invoke(level.Choice);
            }
        }

        var nullable = analysis.Invoke(_operand);

        // The other operators follow the first operand
        if (nullable)
        {
            foreach (var level in _levels)
            {
                if (level.Kind != OperatorKind.Prefix)
                {
                    analysis.Invoke(level.Choice);
                }
            }
        }

        return nullable;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var next = new Lookahead();
        var success = Climb(context, _levels.Length - 1, ref result, ref next);

        context.ExitParser(this);
        return success;
    }

    /// <summary>
    /// The operator following an operand, which the climb of a looser level applies.
    /// </summary>
    private struct Lookahead
    {
        // The index of the operator, -1 when none follows
        public int Operator;

        // Where the operator starts, or where none was found
        public int Offset;

        // Where the operator ends
        public int End;
    }

    /// <summary>
    /// Parses an operand and the operators of the levels up to <paramref name="maxLevel"/> which follow it, like the parser
    /// of that level in the chain would. When it succeeds, <paramref name="next"/> is the operator which follows, of a looser
    /// level, with the cursor after it, or none with the cursor where it was looked for.
    /// </summary>
    /// <remarks>
    /// When <paramref name="parsed"/> is set, the operand is the value of <paramref name="result"/>, and
    /// <paramref name="lookup"/> tells whether the operator following it is still to be looked for, see ParsePrefixed().
    /// </remarks>
    private bool Climb(ParseContext context, int maxLevel, ref ParseResult<T> result, ref Lookahead next, bool parsed = false, bool lookup = true)
    {
        if (!parsed && !ParseOperand(context, maxLevel, ref result, ref next, out lookup))
        {
            return false;
        }

        var cursor = context.Scanner.Cursor;
        var pin = -1;

        // The levels tighter than this one were tried at the position of the next lookup
        var lowest = 0;

        while (true)
        {
            if (lookup)
            {
                // The operator is read again by the looser levels when it isn't followed by an operand
                if (pin < 0)
                {
                    pin = context.Pin();
                }
                else
                {
                    context.MovePin(pin);
                }

                if (!Lookup(context, lowest, ref next))
                {
                    break;
                }

                lookup = false;
            }

            var op = next.Operator;

            if (op < 0)
            {
                break;
            }

            var level = _operatorLevels[op];

            if (level > maxLevel)
            {
                // A looser level applies it
                break;
            }

            var position = next.Offset;

            // An operator found by the climb of a tighter level was pinned until it returned
            if (pin < 0)
            {
                pin = context.PinAt(position);
            }
            else
            {
                context.MovePin(pin, position);
            }

            var kind = _levels[level].Kind;

            if (kind == OperatorKind.Postfix)
            {
                // An operator that didn't read anything would be matched again forever
                if (cursor.Offset != position)
                {
                    result.Set(result.Start, next.End, _unary[op]!(result.Value));
                    lowest = level;
                }
                else
                {
                    lowest = level + 1;
                }

                lookup = true;
                continue;
            }

            var right = new ParseResult<T>();

            // Neither the operator nor the operand read anything, so they would match forever
            if (!Climb(context, level - 1, ref right, ref next) || next.Offset == position)
            {
                // Operator matched but no right operand - rollback operator consumption.
                cursor.ResetPosition(position);
                lowest = level + 1;
                lookup = true;
                continue;
            }

            if (kind == OperatorKind.LeftAssociative)
            {
                // The operator following the operand is of this level or a looser one
                result.Set(result.Start, right.End, _binary[op]!(result.Value, right.Value));
                continue;
            }

            // a (op1) b (op2) c is applied as op1(a, op2(b, c)), so the following operators of the level are read first
            var end = right.End;
            HybridList<(int Operator, T Operand)>? operations = null;

            while (next.Operator >= 0 && _operatorLevels[next.Operator] == level)
            {
                var operationPosition = next.Offset;
                var operation = next.Operator;
                context.MovePin(pin, operationPosition);

                var operand = new ParseResult<T>();

                if (!Climb(context, level - 1, ref operand, ref next) || next.Offset == operationPosition)
                {
                    cursor.ResetPosition(operationPosition);
                    lowest = level + 1;
                    lookup = true;
                    break;
                }

                operations ??= [];
                operations.Add((operation, operand.Value));
                end = operand.End;
            }

            var value = right.Value;

            if (operations is not null)
            {
                var accumulated = operations[operations.Count - 1].Operand;

                for (var i = operations.Count - 1; i > 0; i--)
                {
                    accumulated = _binary[operations[i].Operator]!(operations[i - 1].Operand, accumulated);
                }

                value = _binary[operations[0].Operator]!(value, accumulated);
            }

            result.Set(result.Start, end, _binary[op]!(result.Value, value));
        }

        context.Unpin(pin);
        return true;
    }

    /// <summary>
    /// Parses the prefix operators of the levels up to <paramref name="maxLevel"/> and the operand they apply to. When an
    /// operator applied, <paramref name="next"/> is the operator following its operand, and <paramref name="lookup"/> is false.
    /// </summary>
    private bool ParseOperand(ParseContext context, int maxLevel, ref ParseResult<T> result, ref Lookahead next, out bool lookup)
    {
        lookup = true;

        var prefixLevels = _prefixLevels;
        var i = prefixLevels.Length - 1;

        while (i >= 0 && prefixLevels[i] > maxLevel)
        {
            i--;
        }

        if (i >= 0)
        {
            var start = context.Scanner.Cursor.Offset;
            var operatorResult = new ParseResult<int>();

            var top = MatchPrefixes(context, i, ref operatorResult);

            if (top >= 0)
            {
                lookup = false;
                return ParsePrefixed(context, start, top, ref operatorResult, ref result, ref next);
            }
        }

        return _operand.Parse(context, ref result);
    }

    /// <summary>
    /// Matches an operator of the prefix levels from <c>_prefixLevels[top]</c> to the tightest one, and returns the index of
    /// its level in <c>_prefixLevels</c>, or -1.
    /// </summary>
    private int MatchPrefixes(ParseContext context, int top, ref ParseResult<int> operatorResult)
    {
        var offset = context.Scanner.Cursor.Offset;

        // The operators of each level are tried from the same position
        var pin = context.Pin();

        // The operators of the loosest level are tried first, as its parser in the chain runs first
        for (var i = top; i >= 0; i--)
        {
            if (MatchPrefix(context, _levels[_prefixLevels[i]], offset, ref operatorResult))
            {
                context.Unpin(pin);
                return i;
            }
        }

        context.Unpin(pin);
        return -1;
    }

    /// <summary>
    /// Parses the prefix operators following the one in <paramref name="operatorResult"/>, which matched at
    /// <paramref name="start"/> in the level <c>_prefixLevels[top]</c>, then their operand, and applies them from the last one.
    /// </summary>
    /// <remarks>
    /// The operand of an operator can start with another operator of its level or of a tighter one, and the operators of the
    /// levels up to its own that follow the operand apply to it first. The operators are read in a loop rather than recursively,
    /// so a long chain doesn't use the stack.
    /// </remarks>
    private bool ParsePrefixed(ParseContext context, int start, int top, ref ParseResult<int> operatorResult, ref ParseResult<T> result, ref Lookahead next)
    {
        // Each operator counts as one level of recursion, as when the operand of each one was climbed recursively
        var limitRecursion = context.MaxRecursionDepth > 0;
        var resultStart = operatorResult.Start;
        var operators = new ListBuilder<int>();

        try
        {
            do
            {
                if (limitRecursion)
                {
                    context.EnterRecursion();
                }

                operators.Add(operatorResult.Value);

                // The next operator is of the level of this one or of a tighter one
                top = MatchPrefixes(context, top, ref operatorResult);
            }
            while (top >= 0);

            if (!_operand.Parse(context, ref result))
            {
                // An operator not followed by an operand fails the parser
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            var lookup = true;

            for (var i = operators.Count - 1; i >= 0; i--)
            {
                var op = operators[i];

                // Never fails once the operand is parsed
                Climb(context, _operatorLevels[op], ref result, ref next, parsed: true, lookup);

                // The result spans the first operator and the operand
                result.Set(resultStart, result.End, _unary[op]!(result.Value));
                lookup = false;
            }

            return true;
        }
        finally
        {
            if (limitRecursion)
            {
                for (var i = 0; i < operators.Count; i++)
                {
                    context.ExitRecursion();
                }
            }

            operators.Release();
        }
    }

    /// <summary>
    /// Matches the first operator of a prefix level which reads some text.
    /// </summary>
    private static bool MatchPrefix(ParseContext context, OperatorLevel<T> level, int offset, ref ParseResult<int> result)
    {
        if (!level.Choice.Parse(context, ref result))
        {
            return false;
        }

        var cursor = context.Scanner.Cursor;

        if (cursor.Offset != offset)
        {
            return true;
        }

        // An operator that didn't read anything would be matched again by the operand, forever. It is treated as no
        // operator, and the next ones are tried at the same position.
        var operators = level.Operators;

        for (var i = result.Value - level.First + 1; i < operators.Length; i++)
        {
            if (operators[i].Parse(context, ref result) && cursor.Offset != offset)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Looks for an operator following an operand, from the level <paramref name="lowest"/>.
    /// </summary>
    private bool Lookup(ParseContext context, int lowest, ref Lookahead next)
    {
        next.Offset = context.Scanner.Cursor.Offset;

        var operatorResult = new ParseResult<int>();

        if (lowest <= _lowestLookupLevel)
        {
            // The choice returns the first operator which matches, from the tightest level
            var choice = Volatile.Read(ref _choice) ?? GetChoice();

            if (choice is null || !choice.Parse(context, ref operatorResult))
            {
                next.Operator = -1;
                return false;
            }
        }
        else if (!LookupFrom(context, lowest, ref operatorResult))
        {
            next.Operator = -1;
            return false;
        }

        next.Operator = operatorResult.Value;
        next.End = operatorResult.End;
        return true;
    }

    // Only after an operator wasn't followed by an operand, or after a postfix operator
    private bool LookupFrom(ParseContext context, int lowest, ref ParseResult<int> result)
    {
        for (var level = lowest; level < _levels.Length; level++)
        {
            var operatorLevel = _levels[level];

            if (operatorLevel.Kind != OperatorKind.Prefix && operatorLevel.Choice.Parse(context, ref result))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Each callback needs the values of its operands, even if the final value is discarded.
        if (context.DiscardResult)
        {
            return context.WithDiscardResult(false, () => GenerateSource(context));
        }

        if (_operand is not ISourceable operandSourceable)
        {
            throw new NotSupportedException("Operators require the operand parser to be source-generatable.");
        }

        foreach (var level in _levels)
        {
            foreach (var op in level.Operators)
            {
                if (op is not ISourceable)
                {
                    throw new NotSupportedException("Operators require all operator parsers to be source-generatable.");
                }
            }
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var valueType = SourceGenerationContext.GetTypeName(typeof(T));
        var id = context.NextNumber();
        var prefix = $"{context.MethodNamePrefix}_Operators{id}";
        var compacting = context.IsCompacting;

        var operandHelper = context.Helpers
            .GetOrCreate(operandSourceable, prefix, valueType, () => operandSourceable.GenerateSource(context))
            .MethodName;

        // The choices return the index of the operator they matched
        string ChoiceHelper(OneOf<int> choice) => context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(choice, prefix, "int", () => choice.GenerateSource(context))
            .MethodName);

        var levelHelpers = new string[_levels.Length];

        for (var level = 0; level < _levels.Length; level++)
        {
            levelHelpers[level] = ChoiceHelper(_levels[level].Choice);
        }

        var choice = GetChoice();
        var choiceHelper = choice is null ? null : ChoiceHelper(choice);

        var factories = new string[_operatorLevels.Length];

        for (var i = 0; i < factories.Length; i++)
        {
            factories[i] = context.RegisterLambda((Delegate?)_unary[i] ?? _binary[i]!);
        }

        var levelsField = context.RegisterStaticField(
            "private static readonly int[]",
            $"new int[] {{ {string.Join(", ", Array.ConvertAll(_operatorLevels, static level => level.ToString(CultureInfo.InvariantCulture)))} }}");

        var climb = $"climb{id}";
        var lookupName = $"lookup{id}";
        var nextName = $"next{id}";
        var nextPositionName = $"nextPosition{id}";

        result.Body.Add($"var {nextName} = -1;");
        result.Body.Add($"var {nextPositionName} = 0;");
        result.Body.Add($"{result.SuccessVariable} = {climb}({_levels.Length - 1}, ref {result.ValueVariable}, ref {nextName}, ref {nextPositionName}, false, true);");

        // The local functions are each a single statement, so that their returns are not taken for the parser's own
        result.Body.Add(GenerateClimb(climb, lookupName, valueType, operandHelper, levelHelpers, factories, levelsField, ctx, cursor, compacting));
        result.Body.Add(GenerateLookup(lookupName, levelHelpers, choiceHelper, ctx, cursor));

        if (_prefixLevels.Length > 0)
        {
            var idText = id.ToString(CultureInfo.InvariantCulture);
            result.Body.Add(GeneratePrefixes(idText, ctx, compacting));
            result.Body.Add(GeneratePrefixed(climb, idText, valueType, operandHelper, factories, levelsField, ctx, cursor, context.MaxRecursionDepth > 0));

            foreach (var level in _prefixLevels)
            {
                result.Body.Add(GeneratePrefix(level, $"prefix{id}_{level}", levelHelpers[level], prefix, context));
            }
        }

        return result;
    }

    private string GenerateClimb(string climb, string lookup, string valueType, string operandHelper, string[] levelHelpers, string[] factories, string levelsField, string ctx, string cursor, bool compacting)
    {
        var id = climb.Substring("climb".Length);
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        string Switch(string indent, string index, IEnumerable<int> operators, Func<int, string> statement)
        {
            var text = new StringBuilder();
            text.Append(indent).Append("switch (").Append(index).Append(")\n");
            text.Append(indent).Append("{\n");

            foreach (var op in operators)
            {
                text.Append(indent).Append("    case ").Append(op.ToString(CultureInfo.InvariantCulture)).Append(": ").Append(statement(op)).Append(" break;\n");
            }

            text.Append(indent).Append('}');
            return text.ToString();
        }

        IEnumerable<int> OperatorsOf(int level)
        {
            for (var i = 0; i < _levels[level].Operators.Length; i++)
            {
                yield return _levels[level].First + i;
            }
        }

        string Apply(int level, string op, string left, string right)
        {
            var arms = new List<string>();

            foreach (var i in OperatorsOf(level))
            {
                arms.Add($"{i} => {factories[i]}({left}, {right})");
            }

            arms.Add("_ => throw new global::System.InvalidOperationException()");
            return $"({op} switch {{ {string.Join(", ", arms)} }})";
        }

        // With parsed set, the operand is the value of climbed and lookup tells whether the operator following it is still to be
        // looked for, see GeneratePrefixed()
        Line($"bool {climb}(int maxLevel, ref {valueType} climbed, ref int next, ref int nextPosition, bool parsed, bool lookup)");
        Line("{");

        if (compacting)
        {
            Line("    var pin = -1;");
        }

        Line("    var lowest = 0;");
        Line("    if (!parsed)");
        Line("    {");
        Line("        lookup = true;");

        if (_prefixLevels.Length > 0)
        {
            Line($"        var start = {cursor}.Offset;");
            Line($"        var prefixOperator = prefixes{id}(maxLevel);");
            Line("        if (prefixOperator >= 0)");
            Line("        {");
            Line($"            if (!prefixed{id}(prefixOperator, start, ref climbed, ref next, ref nextPosition)) return false;");
            Line("            lookup = false;");
            Line("        }");
            Line($"        else if (!{operandHelper}({ctx}, out climbed)) return false;");
        }
        else
        {
            Line($"        if (!{operandHelper}({ctx}, out climbed)) return false;");
        }

        Line("    }");
        Line("    while (true)");
        Line("    {");
        Line("        if (lookup)");
        Line("        {");

        if (compacting)
        {
            // The operator is read again by the looser levels when it isn't followed by an operand
            Line($"            if (pin < 0) pin = {ctx}.Pin(); else {ctx}.MovePin(pin);");
        }

        Line($"            if (!{lookup}(lowest, ref next, ref nextPosition)) break;");
        Line("            lookup = false;");
        Line("        }");
        Line("        if (next < 0) break;");
        Line($"        var level = {levelsField}[next];");
        Line("        if (level > maxLevel) break;");
        Line("        var position = nextPosition;");

        if (compacting)
        {
            // An operator found by the climb of a tighter level was pinned until it returned
            Line($"        if (pin < 0) pin = {ctx}.PinAt(position); else {ctx}.MovePin(pin, position);");
        }

        Line("        switch (level)");
        Line("        {");

        for (var level = 0; level < _levels.Length; level++)
        {
            var kind = _levels[level].Kind;
            var levelText = level.ToString(CultureInfo.InvariantCulture);
            var nextLevelText = (level + 1).ToString(CultureInfo.InvariantCulture);

            if (kind == OperatorKind.Prefix)
            {
                continue;
            }

            Line($"            case {levelText}:");
            Line("            {");

            if (kind == OperatorKind.Postfix)
            {
                // An operator that didn't read anything would be matched again forever
                Line($"                if ({cursor}.Offset == position)");
                Line("                {");
                Line($"                    lowest = {nextLevelText};");
                Line("                    lookup = true;");
                Line("                    continue;");
                Line("                }");
                Line(Switch("                ", "next", OperatorsOf(level), op => $"climbed = {factories[op]}(climbed);"));
                Line($"                lowest = {levelText};");
                Line("                lookup = true;");
                Line("                continue;");
                Line("            }");
                continue;
            }

            Line("                var op = next;");
            Line($"                {valueType} right = default!;");
            Line($"                if (!{climb}({(level - 1).ToString(CultureInfo.InvariantCulture)}, ref right, ref next, ref nextPosition, false, true) || nextPosition == position)");
            Line("                {");
            Line($"                    {cursor}.ResetPosition(position);");
            Line($"                    lowest = {nextLevelText};");
            Line("                    lookup = true;");
            Line("                    continue;");
            Line("                }");

            if (kind == OperatorKind.RightAssociative)
            {
                var listType = $"global::Parlot.Fluent.HybridList<(int, {valueType})>";

                Line($"                {listType}? operations = null;");
                Line($"                while (next >= 0 && {levelsField}[next] == {levelText})");
                Line("                {");
                Line("                    var operation = next;");
                Line("                    var operationPosition = nextPosition;");

                if (compacting)
                {
                    Line($"                    {ctx}.MovePin(pin, operationPosition);");
                }

                Line($"                    {valueType} operand = default!;");
                Line($"                    if (!{climb}({(level - 1).ToString(CultureInfo.InvariantCulture)}, ref operand, ref next, ref nextPosition, false, true) || nextPosition == operationPosition)");
                Line("                    {");
                Line($"                        {cursor}.ResetPosition(operationPosition);");
                Line($"                        lowest = {nextLevelText};");
                Line("                        lookup = true;");
                Line("                        break;");
                Line("                    }");
                Line($"                    operations ??= new {listType}();");
                Line("                    operations.Add((operation, operand));");
                Line("                }");
                Line("                if (operations is not null)");
                Line("                {");
                Line("                    var accumulated = operations[operations.Count - 1].Item2;");
                Line("                    for (var i = operations.Count - 1; i > 0; i--)");
                Line("                    {");
                Line($"                        accumulated = {Apply(level, "operations[i].Item1", "operations[i - 1].Item2", "accumulated")};");
                Line("                    }");
                Line($"                    right = {Apply(level, "operations[0].Item1", "right", "accumulated")};");
                Line("                }");
            }

            Line(Switch("                ", "op", OperatorsOf(level), op => $"climbed = {factories[op]}(climbed, right);"));
            Line("                continue;");
            Line("            }");
        }

        Line("        }");
        Line("    }");

        if (compacting)
        {
            Line($"    {ctx}.Unpin(pin);");
        }

        Line("    return true;");
        sb.Append('}');

        return sb.ToString();
    }

    private string GenerateLookup(string lookup, string[] levelHelpers, string? choiceHelper, string ctx, string cursor)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"bool {lookup}(int lowest, ref int next, ref int nextPosition)");
        Line("{");
        Line($"    nextPosition = {cursor}.Offset;");

        if (choiceHelper is not null)
        {
            // The choice returns the first operator which matches, from the tightest level
            Line($"    if (lowest <= {_lowestLookupLevel.ToString(CultureInfo.InvariantCulture)})");
            Line("    {");
            Line($"        if ({choiceHelper}({ctx}, out next)) return true;");
            Line("        next = -1;");
            Line("        return false;");
            Line("    }");

            // Only after an operator wasn't followed by an operand, or after a postfix operator
            for (var level = _lowestLookupLevel + 1; level < _levels.Length; level++)
            {
                if (_levels[level].Kind != OperatorKind.Prefix)
                {
                    Line($"    if (lowest <= {level.ToString(CultureInfo.InvariantCulture)} && {levelHelpers[level]}({ctx}, out next)) return true;");
                }
            }
        }

        Line("    next = -1;");
        Line("    return false;");
        sb.Append('}');

        return sb.ToString();
    }

    // The index of an operator of the prefix levels up to maxLevel which reads some text, or -1
    private string GeneratePrefixes(string id, string ctx, bool compacting)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"int prefixes{id}(int maxLevel)");
        Line("{");
        Line("    var prefixOperator = -1;");

        if (compacting)
        {
            // The operators of each level are tried from the same position
            Line($"    var prefixPin = {ctx}.Pin();");
        }

        // The operators of the loosest level are tried first, as its parser in the chain runs first
        for (var i = _prefixLevels.Length - 1; i >= 0; i--)
        {
            var level = _prefixLevels[i];
            Line($"    if (prefixOperator < 0 && maxLevel >= {level.ToString(CultureInfo.InvariantCulture)}) prefix{id}_{level.ToString(CultureInfo.InvariantCulture)}(out prefixOperator);");
        }

        if (compacting)
        {
            Line($"    {ctx}.Unpin(prefixPin);");
        }

        Line("    return prefixOperator;");
        sb.Append('}');

        return sb.ToString();
    }

    // The prefix operators following the first one, then their operand, applied from the last operator like at runtime
    private string GeneratePrefixed(string climb, string id, string valueType, string operandHelper, string[] factories, string levelsField, string ctx, string cursor, bool limitRecursion)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        var prefixOperators = new List<int>();

        foreach (var level in _prefixLevels)
        {
            for (var i = 0; i < _levels[level].Operators.Length; i++)
            {
                prefixOperators.Add(_levels[level].First + i);
            }
        }

        Line($"bool prefixed{id}(int op, int start, ref {valueType} climbed, ref int next, ref int nextPosition)");
        Line("{");
        Line($"    {ListBuilderSource.Declare("operators", "int")}");
        Line("    try");
        Line("    {");
        Line("        do");
        Line("        {");

        // Like at runtime, each operator counts as one level of recursion
        if (limitRecursion)
        {
            Line($"            {ctx}.EnterRecursion();");
        }

        Line("            operators.Add(op);");

        // The next operator is of the level of this one or of a tighter one
        Line($"            op = prefixes{id}({levelsField}[op]);");
        Line("        }");
        Line("        while (op >= 0);");
        Line($"        if (!{operandHelper}({ctx}, out climbed))");
        Line("        {");
        Line($"            {cursor}.ResetPosition(start);");
        Line("            return false;");
        Line("        }");
        Line("        var lookup = true;");
        Line("        for (var i = operators.Count - 1; i >= 0; i--)");
        Line("        {");
        Line("            var prefixOperator = operators[i];");

        // The operators of the levels up to its own that follow the operand apply to it first
        Line($"            {climb}({levelsField}[prefixOperator], ref climbed, ref next, ref nextPosition, true, lookup);");
        Line("            switch (prefixOperator)");
        Line("            {");

        foreach (var op in prefixOperators)
        {
            Line($"                case {op.ToString(CultureInfo.InvariantCulture)}: climbed = {factories[op]}(climbed); break;");
        }

        Line("            }");
        Line("            lookup = false;");
        Line("        }");
        Line("        return true;");
        Line("    }");
        Line("    finally");
        Line("    {");

        if (limitRecursion)
        {
            Line($"        for (var i = 0; i < operators.Count; i++) {ctx}.ExitRecursion();");
        }

        Line("        operators.Release();");
        Line("    }");
        sb.Append('}');

        return sb.ToString();
    }

    private string GeneratePrefix(int level, string name, string levelHelper, string prefix, SourceGenerationContext context)
    {
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var operatorLevel = _levels[level];
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"bool {name}(out int op)");
        Line("{");
        Line($"    var offset = {cursor}.Offset;");
        Line($"    if (!{levelHelper}({ctx}, out op))");
        Line("    {");
        Line("        op = -1;");
        Line("        return false;");
        Line("    }");
        Line($"    if ({cursor}.Offset != offset) return true;");

        if (operatorLevel.Operators.Length > 1)
        {
            // An operator that didn't read anything is treated as no operator, and the next ones are tried
            Line("    var matched = op;");

            for (var i = 1; i < operatorLevel.Operators.Length; i++)
            {
                var wrapper = operatorLevel.Operators[i];
                var sourceable = (ISourceable)wrapper;
                var helper = context.WithDiscardResult(true, () => context.Helpers
                    .GetOrCreate(wrapper, prefix, "int", () => sourceable.GenerateSource(context))
                    .MethodName);
                var index = (operatorLevel.First + i).ToString(CultureInfo.InvariantCulture);

                Line($"    if (matched < {index} && {helper}({ctx}, out _) && {cursor}.Offset != offset)");
                Line("    {");
                Line($"        op = {index};");
                Line("        return true;");
                Line("    }");
            }
        }

        Line("    op = -1;");
        Line("    return false;");
        sb.Append('}');

        return sb.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => Name ?? $"Operators({_operand})";
}
