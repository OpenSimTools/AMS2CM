using System.Collections.Immutable;

namespace Core.Mods;

internal class RootFinderFromContainedDirs : IRootFinder
{
    private readonly ImmutableHashSet<string> rootDirs;

    internal RootFinderFromContainedDirs(IEnumerable<string> rootDirs)
    {
        this.rootDirs = rootDirs.ToImmutableHashSet();
    }

    public IImmutableSet<string> FromFileList(IEnumerable<string> files) =>
        files
            .SelectMany(file =>
            {
                var dirSegments = file.Split(Path.DirectorySeparatorChar).SkipLast(1);
                var segmentsUntilRoot = dirSegments.TakeWhile(_ => !rootDirs.Contains(_));
                if (dirSegments.Count() == segmentsUntilRoot.Count())
                {
                    return Array.Empty<string>();
                }
                else
                {
                    return new[] { Path.Join(segmentsUntilRoot.ToArray()) };
                }
            })
            .ToImmutableSortedSet()
            .Aggregate(new List<string>(), (roots, potentialRoot) =>
            {
                if (!roots.Any(_ => potentialRoot.StartsWith(_)))
                {
                    roots.Add(potentialRoot);
                }
                return roots;
            })
            .ToImmutableHashSet();
}