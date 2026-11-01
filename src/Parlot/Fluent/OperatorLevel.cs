using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

/// <summary>
/// The kind of the operators of an <see cref="OperatorLevel{T}"/>.
/// </summary>
internal enum OperatorKind : byte
{
    Prefix,
    Postfix,
    LeftAssociative,
    RightAssociative,
}

/// <summary>
/// The operators of a <see cref="PrecedenceParser{T}"/> which have the same precedence.
/// </summary>
/// <typeparam name="T">The type of the operands.</typeparam>
internal sealed class OperatorLevel<T>
{
    private OperatorLevel(OperatorKind kind, int first, Parser<int>[] operators, Delegate[] factories)
    {
        Kind = kind;
        First = first;
        Operators = operators;
        Factories = factories;

        // The operators are tried through a choice, which only tries the ones that can start with the next char
        Choice = new OneOf<int>(operators);
    }

    private OperatorLevel(OperatorKind kind, int first, ValuedOperators<T> valued)
    {
        Kind = kind;
        First = first;
        Operators = [];
        Factories = [];
        Valued = valued;
    }

    public OperatorKind Kind { get; }

    /// <summary>
    /// The index of the first operator of this level in its table. The operators return their index in the table.
    /// </summary>
    public int First { get; }

    public Parser<int>[] Operators { get; }

    /// <summary>
    /// <see cref="Func{T, TResult}"/> of the unary operators, <see cref="Func{T1, T2, TResult}"/> of the binary ones.
    /// </summary>
    public Delegate[] Factories { get; }

    /// <summary>
    /// The choice of <see cref="Operators"/>, <see langword="null"/> for the levels of <see cref="Valued"/> operators.
    /// </summary>
    public OneOf<int>? Choice { get; }

    /// <summary>
    /// The operators whose values are passed to their factories, which have no index in the table.
    /// </summary>
    public ValuedOperators<T>? Valued { get; }

    public static OperatorLevel<T> CreateValued(OperatorKind kind, int first, ValuedOperators<T> valued) => new(kind, first, valued);

    public static OperatorLevel<T> Create<TInput, TFactory>(OperatorKind kind, int first, (Parser<TInput> Op, TFactory Factory)[] operators, string parameterName)
        where TFactory : Delegate
    {
        if (operators is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", parameterName);
        }

        var parsers = new Parser<int>[operators.Length];
        var factories = new Delegate[operators.Length];

        for (var i = 0; i < operators.Length; i++)
        {
            var (op, factory) = operators[i];
            parsers[i] = new OperatorIndex<TInput>(op ?? throw new ArgumentException("Operators must not contain null parsers.", parameterName), first + i);
            factories[i] = factory ?? throw new ArgumentException("Operators must not contain null factories.", parameterName);
        }

        return new OperatorLevel<T>(kind, first, parsers, factories);
    }

    /// <summary>
    /// Gets the index of the first operator of a level added after <paramref name="levels"/>.
    /// </summary>
    public static int NextIndex(List<OperatorLevel<T>> levels) =>
        levels.Count == 0 ? 0 : levels[levels.Count - 1].First + levels[levels.Count - 1].Operators.Length;
}
