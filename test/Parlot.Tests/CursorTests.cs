using System;
using Xunit;

namespace Parlot.Tests
{
    public class CursorTests
    {
        [Fact]
        public void ShouldMatchString()
        {
            var c = new Cursor<char>("Lorem ipsum".ToCharArray(), TextPosition.Start);

            Assert.True(c.Match(""));
            Assert.True(c.Match("Lorem"));
            Assert.True(c.Match("Lorem ipsum"));
        }

        [Fact]
        public void ShouldMatchEmptyString()
        {
            var c = new Cursor<char>("Lorem ipsum".ToCharArray(), TextPosition.Start);

            Assert.True(c.Match(""));
        }

        [Fact]
        public void ShouldNotMatchString()
        {
            var c = new Cursor<char>("Lorem ipsum".ToCharArray(), TextPosition.Start);

            Assert.False(c.Match("Lorem ipsum dolor"));
        }

        [Fact]
        public void AdvanceShouldReturnOnEof()
        {
            var c = new Cursor<char>("Lorem ipsum".ToCharArray());

            for (var i = 0; i < c.Buffer.Length - 1; i++)
            {
                c.Advance();
                Assert.False(c.Eof);
            }

            Assert.Equal('m', c.Current);

            c.Advance();
            Assert.True(c.Eof);
            Assert.Equal(Cursor<char>.NullChar, c.Current);

            c.Advance();
            Assert.True(c.Eof);
            Assert.Equal(Cursor<char>.NullChar, c.Current);
        }

        [Fact]
        public void PeekShouldReturnFirstChar()
        {
            var c = new Cursor<char>("123".ToCharArray());

            Assert.Equal('1', c.Current);
            Assert.Equal('1', c.Current);
        }

        [Fact]
        public void AdvanceShouldCountLinesAndColumns()
        {
            var c = new Cursor<char>("123\n456\r\n789".ToCharArray());

            Assert.Equal('1', c.Current);
            Assert.Equal(0, c.Position.Offset);
            Assert.Equal(1, c.Position.Column);
            Assert.Equal(1, c.Position.Line);

            c.Advance();

            Assert.Equal('2', c.Current);
            Assert.Equal(1, c.Position.Offset);
            Assert.Equal(2, c.Position.Column);
            Assert.Equal(1, c.Position.Line);

            c.Advance();

            Assert.Equal('3', c.Current);
            Assert.Equal(2, c.Position.Offset);
            Assert.Equal(3, c.Position.Column);
            Assert.Equal(1, c.Position.Line);
            c.Advance();

            Assert.Equal('\n', c.Current);
            Assert.Equal(3, c.Position.Offset);
            Assert.Equal(4, c.Position.Column);
            Assert.Equal(1, c.Position.Line);
            c.Advance();

            Assert.Equal('4', c.Current);
            Assert.Equal(4, c.Position.Offset);
            Assert.Equal(1, c.Position.Column);
            Assert.Equal(2, c.Position.Line);
            c.Advance();

            Assert.Equal('5', c.Current);
            Assert.Equal(5, c.Position.Offset);
            Assert.Equal(2, c.Position.Column);
            Assert.Equal(2, c.Position.Line);
            c.Advance();

            Assert.Equal('6', c.Current);
            Assert.Equal(6, c.Position.Offset);
            Assert.Equal(3, c.Position.Column);
            Assert.Equal(2, c.Position.Line);
            c.Advance();

            Assert.Equal('\r', c.Current);
            Assert.Equal(7, c.Position.Offset);
            Assert.Equal(3, c.Position.Column);
            Assert.Equal(2, c.Position.Line);
            c.Advance();

            Assert.Equal('\n', c.Current);
            Assert.Equal(8, c.Position.Offset);
            Assert.Equal(4, c.Position.Column);
            Assert.Equal(2, c.Position.Line);
            c.Advance();

            Assert.Equal('7', c.Current);
            Assert.Equal(9, c.Position.Offset);
            Assert.Equal(1, c.Position.Column);
            Assert.Equal(3, c.Position.Line);
            c.Advance();

            Assert.Equal('8', c.Current);
            Assert.Equal(10, c.Position.Offset);
            Assert.Equal(2, c.Position.Column);
            Assert.Equal(3, c.Position.Line);
            c.Advance();

            Assert.Equal('9', c.Current);
            Assert.Equal(11, c.Position.Offset);
            Assert.Equal(3, c.Position.Column);
            Assert.Equal(3, c.Position.Line);
            c.Advance();

            Assert.Equal(Cursor<char>.NullChar, c.Current);
            Assert.Equal(12, c.Position.Offset);
            Assert.Equal(4, c.Position.Column);
            Assert.Equal(3, c.Position.Line);
        }

        [Fact]
        public void ResetPositionShouldMoveToEof()
        {
            var c = new Cursor<char>("123".ToCharArray());

            c.ResetPosition(new TextPosition(4, 1, 1));

            Assert.True(c.Eof);
            Assert.Equal(Cursor<char>.NullChar, c.Current);
        }

        [Fact]
        public void PeekNextShouldReturnNullChar()
        {
            var c = new Cursor<char>("123".ToCharArray());

            Assert.Equal('1', c.PeekNext(0));
            Assert.Equal('2', c.PeekNext());
            Assert.Equal('2', c.PeekNext(1));
            Assert.Equal('3', c.PeekNext(2));
            Assert.Equal(Cursor<char>.NullChar, c.PeekNext(3));
            Assert.Equal(Cursor<char>.NullChar, c.PeekNext(4));
        }

        [Fact]
        public void MatchAnyOfShouldMatchAny()
        {
            var c = new Cursor<char>("1234".ToCharArray());

            Assert.Throws<ArgumentNullException>(() => c.MatchAnyOf(null));

            Assert.True(c.MatchAnyOf("".ToCharArray()));
            Assert.True(c.MatchAnyOf("1".ToCharArray()));
            Assert.True(c.MatchAnyOf("abc1".ToCharArray()));
            Assert.True(c.MatchAnyOf("123".ToCharArray()));
            Assert.False(c.MatchAnyOf("abc".ToCharArray()));

            c.ResetPosition(new TextPosition(4, 0, 0));

            Assert.False(c.MatchAnyOf("".ToCharArray()));
            Assert.False(c.MatchAnyOf("1".ToCharArray()));
            Assert.False(c.MatchAnyOf("abc1".ToCharArray()));
            Assert.False(c.MatchAnyOf("123".ToCharArray()));
            Assert.False(c.MatchAnyOf("abc".ToCharArray()));
        }

        [Fact]
        public void MatchAnyShouldMatchAny()
        {
            var c = new Cursor<char>("1234".ToCharArray());

            Assert.Throws<ArgumentNullException>(() => c.MatchAny(null));

            Assert.True(c.MatchAny(Array.Empty<char>()));
            Assert.True(c.MatchAny('1'));
            Assert.True(c.MatchAny('a', 'b', 'c', '1'));
            Assert.True(c.MatchAny('1', '2', '3'));
            Assert.False(c.MatchAny('a', 'b', 'c'));

            c.ResetPosition(new TextPosition(4, 0, 0));

            Assert.False(c.MatchAny('\0'));
            Assert.False(c.MatchAny('1'));
            Assert.False(c.MatchAny('a', 'b', 'c', '1'));
            Assert.False(c.MatchAny('1', '2', '3'));
            Assert.False(c.MatchAny('a', 'b', 'c'));
        }

        [Fact]
        public void MatchShouldMatch()
        {
            var c = new Cursor<char>("1234".ToCharArray());

            Assert.True(c.Match("1"));
            Assert.False(c.Match("2"));
            Assert.True(c.Match("123"));
            Assert.False(c.Match("11"));
            Assert.False(c.Match("122"));
            Assert.False(c.Match("1231"));

            c.ResetPosition(new TextPosition(4, 0, 0));

            Assert.False(c.Match("1"));
            Assert.False(c.Match("2"));
        }

#pragma warning disable CS0649
        private class BufferSpanHolder
        {
            public BufferSpan<char> T;
        }

        [Fact]
        public void BufferSpanShoudNotThrow()
        {
            var t = new BufferSpanHolder();

            Assert.Null(t.T.ToString());
            Assert.Equal(0, t.T.Length);
            Assert.Equal(0, t.T.Offset);

            var t2 = new BufferSpan<char>(null);

            Assert.Null(t2.ToString());
            Assert.Equal(0, t2.Length);
            Assert.Equal(0, t2.Offset);

#if NETCOREAPP3_1_OR_GREATER
            Assert.True(ReadOnlySpan<char>.Empty == t2.Span);
#endif
        }
#pragma warning restore
    }
}
