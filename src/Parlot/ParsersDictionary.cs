using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot;

public sealed class ParsersDictionary<T>
{
    private readonly List<Parser<T>>[] _asciiMap = new List<Parser<T>>[128];
    private Dictionary<uint, List<Parser<T>>>? _nonAsciiMap;

    public ParsersDictionary()
    {
        ExpectedChars = Array.Empty<char>();
    }

    public ParsersDictionary(IEnumerable<KeyValuePair<char, List<Parser<T>>>> map)
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

    public void Set(char c, List<Parser<T>> value)
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

    public List<Parser<T>>? this[uint c]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            List<Parser<T>>[] asciiMap = _asciiMap;
            if (c < (uint)asciiMap.Length)
            {
                return asciiMap[c];
            }
            else
            {
                List<Parser<T>>? map = null;
                _nonAsciiMap?.TryGetValue(c, out map);
                return map;
            }
        }
    }
}
