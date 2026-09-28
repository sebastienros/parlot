#if NET10_0_OR_GREATER
using System;
using System.Linq;
using System.Text;
using Parlot.Benchmarks;
using Xunit;

namespace Parlot.Tests;

public class KeywordLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateAndOverlappingMasksPreserveFirstMatch(bool ignoreCase)
    {
        string[] words = ["", "if", "IF", "if", "a!", "A!", "!a", "!A", "b?", "_@", "_`"];
        var recognizers = KeywordLookupBenchmarks.Compile(words, ignoreCase);
        foreach (var left in Enumerable.Range(0, 128))
        {
            foreach (var right in Enumerable.Range(0, 128))
            {
                var input = new string([(char)left, (char)right]);
                var expected = Array.FindIndex(words, word => input.Equals(word,
                    ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
                Assert.Equal(expected, recognizers.Tree(input));
                Assert.Equal(expected, recognizers.Narrow(input));
                Assert.Equal(expected, recognizers.ByteTree(Encoding.ASCII.GetBytes(input)));
            }
        }

        Assert.Equal(0, recognizers.Tree(""));
        Assert.Equal(-1, recognizers.Tree("ifx"));
    }

    [Theory]
    [InlineData("Small", false)]
    [InlineData("Small", true)]
    [InlineData("Language", false)]
    [InlineData("Language", true)]
    [InlineData("Headers", false)]
    [InlineData("Headers", true)]
    [InlineData("SharedPrefix", false)]
    [InlineData("SharedPrefix", true)]
    public void ExperimentalRecognizersAgreeWithReference(string vocabulary, bool ignoreCase)
    {
        var words = KeywordLookupBenchmarks.Words(vocabulary);
        var (tree, chain, byteTree, byteChain, narrow, hash) = KeywordLookupBenchmarks.Compile(words, ignoreCase);
        var bigEndian = KeywordLookupBenchmarks.Compile(words, ignoreCase, simulateBigEndian: true);
        foreach (var word in words)
        {
            Check(word);
            Check(word.ToUpperInvariant());
            Check(word.ToLowerInvariant());
            Check(string.Concat(word.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))));
            Check(word + "x");
            Check("x" + word);
            Check(word[..^1]);
            Check(word[1..]);
            for (var offset = 0; offset < word.Length; offset++)
            {
                for (var c = 0; c < 256; c++)
                {
                    Check(word[..offset] + (char)c + word[(offset + 1)..]);
                }

                Check(word[..offset] + '\u017f' + word[(offset + 1)..]);
                Check(word[..offset] + '\u212a' + word[(offset + 1)..]);
            }
        }

        var random = new Random(1742);
        for (var i = 0; i < 2000; i++)
        {
            var input = new char[random.Next(0, 24)];
            for (var offset = 0; offset < input.Length; offset++)
            {
                input[offset] = (char)random.Next(0, 65536);
            }

            Check(new string(input));
            var bytes = new byte[random.Next(0, 24)];
            random.NextBytes(bytes);
            var expected = Reference(Encoding.Latin1.GetString(bytes));
            Assert.Equal(expected, byteTree(bytes));
            Assert.Equal(expected, byteChain(bytes));
            Assert.Equal(expected, bigEndian.ByteTree(bytes));
        }

        void Check(string input)
        {
            var expected = Reference(input);
            Assert.Equal(expected, tree(input));
            Assert.Equal(expected, narrow(input));
            Assert.Equal(expected, hash(input));
            Assert.Equal(expected, bigEndian.Tree(input));
            Assert.Equal(expected, bigEndian.Narrow(input));
            // OrdinalIgnoreCase is deliberately not the reference for the ASCII-only experiment.
            if (input.All(static c => c < 128))
            {
                Assert.Equal(expected, chain(input));
                Assert.Equal(expected, byteTree(Encoding.ASCII.GetBytes(input)));
                Assert.Equal(expected, byteChain(Encoding.ASCII.GetBytes(input)));
                Assert.Equal(expected, bigEndian.ByteTree(Encoding.ASCII.GetBytes(input)));
            }
        }

        int Reference(string input)
        {
            for (var i = 0; i < words.Length; i++)
            {
                if (Equal(words[i], input))
                {
                    return i;
                }
            }

            return -1;
        }

        bool Equal(string left, string right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                var a = (int)left[i];
                var b = (int)right[i];
                if (ignoreCase)
                {
                    a = a is >= 'a' and <= 'z' ? a - 32 : a;
                    b = b is >= 'a' and <= 'z' ? b - 32 : b;
                }

                if (a != b)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
