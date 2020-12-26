using System;

namespace Parlot
{
    public class ParseResult : IParseResult
    {
        protected string _text;

        public bool Success { get; protected set; }

        public TextPosition Start { get; protected set; }

        public TextPosition End { get; protected set; }

        public int Length { get; protected set; }
        public string Buffer { get; protected set; }
        public string Text => _text ??= Buffer?.Substring(Start.Offset, Length);

        public ReadOnlySpan<char> Span => Buffer.AsSpan(Start.Offset, Length);

        public void Fail()
        {
            Success = false;
            Buffer = null;
            _text = null;
            Start = TextPosition.Start;
            End = TextPosition.Start;
            Length = 0;
            _value = default;
        }

        public void Succeed(string buffer, TextPosition start, TextPosition end, object value)
        {
            Success = true;
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;
            _value = value;
        }

        protected object _value;

        public object GetValue() => _value;

        public void SetValue(object value)
        {
            _value = value;
        }
    }

    public class ParseResult<T> : ParseResult, IParseResult<T>
    {
        private T _typedValue;
        private bool _set;

        public void Succeed(string buffer, TextPosition start, TextPosition end, T value)
        {
            Success = true;
            Buffer = buffer;
            Start = start;
            End = end;
            Length = end - start;
            _text = null;
            _typedValue = value;
            _set = true;
        }

        public new T GetValue() => _set ? _typedValue : _value == null ? default : (T) _value;

        public void SetValue(T value)
        {
            _set = true;
            _typedValue = value;
        }
    }
}
