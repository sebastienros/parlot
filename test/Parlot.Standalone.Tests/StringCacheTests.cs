using System;
using System.Threading.Tasks;
using System.Reflection;
using Xunit;

namespace Parlot.Standalone.Tests;

// Exercise the actual embedded support class without a runtime Parlot reference.
public class StringCacheTests
{
    [Fact]
    public void AdmitsOnSecondMissAndReplacesCollisionsOnlyAfterTwoMisses()
    {
        var cache = new Cache(1, 256);
        var first = cache.GetString("alpha".AsSpan());
        var admitted = cache.GetString("alpha".AsSpan());
        Assert.NotSame(first, admitted);
        Assert.Same(admitted, cache.GetString("alpha".AsSpan()));
        _ = cache.GetString("beta".AsSpan());
        Assert.Same(admitted, cache.GetString("alpha".AsSpan()));
        var replacement = cache.GetString("beta".AsSpan());
        Assert.Same(replacement, cache.GetString("beta".AsSpan()));
        Assert.Equal("alpha", cache.GetString("alpha".AsSpan()));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(32, 0)]
    [InlineData(1, 3)]
    public void DisabledAndOversizedValuesBypass(int capacity, int maximumLength)
    {
        var cache = new Cache(capacity, maximumLength);
        Assert.NotSame(cache.GetString("abcdef".AsSpan()), cache.GetString("abcdef".AsSpan()));
        Assert.Same(string.Empty, cache.GetString(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void DisableDropsTheTableAndNonPowerOfTwoCapacityWorks()
    {
        var cache = new Cache(3, 256);
        _ = cache.GetString("hello".AsSpan());
        var admitted = cache.GetString("hello".AsSpan());
        Assert.Same(admitted, cache.GetString("hello".AsSpan()));
        cache.Disable();
        Assert.NotSame(admitted, cache.GetString("hello".AsSpan()));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => new Cache(int.MaxValue, 32)).InnerException);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    public void LengthBoundary(int length)
    {
        var cache = new Cache(8, 256);
        var text = new string('雪', length);
        _ = cache.GetString(text.AsSpan());
        var second = cache.GetString(text.AsSpan());
        var third = cache.GetString(text.AsSpan());
        Assert.Equal(text, third);
        Assert.Equal(length <= 256, ReferenceEquals(second, third));
    }

    [Fact]
    public void ConcurrentCollisionsAndDisableCannotMixValues()
    {
        var cache = new Cache(1, 256);
        Parallel.For(0, 10000, i =>
        {
            var expected = "雪\0😀" + i % 31;
            Assert.Equal(expected, cache.GetString(expected.AsSpan()));
            if (i == 5000) cache.Disable();
        });
    }

    [Theory]
    [InlineData("\"standalone-plain\"", "standalone-plain")]
    [InlineData("\"standalone\\n雪\"", "standalone\n雪")]
    public void GeneratedStringsReuseAdmittedValues(string input, string expected)
    {
        Assert.True(Grammar.TryParseString(input, out _));
        Assert.True(Grammar.TryParseString(input, out var admitted));
        Assert.True(Grammar.TryParseString(input, out var hit));
        Assert.Equal(expected, hit);
        Assert.Same(admitted, hit);
        Assert.False(Grammar.TryParseString("\"unterminated", out _));
    }

    [Fact]
    public void GeneratedMatchedTextUsesCacheAndCanonicalLiteralStillReuses()
    {
        Assert.True(Grammar.TryParseMatchedText("MaTcHeD!", out _));
        Assert.True(Grammar.TryParseMatchedText("MaTcHeD!", out var admitted));
        Assert.True(Grammar.TryParseMatchedText("MaTcHeD!", out var hit));
        Assert.Equal("MaTcHeD", hit);
        Assert.Same(admitted, hit);
        Assert.True(Grammar.TryParsePrefix("hello world", out var canonical));
        Assert.Same("hello", canonical);
    }
    // The grammar factory host runs before support sources exist. Bind after generation,
    // so these tests exercise the embedded class without introducing a Parlot reference.
    private sealed class Cache
    {
        private delegate string GetStringDelegate(ReadOnlySpan<char> value);
        private readonly GetStringDelegate _getString;
        private readonly Action _disable;

        public Cache(int capacity, int maximumLength)
        {
            var type = typeof(Grammar).Assembly.GetType("Parlot.Generated.StringCache", throwOnError: true);
            var instance = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: new object[] { capacity, maximumLength }, culture: null);
            _getString = (GetStringDelegate)Delegate.CreateDelegate(typeof(GetStringDelegate), instance,
                type.GetMethod("GetString", BindingFlags.Instance | BindingFlags.NonPublic));
            _disable = (Action)Delegate.CreateDelegate(typeof(Action), instance,
                type.GetMethod("Disable", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        public string GetString(ReadOnlySpan<char> value) => _getString(value);
        public void Disable() => _disable();
    }
}
