using System.Collections.Immutable;

namespace Core.Mods;

public interface IRootFinder
{
    IImmutableSet<string> FromDirectoryList(IEnumerable<string> directories);
}