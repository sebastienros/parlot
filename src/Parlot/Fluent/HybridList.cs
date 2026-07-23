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
internal sealed class HybridList<T> : IReadOnlyList<T>, ICollection<T>
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

    // ICollection<T> is implemented so that BCL collections built from a parser result, e.g.
    // new Dictionary<K, V>(result) or new List<T>(result), can allocate their storage once
    // instead of growing it as they enumerate. Only Count and CopyTo are needed for that, and the
    // instance is handed out as an IReadOnlyList<T>, so the mutating members are implemented
    // explicitly and throw rather than letting a cast alter a parser result.

    bool ICollection<T>.IsReadOnly => true;

    void ICollection<T>.Add(T item) => throw new NotSupportedException();

    void ICollection<T>.Clear() => throw new NotSupportedException();

    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    public bool Contains(T item)
    {
        if (_list is not null)
        {
            return _list.Contains(item);
        }

        var comparer = EqualityComparer<T>.Default;

        for (var i = 0; i < _count; i++)
        {
            if (comparer.Equals(this[i], item))
            {
                return true;
            }
        }

        return false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _ = array ?? throw new ArgumentNullException(nameof(array));

        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < _count)
        {
            throw new ArgumentException("The destination array has insufficient space.", nameof(array));
        }

        if (_list is not null)
        {
            _list.CopyTo(array, arrayIndex);
            return;
        }

        for (var i = 0; i < _count; i++)
        {
            array[arrayIndex + i] = this[i];
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
