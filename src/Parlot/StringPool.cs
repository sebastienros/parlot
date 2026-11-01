using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Parlot;

/// <summary>
/// De-duplicates the strings created from the text, such that the values a parser returns many times are allocated once.
/// </summary>
/// <remarks>
/// <para>
/// A pool is owned by a parser, which is built once and used by many parses, possibly at the same time: lookups that find
/// a value take no lock, adding one does. An entry is published by writing its <see cref="Entry.Value"/> last and never
/// changes afterwards; growing replaces the table.
/// </para>
/// <para>
/// The table uses open addressing. The first and the last 8 chars of a value, zero padded when it is shorter, are with
/// its length an exact key for values of up to 16 chars, which are then found without reading the string. A longer
/// value also compares the string.
/// </para>
/// <para>
/// The pool is bounded: it holds at most <see cref="MaxCount"/> values of at most its maximum length, a lookup probes a
/// bounded number of slots, and a pool whose values rarely repeat switches itself off.
/// </para>
/// </remarks>
internal sealed class StringPool
{
    /// <summary>The default maximum length of the values that are pooled: longer ones rarely repeat and cost more to compare.</summary>
    internal const int DefaultMaximumLength = 64;

    // Values; the table has up to twice as many slots
    internal const int MaxCount = 4096;

    private const int InitialSlots = 64;

    // The slots of a full table, at half its load
    private const int MaxSlots = 2 * MaxCount;

    // A lookup gives up after this many slots, which bounds the cost of inputs whose values collide. Linear probing at half
    // the load of a full table already makes runs of 40 slots, and a value beyond the probes is created instead of pooled.
    private const int MaxProbes = 64;

    // When the pool holds this many values and nearly every lookup brought a new one (ids, numbers), it switches off, before
    // its table grows to 2048 slots (96 KB, which would go to the large object heap)
    private const int EarlyCheckCount = 512;

    // The lookups beyond which the early check can't switch the pool off anymore: it does when more than 95% of them added
    private const int EarlyCheckLookups = EarlyCheckCount * 20 / 19 + 1;

    // Once the pool is full, it switches off when this many lookups missed and they were more than half of them
    private const int MinMissesToSwitchOff = 1024;

    // The strings of the ASCII chars, which are returned for the values of a single char without a lookup
    private static readonly string[] s_ascii = CreateAscii();

    // The table of a pool that has no value yet, or is switched off; it is never written
    private static readonly Entry[] s_noEntries = new Entry[1];

    private struct Entry
    {
        // The first 8 chars of the value and the last 8, zero padded when it is shorter
        public ulong Head0;
        public ulong Head1;
        public ulong Tail0;
        public ulong Tail1;

        // The length of the value
        public int Length;

        // The string, written last when the entry is added; null marks an empty slot
        public string? Value;
    }

    private readonly ulong _seed;
    private readonly object _lock = new();

    private volatile Entry[] _entries = s_noEntries;

    // 0 once the pool is switched off
    private volatile int _maximumLength;

    // Set when the pool is full and keeps the values it has: a value it doesn't have is then created without the lock
    private volatile bool _frozen;

    // The lookups are counted while a decision depends on them, without synchronization: a lost count only delays it
    private volatile bool _counting = true;
    private long _lookups;
    private long _lookupLimit = EarlyCheckLookups;

    // Guarded by the lock
    private int _count;
    private int _fullMisses;

    /// <summary>
    /// Creates a pool.
    /// </summary>
    /// <param name="maximumLength">The length beyond which values are not pooled.</param>
    public StringPool(int maximumLength = DefaultMaximumLength)
    {
        _maximumLength = maximumLength;
        ConfiguredMaximumLength = maximumLength;

        // Each pool hashes differently, so that an input can't be crafted to collide in every process
        _seed = ((ulong)(uint)RuntimeHelpers.GetHashCode(this) << 32 | (uint)Environment.TickCount) * 0x9E3779B97F4A7C15UL;
    }

    /// <summary>
    /// Gets the string of a single ASCII char, which all the strings of a single char share.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Ascii(char c) => s_ascii[c];

    /// <summary>
    /// The length beyond which values are not pooled, as the pool was created.
    /// </summary>
    internal int ConfiguredMaximumLength { get; }

    /// <summary>
    /// Gets the string of a span: its buffer when the span covers all of it, otherwise the pooled string.
    /// </summary>
    public string Get(TextSpan span)
    {
        var buffer = span.Buffer;

        if (buffer == null)
        {
            return "";
        }

        // The buffer is the value, like TextSpan.ToString() returns it
        if (span.Offset == 0 && span.Length == buffer.Length)
        {
            return buffer;
        }

        return Get(buffer.AsSpan(span.Offset, span.Length));
    }

    /// <summary>
    /// Gets the string of these chars, from the pool when it has it, adding it otherwise.
    /// </summary>
    public string Get(ReadOnlySpan<char> value)
    {
        var length = value.Length;

        if (length <= 1)
        {
            if (length == 0)
            {
                return "";
            }

            var c = value[0];

            if (c < 128)
            {
                return s_ascii[c];
            }
        }

        if ((uint)length > (uint)_maximumLength)
        {
            return value.ToString();
        }

        if (_counting)
        {
            CountLookup();
        }

        Load(value, out var head0, out var head1, out var tail0, out var tail1);

        var table = _entries;
        var mask = table.Length - 1;
        var i = Slot(Hash(head0, head1, tail0, tail1, length), mask);

        for (var probes = 0; probes < MaxProbes; probes++)
        {
            ref var e = ref table[i];

            // Read the value first: once it is set, the rest of the entry is complete
            var s = Volatile.Read(ref e.Value);

            if (s == null)
            {
                return Add(value, head0, head1, tail0, tail1);
            }

            if (e.Length == length
                && ((e.Head0 ^ head0) | (e.Head1 ^ head1) | (e.Tail0 ^ tail0) | (e.Tail1 ^ tail1)) == 0
                && (length <= 16 || value.SequenceEqual(s.AsSpan())))
            {
                return s;
            }

            i = (i + 1) & mask;
        }

        // Too many collisions
        return value.ToString();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CountLookup()
    {
        if (++_lookups >= _lookupLimit)
        {
            // No decision depends on the lookups anymore. A full pool keeps the values it has.
            if (_count == MaxCount)
            {
                _frozen = true;
            }

            _counting = false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string Add(ReadOnlySpan<char> value, ulong head0, ulong head1, ulong tail0, ulong tail1)
    {
        if (_frozen)
        {
            return value.ToString();
        }

        lock (_lock)
        {
            var length = value.Length;

            if ((uint)length > (uint)_maximumLength || _frozen)
            {
                return value.ToString();
            }

            var table = _entries;

            if (table.Length < InitialSlots)
            {
                table = new Entry[InitialSlots];
                _entries = table;
            }

            // Probe again: another thread may have added the value, or grown the table, in the meantime
            var hash = Hash(head0, head1, tail0, tail1, length);
            var i = 0;
            var found = false;

            while (true)
            {
                var mask = table.Length - 1;
                i = Slot(hash, mask);

                for (var probes = 0; probes < MaxProbes; probes++)
                {
                    ref var e = ref table[i];

                    if (e.Value == null)
                    {
                        found = true;
                        break;
                    }

                    if (e.Length == length
                        && ((e.Head0 ^ head0) | (e.Head1 ^ head1) | (e.Tail0 ^ tail0) | (e.Tail1 ^ tail1)) == 0
                        && (length <= 16 || value.SequenceEqual(e.Value.AsSpan())))
                    {
                        return e.Value;
                    }

                    i = (i + 1) & mask;
                }

                // The slots a lookup probes are taken: a bigger table spreads them, when the pool can still add values
                if (found || _count == MaxCount || table.Length >= MaxSlots)
                {
                    break;
                }

                Grow();
                table = _entries;
            }

            var s = value.ToString();

            if (_count == MaxCount)
            {
                // Full: keep the values seen so far, and give up when most lookups still bring a new value
                if (++_fullMisses >= MinMissesToSwitchOff)
                {
                    if (_fullMisses * 2L > _lookups)
                    {
                        SwitchOff();
                    }
                    else
                    {
                        _frozen = true;
                        _counting = false;
                    }
                }

                return s;
            }

            if (!found)
            {
                return s;
            }

            ref var slot = ref table[i];
            slot.Head0 = head0;
            slot.Head1 = head1;
            slot.Tail0 = tail0;
            slot.Tail1 = tail1;
            slot.Length = length;
            Volatile.Write(ref slot.Value, s);

            var count = ++_count;

            if (count == EarlyCheckCount && _counting)
            {
                if (count * 20L > _lookups * 19L)
                {
                    SwitchOff();
                    return s;
                }

                _counting = false;
            }

            if (count * 2 > table.Length)
            {
                Grow();
            }

            if (count == MaxCount)
            {
                // From now on the misses are counted against the lookups
                _lookups = 0;
                _lookupLimit = 2L * MinMissesToSwitchOff;
                _counting = true;
            }

            return s;
        }
    }

    private void Grow()
    {
        var old = _entries;
        var table = new Entry[old.Length * 2];
        var mask = table.Length - 1;

        foreach (var e in old)
        {
            if (e.Value == null)
            {
                continue;
            }

            // An entry beyond the probes of a lookup is not found, and its value is then created: the result is the same
            var i = Slot(Hash(e.Head0, e.Head1, e.Tail0, e.Tail1, e.Length), mask);

            while (table[i].Value != null)
            {
                i = (i + 1) & mask;
            }

            table[i] = e;
        }

        _entries = table;
    }

    private void SwitchOff()
    {
        _maximumLength = 0;
        _counting = false;

        // Lookups still running may use the old table, which stays valid
        _entries = s_noEntries;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Load(ReadOnlySpan<char> value, out ulong head0, out ulong head1, out ulong tail0, out ulong tail1)
    {
        if (value.Length >= 8)
        {
            var bytes = MemoryMarshal.AsBytes(value);
            var tail = bytes.Slice(bytes.Length - 16);

            head0 = MemoryMarshal.Read<ulong>(bytes);
            head1 = MemoryMarshal.Read<ulong>(bytes.Slice(8));
            tail0 = MemoryMarshal.Read<ulong>(tail);
            tail1 = MemoryMarshal.Read<ulong>(tail.Slice(8));
        }
        else
        {
            // A short value is read char by char, as reading past its end could read past the end of the string. Its
            // length is part of the key, so it is never compared with a value read the other way.
            head0 = 0;
            head1 = 0;

            for (var i = 0; i < value.Length; i++)
            {
                if (i < 4)
                {
                    head0 |= (ulong)value[i] << (i * 16);
                }
                else
                {
                    head1 |= (ulong)value[i] << ((i - 4) * 16);
                }
            }

            tail0 = head0;
            tail1 = head1;
        }
    }

    // Each part is multiplied with the seed mixed in, so that values can't be made to collide without knowing it. The
    // multiplications are independent, and the murmur3 finalizer makes each bit of the result depend on all of them.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Hash(ulong head0, ulong head1, ulong tail0, ulong tail1, int length)
    {
        var seed = _seed;
        var hash = ((head0 ^ seed) * 0x9E3779B97F4A7C15UL) ^ ((head1 ^ seed) * 0xC2B2AE3D27D4EB4FUL)
            ^ ((tail0 ^ seed) * 0x165667B19E3779F9UL) ^ ((tail1 ^ seed) * 0x27D4EB2F165667C5UL) ^ (ulong)length;

        hash = (hash ^ (hash >> 33)) * 0xFF51AFD7ED558CCDUL;
        hash = (hash ^ (hash >> 33)) * 0xC4CEB9FE1A85EC53UL;
        return hash ^ (hash >> 33);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(ulong hash, int mask) => (int)hash & mask;

    private static string[] CreateAscii()
    {
        var strings = new string[128];

        for (var i = 0; i < strings.Length; i++)
        {
            strings[i] = ((char)i).ToString();
        }

        return strings;
    }
}
