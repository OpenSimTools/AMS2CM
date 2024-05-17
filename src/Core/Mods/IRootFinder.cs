using System.Collections.Immutable;

namespace Core.Mods;

public interface IRootFinder
{
    IImmutableSet<string> FromFileList(IEnumerable<string> files);
}