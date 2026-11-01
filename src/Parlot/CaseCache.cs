using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Parlot;

/// <summary>
/// The strings of the cases of a text a case-insensitive parser matched, when it returns the matched text: a text has
/// few cases in an input, often a single one, so they are kept in a few slots instead of a <see cref="StringPool"/>.
/// </summary>
/// <remarks>
/// The first cases seen are kept, which are usually the ones the input uses most. The other ones are created each time,
/// so that an input using many cases can't grow the cache. A parser is shared by concurrent parses: a slot is set once,
/// with a compare-and-swap, and read without a lock.
/// </remarks>
internal sealed class CaseCache
{
    private string? _case0;
    private string? _case1;
    private string? _case2;
    private string? _case3;

    /// <summary>
    /// Gets the string of these chars, from the cache when it has it, adding it otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Get(ReadOnlySpan<char> value)
    {
        var s = _case0;

        if (s != null)
        {
            if (value.SequenceEqual(s.AsSpan()))
            {
                return s;
            }

            s = _case1;

            if (s != null && value.SequenceEqual(s.AsSpan()))
            {
                return s;
            }
        }

        return GetOther(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string GetOther(ReadOnlySpan<char> value)
    {
        var s = _case2;

        if (s != null && value.SequenceEqual(s.AsSpan()))
        {
            return s;
        }

        s = _case3;

        if (s != null && value.SequenceEqual(s.AsSpan()))
        {
            return s;
        }

        if (s != null)
        {
            // Full
            return value.ToString();
        }

        var created = value.ToString();

        // The first empty slot takes the string. Another thread may have set a slot with the same case meanwhile.
        return Add(ref _case0, created) ?? Add(ref _case1, created) ?? Add(ref _case2, created) ?? Add(ref _case3, created) ?? created;
    }

    // The string of the slot when it is this case, the string set in an empty slot, or null to try the next slot
    private static string? Add(ref string? slot, string value)
    {
        var existing = Interlocked.CompareExchange(ref slot, value, null);

        if (existing == null)
        {
            return value;
        }

        return existing.AsSpan().SequenceEqual(value.AsSpan()) ? existing : null;
    }
}
