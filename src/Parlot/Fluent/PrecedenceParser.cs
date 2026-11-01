using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
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

    // The segment of each level. A level of valued operators starts a segment.
    private readonly Segment[] _segments;

    // The only segment of a table without valued operators, or null
    private readonly Segment? _plainSegment;

    // Whether operators can follow an operand, false when the table has only prefix levels, or none
    private readonly bool _hasFollowingOperators;

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
        _segments = new Segment[levels.Length];
        Segment? segment = null;

        // The parser starts with a prefix operator or an operand
        var first = new List<object> { operand };

        for (var level = 0; level < levels.Length; level++)
        {
            var operatorLevel = levels[level];

            if (operatorLevel.Valued is not null)
            {
                // The lookups stop at this level, which parses its operators
                if (segment is not null)
                {
                    segment.End = level;
                }

                segment = new Segment { Valued = level, End = levels.Length };
                _segments[level] = segment;
                continue;
            }

            segment ??= new Segment { End = levels.Length };
            _segments[level] = segment;

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
            else if (segment.FirstLookupLevel == int.MaxValue)
            {
                segment.FirstLookupLevel = level;
            }
        }

        _hasFollowingOperators = prefixLevels.Count < levels.Length;

        _prefixLevels = prefixLevels.ToArray();
        _first = first.ToArray();
        _plainSegment = levels.Length > 0 && _segments[0].End == levels.Length && _segments[0].Valued < 0 ? _segments[0] : null;
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

    /// <summary>
    /// Returns a parser with an additional level of postfix operators whose factories receive the value of the operator,
    /// which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// The operators of the level are applied in turn to the value of the tighter levels, as long as one of them reads some
    /// text, like <see cref="Postfix{TInput}(ValueTuple{Parser{TInput}, Func{T, T}}[])"/>. The level which applies an
    /// operator parses it, after the operators of the tighter levels didn't match, so an operator can parse an expression
    /// of the table, like an index.
    /// </remarks>
    /// <typeparam name="TOperator">The type of the operator parsers.</typeparam>
    /// <param name="list">The operators of the level and the functions creating their result from the operand and the value of the operator.</param>
    /// <returns>A new parser with the additional level.</returns>
    /// <example>
    /// <code>
    /// var expression = Deferred&lt;Expression&gt;();
    /// expression.Parser = primary.Operators()
    ///     .Postfix(
    ///         (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => new Member(x, name)),
    ///         (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => new Index(x, index)))
    ///     .LeftAssociative((Terms.Char('*'), static (a, b) => new Multiply(a, b)));
    /// </code>
    /// </example>
    public PrecedenceParser<T> Postfix<TOperator>(params (Parser<TOperator> op, Func<T, TOperator, T> factory)[] list) =>
        Add(OperatorLevel<T>.CreateValued(OperatorKind.Postfix, OperatorCount, new ValuedPostfix<T>([new PostfixGroup<T, TOperator>(list, nameof(list))])));

    /// <summary>
    /// Returns a parser with an additional level of two postfix operators whose values have different types, and whose
    /// factories receive the value of the operator, which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// The level is the one of <see cref="Postfix{TOperator}(ValueTuple{Parser{TOperator}, Func{T, TOperator, T}}[])"/>,
    /// whose operators are <paramref name="first"/> then <paramref name="second"/>.
    /// </remarks>
    /// <typeparam name="TOperator1">The type of the first operator parser.</typeparam>
    /// <typeparam name="TOperator2">The type of the second operator parser.</typeparam>
    /// <param name="first">The first operator of the level and the function creating its result.</param>
    /// <param name="second">The second operator of the level and the function creating its result.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> Postfix<TOperator1, TOperator2>((Parser<TOperator1> op, Func<T, TOperator1, T> factory) first, (Parser<TOperator2> op, Func<T, TOperator2, T> factory) second) =>
        Add(OperatorLevel<T>.CreateValued(OperatorKind.Postfix, OperatorCount, new ValuedPostfix<T>(
        [
            new PostfixGroup<T, TOperator1>([first], nameof(first)),
            new PostfixGroup<T, TOperator2>([second], nameof(second)),
        ])));

    /// <summary>
    /// Returns a parser with an additional level of left-associative binary operators whose factory receives the value of
    /// the operator, which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Parsers.LeftAssociative{T, TOperator}(Parser{T}, Parser{TOperator}[], Func{T, T, TOperator, T})"/>,
    /// <c>a - b - c</c> is <c>(a - b) - c</c>. The level which applies an operator parses it, after the operators of the
    /// tighter levels didn't match.
    /// </remarks>
    /// <typeparam name="TOperator">The type of the operator parsers.</typeparam>
    /// <param name="operators">The operators of the level.</param>
    /// <param name="factory">The function creating the result from the left operand, the right operand and the value of the operator.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> LeftAssociative<TOperator>(Parser<TOperator>[] operators, Func<T, T, TOperator, T> factory) =>
        Add(OperatorLevel<T>.CreateValued(OperatorKind.LeftAssociative, OperatorCount, new ValuedBinary<T, TOperator>(
            operators, factory ?? throw new ArgumentNullException(nameof(factory)), null, rightAssociative: false, nameof(operators))));

    /// <summary>
    /// Returns a parser with an additional level of right-associative binary operators whose factory receives the value of
    /// the operator, which binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Parsers.RightAssociative{T, TOperator}(Parser{T}, Parser{TOperator}[], Func{T, T, TOperator, T})"/>,
    /// <c>a ^ b ^ c</c> is <c>a ^ (b ^ c)</c>. The level which applies an operator parses it, after the operators of the
    /// tighter levels didn't match.
    /// </remarks>
    /// <typeparam name="TOperator">The type of the operator parsers.</typeparam>
    /// <param name="operators">The operators of the level.</param>
    /// <param name="factory">The function creating the result from the left operand, the right operand and the value of the operator.</param>
    /// <returns>A new parser with the additional level.</returns>
    public PrecedenceParser<T> RightAssociative<TOperator>(Parser<TOperator>[] operators, Func<T, T, TOperator, T> factory) =>
        Add(OperatorLevel<T>.CreateValued(OperatorKind.RightAssociative, OperatorCount, new ValuedBinary<T, TOperator>(
            operators, factory ?? throw new ArgumentNullException(nameof(factory)), null, rightAssociative: true, nameof(operators))));

    /// <summary>
    /// Returns a parser with an additional level of a conditional operator <c>condition ? whenTrue : whenFalse</c>, which
    /// binds less tightly than the existing levels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The condition is a value of the tighter levels. <paramref name="middle"/> is parsed between
    /// <paramref name="question"/> and <paramref name="colon"/>, and is usually the whole expression. The operand after
    /// <paramref name="colon"/> is a value of the tighter levels when the level is left-associative,
    /// <c>a ? b : c ? d : e</c> being <c>(a ? b : c) ? d : e</c>, and can be another conditional when it is
    /// right-associative, <c>a ? b : c ? d : e</c> being <c>a ? b : (c ? d : e)</c>. The operators of the looser levels
    /// apply to the conditional: <c>a ? b : c ?? d</c> is <c>(a ? b : c) ?? d</c> when <c>??</c> is looser.
    /// </para>
    /// <para>
    /// It is the level of binary operators whose operator is <paramref name="question"/>, <paramref name="middle"/> and
    /// <paramref name="colon"/>: when one of them, or the operand that follows, doesn't match, nothing is read, and the
    /// looser levels are tried at the position of <paramref name="question"/>. A <c>?</c> doesn't need to exclude a looser
    /// <c>??</c> operator, whose second char is not a middle operand.
    /// </para>
    /// </remarks>
    /// <typeparam name="TQuestion">The type of the parser before the middle operand.</typeparam>
    /// <typeparam name="TMiddle">The type of the middle operand.</typeparam>
    /// <typeparam name="TColon">The type of the parser after the middle operand.</typeparam>
    /// <param name="question">The parser before the middle operand.</param>
    /// <param name="middle">The parser of the middle operand.</param>
    /// <param name="colon">The parser after the middle operand.</param>
    /// <param name="factory">The function creating the result from the condition, the middle operand and the last operand.</param>
    /// <param name="rightAssociative">Whether the last operand can be another conditional of the level.</param>
    /// <returns>A new parser with the additional level.</returns>
    /// <example>
    /// <code>
    /// var expression = Deferred&lt;Expression&gt;();
    /// expression.Parser = primary.Operators()
    ///     .LeftAssociative((Terms.Text("=="), static (a, b) => new Equal(a, b)))
    ///     .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (condition, whenTrue, whenFalse) => new Conditional(condition, whenTrue, whenFalse));
    /// </code>
    /// </example>
    public PrecedenceParser<T> Ternary<TQuestion, TMiddle, TColon>(Parser<TQuestion> question, Parser<TMiddle> middle, Parser<TColon> colon, Func<T, TMiddle, T, T> factory, bool rightAssociative = true)
    {
        ThrowHelper.ThrowIfNull(question, nameof(question));
        ThrowHelper.ThrowIfNull(middle, nameof(middle));
        ThrowHelper.ThrowIfNull(colon, nameof(colon));
        ThrowHelper.ThrowIfNull(factory, nameof(factory));

        var kind = rightAssociative ? OperatorKind.RightAssociative : OperatorKind.LeftAssociative;
        Parser<TMiddle>[] operators = [question.SkipAnd(middle).AndSkip(colon)];

        return Add(OperatorLevel<T>.CreateValued(kind, OperatorCount, new ValuedBinary<T, TMiddle>(operators, null, factory, rightAssociative, nameof(middle))));
    }

    private int OperatorCount => _operatorLevels.Length;

    private PrecedenceParser<T> Add(OperatorLevel<T> level)
    {
        var levels = new OperatorLevel<T>[_levels.Length + 1];
        _levels.CopyTo(levels, 0);
        levels[_levels.Length] = level;

        return new PrecedenceParser<T>(_operand, levels);
    }

    /// <summary>
    /// Consecutive levels whose operators are looked up through a single choice: a level of valued operators, if any, then
    /// levels without valued operators.
    /// </summary>
    private sealed class Segment
    {
        // The level of valued operators the segment starts with, -1 when there is none
        public int Valued = -1;

        // The tightest level without valued operators whose operators follow an operand, int.MaxValue when there is none. A
        // lookup from it, or from a tighter level, tries all the operators of these levels at once through a choice.
        public int FirstLookupLevel = int.MaxValue;

        // The next level of valued operators, or the number of levels
        public int End;

        // The choice of a lookup from the start of the segment. When it starts with a level of valued operators, the choice
        // starts with a probe of their first chars, which returns the operator of the level.
        public OneOf<int>? Choice;

        // The choice of the levels without valued operators, when the segment starts with a level of valued operators
        public OneOf<int>? PlainChoice;
    }

    // The choice of the operators of the segment which can follow an operand, from the level of valued operators when
    // plain is false, built when it is first used since a table is usually built level by level. Null when there are none.
    private OneOf<int>? GetChoice(Segment segment, bool plain)
    {
        plain = plain && segment.Valued >= 0;
        ref var field = ref plain ? ref segment.PlainChoice : ref segment.Choice;
        var choice = Volatile.Read(ref field);

        if (choice is null)
        {
            var operators = new List<Parser<int>>();

            if (!plain && segment.Valued >= 0)
            {
                // The operators of the level are tried before the ones of the looser levels
                operators.Add(new ValuedProbe(Lookahead.Valued(segment.Valued), _levels[segment.Valued].Valued!.Choices));
            }

            for (var level = segment.FirstLookupLevel; level < segment.End; level++)
            {
                if (_levels[level].Kind != OperatorKind.Prefix)
                {
                    operators.AddRange(_levels[level].Operators);
                }
            }

            if (operators.Count == 0)
            {
                return null;
            }

            choice = new OneOf<int>(operators.ToArray());
            choice = Interlocked.CompareExchange(ref field, choice, null) ?? choice;
        }

        return choice;
    }

    // The segments of the table, in order, for the source generator
    private List<Segment> Segments()
    {
        var segments = new List<Segment>();

        foreach (var segment in _segments)
        {
            if (segments.Count == 0 || segments[segments.Count - 1] != segment)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    /// <inheritdoc />
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        // The prefix operators are tried first, one is followed by an operand only when it moved the cursor
        foreach (var level in _levels)
        {
            if (level.Kind == OperatorKind.Prefix)
            {
                analysis.Invoke(level.Choice!);
            }
        }

        var nullable = analysis.Invoke(_operand);

        // The other operators follow the first operand
        if (nullable)
        {
            foreach (var level in _levels)
            {
                if (level.Valued is not null)
                {
                    foreach (var choice in level.Valued.Choices)
                    {
                        analysis.Invoke(choice);
                    }
                }
                else if (level.Kind != OperatorKind.Prefix)
                {
                    analysis.Invoke(level.Choice!);
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
    internal struct Lookahead
    {
        public const int None = -1;

        // The index of the operator, None when none follows, or Valued(level) when the operators of a level of valued
        // operators are next, with the cursor where they were looked up
        public int Operator;

        // Where the operator starts, or where none was found
        public int Offset;

        // Where the operator ends
        public int End;

        /// <summary>
        /// The <see cref="Operator"/> of the operators of a valued level, and the level of such an operator.
        /// </summary>
        public static int Valued(int level) => -2 - level;
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
    internal bool Climb(ParseContext context, int maxLevel, ref ParseResult<T> result, ref Lookahead next, bool parsed = false, bool lookup = true)
    {
        if (!parsed && !ParseOperand(context, maxLevel, ref result, ref next, out lookup))
        {
            return false;
        }

        if (!_hasFollowingOperators)
        {
            // There is nothing to look up after the operand
            next.Operator = Lookahead.None;
            next.Offset = context.Scanner.Cursor.Offset;
            return true;
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
                // No operator, or the operators of a valued level
                if (op == Lookahead.None || !ApplyValued(context, maxLevel, ref pin, ref result, ref next, ref lowest))
                {
                    break;
                }

                lookup = lowest >= 0;
                continue;
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
    /// Applies the operators of the valued level of <paramref name="next"/>, which are parsed by the level that applies
    /// them, such that their values are only used here. Returns false when a looser level applies them, otherwise
    /// <paramref name="lowest"/> is the level from which the operators are looked up next, or -1 when
    /// <paramref name="next"/> is the operator that follows.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ApplyValued(ParseContext context, int maxLevel, ref int pin, ref ParseResult<T> result, ref Lookahead next, ref int lowest)
    {
        var level = Lookahead.Valued(next.Operator);

        if (level > maxLevel)
        {
            // A looser level applies them
            return false;
        }

        // The operators are read again by the looser levels when they don't match
        if (pin < 0)
        {
            pin = context.PinAt(next.Offset);
        }
        else
        {
            context.MovePin(pin, next.Offset);
        }

        lowest = _levels[level].Valued!.Apply(this, context, level, pin, ref result, ref next);
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

            // The operators of each level are tried from the same position
            var pin = context.Pin();

            // The operators of the loosest level are tried first, as its parser in the chain runs first. The first operator is
            // matched here rather than in MatchPrefixes(): most operands have none, and their code then has no call to set up.
            for (; i >= 0; i--)
            {
                var operatorResult = new ParseResult<int>();

                if (MatchPrefix(context, _levels[prefixLevels[i]], start, ref operatorResult))
                {
                    context.Unpin(pin);
                    lookup = false;
                    return ParsePrefixed(context, start, i, ref operatorResult, ref result, ref next);
                }
            }

            context.Unpin(pin);
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
        var resultStart = operatorResult.Start;
        var first = operatorResult.Value;

        // With a limit, each operator counts as one level of recursion when it is read, see ParseChain()
        if (context.MaxRecursionDepth > 0)
        {
            return ParseChain(context, start, resultStart, first, top, limitRecursion: true, ref operatorResult, ref result, ref next);
        }

        // The next operator is of the level of this one or of a tighter one
        top = MatchPrefixes(context, top, ref operatorResult);

        if (top >= 0)
        {
            return ParseChain(context, start, resultStart, first, top, limitRecursion: false, ref operatorResult, ref result, ref next);
        }

        // A single operator, the most common case, needs no list
        if (!_operand.Parse(context, ref result))
        {
            // An operator not followed by an operand fails the parser
            context.Scanner.Cursor.ResetPosition(start);
            return false;
        }

        // Never fails once the operand is parsed. Without operators that can follow an operand, the climb only returns it.
        if (_hasFollowingOperators)
        {
            Climb(context, _operatorLevels[first], ref result, ref next, parsed: true);
        }

        // The result spans the operator and the operand
        result.Set(resultStart, result.End, _unary[first]!(result.Value));
        return true;
    }

    /// <summary>
    /// Parses a chain of prefix operators from <paramref name="first"/>. Without <paramref name="limitRecursion"/>, the second
    /// operator was matched, in <paramref name="operatorResult"/> and the level <c>_prefixLevels[top]</c>. With it, only the
    /// first one was, and each one counts as one level of recursion, as when the operand of each one was climbed recursively.
    /// </summary>
    private bool ParseChain(ParseContext context, int start, int resultStart, int first, int top, bool limitRecursion, ref ParseResult<int> operatorResult, ref ParseResult<T> result, ref Lookahead next)
    {
        var operators = new OperatorList(stackalloc int[OperatorList.InlineCapacity]);

        try
        {
            if (limitRecursion)
            {
                context.EnterRecursion();
            }

            operators.Add(first);

            if (limitRecursion)
            {
                top = MatchPrefixes(context, top, ref operatorResult);
            }

            while (top >= 0)
            {
                if (limitRecursion)
                {
                    context.EnterRecursion();
                }

                operators.Add(operatorResult.Value);

                // The next operator is of the level of this one or of a tighter one
                top = MatchPrefixes(context, top, ref operatorResult);
            }

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
                if (_hasFollowingOperators)
                {
                    Climb(context, _operatorLevels[op], ref result, ref next, parsed: true, lookup);
                }

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
                context.ExitRecursion(operators.Count);
            }

            operators.Release();
        }
    }

    /// <summary>
    /// Matches the first operator of a prefix level which reads some text.
    /// </summary>
    private static bool MatchPrefix(ParseContext context, OperatorLevel<T> level, int offset, ref ParseResult<int> result)
    {
        if (!level.Choice!.Parse(context, ref result))
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
    /// Looks for an operator following an operand, from the level <paramref name="lowest"/>. The lookup stops at a level
    /// of valued operators, which the level that applies them parses.
    /// </summary>
    private bool Lookup(ParseContext context, int lowest, ref Lookahead next)
    {
        next.Offset = context.Scanner.Cursor.Offset;

        var operatorResult = new ParseResult<int>();

        if (_plainSegment is { } plain)
        {
            if (lowest <= plain.FirstLookupLevel)
            {
                // The choice returns the first operator which matches, from the tightest level
                var plainChoice = Volatile.Read(ref plain.Choice) ?? GetChoice(plain, plain: true);

                if (plainChoice is null || !plainChoice.Parse(context, ref operatorResult))
                {
                    next.Operator = Lookahead.None;
                    return false;
                }
            }
            else if (!LookupFrom(context, lowest, _levels.Length, ref operatorResult))
            {
                next.Operator = Lookahead.None;
                return false;
            }

            next.Operator = operatorResult.Value;
            next.End = operatorResult.End;
            return true;
        }

        var level = lowest;

        while (level < _levels.Length)
        {
            var segment = _segments[level];
            OneOf<int>? choice;

            if (level == segment.Valued)
            {
                // The probe of the valued operators, then the operators of the looser levels of the segment
                choice = Volatile.Read(ref segment.Choice) ?? GetChoice(segment, plain: false);
            }
            else if (level <= segment.FirstLookupLevel)
            {
                // The choice returns the first operator which matches, from the tightest level
                choice = Volatile.Read(ref segment.Valued >= 0 ? ref segment.PlainChoice : ref segment.Choice) ?? GetChoice(segment, plain: true);
            }
            else
            {
                if (LookupFrom(context, level, segment.End, ref operatorResult))
                {
                    next.Operator = operatorResult.Value;
                    next.End = operatorResult.End;
                    return true;
                }

                level = segment.End;
                continue;
            }

            if (choice is not null && choice.Parse(context, ref operatorResult))
            {
                if (operatorResult.Value < 0)
                {
                    // The probe of a level of valued operators, whose choice may have skipped white space
                    context.Scanner.Cursor.ResetPosition(next.Offset);
                }

                next.Operator = operatorResult.Value;
                next.End = operatorResult.End;
                return true;
            }

            level = segment.End;
        }

        next.Operator = Lookahead.None;
        return false;
    }

    // Only after an operator wasn't followed by an operand, or after a postfix operator
    private bool LookupFrom(ParseContext context, int lowest, int end, ref ParseResult<int> result)
    {
        for (var level = lowest; level < end; level++)
        {
            var operatorLevel = _levels[level];

            if (operatorLevel.Kind != OperatorKind.Prefix && operatorLevel.Choice!.Parse(context, ref result))
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

        // The choices return the index of the operator they matched, or the value of a valued operator
        string Helper(object choice, Type type)
        {
            if (choice is not ISourceable sourceable)
            {
                throw new NotSupportedException("Operators require all operator parsers to be source-generatable.");
            }

            return context.WithDiscardResult(false, () => context.Helpers
                .GetOrCreate(choice, prefix, SourceGenerationContext.GetTypeName(type), () => sourceable.GenerateSource(context))
                .MethodName);
        }

        string ChoiceHelper(OneOf<int> choice) => Helper(choice, typeof(int));

        var levelHelpers = new string?[_levels.Length];
        var valuedCases = new string?[_levels.Length];

        for (var level = 0; level < _levels.Length; level++)
        {
            if (_levels[level].Choice is { } levelChoice)
            {
                levelHelpers[level] = ChoiceHelper(levelChoice);
            }
        }

        var segmentHelpers = new Dictionary<Segment, (string? Choice, string? Plain)>();

        if (_hasFollowingOperators)
        {
            foreach (var segment in Segments())
            {
                var segmentChoice = GetChoice(segment, plain: false);
                var plainChoice = segment.Valued >= 0 ? GetChoice(segment, plain: true) : null;
                segmentHelpers[segment] = (segmentChoice is null ? null : ChoiceHelper(segmentChoice), plainChoice is null ? null : ChoiceHelper(plainChoice));
            }
        }

        var factories = new string[_operatorLevels.Length];

        for (var i = 0; i < factories.Length; i++)
        {
            factories[i] = context.RegisterLambda((Delegate?)_unary[i] ?? _binary[i]!);
        }

        // The level of each operator, read by the climb when operators can follow an operand, and by chains of operators of
        // several prefix levels
        var levelsField = _hasFollowingOperators || _prefixLevels.Length > 1
            ? context.RegisterStaticField(
                "private static readonly int[]",
                $"new int[] {{ {string.Join(", ", Array.ConvertAll(_operatorLevels, static level => level.ToString(CultureInfo.InvariantCulture)))} }}")
            : null;

        var climb = $"climb{id}";
        var lookupName = $"lookup{id}";
        var nextName = $"next{id}";
        var nextPositionName = $"nextPosition{id}";

        result.Body.Add($"var {nextName} = -1;");
        result.Body.Add($"var {nextPositionName} = 0;");
        result.Body.Add($"{result.SuccessVariable} = {climb}({_levels.Length - 1}, ref {result.ValueVariable}, ref {nextName}, ref {nextPositionName}, false, true);");

        // The local functions are each a single statement, so that their returns are not taken for the parser's own
        var chains = new List<string>();

        for (var level = 0; level < _levels.Length; level++)
        {
            switch (_levels[level].Valued)
            {
                case ValuedPostfix<T> postfix:
                    valuedCases[level] = GenerateValuedPostfix(level, postfix, Helper, context);
                    break;
                case { } binary:
                    valuedCases[level] = GenerateValuedBinary(level, binary, Helper, climb, valueType, context, out var chain);

                    if (chain is not null)
                    {
                        chains.Add(chain);
                    }

                    break;
            }
        }

        result.Body.Add(GenerateClimb(climb, lookupName, valueType, operandHelper, levelHelpers, valuedCases, factories, levelsField, ctx, cursor, compacting));

        foreach (var chain in chains)
        {
            result.Body.Add(chain);
        }

        if (_hasFollowingOperators)
        {
            result.Body.Add(GenerateLookup(lookupName, levelHelpers, segmentHelpers, ctx, cursor));
        }

        if (_prefixLevels.Length > 0)
        {
            var idText = id.ToString(CultureInfo.InvariantCulture);
            result.Body.Add(GeneratePrefixed(climb, idText, valueType, operandHelper, factories, levelsField, ctx, cursor, compacting, context.MaxRecursionDepth > 0));

            foreach (var level in _prefixLevels)
            {
                result.Body.Add(GeneratePrefix(level, $"prefix{id}_{level}", levelHelpers[level]!, prefix, context));
            }
        }

        return result;
    }

    // The climb of the levels up to maxLevel. Without operators that can follow an operand, it parses the prefix operators
    // and the operand only, and the lookup isn't generated.
    private string GenerateClimb(string climb, string lookup, string valueType, string operandHelper, string?[] levelHelpers, string?[] valuedCases, string[] factories, string? levelsField, string ctx, string cursor, bool compacting)
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

        var follows = _hasFollowingOperators;

        // With parsed set, the operand is the value of climbed and lookup tells whether the operator following it is still to be
        // looked for, see GeneratePrefixed(). Without operators that can follow an operand, parsed is never set.
        Line($"bool {climb}(int maxLevel, ref {valueType} climbed, ref int next, ref int nextPosition, bool parsed, bool lookup)");
        Line("{");

        if (!follows)
        {
            // No operator can follow the operand, and its switch would be empty
            if (_prefixLevels.Length > 0)
            {
                Line($"    var start = {cursor}.Offset;");
                Line("    var prefixOperator = -1;");

                foreach (var line in MatchPrefixLines("    ", id, ctx, compacting, "maxLevel", "prefixOperator"))
                {
                    Line(line);
                }

                Line($"    if (prefixOperator >= 0) return prefixed{id}(prefixOperator, start, ref climbed, ref next, ref nextPosition);");
            }

            Line($"    return {operandHelper}({ctx}, out climbed);");
            sb.Append('}');

            return sb.ToString();
        }

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
            // The first operator is matched here rather than in prefixes(): most operands have none, and their code then has
            // no call to set up
            Line($"        var start = {cursor}.Offset;");
            Line("        var prefixOperator = -1;");

            foreach (var line in MatchPrefixLines("        ", id, ctx, compacting, "maxLevel", "prefixOperator"))
            {
                Line(line);
            }

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
        Line("        if (next == -1) break;");

        // The operators of a valued level are parsed by the level which applies them
        Line($"        var level = next >= 0 ? {levelsField}[next] : -2 - next;");
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

            if (valuedCases[level] is { } valuedCase)
            {
                sb.Append(valuedCase);
                Line("            }");
                continue;
            }

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

    private string GenerateLookup(string lookup, string?[] levelHelpers, Dictionary<Segment, (string? Choice, string? Plain)> segmentHelpers, string ctx, string cursor)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        Line($"bool {lookup}(int lowest, ref int next, ref int nextPosition)");
        Line("{");
        Line($"    nextPosition = {cursor}.Offset;");

        foreach (var segment in Segments())
        {
            var (choiceHelper, plainHelper) = segmentHelpers[segment];
            var condition = "if";

            if (segment.Valued >= 0)
            {
                // The probe of the valued operators, then the operators of the looser levels of the segment. The probe
                // returns the operator of the level, and its choice may have skipped white space.
                Line($"    if (lowest <= {Text(segment.Valued)})");
                Line("    {");
                Line($"        if ({choiceHelper}({ctx}, out next))");
                Line("        {");
                Line($"            if (next < 0) {cursor}.ResetPosition(nextPosition);");
                Line("            return true;");
                Line("        }");
                Line("    }");
                condition = "else if";
            }
            else
            {
                plainHelper = choiceHelper;
            }

            if (plainHelper is null)
            {
                continue;
            }

            // The choice returns the first operator which matches, from the tightest level
            Line($"    {condition} (lowest <= {Text(segment.FirstLookupLevel)})");
            Line("    {");
            Line($"        if ({plainHelper}({ctx}, out next)) return true;");
            Line("    }");

            // Only after an operator wasn't followed by an operand, or after a postfix operator
            var first = true;

            for (var other = segment.FirstLookupLevel + 1; other < segment.End; other++)
            {
                if (_levels[other].Kind == OperatorKind.Prefix)
                {
                    continue;
                }

                if (first)
                {
                    Line("    else");
                    Line("    {");
                    first = false;
                }

                Line($"        if (lowest <= {Text(other)} && {levelHelpers[other]}({ctx}, out next)) return true;");
            }

            if (!first)
            {
                Line("    }");
            }
        }

        Line("    next = -1;");
        Line("    return false;");
        sb.Append('}');

        return sb.ToString();
    }

    // Sets target, which is -1, to the index of an operator of the prefix levels up to maxLevel which reads some text, or
    // leaves it at -1. A constant maxLevel is compared when the code is generated.
    private List<string> MatchPrefixLines(string indent, string id, string ctx, bool compacting, string maxLevel, string target)
    {
        var lines = new List<string>();
        var constant = int.TryParse(maxLevel, NumberStyles.None, CultureInfo.InvariantCulture, out var max);

        if (compacting)
        {
            // The operators of each level are tried from the same position
            lines.Add($"{indent}var {target}Pin = {ctx}.Pin();");
        }

        // The operators of the loosest level are tried first, as its parser in the chain runs first
        for (var i = _prefixLevels.Length - 1; i >= 0; i--)
        {
            if (constant && _prefixLevels[i] > max)
            {
                continue;
            }

            var level = _prefixLevels[i].ToString(CultureInfo.InvariantCulture);
            var condition = constant ? $"{target} < 0" : $"{target} < 0 && {maxLevel} >= {level}";
            lines.Add($"{indent}if ({condition}) prefix{id}_{level}(out {target});");
        }

        if (compacting)
        {
            lines.Add($"{indent}{ctx}.Unpin({target}Pin);");
        }

        return lines;
    }

    // The prefix operators following the first one, then their operand, applied from the last operator like at runtime
    private string GeneratePrefixed(string climb, string id, string valueType, string operandHelper, string[] factories, string? levelsField, string ctx, string cursor, bool compacting, bool limitRecursion)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        // The level of an operator, which is the only prefix level when there is one
        string LevelOf(string op) => _prefixLevels.Length == 1 ? _prefixLevels[0].ToString(CultureInfo.InvariantCulture) : $"{levelsField}[{op}]";

        // Sets target to the next operator, which is of the level of op or of a tighter one, or to -1. The operators are matched
        // inline, as the climb matches the first one, rather than in a function called for each operator.
        void MatchNext(string indent, string op, string target, string assignment)
        {
            var level = LevelOf(op);

            if (_prefixLevels.Length > 1)
            {
                Line($"{indent}var {target}Level = {level};");
                level = $"{target}Level";
            }

            Line($"{indent}{assignment} = -1;");

            foreach (var line in MatchPrefixLines(indent, id, ctx, compacting, level, target))
            {
                Line(line);
            }
        }

        // Without operators that can follow an operand, the climb of an operator only returns its operand
        var follows = _hasFollowingOperators;
        var prefixOperators = new List<int>();

        foreach (var level in _prefixLevels)
        {
            for (var i = 0; i < _levels[level].Operators.Length; i++)
            {
                prefixOperators.Add(_levels[level].First + i);
            }
        }

        void Apply(string indent, string op)
        {
            Line($"{indent}switch ({op})");
            Line($"{indent}{{");

            foreach (var prefixOperator in prefixOperators)
            {
                Line($"{indent}    case {prefixOperator.ToString(CultureInfo.InvariantCulture)}: climbed = {factories[prefixOperator]}(climbed); break;");
            }

            Line($"{indent}}}");
        }

        Line($"bool prefixed{id}(int op, int start, ref {valueType} climbed, ref int next, ref int nextPosition)");
        Line("{");

        // Like at runtime, each operator counts as one level of recursion. An exception fails the whole parse, and the context
        // isn't used again, so the levels are only left on a return.
        if (limitRecursion)
        {
            Line($"    {ctx}.EnterRecursion();");
        }

        // The next operator is of the level of this one or of a tighter one
        MatchNext("    ", "op", "second", "var second");
        Line("    if (second < 0)");
        Line("    {");

        // A single operator, the most common case, needs no list. The operators of the levels up to its own that follow the
        // operand apply to it first.
        Line($"        if (!{operandHelper}({ctx}, out climbed))");
        Line("        {");
        Line($"            {cursor}.ResetPosition(start);");

        if (limitRecursion)
        {
            Line($"            {ctx}.ExitRecursion();");
        }

        Line("            return false;");
        Line("        }");

        if (follows)
        {
            Line($"        {climb}({LevelOf("op")}, ref climbed, ref next, ref nextPosition, true, true);");
        }

        Apply("        ", "op");

        if (limitRecursion)
        {
            Line($"        {ctx}.ExitRecursion();");
        }

        Line("        return true;");
        Line("    }");
        Line($"    var operators = new global::Parlot.Fluent.OperatorList(stackalloc int[{OperatorList.InlineCapacity.ToString(CultureInfo.InvariantCulture)}]);");
        Line("    try");
        Line("    {");
        Line("        operators.Add(op);");
        Line("        op = second;");
        Line("        do");
        Line("        {");

        if (limitRecursion)
        {
            Line($"            {ctx}.EnterRecursion();");
        }

        Line("            operators.Add(op);");
        MatchNext("            ", "op", "op", "op");
        Line("        }");
        Line("        while (op >= 0);");
        Line($"        if (!{operandHelper}({ctx}, out climbed))");
        Line("        {");
        Line($"            {cursor}.ResetPosition(start);");
        Line("            return false;");
        Line("        }");

        if (follows)
        {
            Line("        var lookup = true;");
        }

        Line("        for (var i = operators.Count - 1; i >= 0; i--)");
        Line("        {");
        Line("            var prefixOperator = operators[i];");

        if (follows)
        {
            Line($"            {climb}({LevelOf("prefixOperator")}, ref climbed, ref next, ref nextPosition, true, lookup);");
        }

        Apply("            ", "prefixOperator");

        if (follows)
        {
            Line("            lookup = false;");
        }

        Line("        }");
        Line("        return true;");
        Line("    }");
        Line("    finally");
        Line("    {");

        if (limitRecursion)
        {
            Line($"        {ctx}.ExitRecursion(operators.Count);");
        }

        Line("        operators.Release();");
        Line("    }");
        sb.Append('}');

        return sb.ToString();
    }

    // The case of the climb's switch for a level of valued postfix operators, which are parsed and applied there
    private static string GenerateValuedPostfix(int level, ValuedPostfix<T> postfix, Func<object, Type, string> helper, SourceGenerationContext context)
    {
        var cursor = context.CursorName;
        var ctx = context.ParseContextName;
        var levelText = level.ToString(CultureInfo.InvariantCulture);
        var nextLevelText = (level + 1).ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        for (var g = 0; g < postfix.Groups.Length; g++)
        {
            var group = postfix.Groups[g];
            var name = $"postfix{g.ToString(CultureInfo.InvariantCulture)}";
            var groupHelper = helper(group.Choice, group.ValueType);

            Line($"                if ({groupHelper}({ctx}, out var {name}))");
            Line("                {");

            // An operator that didn't read anything would be matched again forever
            Line($"                    if ({cursor}.Offset == position)");
            Line("                    {");
            Line($"                        lowest = {nextLevelText};");
            Line("                        lookup = true;");
            Line("                        continue;");
            Line("                    }");

            if (group.Factories.Length == 1)
            {
                Line($"                    climbed = {context.RegisterLambda(group.Factories[0])}(climbed, {name}.Item2);");
            }
            else
            {
                Line($"                    switch ({name}.Item1)");
                Line("                    {");

                for (var i = 0; i < group.Factories.Length; i++)
                {
                    Line($"                        case {i.ToString(CultureInfo.InvariantCulture)}: climbed = {context.RegisterLambda(group.Factories[i])}(climbed, {name}.Item2); break;");
                }

                Line("                    }");
            }

            Line($"                    lowest = {levelText};");
            Line("                    lookup = true;");
            Line("                    continue;");
            Line("                }");
        }

        Line($"                lowest = {nextLevelText};");
        Line("                lookup = true;");
        Line("                continue;");

        return sb.ToString();
    }

    // The case of the climb's switch for a level of valued binary operators, which are parsed and applied there. The
    // operations which follow the right operand of a right-associative level are read by chain, a local function of their
    // own: the builder they are kept in is released in a finally block, and exception handling in the climb would slow
    // down every operand of the table, whether the level's operators are used or not.
    private static string GenerateValuedBinary(int level, ValuedOperators<T> valued, Func<object, Type, string> helper, string climb, string valueType, SourceGenerationContext context, out string? chain)
    {
        var binary = (IValuedBinary)valued;
        var cursor = context.CursorName;
        var ctx = context.ParseContextName;
        var levelText = level.ToString(CultureInfo.InvariantCulture);
        var tighterText = (level - 1).ToString(CultureInfo.InvariantCulture);
        var nextLevelText = (level + 1).ToString(CultureInfo.InvariantCulture);
        var valuedText = Lookahead.Valued(level).ToString(CultureInfo.InvariantCulture);
        var operatorHelper = helper(valued.Choices[0], binary.OperatorType);
        var factory = context.RegisterLambda(binary.Factory);
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        // A conditional's factory takes the middle operand before the last one
        string Apply(string left, string right, string op) =>
            binary.IsConditional ? $"{factory}({left}, {op}, {right})" : $"{factory}({left}, {right}, {op})";

        Line($"                if (!{operatorHelper}({ctx}, out var op))");
        Line("                {");
        Line($"                    lowest = {nextLevelText};");
        Line("                    lookup = true;");
        Line("                    continue;");
        Line("                }");
        Line($"                {valueType} right = default!;");
        Line($"                if (!{climb}({tighterText}, ref right, ref next, ref nextPosition, false, true) || nextPosition == position)");
        Line("                {");
        Line($"                    {cursor}.ResetPosition(position);");
        Line($"                    lowest = {nextLevelText};");
        Line("                    lookup = true;");
        Line("                    continue;");
        Line("                }");

        chain = null;

        if (binary.RightAssociative)
        {
            var chainName = $"chain{climb.Substring("climb".Length)}_{levelText}";
            var pinArgument = context.IsCompacting ? ", pin" : "";

            // The chain returns true when it stopped at an operator which isn't followed by an operand
            Line($"                if (next == {valuedText} && {chainName}(ref right, ref next, ref nextPosition{pinArgument}))");
            Line("                {");
            Line($"                    lowest = {nextLevelText};");
            Line("                    lookup = true;");
            Line("                }");

            chain = GenerateValuedChain(chainName, valuedText, operatorHelper, binary, climb, tighterText, valueType, Apply, context);
        }

        Line($"                climbed = {Apply("climbed", "right", "op")};");
        Line("                continue;");

        return sb.ToString();
    }

    // The local function reading the operations which follow the right operand of a right-associative level of valued
    // binary operators, and folding them into it
    private static string GenerateValuedChain(string name, string valuedText, string operatorHelper, IValuedBinary binary, string climb, string tighterText, string valueType, Func<string, string, string, string> apply, SourceGenerationContext context)
    {
        var cursor = context.CursorName;
        var ctx = context.ParseContextName;
        var elementType = $"({SourceGenerationContext.GetTypeName(binary.OperatorType)}, {valueType})";
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"bool {name}(ref {valueType} right, ref int next, ref int nextPosition{(context.IsCompacting ? ", int pin" : "")})");
        Line("{");
        Line("    var stopped = false;");
        Line($"    {ListBuilderSource.Declare("operations", elementType)}");
        Line("    try");
        Line("    {");
        Line($"        while (next == {valuedText})");
        Line("        {");
        Line("            var operationPosition = nextPosition;");

        if (context.IsCompacting)
        {
            Line($"            {ctx}.MovePin(pin, operationPosition);");
        }

        Line($"            if (!{operatorHelper}({ctx}, out var operation))");
        Line("            {");
        Line("                stopped = true;");
        Line("                break;");
        Line("            }");
        Line($"            {valueType} operand = default!;");
        Line($"            if (!{climb}({tighterText}, ref operand, ref next, ref nextPosition, false, true) || nextPosition == operationPosition)");
        Line("            {");
        Line($"                {cursor}.ResetPosition(operationPosition);");
        Line("                stopped = true;");
        Line("                break;");
        Line("            }");
        Line("            operations.Add((operation, operand));");
        Line("        }");

        // The pairs are read from the builder: the factories can parse, and grow the scratch stack
        Line("        var count = operations.Count;");
        Line("        if (count > 0)");
        Line("        {");
        Line("            var accumulated = operations[count - 1].Item2;");
        Line("            for (var i = count - 1; i > 0; i--)");
        Line("            {");
        Line($"                accumulated = {apply("operations[i - 1].Item2", "accumulated", "operations[i].Item1")};");
        Line("            }");
        Line($"            right = {apply("right", "accumulated", "operations[0].Item1")};");
        Line("        }");
        Line("    }");
        Line("    finally");
        Line("    {");
        Line("        operations.Release();");
        Line("    }");
        Line("    return stopped;");
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
