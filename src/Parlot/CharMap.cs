using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot;

internal sealed class CharMap<T> where T : class
{
    private readonly T[] _asciiMap = new T[128];
    private Dictionary<uint, T>? _nonAsciiMap;

    public CharMap()
    {
        ExpectedChars = Array.Empty<char>();
    }

    public CharMap(IEnumerable<KeyValuePair<char, T>> map)
    {
        var charSet = new HashSet<char>();

        foreach (var item in map)
        {
            charSet.Add(item.Key);
        }

        ExpectedChars = [.. charSet];
        Array.Sort(ExpectedChars);

        foreach (var item in map)
        {
            var c = item.Key;
            if (c < 128)
            {
                _asciiMap[c] ??= item.Value;
            }
            else
            {
                _nonAsciiMap ??= [];

                if (!_nonAsciiMap.ContainsKey(c))
                {
                    _nonAsciiMap[c] = item.Value;
                }
            }
        }
    }

    public void Set(char c, T value)
    {
        ExpectedChars = new HashSet<char>([c, .. ExpectedChars]).ToArray();
        Array.Sort(ExpectedChars);

        if (c < 128)
        {
            _asciiMap[c] ??= value;
        }
        else
        {
            _nonAsciiMap ??= [];

            if (!_nonAsciiMap.ContainsKey(c))
            {
                _nonAsciiMap[c] = value;
            }
        }
    }

    public char[] ExpectedChars { get; private set; }

    public T? this[uint c]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            T[] asciiMap = _asciiMap;
            if (c < (uint)asciiMap.Length)
            {
                return asciiMap[c];
            }
            else
            {
                T? map = null;
                _nonAsciiMap?.TryGetValue(c, out map);
                return map;
            }
        }
    }
}
