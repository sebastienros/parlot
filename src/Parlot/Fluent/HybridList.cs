using System;
using System.Collections;
using System.Collections.Generic;

namespace Parlot.Fluent;

/// <summary>
/// An internal implementation of IReadOnlyList&lt;T&gt; that stores up to 4 items inline
/// before switching to a List&lt;T&gt; for growth.
/// This provides efficient memory usage for small result sets while maintaining
/// flexibility for larger lists.
/// </summary>
#nullable enable
internal sealed class HybridList<T> : IReadOnlyList<T>
{
    private T? _item1;
    private T? _item2;
    private T? _item3;
    private T? _item4;
    private List<T>? _list;
    private int _count;

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_list is not null)
            {
                return _list[index];
            }

            return index switch
            {
                0 => _item1!,
                1 => _item2!,
                2 => _item3!,
                3 => _item4!,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }

    public void Add(T item)
    {
        if (_list is not null)
        {
            _list.Add(item);
            _count++;
        }
        else
        {
            switch (_count)
            {
                case 0:
                    _item1 = item;
                    _count++;
                    break;
                case 1:
                    _item2 = item;
                    _count++;
                    break;
                case 2:
                    _item3 = item;
                    _count++;
                    break;
                case 3:
                    _item4 = item;
                    _count++;
                    break;
                case 4:
                    // Transition to List<T>
                    _list = new List<T>(8) { _item1!, _item2!, _item3!, _item4!, item };
                    _item1 = default;
                    _item2 = default;
                    _item3 = default;
                    _item4 = default;
                    _count++;
                    break;
                default:
                    throw new InvalidOperationException("Unexpected count value");
            }
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (_list is not null)
        {
            return _list.GetEnumerator();
        }

        return GetEnumeratorInternal();
    }

    private IEnumerator<T> GetEnumeratorInternal()
    {
        if (_count >= 1)
            yield return _item1!;
        if (_count >= 2)
            yield return _item2!;
        if (_count >= 3)
            yield return _item3!;
        if (_count >= 4)
            yield return _item4!;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
