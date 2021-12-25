using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;
using static Parlot.Tests.Models.RewriteTests;

namespace Parlot.Tests
{
    public partial class RewriteTests
    {
        [Fact]
        public void TextLiteralShouldBeSeekable()
        {
            var text = Literals.Text("hello");
            var seekable = text as ISeekable;

            Assert.NotNull(seekable);
            Assert.True(seekable.CanSeek);
            Assert.Equal(new[] { 'h' }, seekable.ExpectedChars);
            Assert.False(seekable.SkipWhitespace);
        }

        [Fact]
        public void SkipWhiteSpaceShouldBeSeekable()
        {
            var text = Terms.Text("hello");
            var seekable = text as ISeekable;

            Assert.NotNull(seekable);
            Assert.True(seekable.CanSeek);
            Assert.Equal(new[] { 'h' }, seekable.ExpectedChars);
            Assert.True(seekable.SkipWhitespace);
        }

        [Fact]
        public void CharLiteralShouldBeSeekable()
        {
            var text = Literals.Char('a');
            var seekable = text as ISeekable;

            Assert.NotNull(seekable);
            Assert.True(seekable.CanSeek);
            Assert.Equal(new[] { 'a' }, seekable.ExpectedChars);
            Assert.False(seekable.SkipWhitespace);
        }

        [Fact]
        public void OneOfShouldRewriteAllSeekable()
        {
            var hello = new FakeSeekable { CanSeek = true, ExpectedChars = new[] { 'a' }, SkipWhitespace = false, Success = true, Text = "hello" };
            var goodbye = new FakeSeekable { CanSeek = true, ExpectedChars = new[] { 'b' }, SkipWhitespace = false, Success = true, Text = "goodbye" };
            var oneof = Parsers.OneOf(hello, goodbye);

            Assert.Equal("hello", oneof.Parse("a"));
            Assert.Equal("goodbye", oneof.Parse("b"));
            Assert.Null(oneof.Parse("hello"));
        }

        [Fact]
        public void OneOfShouldRewriteAllSeekableCompiled()
        {
            var helloOrGoodbye = Parsers.OneOf(Terms.Text("hello"), Terms.Text("goodbye")).Compile();

            Assert.Equal("hello", helloOrGoodbye.Parse(" hello"));
            Assert.Equal("goodbye", helloOrGoodbye.Parse(" goodbye"));
            Assert.Null(helloOrGoodbye.Parse("yo!"));
        }

        [Fact]
        public void OneOfShouldRewriteAllSeekableSkipwhiteSpaceCompiled()
        {
            var hello = new FakeSeekable { CanSeek = true, ExpectedChars = new[] { 'a' }, SkipWhitespace = false, Success = true, Text = "hello" };
            var goodbye = new FakeSeekable { CanSeek = true, ExpectedChars = new[] { 'b' }, SkipWhitespace = false, Success = true, Text = "goodbye" };
            var oneof = Parsers.OneOf(hello, goodbye).Compile();

            Assert.Equal("hello", oneof.Parse("a"));
            Assert.Equal("goodbye", oneof.Parse("b"));
            Assert.Null(oneof.Parse("hello"));
        }

        [Fact]
        public void OneOfShouldNotRewriteIfOneIsNotSeekable()
        {
            var hello = new FakeSeekable { CanSeek = true, ExpectedChars = new[] { 'a' }, SkipWhitespace = false, Success = true, Text = "hello" };
            var goodbye = OneOf(Parsers.Literals.Text("goodbye"));
            var oneof = Parsers.OneOf(goodbye, hello);

            Assert.Equal("hello", oneof.Parse("a"));
            Assert.Equal("goodbye", oneof.Parse("goodbye"));
            Assert.Equal("hello", oneof.Parse("b")); // b is not found in "goodbye" so the next parser is checked and always succeeds true
        }
    }
}
