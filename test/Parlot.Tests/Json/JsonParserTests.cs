using Xunit;

namespace Parlot.Tests.Json;

public class JsonParserTests
{
    [Theory]
    [InlineData("{\"property\":\"value\"}")]
    [InlineData("{\"property\":[\"value\",\"value\",\"value\"]}")]
    [InlineData("{\"property\":{\"property\":\"value\"}}")]
    public void ShouldParseJson(string json)
    {
        var result = JsonParser.Parse(json);
        Assert.Equal(json, result.ToString());
    }

    //[Theory]
    //[InlineData("{\"property\":\"value\"}")]
    //[InlineData("{\"property\":[\"value\",\"value\",\"value\"]}")]
    //[InlineData("{\"property\":{\"property\":\"value\"}}")]
    //public void ShouldParseJsonCompiled (string json)
    //{
    //    var _compiled = CompileTests.Compile(JsonParser.Json);

    //    var scanner = new Scanner(json);
    //    var context = new ParseContext(scanner);

    //    var result = _compiled(context);
    //    Assert.Equal(json, result.ToString());
    //}
}
