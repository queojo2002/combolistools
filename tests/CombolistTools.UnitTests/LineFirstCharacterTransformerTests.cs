using Xunit;

namespace CombolistTools.UnitTests;

public class LineFirstCharacterTransformerTests
{
    [Theory]
    [InlineData("abc", "Abc")]
    [InlineData("a", "A")]
    [InlineData("123x", "123x")]
    [InlineData("aBC", "ABC")]
    [InlineData(" hello", " hello")]
    public void TransformLine_WhenNonEmpty_UppercasesFirstCharacterOnly(string input, string expected)
    {
        var output = CombolistTools.Core.LineFirstCharacterTransformer.TransformLine(input);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TransformLine_WhenEmpty_ReturnsEmpty()
    {
        Assert.Equal("", CombolistTools.Core.LineFirstCharacterTransformer.TransformLine(""));
    }
}
