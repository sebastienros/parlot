using System;
using System.Collections.Generic;
#if NET10_0_OR_GREATER
using System.Collections.Frozen;
#endif
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
#if NET10_0_OR_GREATER
    private FrozenDictionary<uint, T>? _nonAsciiMap;
#else
    private Dictionary<uint, T>? _nonAsciiMap;
#endif

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
#if NET10_0_OR_GREATER
            _nonAsciiMap = nonAsciiMap.ToFrozenDictionary();
#else
            _nonAsciiMap = nonAsciiMap;
#endif
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
#if NET10_0_OR_GREATER
            Dictionary<uint, T> dic = _nonAsciiMap == null ? [] : new(_nonAsciiMap);
#else
            var dic = _nonAsciiMap ??= [];
#endif

            if (!dic.ContainsKey(c))
            {
                dic[c] = value;
#if NET10_0_OR_GREATER
                _nonAsciiMap = dic.ToFrozenDictionary();
#endif
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
