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
        List<char>? expectedChars = null;
        Dictionary<uint, T>? nonAsciiMap = null;

        foreach (var item in map)
        {
            var c = item.Key;
            if (c < 128)
            {
                if (_asciiMap[c] is null)
                {
                    _asciiMap[c] = item.Value;
                    (expectedChars ??= []).Add(c);
                }
            }
            else
            {
                nonAsciiMap ??= [];

                if (!nonAsciiMap.ContainsKey(c))
                {
                    nonAsciiMap[c] = item.Value;
                    (expectedChars ??= []).Add(c);
                }
            }
        }

        if (expectedChars is not null)
        {
            ExpectedChars = expectedChars.ToArray();
            Array.Sort(ExpectedChars);
        }
        else
        {
            ExpectedChars = Array.Empty<char>();
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

    /// <summary>
    /// Gets or sets the value of the non-ASCII chars which aren't in the map, <c>null</c> by default.
    /// </summary>
    public T? NonAsciiFallback { get; set; }

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

            return GetNonAscii(c);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T? GetNonAscii(uint c)
    {
        T? map = null;
        _nonAsciiMap?.TryGetValue(c, out map);
        return map ?? NonAsciiFallback;
    }
}
