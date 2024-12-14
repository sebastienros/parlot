using Parlot.Fluent;
using Parlot.Rewriting;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using static Parlot.Fluent.Parsers;
using static Parlot.Tests.Models.RewriteTests;

namespace Parlot.Tests;

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneOfShouldRewriteAllSeekable(bool compile)
    {
        var hello = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], SkipWhitespace = false, Success = true, Result = "hello" };
        var goodbye = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], SkipWhitespace = false, Success = true, Result = "goodbye" };
        var oneof = Parsers.OneOf(hello, goodbye);
        if (compile) oneof = oneof.Compile();

        Assert.Equal("hello", oneof.Parse("a"));
        Assert.Equal("goodbye", oneof.Parse("b"));
        Assert.Null(oneof.Parse("hello"));
    }

    [Fact]
    public void LookupTableInvokesNonSeekableInOrder()
    {
        var p1 = new FakeParser<string> { CanSeek = false, ExpectedChars = ['a'], SkipWhitespace = false, Success = true, Result = "a" };
        var p2 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], SkipWhitespace = false, Success = true, Result = "b" };
        var p3 = new FakeParser<string> { CanSeek = false, ExpectedChars = ['c'], SkipWhitespace = false, Success = true, Result = "c" };
        
        var p = OneOf(p1, p2, p3);
        
        // Parsing 'd' such that there is no match in the lookup and it invokes non-seekable parsers
        Assert.Equal("a", p.Parse("d"));

        p1.Success = false;
        
        // We know the first non-seekable parser is invoked, now check if it invokes the other
        // ones if the first fails 
        Assert.Equal("c", p.Parse("d"));
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LookupTableSkipsParsers(bool compile)
    {
        var p1 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], ThrowOnParse = true };
        var p2 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], SkipWhitespace = false, Success = true, Result = "b" };
        var p3 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['c'], SkipWhitespace = false, Success = true, Result = "c" };
        
        var p = OneOf(p1, p2, p3);
        if (compile) p = p.Compile();

        Assert.Equal("b", p.Parse("b"));
        Assert.Equal("c", p.Parse("c"));
    }

    [Fact]
    public void LookupTableInvokesAllParserWithSameLookup()
    {
        var p1 = new FakeParser<string> { CanSeek = false, ExpectedChars = ['a'], Success = false, Result = "a" };
        var p2 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], Success = true, Result = "b" };
        var p3 = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], Success = true, Result = "c" };
        
        var p = OneOf(p1, p2, p3);
        
        // Parsing 'b' such that there is a match in the lookup and it invokes all parsers
        Assert.Equal("b", p.Parse("b"));

        p2.Success = false;

        // We know the first seekable parser is invoked, now check if it invokes others in the same lookup
        Assert.Equal("c", p.Parse("b"));
    }

    [Fact]
    public void OneOfIsSeekableIfAllAreSeekable()
    {
        // OneOf can create a lookup table based on ISeekable.
        // However it can only be an ISeekable itself if all its parsers are.
        // If one is not, then the caller would not be able to invoke it.
        // This test ensures that such a parser is correctly invoked.

        var pa = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], Success = true, Result = "a" };
        var pb = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], Success = true, Result = "b" };
        var pc = new FakeParser<string> { CanSeek = false, ExpectedChars = ['c'], Success = false, Result = "c" };
        var pd = new FakeParser<string> { CanSeek = false, ExpectedChars = ['d'], Success = true, Result = "d" };

        // This one should be seekable because it only contains seekable parsers
        var p1 = OneOf(pa, pb);

        Assert.True(p1 is ISeekable seekable1 && seekable1.CanSeek);

        // This one should not be seekable because not of its parsers are. 
        var p2 = OneOf(pc, pd);
        
        Assert.False(p2 is ISeekable seekable2 && seekable2.CanSeek);

        var p3 = OneOf(p1, p2);

        Assert.Equal("a", p3.Parse("a"));
        Assert.Equal("b", p3.Parse("b"));
        Assert.Equal("d", p3.Parse("c"));
        
        // Because p2 is non-seekable and pc always succeeds, anything else returns 'c'
        Assert.Equal("d", p3.Parse("d"));
        Assert.Equal("d", p3.Parse("e"));

    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneOfCanForwardSeekable(bool compiled)
    {
        // OneOf can create a lookup table based on ISeekable.
        // It can bee seekable even if one of its parsers is not seekable.
        // In that case it creates a custom `\0` with the non-seekable parsers.

        var pa = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], Success = true, Result = "a" };
        var pb = new FakeParser<string> { CanSeek = false, ExpectedChars = ['b'], Success = true, Result = "b" };
        var pc = new FakeParser<string> { CanSeek = true, ExpectedChars = ['c'], Success = true, Result = "c" };
        var pd = new FakeParser<string> { CanSeek = false, ExpectedChars = ['d'], Success = true, Result = "d" };

        // These ones are seekable because one is at least.
        var p1 = OneOf(pa, pb);
        var p2 = OneOf(pc, pd);

        Assert.True(p1 is ISeekable seekable1 && seekable1.CanSeek);
        Assert.Equal(['a', '\0'], ((ISeekable)p1).ExpectedChars);
        Assert.True(p2 is ISeekable seekable2 && seekable2.CanSeek);
        Assert.Equal(['c', '\0'], ((ISeekable)p2).ExpectedChars);

        var p3 = OneOf(p1, p2);

        if (compiled) p3 = p3.Compile();

        Assert.Equal("a", p3.Parse("a"));
        Assert.Equal("b", p3.Parse("b"));
        Assert.Equal("b", p3.Parse("c"));  // p1's non-seekable are invoked, and pb is always successful
        Assert.Equal("b", p3.Parse("d"));

        pb.Success = false;

        Assert.Equal("a", p3.Parse("a"));
        Assert.Equal("d", p3.Parse("b")); // p1's non-seekable are invoked, now fail, p2 is invoked, 'c' doesn't match so pd is invoked and succeeds
        Assert.Equal("c", p3.Parse("c")); 
        Assert.Equal("d", p3.Parse("d"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneOfShouldFoldOneOfs(bool compiled)
    {
        // Recursive one-ofs should build a lookup table that is a combination of all the lookups.
        // There should be a single lookup to find the best match

        var pa = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], Success = true, Result = "a" };
        var pb = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], Success = true, Result = "b" };
        var pc = new FakeParser<string> { CanSeek = true, ExpectedChars = ['c'], Success = true, Result = "c" };
        var pd = new FakeParser<string> { CanSeek = true, ExpectedChars = ['d'], Success = true, Result = "d" };

        var p1 = OneOf(pa, pb);
        var p2 = OneOf(pc, pd);

        var p3 = OneOf(p1, p2);

        Assert.True(p3 is ISeekable seekable && seekable.CanSeek);
        Assert.Equal(['a', 'b', 'c', 'd'], ((ISeekable)p3).ExpectedChars);

        if (compiled) p3 = p3.Compile();

        Assert.Equal("a", p3.Parse("a"));
        Assert.Equal("b", p3.Parse("b"));
        Assert.Equal("c", p3.Parse("c"));
        Assert.Equal("d", p3.Parse("d"));
    }

    [Fact]
    public void OneOfCompiled()
    {
        var pa = new FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], Success = true, Result = "a" };
        var pb = new FakeParser<string> { CanSeek = true, ExpectedChars = ['b'], Success = true, Result = "b" };
        var pc = new FakeParser<string> { CanSeek = false, Success = true, Result = "c" };

        var p1 = OneOf(pa, pb, pc).Compile();
        Assert.Equal("a", p1.Parse("a"));
        Assert.Equal("b", p1.Parse("b"));
        Assert.Equal("c", p1.Parse("c"));

        pc.Success = false;

        Assert.Null(p1.Parse("c"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneOfShouldHandleWhiteSpace(bool compiled)
    {
        var pa = Literals.Text("a");
        var pb = Literals.Text("b");
        var pc = Terms.Text("b").Then("c");

        var p = OneOf(pa, pb, pc);

        if (compiled) p = p.Compile();

        Assert.Equal("a", p.Parse("a"));
        Assert.Equal("b", p.Parse("b"));

        Assert.Null(p.Parse(" a"));
        Assert.Equal("c", p.Parse(" b"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void OneOfShouldFindNonSeekableWithSpace(bool compiled, bool skipWhiteSpace)
    {
        var pa = Literals.Text("a");
        var pb = Literals.Text("b");
        var pc = new FakeParser<string> { CanSeek = false, Success = true, SkipWhitespace = skipWhiteSpace, Result = "c" };

        var p = OneOf(pa, pb, pc);

        if (compiled) p = p.Compile();

        Assert.Equal("a", p.Parse("a"));
        Assert.Equal("b", p.Parse("b"));
        Assert.Equal("c", p.Parse("c"));

        Assert.Equal("c", p.Parse(" a"));
        Assert.Equal("c", p.Parse(" b"));
        Assert.Equal("c", p.Parse(" c"));
    }
}
