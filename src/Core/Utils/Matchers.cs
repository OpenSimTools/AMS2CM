using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Utils;

internal static class Matchers
{
    public static Matcher ExcludingPatterns(IEnumerable<string> exclusions)
    {
        var matcher = new Matcher();
        matcher.AddInclude(@"**\*");
        matcher.AddExcludePatterns(exclusions);
        return matcher;
    }
}
