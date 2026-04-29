using Xunit;

namespace CombolistTools.UnitTests;

public class UserPassLineTransformerTests
{
    [Theory]
    [InlineData("zanduc:asd123", "zanduc:Asd123")]
    [InlineData("zanduc:123asd", "zanduc:123Asd")]
    [InlineData("zanduc:1w2e", "zanduc:1W2e")]
    [InlineData("user:1234", "user:1234")] // no letters -> keep as-is
    [InlineData("user:", "user:")] // empty pass -> keep as-is
    public void TransformUserPassLine_WhenMatchedAndFirstLetterIsLowercaseOrMissingLetters_ReturnsTransformedLine(string input, string expected)
    {
        var output = CombolistTools.Core.UserPassLineTransformer.TransformUserPassLine(input);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("zanduc:Asd123")]
    [InlineData("zanduc:1Asd2")]
    [InlineData("a:B:C")] // strict format requires exactly one ':'
    [InlineData("badformat")] // no ':'
    public void TransformUserPassLine_WhenFormatIsNotUserPassOrFirstLetterInPassIsUppercase_ReturnsNull(string input)
    {
        var output = CombolistTools.Core.UserPassLineTransformer.TransformUserPassLine(input);
        Assert.Null(output);
    }
}

