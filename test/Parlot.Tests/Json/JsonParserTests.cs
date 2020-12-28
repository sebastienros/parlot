using Xunit;

namespace Parlot.Tests.Json
{
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
    }
}
