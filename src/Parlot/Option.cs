namespace Parlot;

/// <summary>
/// Represents the result of an optional parser invocation.
/// </summary>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
public readonly struct Option<T>
{
    private readonly bool _hasValue;
    private readonly T _value;

    private Option(bool hasValue, T value)
    {
        _hasValue = hasValue;
        _value = value;
    }

    /// <summary>
    /// Creates an <see cref="Option{T}"/> that wraps a value.
    /// </summary>
    public Option(T value)
    {
        _hasValue = true;
        _value = value;
    }

    /// <summary>
    /// Gets a value indicating whether the optional value has been set.
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// Gets the wrapped value. When <see cref="HasValue"/> is <see langword="false"/>, the default value of <typeparamref name="T"/> is returned.
    /// </summary>
    public T Value => _value;

    /// <summary>
    /// Tries to get the wrapped value.
    /// </summary>
    /// <param name="value">The wrapped value when set; otherwise the default value of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> when the value is set; otherwise <see langword="false"/>.</returns>
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _hasValue;
    }

    /// <summary>
    /// Gets the wrapped value or the specified default value.
    /// </summary>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public T OrSome(T? defaultValue)
        => _hasValue ? _value : defaultValue!;
}
