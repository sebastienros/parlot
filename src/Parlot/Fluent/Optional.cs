namespace Parlot.Fluent
{
    /// <summary>
    /// Represents an optional result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public readonly struct OptionalResult<T>
    {
        public OptionalResult(bool hasValue, T value)
        {
            HasValue = hasValue;
            Value = value;
        }

        /// <summary>
        /// Whether the result has a value or not.
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// Gets the value of the result if any.
        /// </summary>
        public T Value { get; }
    }
}
