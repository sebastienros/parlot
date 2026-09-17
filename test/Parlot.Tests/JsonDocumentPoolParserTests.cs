using System.Threading.Tasks;
using Parlot.Tests.Json;
using Xunit;

namespace Parlot.Tests;

public class JsonDocumentPoolParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesTokensKeysEscapesAndFailureBehavior(bool optimize)
    {
        var array = Assert.IsType<JsonArray>(JsonDocumentPoolParser.Parse("[{\"key\":\"a\\nb\",\"key\":\"final\"},\"\\u0122\"]", optimize));
        Assert.Equal("\u0122", Text(array.Elements[1]));
        var obj = Assert.IsType<JsonObject>(array.Elements[0]);
        Assert.Equal("final", Text(obj.Members["key"]));
        Assert.Single(obj.Members);
        Assert.Equal("a\nb", Text(JsonDocumentPoolParser.Parse("\"a\\nb\"", optimize)));
        Assert.Null(JsonDocumentPoolParser.Parse("[\"unfinished", optimize));
        Assert.Null(JsonDocumentPoolParser.Parse("[\"valid\"]trailing", optimize));
        Assert.NotNull(JsonDocumentPoolParser.Parse("[\"valid\"]", optimize));
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
                var array = (JsonArray)JsonDocumentPoolParser.Parse(input);
                Assert.Equal(token, Text(array.Elements[0]));
                Assert.Equal(token, Text(array.Elements[1]));
                if (token.Length <= 32)
                {
                    Assert.Same(((JsonString)array.Elements[0]).Value, ((JsonString)array.Elements[1]).Value);
                }
            }
        });
    }

    private static string Text(IJson value) => ((JsonString)value).Value;
}
