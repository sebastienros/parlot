using Xunit;

namespace Parlot.Tests
{
    public class CursorTests
    {
        [Fact]
        public void ShouldMatchString()
        {
            var c = new Cursor("Lorem ipsum", TextPosition.Start);

            Assert.True(c.Match(""));
            Assert.True(c.Match("Lorem"));
            Assert.True(c.Match("Lorem ipsum"));
        }

        [Fact]
        public void ShouldNotMatchString()
        {
            var c = new Cursor("Lorem ipsum", TextPosition.Start);

            Assert.False(c.Match("Lorem ipsum dolor"));
        }
    }
}
