using System;
using System.Collections.Generic;
#if !PARLOT_STANDALONE || NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Parlot;

/// <summary>
/// Represents the result of an optional parser invocation.
/// </summary>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    /// <summary>
    /// Gets an <see cref="Option{T}"/> representing the absence of a value.
    /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static Option<T> None => default;
#pragma warning restore CA1000

    /// <summary>
    /// Creates an <see cref="Option{T}"/> that wraps a value.
    /// </summary>
    public Option(T value)
    {
        HasValue = true;
        Value = value;
    }

    /// <summary>
    /// Gets a value indicating whether the optional value has been set.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the wrapped value. When <see cref="HasValue"/> is <see langword="false"/>, the default value of <typeparamref name="T"/> is returned.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Tries to get the wrapped value.
    /// </summary>
    /// <param name="value">The wrapped value when set; otherwise the default value of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> when the value is set; otherwise <see langword="false"/>.</returns>
#if !PARLOT_STANDALONE || NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
#else
    public bool TryGetValue(out T value)
#endif
    {
        value = Value;
        return HasValue;
    }

    /// <summary>
    /// Gets the wrapped value or the specified default value.
    /// </summary>
    /// <param name="defaultValue">The value to return when <see cref="HasValue"/> is <see langword="false"/>.</param>
    /// <returns>The wrapped value if present; otherwise <paramref name="defaultValue"/>.</returns>
    public T OrSome(T? defaultValue)
        => HasValue ? Value : defaultValue!;

    /// <summary>
    /// Gets the wrapped value or <see langword="default"/> when <see cref="HasValue"/> is <see langword="false"/>.
    /// </summary>
    /// <returns>The wrapped value if present; otherwise <see langword="default"/>.</returns>
    public T? OrDefault()
        => HasValue ? Value : default;

    /// <inheritdoc />
    public bool Equals(Option<T> other)
        => HasValue == other.HasValue && (!HasValue || EqualityComparer<T>.Default.Equals(Value, other.Value));

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is Option<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (!HasValue)
        {
            return 0;
        }

        unchecked
        {
            return Value is null ? 1 : (EqualityComparer<T>.Default.GetHashCode(Value) * 397) ^ 1;
        }
    }

    /// <summary>
    /// Determines whether two <see cref="Option{T}"/> values are equal.
    /// </summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Option{T}"/> values are not equal.
    /// </summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
        => HasValue ? (Value?.ToString() ?? string.Empty) : string.Empty;
}
