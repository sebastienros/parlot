#if NET10_0_OR_GREATER
using Parlot.Benchmarks;
using Xunit;

namespace Parlot.Tests;

public class WhitespaceBenchmarkFixtureTests
{
    [Fact]
    public void SearchSetsMatchProductionCharacterPredicates()
    {
        new SkipWhiteSpaceBenchmarks().Setup();
    }
}
#endif
