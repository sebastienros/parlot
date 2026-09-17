using System.Threading.Tasks;
using Parlot.Tests.Json;
using Xunit;

namespace Parlot.Tests;

public class JsonRetainedTextParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesTokensKeysEscapesAndFailureBehavior(bool optimize)
    {
        var array = Assert.IsType<JsonArray>(JsonRetainedTextParser.Parse("[{\"key\":\"a\\nb\",\"key\":\"final\"},\"\\u0122\"]", optimize));
        Assert.Equal("\u0122", Text(array.Elements[1]));
        if (optimize)
        {
            var obj = Assert.IsType<JsonRetainedObject>(array.Elements[0]);
            Assert.Equal("final", Text(obj.Members[new TextSpan("key")]));
            Assert.Single(obj.Members);
        }
        else
        {
            var obj = Assert.IsType<JsonObject>(array.Elements[0]);
            Assert.Equal("final", Text(obj.Members["key"]));
            Assert.Single(obj.Members);
        }
        Assert.Equal("a\nb", Text(JsonRetainedTextParser.Parse("\"a\\nb\"", optimize)));
        Assert.Null(JsonRetainedTextParser.Parse("[\"unfinished", optimize));
        Assert.Null(JsonRetainedTextParser.Parse("[\"valid\"]trailing", optimize));
        Assert.NotNull(JsonRetainedTextParser.Parse("[\"valid\"]", optimize));
    }

    [Fact]
    public void DocumentStateIsIndependentAndLongTokensRemainCorrect()
    {
        Parallel.For(0, 32, i =>
        {
            foreach (var length in new[] { 8, 32, 33, 256 })
            {
                var token = new string('a', length) + i;
                var input = "[\"" + token + "\",\"" + token + "\"]";
                var array = (JsonArray)JsonRetainedTextParser.Parse(input);
                Assert.Equal(token, Text(array.Elements[0]));
                Assert.Equal(token, Text(array.Elements[1]));
                Assert.Same(input, ((JsonRetainedString)array.Elements[0]).Value.Buffer);
            }
        });
    }

    private static string Text(IJson value) => value is JsonRetainedString retained ? retained.Value.ToString() : ((JsonString)value).Value;
}
