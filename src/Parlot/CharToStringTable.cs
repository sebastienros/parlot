using System.Runtime.CompilerServices;

namespace Parlot;

/// <summary>
/// Cache of char to string mapping to reduce allocations
/// when doing chars comparisons.
/// </summary>
internal static class CharToStringTable
{
    private const int _size = 256;
    private static readonly string[] _table = new string[_size];

    static CharToStringTable()
    {
        for (int i = 0; i < _size; i++)
        {
            _table[i] = ((char)i).ToString();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(char c)
    {
        string[] table = _table;
        if (c < (uint)table.Length)
        {
            return table[c];
        }

        return c.ToString();
    }
}
