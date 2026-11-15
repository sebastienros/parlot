using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot;

/// <summary>
/// A set of chars tested one at a time: a bit for each ASCII char, and the chars above ASCII in a <c>SearchValues</c>, or a
/// sorted array below net8.0.
/// </summary>
/// <remarks>
/// It is a struct so that a parser holding it in a field tests an ASCII char with no other indirection. Shared by
/// <c>CustomWhiteSpace</c>, <c>CommentsParser</c> and the code it generates.
/// </remarks>
internal readonly struct CharSet
{
    // The ASCII chars, a bit each
    private readonly ulong _low;
    private readonly ulong _high;

    // Whether it contains every char above ASCII
    private readonly bool _anyOther;

    // The chars above ASCII, null when there are none
#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _others;
#else
    private readonly char[]? _others;
#endif

    /// <summary>
    /// Creates the set of <paramref name="chars"/>, which can repeat.
    /// </summary>
    public CharSet(ReadOnlySpan<char> chars)
    {
        _low = 0;
        _high = 0;
        _anyOther = false;
        _others = null;

        var others = 0;

        foreach (var c in chars)
        {
            if (c < 64)
            {
                _low |= 1UL << c;
            }
            else if (c < 128)
            {
                _high |= 1UL << (c - 64);
            }
            else
            {
                others++;
            }
        }

        if (others == 0)
        {
            return;
        }

        var otherChars = new char[others];
        var i = 0;

        foreach (var c in chars)
        {
            if (c >= 128)
            {
                otherChars[i++] = c;
            }
        }

#if NET8_0_OR_GREATER
        _others = SearchValues.Create(otherChars);
#else
        Array.Sort(otherChars);
        _others = otherChars;
#endif
    }

    private CharSet(bool all)
    {
        _low = all ? ulong.MaxValue : 0;
        _high = all ? ulong.MaxValue : 0;
        _anyOther = all;
        _others = null;
    }

    /// <summary>
    /// The set of every char.
    /// </summary>
    public static CharSet All => new(all: true);

    /// <summary>
    /// Whether the set contains <paramref name="c"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(char c)
    {
        if (c < 64)
        {
            return ((_low >> c) & 1) != 0;
        }

        if (c < 128)
        {
            return ((_high >> (c - 64)) & 1) != 0;
        }

        return _anyOther || (_others is not null && ContainsOther(c));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ContainsOther(char c)
    {
#if NET8_0_OR_GREATER
        return _others!.Contains(c);
#else
        return Array.BinarySearch(_others!, c) >= 0;
#endif
    }
}
