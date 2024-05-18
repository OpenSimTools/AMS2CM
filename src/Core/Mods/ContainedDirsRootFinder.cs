using System.Collections.Immutable;

namespace Core.Mods;

internal class ContainedDirsRootFinder : IRootFinder
{
    private readonly ImmutableHashSet<string> rootDirs;

    internal ContainedDirsRootFinder(IEnumerable<string> rootDirs)
    {
        this.rootDirs = rootDirs.ToImmutableHashSet();
    }

    // Simplistic implementation. Not the best performance, but short and readable.
    public IImmutableSet<string> FromDirectoryList(IEnumerable<string> directories) =>
        directories
            .SelectMany(file =>
            {
                var dirSegments = file.Split(Path.DirectorySeparatorChar);
                var segmentsUntilRoot = dirSegments.TakeWhile(_ => !rootDirs.Contains(_));
                if (dirSegments.Length == segmentsUntilRoot.Count())
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