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

    [Fact]
    public void PoolCapacityDoesNotChangeUniqueOrRepeatedValues()
    {
        var input = new System.Text.StringBuilder("[");
        for (var i = 0; i < 300; i++)
        {
            if (i != 0) input.Append(',');
            input.Append('"').Append("value").Append(i).Append('"');
        }
        input.Append(",\"value0\",\"value299\"]");
        var array = Assert.IsType<JsonArray>(JsonDocumentPoolParser.Parse(input.ToString()));
        Assert.Equal(302, array.Elements.Count);
        for (var i = 0; i < 300; i++)
        {
            Assert.Equal("value" + i, Text(array.Elements[i]));
        }
        Assert.Same(((JsonString)array.Elements[0]).Value, ((JsonString)array.Elements[300]).Value);
        Assert.Equal("value299", Text(array.Elements[301]));
    }

    private static string Text(IJson value) => ((JsonString)value).Value;
}
