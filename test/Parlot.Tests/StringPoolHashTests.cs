using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Parlot.Tests;

/// <summary>
/// Values which differ only in the high bits of their chars, or only between their first and last 8 chars, must still
/// hash differently, whatever the keys of the pool: a multiplication keeps a difference in the high bits of its operand
/// in the high bits of its product, and a hash which reads only part of a value lets all the values sharing it collide.
/// </summary>
public class StringPoolHashTests
{
    private const int Values = 1000;

    private delegate ulong HashOf(ReadOnlySpan<char> value);

    private delegate string Get(ReadOnlySpan<char> value);

    private static readonly Type s_poolType = typeof(TextSpan).Assembly.GetType("Parlot.StringPool", throwOnError: true)!;

    // The internal pool, created and called through reflection: its two methods take a span, which Invoke can't pass
    private sealed class Pool
    {
        public Pool(int maximumLength = 64)
        {
            var pool = Activator.CreateInstance(s_poolType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [maximumLength], null)!;

            Hash = (HashOf)s_poolType.GetMethod("HashOf", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate(typeof(HashOf), pool);
            Get = (Get)s_poolType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public, null, [typeof(ReadOnlySpan<char>)], null)!.CreateDelegate(typeof(Get), pool);
        }

        public HashOf Hash { get; }

        public Get Get { get; }
    }

    // 16 chars, in which the 4th char of each 8-byte word (chars 3, 7, 11 and 15) varies in its top 4 bits only
    private static char[][] HighBitValues() => Enumerable.Range(0, Values).Select(k =>
    {
        var chars = "abcdefghijklmnop".ToCharArray();
        chars[3] = (char)(((k >> 0) & 15) << 12 | 'd');
        chars[7] = (char)(((k >> 4) & 15) << 12 | 'h');
        chars[11] = (char)(((k >> 8) & 15) << 12 | 'l');
        chars[15] = (char)(((k >> 12) & 15) << 12 | 'p');
        return chars;
    }).ToArray();

    // Pools each value, twice so that the pool doesn't switch itself off, then asserts that each one is found again
    private static void AssertPooled(Pool pool, char[][] values)
    {
        var first = values.Select(v =>
        {
            var s = pool.Get(v);
            pool.Get(v);
            return s;
        }).ToArray();

        for (var i = 0; i < values.Length; i++)
        {
            Assert.Same(first[i], pool.Get(values[i]));
        }
    }

    [Fact]
    public void SpreadsValuesWhichDifferInTheirHighBits()
    {
        var values = HighBitValues();

        for (var round = 0; round < 3; round++)
        {
            var pool = new Pool();
            var slots = values.Select(v => pool.Hash(v) & 4095).Distinct().Count();

            // Random slots would give about 885 distinct ones
            Assert.True(slots > 800, $"{slots} distinct slots for {Values} values");
        }
    }

    [Fact]
    public void PoolsValuesWhichDifferInTheirHighBits()
    {
        AssertPooled(new Pool(), HighBitValues());
    }

    [Theory]
    [InlineData("abcA", "abc聁")]                 // short: the head is hashed twice, as head and tail
    [InlineData("abcdefgA", "abcdefg聁")]         // 8 chars: the same
    [InlineData("abcAefgA", "abc聁efg聁")]    // the top bit of two words
    public void SeparatesValuesWhichDifferInTheTopBitOfAWord(string a, string b)
    {
        for (var round = 0; round < 3; round++)
        {
            var pool = new Pool();
            Assert.NotEqual(pool.Hash(a.AsSpan()), pool.Hash(b.AsSpan()));
        }
    }

    // Flips the same bit of one or two chars of random values of each length: the hash must change, in every pool.
    // Words overlap for values of 9 to 15 chars, a short value is hashed twice, as its head and its tail, and a value
    // of more than 16 chars hashes its middle too, reusing its keys past 256 chars.
    [Theory]
    [InlineData(1, 40)]
    [InlineData(250, 270)]
    public void HashChangesWithAnyBitOfOneOrTwoChars(int minimumLength, int maximumLength)
    {
        var random = new Random(minimumLength);

        for (var length = minimumLength; length <= maximumLength; length += minimumLength == 1 ? 1 : 7)
        {
            var chars = new char[length];

            for (var c = 0; c < length; c++)
            {
                chars[c] = (char)random.Next(0x20, 0x7F);
            }

            var pool = new Pool(maximumLength: 1000);
            var hash = pool.Hash(chars);

            for (var bit = 0; bit < 16; bit++)
            {
                for (var i = 0; i < length; i++)
                {
                    // Pairs of chars far apart in long values: those in the same word or block of keys are tested
                    for (var j = i; j < length && j - i < 80; j += j - i < 16 ? 1 : 13)
                    {
                        var changed = (char[])chars.Clone();
                        changed[i] ^= (char)(1 << bit);

                        if (j != i)
                        {
                            changed[j] ^= (char)(1 << bit);
                        }

                        Assert.True(pool.Hash(changed) != hash, $"length {length}, bit {bit} of chars {i} and {j}");
                    }
                }
            }
        }
    }

    [Fact]
    public void PoolsValuesWhichDifferOnlyInTheirMiddle()
    {
        var values = Enumerable.Range(0, Values).Select(i => ("abcdefgh" + i.ToString("D6") + "ijklmnop").ToCharArray()).ToArray();

        AssertPooled(new Pool(), values);
    }

    [Theory]
    [InlineData(-1)]   // unlimited, as the lookups compare the length unsigned
    [InlineData(17)]   // a single middle char
    [InlineData(1000)] // more middle chars than keys
    public void PoolsLongValuesWithAnyMaximumLength(int maximumLength)
    {
        var pool = new Pool(maximumLength);
        var length = maximumLength < 0 ? 300 : maximumLength;
        var value = new string('x', length - 1) + "y";

        Assert.Same(pool.Get(value.AsSpan()), pool.Get(value.ToCharArray()));
    }

    [Fact]
    public void PoolsHashDifferently()
    {
        var hashes = Enumerable.Range(0, Values).Select(_ => new Pool().Hash("identifier".AsSpan())).Distinct().Count();

        Assert.Equal(Values, hashes);
    }
}
