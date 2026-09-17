/*
Adapted from Apex.PgClient PgValueCaches.cs.

MIT License

Copyright (c) 2026 Sebastien Ros

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

using System;
using System.Threading;

namespace Parlot;

// Adapted from the two-hit admission policy in Apex.PgClient's Utf8StringCache:
// https://github.com/sebastienros/apex/blob/2527e58d1656d54a1ccc84516b68afbdd69753a8/src/Apex.PgClient/Internal/PgValueCaches.cs
// UTF-16 needs neither a separate encoded key nor a decoding allocation on a hit.
internal sealed class StringCache
{
    internal static readonly StringCache Shared = new(4096, 256);

    private readonly int _maximumLength;
    private Table? _table;

    internal StringCache(int capacity, int maximumLength)
    {
        if (capacity <= 0 || maximumLength <= 0)
        {
            return;
        }

        // Bound normalization to avoid integer overflow and accidental enormous tables.
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, 1 << 20);
#else
        if (capacity > 1 << 20)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

#endif
        var normalizedCapacity = 1;
        while (normalizedCapacity < capacity)
        {
            normalizedCapacity <<= 1;
        }

        _maximumLength = maximumLength;
        _table = new Table(normalizedCapacity);
    }

    internal string GetString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        var table = Volatile.Read(ref _table);
        if (table is null || value.Length > _maximumLength)
        {
            return value.ToString();
        }

#if NET8_0_OR_GREATER
        // Randomized ordinal span hashing needs no additional runtime/consumer dependency.
        var hash = string.GetHashCode(value);
#else
        // Downlevel span hashing must not allocate the very string we are trying to avoid.
        var hash = unchecked((int)2166136261);
        foreach (var c in value)
        {
            hash = unchecked((hash ^ c) * 16777619);
        }
#endif
        hash = hash == 0 ? 1 : hash;
        var index = hash & (table.Entries.Length - 1);
        var entry = Volatile.Read(ref table.Entries[index]);
        if (entry is not null && entry.Hash == hash && value.SequenceEqual(entry.Value.AsSpan()))
        {
            return entry.Value;
        }

        var result = value.ToString();
        if (Volatile.Read(ref table.CandidateHashes[index]) == hash)
        {
            // Publish one immutable object: readers never see a mismatched hash and string.
            Volatile.Write(ref table.Entries[index], new Entry(hash, result));
            Volatile.Write(ref table.CandidateHashes[index], 0);
        }
        else
        {
            Volatile.Write(ref table.CandidateHashes[index], hash);
        }

        return result;
    }

    internal void Disable() => Volatile.Write(ref _table, null);

    private sealed class Entry
    {
        internal readonly int Hash;
        internal readonly string Value;

        internal Entry(int hash, string value)
        {
            Hash = hash;
            Value = value;
        }
    }

    private sealed class Table
    {
        internal readonly Entry?[] Entries;
        internal readonly int[] CandidateHashes;

        internal Table(int capacity)
        {
            Entries = new Entry?[capacity];
            CandidateHashes = new int[capacity];
        }
    }
}
