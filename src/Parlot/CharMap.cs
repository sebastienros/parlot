using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot;

/// <summary>
/// Combines maps of ascii and non-ascii characters.
/// If all characters are ascii, the non-ascii dictionary is not used.
/// </summary>
internal sealed class CharMap<T> where T : class
{
    private readonly T[] _asciiMap = new T[128];
    private FrozenDictionary<uint, T>? _nonAsciiMap;

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

        Dictionary<uint, T>? nonAsciiMap = null;

        foreach (var item in map)
        {
            var c = item.Key;
            if (c < 128)
            {
                _asciiMap[c] ??= item.Value;
            }
            else
            {
                nonAsciiMap ??= [];

                if (!nonAsciiMap.ContainsKey(c))
                {
                    nonAsciiMap[c] = item.Value;
                }
            }
        }

        if (nonAsciiMap != null)
        {
            _nonAsciiMap = nonAsciiMap.ToFrozenDictionary();
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
            Dictionary<uint, T> dic = _nonAsciiMap == null ? [] : new(_nonAsciiMap);

            if (!dic.ContainsKey(c))
            {
                dic[c] = value;
                _nonAsciiMap = dic.ToFrozenDictionary();
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
