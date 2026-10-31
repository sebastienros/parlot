using System.Collections.Generic;
using System.Threading;

namespace Parlot.Fluent;

/// <summary>
/// A parser applying operators to the values of another one, like <see cref="LeftAssociative{T, TInput}"/>, which can be
/// combined with the parser it reads its operands with into a single <see cref="PrecedenceParser{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A chain of such parsers, each one reading its operands with the next one, parses the same values as the
/// <see cref="PrecedenceParser{T}"/> made of their levels. The precedence parser looks for an operator once per position, where
/// the chain tries the operators of each level from the tightest one. The parsers of the chain parse through the precedence
/// parser, unless the parse is instrumented (<see cref="ParseContext.IsInstrumented"/>), which only the chain reproduces.
/// </para>
/// <para>
/// Only the levels whose callbacks don't receive the <see cref="ParseContext"/> are combined: the precedence parser reads the
/// operator following an operand before it calls the callbacks of the levels that end before it, which they could observe.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the operands.</typeparam>
internal interface IOperatorLevels<T>
{
    /// <summary>
    /// The parser the operands are read with.
    /// </summary>
    Parser<T> Operand { get; }

    /// <summary>
    /// Adds the levels of this parser to <paramref name="levels"/>, which holds the levels of <see cref="Operand"/>.
    /// </summary>
    void AddLevels(List<OperatorLevel<T>> levels);
}

/// <summary>
/// Holds the <see cref="PrecedenceParser{T}"/> combining a chain of <see cref="IOperatorLevels{T}"/> parsers.
/// </summary>
internal static class OperatorLevels
{
    /// <summary>
    /// Gets whether a parser reading its operands with <paramref name="operand"/> can be combined with it.
    /// </summary>
    public static bool CanFlatten<T>(Parser<T> operand) => operand is IOperatorLevels<T>;

    /// <summary>
    /// Gets the precedence parser of the chain starting with <paramref name="owner"/>, which is only built when it is used:
    /// the parsers of a chain usually only parse through the precedence parser of the outer one.
    /// </summary>
    public static PrecedenceParser<T> GetFlattened<T>(ref PrecedenceParser<T>? field, IOperatorLevels<T> owner)
    {
        var flattened = Volatile.Read(ref field);

        if (flattened is null)
        {
            flattened = Flatten(owner);
            flattened = Interlocked.CompareExchange(ref field, flattened, null) ?? flattened;
        }

        return flattened;
    }

    private static PrecedenceParser<T> Flatten<T>(IOperatorLevels<T> owner)
    {
        var chain = new List<IOperatorLevels<T>>();
        var current = owner;
        Parser<T> operand;

        while (true)
        {
            chain.Add(current);
            operand = current.Operand;

            if (operand is not IOperatorLevels<T> next)
            {
                break;
            }

            current = next;
        }

        // The tightest level is the one reading the operand
        var levels = new List<OperatorLevel<T>>();

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            chain[i].AddLevels(levels);
        }

        return new PrecedenceParser<T>(operand, levels.ToArray());
    }
}
