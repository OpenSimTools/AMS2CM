namespace Core.Utils;

public static class StringExtensions
{
    public static string RemoveSuffix(this string s, string suffix)
    {
        return s.EndsWith(suffix) ? s[..^suffix.Length] : s;
    }
}