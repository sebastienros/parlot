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
    /// The <see cref="Func{T1, T2, TResult}"/> of each operator.
    /// </summary>
    public abstract Delegate[] Factories { get; }

    public abstract int Apply(ParseContext context, int position, ref ParseResult<T> result);
}

internal sealed class PostfixGroup<T, TOperator> : PostfixGroup<T>
{
    private readonly Parser<(int Index, TOperator Value)> _choice;
    private readonly Func<T, TOperator, T>[] _factories;

    public PostfixGroup((Parser<TOperator> Op, Func<T, TOperator, T> Factory)[] operators, string parameterName)
    {
        if (operators is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", parameterName);
        }

        var parsers = new Parser<(int, TOperator)>[operators.Length];
        _factories = new Func<T, TOperator, T>[operators.Length];

        for (var i = 0; i < operators.Length; i++)
        {
            var (op, factory) = operators[i];
            parsers[i] = new IndexedOperator<TOperator>(op ?? throw new ArgumentException("Operators must not contain null parsers.", parameterName), i);
            _factories[i] = factory ?? throw new ArgumentException("Operators must not contain null factories.", parameterName);
        }

        // A single operator is invoked directly
        _choice = parsers.Length == 1 ? parsers[0] : new OneOf<(int, TOperator)>(parsers);
    }

    public override object Choice => _choice;

    public override Type ValueType => typeof((int, TOperator));

    public override Delegate[] Factories => _factories;

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

        result.Set(result.Start, op.End, _factories[op.Value.Index](result.Value, op.Value.Value));
        return Applied;
    }
}

/// <summary>
/// The description of a <see cref="ValuedBinary{T, TOperator}"/> for the source generator.
/// </summary>
internal interface IValuedBinary
{
    Type OperatorType { get; }

    Delegate Factory { get; }

    bool IsConditional { get; }

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

    public ValuedBinary(Parser<TOperator>[] operators, Func<T, T, TOperator, T>? factory, Func<T, TOperator, T, T>? conditional, bool rightAssociative, string parameterName)
    {
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
        _factory = factory;
        _conditional = conditional;
        RightAssociative = rightAssociative;
    }

    public Parser<TOperator> Choice { get; }

    public override object[] Choices { get; }

    public bool RightAssociative { get; }

    public Type OperatorType => typeof(TOperator);

    /// <summary>
    /// Whether <see cref="Factory"/> takes the condition, the value of the operator and the right operand, in that order.
    /// </summary>
    public bool IsConditional => _conditional is not null;

    public Delegate Factory => (Delegate?)_factory ?? _conditional!;

    private T Apply(T left, T right, TOperator op) => _factory is not null ? _factory(left, right, op) : _conditional!(left, op, right);

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
            result.Set(result.Start, right.End, Apply(result.Value, right.Value, op.Value));
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
                    accumulated = Apply(operations[i - 1].Operand, accumulated, operations[i].Operator);
                }

                value = Apply(value, accumulated, operations[0].Operator);
            }
        }
        finally
        {
            operations.Release();
        }

        result.Set(result.Start, end, Apply(result.Value, value, op.Value));
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
