using System;

namespace Parlot.Fluent;

/// <summary>
/// The operators of a level of a <see cref="PrecedenceParser{T}"/> whose values are passed to their factories.
/// </summary>
/// <remarks>
/// The lookup of the operator following an operand stops at these levels, and the level which applies them parses them.
/// Their values are then used right away, and never kept for a looser level.
/// </remarks>
/// <typeparam name="T">The type of the operands.</typeparam>
internal abstract class ValuedOperators<T>
{
    /// <summary>
    /// The parsers which are tried when the level is looked up, after an operand.
    /// </summary>
    public abstract object[] Choices { get; }

    /// <summary>
    /// Parses an operator of the level at the cursor, where the operators were looked up, and applies it to
    /// <paramref name="result"/>.
    /// </summary>
    /// <returns>
    /// The level from which the operators are looked up next, at the cursor, or -1 when <paramref name="next"/> is the
    /// operator following the right operand.
    /// </returns>
    public abstract int Apply(PrecedenceParser<T> parser, ParseContext context, int level, int pin, ref ParseResult<T> result, ref PrecedenceParser<T>.Lookahead next);
}

/// <summary>
/// A level of postfix operators whose factories receive the values of the operators.
/// </summary>
internal sealed class ValuedPostfix<T> : ValuedOperators<T>
{
    public ValuedPostfix(PostfixGroup<T>[] groups)
    {
        Groups = groups;
        Choices = Array.ConvertAll(groups, static group => group.Choice);
    }

    /// <summary>
    /// The operators of each type, which are tried in order.
    /// </summary>
    public PostfixGroup<T>[] Groups { get; }

    public override object[] Choices { get; }

    public override int Apply(PrecedenceParser<T> parser, ParseContext context, int level, int pin, ref ParseResult<T> result, ref PrecedenceParser<T>.Lookahead next)
    {
        var position = context.Scanner.Cursor.Offset;

        foreach (var group in Groups)
        {
            switch (group.Apply(context, position, ref result))
            {
                case PostfixGroup<T>.Applied:
                    // The operators of the level are tried again
                    return level;
                case PostfixGroup<T>.Empty:
                    // An operator that didn't read anything would be matched again forever
                    return level + 1;
            }
        }

        return level + 1;
    }
}

/// <summary>
/// The postfix operators of a <see cref="ValuedPostfix{T}"/> whose values have the same type.
/// </summary>
internal abstract class PostfixGroup<T>
{
    public const int NoMatch = -1;
    public const int Empty = 0;
    public const int Applied = 1;

    /// <summary>
    /// The choice of the operators, which returns the index of the one that matched and its value.
    /// </summary>
    public abstract object Choice { get; }

    /// <summary>
    /// The type of the values of <see cref="Choice"/>.
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// The <see cref="Func{T1, T2, TResult}"/> of each operator, or its <see cref="Func{T1, T2, T3, TResult}"/> which
    /// receives the <see cref="ParseContext"/> first when <see cref="WithContext"/> is set.
    /// </summary>
    public abstract Delegate[] Factories { get; }

    /// <summary>
    /// Whether the <see cref="Factories"/> receive the <see cref="ParseContext"/> first.
    /// </summary>
    public abstract bool WithContext { get; }

    public abstract int Apply(ParseContext context, int position, ref ParseResult<T> result);
}

internal sealed class PostfixGroup<T, TOperator> : PostfixGroup<T>
{
    private readonly Parser<(int Index, TOperator Value)> _choice;
    private readonly Func<T, TOperator, T>[]? _factories;
    private readonly Func<ParseContext, T, TOperator, T>[]? _contextFactories;

    public PostfixGroup((Parser<TOperator> Op, Func<T, TOperator, T> Factory)[] operators, string parameterName)
    {
        _choice = IndexedOperator<TOperator>.CreateChoice(operators, parameterName, out _factories, out _);
    }

    public PostfixGroup((Parser<TOperator> Op, Func<ParseContext, T, TOperator, T> Factory)[] operators, string parameterName)
    {
        _choice = IndexedOperator<TOperator>.CreateChoice(operators, parameterName, out _contextFactories, out _);
    }

    public override object Choice => _choice;

    public override Type ValueType => typeof((int, TOperator));

    public override Delegate[] Factories => (Delegate[]?)_factories ?? _contextFactories!;

    public override bool WithContext => _contextFactories is not null;

    public override int Apply(ParseContext context, int position, ref ParseResult<T> result)
    {
        var op = new ParseResult<(int Index, TOperator Value)>();

        if (!_choice.Parse(context, ref op))
        {
            return NoMatch;
        }

        if (context.Scanner.Cursor.Offset == position)
        {
            return Empty;
        }

        var value = _factories is not null
            ? _factories[op.Value.Index](result.Value, op.Value.Value)
            : _contextFactories![op.Value.Index](context, result.Value, op.Value.Value);

        result.Set(result.Start, op.End, value);
        return Applied;
    }
}

/// <summary>
/// A level of prefix operators whose factories receive the values of the operators.
/// </summary>
/// <remarks>
/// Unlike the other valued levels, its operators have indices in the table, and are matched by the loop which reads the
/// prefix operators of all the levels before an operand. The value of an operator is pushed on the
/// <see cref="ListScratch{T}"/> of the thread when it matches, and removed when it applies, after its operand: the
/// operators of a chain apply from the last one, so the values of each type are a stack, and the nested parses of the
/// operands release theirs before.
/// </remarks>
/// <typeparam name="T">The type of the operands.</typeparam>
internal abstract class ValuedPrefix<T>
{
    protected ValuedPrefix(int first)
    {
        First = first;
    }

    /// <summary>
    /// The index of the first operator of the level in the table.
    /// </summary>
    public int First { get; }

    /// <summary>
    /// The number of operators of the level.
    /// </summary>
    public abstract int Count { get; }

    /// <summary>
    /// The choice of the operators, which returns the index of the one that matched in the level and its value.
    /// </summary>
    public abstract object Choice { get; }

    /// <summary>
    /// The parser of each operator, which returns its index in the level and its value.
    /// </summary>
    public abstract object[] Operators { get; }

    /// <summary>
    /// The type of the values of the operators.
    /// </summary>
    public abstract Type OperatorType { get; }

    /// <summary>
    /// The <see cref="Func{T1, T2, TResult}"/> of each operator, or its <see cref="Func{T1, T2, T3, TResult}"/> which
    /// receives the <see cref="ParseContext"/> first when <see cref="WithContext"/> is set.
    /// </summary>
    public abstract Delegate[] Factories { get; }

    /// <summary>
    /// Whether the <see cref="Factories"/> receive the <see cref="ParseContext"/> first.
    /// </summary>
    public abstract bool WithContext { get; }

    /// <summary>
    /// Matches the first operator which reads some text at <paramref name="offset"/>, the cursor, and pushes its value.
    /// <paramref name="result"/> is then the index of the operator in the table.
    /// </summary>
    public abstract bool Match(ParseContext context, int offset, ref ParseResult<int> result);

    /// <summary>
    /// Matches the first prefix operator of an operand, at <paramref name="start"/>, then parses what follows like
    /// <c>PrecedenceParser.ParsePrefixed()</c>. A single operator, the most common case, keeps its value in a local.
    /// </summary>
    /// <returns>-1 when no operator matched, otherwise whether the parse succeeded.</returns>
    public abstract int ParsePrefixed(PrecedenceParser<T> parser, ParseContext context, int start, int top, int pin, ref ParseResult<T> result, ref PrecedenceParser<T>.Lookahead next);

    /// <summary>
    /// Applies the operator <paramref name="op"/>, of the table, with the value on the top of the stack, and removes it.
    /// </summary>
    public abstract T Apply(ParseContext context, int op, T operand);

    /// <summary>
    /// Removes the value on the top of the stack, of an operator which doesn't apply.
    /// </summary>
    public abstract void Discard();
}

internal sealed class ValuedPrefix<T, TOperator> : ValuedPrefix<T>
{
    private readonly Parser<(int Index, TOperator Value)> _choice;
    private readonly IndexedOperator<TOperator>[] _operators;
    private readonly Func<T, TOperator, T>[]? _factories;
    private readonly Func<ParseContext, T, TOperator, T>[]? _contextFactories;

    public ValuedPrefix(int first, (Parser<TOperator> Op, Func<T, TOperator, T> Factory)[] operators, string parameterName) : base(first)
    {
        _choice = IndexedOperator<TOperator>.CreateChoice(operators, parameterName, out _factories, out _operators);
    }

    public ValuedPrefix(int first, (Parser<TOperator> Op, Func<ParseContext, T, TOperator, T> Factory)[] operators, string parameterName) : base(first)
    {
        _choice = IndexedOperator<TOperator>.CreateChoice(operators, parameterName, out _contextFactories, out _operators);
    }

    public override int Count => _operators.Length;

    public override object Choice => _choice;

    public override object[] Operators => _operators;

    public override Type OperatorType => typeof(TOperator);

    public override Delegate[] Factories => (Delegate[]?)_factories ?? _contextFactories!;

    public override bool WithContext => _contextFactories is not null;

    public override bool Match(ParseContext context, int offset, ref ParseResult<int> result)
    {
        var op = new ParseResult<(int Index, TOperator Value)>();

        if (!MatchValue(context, offset, ref op))
        {
            return false;
        }

        ListScratch<TOperator>.Current.Push(op.Value.Value);
        result.Set(op.Start, op.End, First + op.Value.Index);
        return true;
    }

    public override int ParsePrefixed(PrecedenceParser<T> parser, ParseContext context, int start, int top, int pin, ref ParseResult<T> result, ref PrecedenceParser<T>.Lookahead next)
    {
        var op = new ParseResult<(int Index, TOperator Value)>();

        if (!MatchValue(context, start, ref op))
        {
            return -1;
        }

        context.Unpin(pin);

        var first = First + op.Value.Index;
        var operatorResult = new ParseResult<int>();
        var matched = false;

        // With a limit, each operator counts as one level of recursion when it is read, see ParseChain()
        if (context.MaxRecursionDepth == 0)
        {
            // The next operator is of the level of this one or of a tighter one
            top = parser.MatchPrefixes(context, top, ref operatorResult);

            if (top < 0)
            {
                // A single operator keeps its value here, the chain isn't needed
                if (!parser.Operand.Parse(context, ref result))
                {
                    // An operator not followed by an operand fails the parser
                    context.Scanner.Cursor.ResetPosition(start);
                    return 0;
                }

                // Never fails once the operand is parsed
                if (parser.HasFollowingOperators)
                {
                    parser.Climb(context, parser.LevelOf(first), ref result, ref next, parsed: true);
                }

                var value = _factories is not null
                    ? _factories[op.Value.Index](result.Value, op.Value.Value)
                    : _contextFactories![op.Value.Index](context, result.Value, op.Value.Value);

                result.Set(op.Start, result.End, value);
                return 1;
            }

            matched = true;
        }

        // The values of a chain are on the stacks. The one of the second operator, when it is on the same stack, was pushed
        // before the one of the first operator, which must be below it.
        var scratch = ListScratch<TOperator>.Current;

        if (matched && parser.ValuedPrefixOf(operatorResult.Value) is { OperatorType: var type } && type == typeof(TOperator))
        {
            scratch.PushBelowTop(op.Value.Value);
        }
        else
        {
            scratch.Push(op.Value.Value);
        }

        return parser.ParseChain(context, start, op.Start, first, top, matched, ref operatorResult, ref result, ref next) ? 1 : 0;
    }

    // Matches the first operator which reads some text at offset, the cursor
    private bool MatchValue(ParseContext context, int offset, ref ParseResult<(int Index, TOperator Value)> op)
    {
        if (!_choice.Parse(context, ref op))
        {
            return false;
        }

        var cursor = context.Scanner.Cursor;

        if (cursor.Offset == offset)
        {
            // An operator that didn't read anything would be matched again by the operand, forever. It is treated as no
            // operator, and the next ones are tried at the same position.
            var matched = false;

            for (var i = op.Value.Index + 1; i < _operators.Length; i++)
            {
                if (_operators[i].Parse(context, ref op) && cursor.Offset != offset)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    public override T Apply(ParseContext context, int op, T operand)
    {
        var scratch = ListScratch<TOperator>.Current;
        var value = scratch.Peek();
        var index = op - First;

        var result = _factories is not null
            ? _factories[index](operand, value)
            : _contextFactories![index](context, operand, value);

        // The value is removed once the factory returned: when it throws, the chain discards the values it didn't apply
        scratch.Pop();
        return result;
    }

    public override void Discard() => ListScratch<TOperator>.Current.Pop();
}

/// <summary>
/// The description of a <see cref="ValuedBinary{T, TOperator}"/> for the source generator.
/// </summary>
internal interface IValuedBinary
{
    Type OperatorType { get; }

    Delegate Factory { get; }

    bool IsConditional { get; }

    bool WithContext { get; }

    bool RightAssociative { get; }
}

/// <summary>
/// A level of binary operators whose factory receives the value of the operator, or of conditional operators whose
/// factory receives the middle operand.
/// </summary>
internal sealed class ValuedBinary<T, TOperator> : ValuedOperators<T>, IValuedBinary
{
    private readonly Func<T, T, TOperator, T>? _factory;
    private readonly Func<T, TOperator, T, T>? _conditional;
    private readonly Func<ParseContext, T, T, TOperator, T>? _contextFactory;
    private readonly Func<ParseContext, T, TOperator, T, T>? _contextConditional;

    /// <param name="operators">The operators of the level.</param>
    /// <param name="factory">
    /// A <see cref="Func{T1, T2, T3, TResult}"/> of the left operand, the right operand and the value of the operator, or a
    /// conditional's one of the condition, the middle operand and the last operand, which can also receive the
    /// <see cref="ParseContext"/> first.
    /// </param>
    /// <param name="conditional">Whether <paramref name="factory"/> is the one of a conditional.</param>
    /// <param name="rightAssociative">Whether the level is right-associative.</param>
    /// <param name="parameterName">The name of the parameter of the operators, for the exceptions.</param>
    public ValuedBinary(Parser<TOperator>[] operators, Delegate factory, bool conditional, bool rightAssociative, string parameterName)
    {
        // The types of the factories are the same when TOperator is T, so the conditional ones are told apart by the caller
        if (conditional)
        {
            _conditional = factory as Func<T, TOperator, T, T>;
            _contextConditional = factory as Func<ParseContext, T, TOperator, T, T>;
        }
        else
        {
            _factory = factory as Func<T, T, TOperator, T>;
            _contextFactory = factory as Func<ParseContext, T, T, TOperator, T>;
        }

        Factory = factory;

        if (operators is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", parameterName);
        }

        foreach (var op in operators)
        {
            if (op is null)
            {
                throw new ArgumentException("Operators must not contain null parsers.", parameterName);
            }
        }

        // A single operator is invoked directly
        Choice = operators.Length == 1 ? operators[0] : new OneOf<TOperator>(operators);
        Choices = [Choice];
        RightAssociative = rightAssociative;
    }

    public Parser<TOperator> Choice { get; }

    public override object[] Choices { get; }

    public bool RightAssociative { get; }

    public Type OperatorType => typeof(TOperator);

    /// <summary>
    /// Whether <see cref="Factory"/> takes the condition, the value of the operator and the right operand, in that order.
    /// </summary>
    public bool IsConditional => _conditional is not null || _contextConditional is not null;

    /// <summary>
    /// Whether <see cref="Factory"/> receives the <see cref="ParseContext"/> first.
    /// </summary>
    public bool WithContext => _contextFactory is not null || _contextConditional is not null;

    public Delegate Factory { get; }

    private T Apply(ParseContext context, T left, T right, TOperator op)
    {
        if (_factory is not null)
        {
            return _factory(left, right, op);
        }

        if (_conditional is not null)
        {
            return _conditional(left, op, right);
        }

        return _contextFactory is not null ? _contextFactory(context, left, right, op) : _contextConditional!(context, left, op, right);
    }

    public override int Apply(PrecedenceParser<T> parser, ParseContext context, int level, int pin, ref ParseResult<T> result, ref PrecedenceParser<T>.Lookahead next)
    {
        var cursor = context.Scanner.Cursor;
        var position = cursor.Offset;
        var op = new ParseResult<TOperator>();

        if (!Choice.Parse(context, ref op))
        {
            return level + 1;
        }

        var right = new ParseResult<T>();

        // Neither the operator nor the operand read anything, so they would match forever
        if (!parser.Climb(context, level - 1, ref right, ref next) || next.Offset == position)
        {
            // Operator matched but no right operand - rollback operator consumption.
            cursor.ResetPosition(position);
            return level + 1;
        }

        if (!RightAssociative)
        {
            // The operator following the operand is of this level or a looser one
            result.Set(result.Start, right.End, Apply(context, result.Value, right.Value, op.Value));
            return -1;
        }

        // a (op1) b (op2) c is applied as op1(a, op2(b, c)), so the following operators of the level are read first
        var from = -1;
        var end = right.End;
        var value = right.Value;
        var operations = new ListBuilder<(TOperator Operator, T Operand)>();

        try
        {
            while (next.Operator == PrecedenceParser<T>.Lookahead.Valued(level))
            {
                var operationPosition = next.Offset;
                context.MovePin(pin, operationPosition);

                var operation = new ParseResult<TOperator>();

                if (!Choice.Parse(context, ref operation))
                {
                    from = level + 1;
                    break;
                }

                var operand = new ParseResult<T>();

                if (!parser.Climb(context, level - 1, ref operand, ref next) || next.Offset == operationPosition)
                {
                    cursor.ResetPosition(operationPosition);
                    from = level + 1;
                    break;
                }

                operations.Add((operation.Value, operand.Value));
                end = operand.End;
            }

            // The pairs are read from the builder: the factories can parse, and grow the scratch stack
            var count = operations.Count;

            if (count > 0)
            {
                var accumulated = operations[count - 1].Operand;

                for (var i = count - 1; i > 0; i--)
                {
                    accumulated = Apply(context, operations[i - 1].Operand, accumulated, operations[i].Operator);
                }

                value = Apply(context, value, accumulated, operations[0].Operator);
            }
        }
        finally
        {
            operations.Release();
        }

        result.Set(result.Start, end, Apply(context, result.Value, value, op.Value));
        return from;
    }
}

/// <summary>
/// Matches without reading anything when the next char can start an operator of a level of valued operators, so the
/// choice of the lookup can skip the level otherwise. It returns the <see cref="PrecedenceParser{T}.Lookahead.Operator"/>
/// of the level.
/// </summary>
internal sealed class ValuedProbe : Parser<int>, Rewriting.ISeekable, SourceGeneration.ISourceable, ILoopAnalyzable
{
    private readonly int _operator;
    private readonly object[] _choices;

    // The hints of the operators of the level, read when they are asked for since they can start with Deferred parsers
    private ForwardedHints? _hints;

    public ValuedProbe(int op, object[] choices)
    {
        _operator = op;
        _choices = choices;
    }

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, _choices, static choices => SeekableHints.Union(choices));

    // It invokes nothing, and succeeds without reading
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => true;

    public override bool Parse(ParseContext context, ref ParseResult<int> result)
    {
        context.EnterParser(this);

        var offset = context.Scanner.Cursor.Offset;
        result.Set(offset, offset, _operator);

        context.ExitParser(this);
        return true;
    }

    public SourceGeneration.SourceResult GenerateSource(SourceGeneration.SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(int));
        result.Body.Add($"{result.SuccessVariable} = true;");

        if (!context.DiscardResult)
        {
            result.Body.Add($"{result.ValueVariable} = {_operator.ToString(System.Globalization.CultureInfo.InvariantCulture)};");
        }

        return result;
    }

    public override string ToString() => "ValuedProbe";
}
