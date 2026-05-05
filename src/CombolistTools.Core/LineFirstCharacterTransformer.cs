using System.Globalization;

namespace CombolistTools.Core;

public static class LineFirstCharacterTransformer
{
    public static string TransformLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var upperFirst = char.ToUpper(line[0], CultureInfo.InvariantCulture);
        return line.Length == 1 ? upperFirst.ToString() : upperFirst + line[1..];
    }
}
