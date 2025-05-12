using System;

namespace Parlot.Fluent
{
    public readonly struct Optional<T>
    {
        private readonly T _value;
        private readonly bool _hasValue;

        public Optional(T value)
        {
            _value = value;
            _hasValue = true;
        }

        public bool HasValue => _hasValue;

        public T Value
        {
            get
            {
                if (!_hasValue)
                {
                    throw new InvalidOperationException("No value present.");
                }
                return _value;
            }
        }

        public T GetValueOrDefault(T defaultValue = default)
        {
            return _hasValue ? _value : defaultValue;
        }

        public override string ToString()
        {
            return _hasValue ? _value?.ToString() ?? "null" : "No value";
        }
    }
}