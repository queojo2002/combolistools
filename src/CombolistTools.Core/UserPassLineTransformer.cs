using System.Globalization;

namespace CombolistTools.Core;

public static class UserPassLineTransformer
{
    // Returns:
    // - transformed line if the input matches `user:pass` and should be kept
    // - null if the input does not match strict `user:pass` format, or if it should be skipped
    public static string? TransformUserPassLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        var firstColon = line.IndexOf(':');
        if (firstColon <= 0) return null; // user must be non-empty
        if (firstColon != line.LastIndexOf(':')) return null; // strict exactly one ':'

        var user = line[..firstColon];
        var pass = line[(firstColon + 1)..];

        if (string.IsNullOrEmpty(user)) return null;

        var firstLetterIndex = FindFirstLetterIndex(pass);
        if (firstLetterIndex < 0)
        {
            // No letters in pass => keep as-is (rule only mentions skipping when first letter is uppercase)
            return line;
        }

        var firstLetter = pass[firstLetterIndex];
        if (char.IsUpper(firstLetter))
        {
            // Skip if the first alphabetic character in pass is uppercase
            return null;
        }

        var uppercased = char.ToUpper(firstLetter, CultureInfo.InvariantCulture);
        if (uppercased == firstLetter) return line; // defensive; shouldn't happen given IsUpper check

        var transformedPass = pass[..firstLetterIndex] + uppercased + pass[(firstLetterIndex + 1)..];
        return user + ":" + transformedPass;
    }

    private static int FindFirstLetterIndex(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i])) return i;
        }

        return -1;
    }
}

