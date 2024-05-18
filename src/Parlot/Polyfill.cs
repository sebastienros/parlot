#if NET462
using System.Collections.Generic;
#endif

namespace Parlot;

internal static class Polyfill
{
#if NET462
    internal static IEnumerable<T> Append<T>(this IEnumerable<T> source, T element)
    {
        var result = new List<T>(source);
        result.Add(element);
        return result;
    }

    internal delegate void SpanAction<T, in TArg>(T[] span, TArg arg);
    internal static string Create<TState>(this string _, int length, TState state, SpanAction<char, TState> action)
    {
        var array = new char[length];

        action(array, state);

        return new string(array);
    }

#endif
}
