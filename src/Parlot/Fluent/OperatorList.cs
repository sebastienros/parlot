using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

#nullable enable

/// <summary>
/// The indices of the operators of a chain, which are read before the operand they apply to. They are kept in a span of the
/// stack of the parser's method, and move to an array of <see cref="ArrayPool{T}.Shared"/> when they don't fit.
/// </summary>
/// <remarks>
/// Unlike <see cref="ListBuilder{T}"/>, which keeps four items inline and moves longer lists to the scratch stack of the
/// thread, it reads and writes a span directly. Initialize it with <c>stackalloc int[OperatorList.InlineCapacity]</c>, and call
/// <see cref="Release"/> in a <see langword="finally"/> block.
/// </remarks>
internal ref struct OperatorList
{
    internal const int InlineCapacity = 16;

    private Span<int> _items;
    private int[]? _rented;
    private int _count;

    public OperatorList(Span<int> initial)
    {
        _items = initial;
        _rented = null;
        _count = 0;
    }

    /// <summary>Gets the number of operators.</summary>
    public readonly int Count => _count;

    /// <summary>Gets the operator at <paramref name="index"/>, which must be less than <see cref="Count"/>.</summary>
    public readonly int this[int index] => _items[index];

    /// <summary>Adds an operator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int item)
    {
        var count = _count;

        if ((uint)count < (uint)_items.Length)
        {
            _items[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(int item)
    {
        var grown = ArrayPool<int>.Shared.Rent(Math.Max(_items.Length * 2, InlineCapacity));
        _items.Slice(0, _count).CopyTo(grown);

        if (_rented is not null)
        {
            ArrayPool<int>.Shared.Return(_rented);
        }

        _rented = grown;
        _items = grown;
        _items[_count++] = item;
    }

    /// <summary>Returns the array the operators moved to, if any.</summary>
    public void Release()
    {
        var rented = _rented;

        if (rented is not null)
        {
            _rented = null;
            _items = default;
            ArrayPool<int>.Shared.Return(rented);
        }
    }
}
