using System.Text.RegularExpressions;

namespace Core.Utils;

public static class StringExtensions
{
    public static string RemoveSuffix(this string s, string suffix) =>
        s.EndsWith(suffix) ? s[..^suffix.Length] : s;

    private static readonly Regex whitespacesRegex = new(@"\s+");

    public static string NormalizeWhitespaces(this string s) =>
        whitespacesRegex.Replace(s, " ").Trim();
}