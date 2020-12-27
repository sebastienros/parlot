using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Parlot
{
    /// <summary>
    /// Cache of char to string mapping to reduce allocations
    /// when doing chars comparisons.
    /// </summary>
    internal static class CharToStringTable
    {
        private static readonly int Size = 256;
        private static readonly string[] Table = new string[Size];

        static CharToStringTable()
        {
            for (int i = 0; i < Size; i++)
            {
                Table[i] = ((char)i).ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetString(char c)
        {
            if (c < Size)
            {
                return Table[c];
            }

            return c.ToString();
        }
    }
}
