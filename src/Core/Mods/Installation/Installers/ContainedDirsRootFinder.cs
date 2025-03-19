using System.Collections.Immutable;

namespace Core.Mods.Installation.Installers;

internal class ContainedDirsRootFinder : IRootFinder
{
    private readonly ImmutableHashSet<string> rootDirs;

    internal ContainedDirsRootFinder(IEnumerable<string> rootDirs)
    {
        this.rootDirs = rootDirs.ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);
    }

    // Simplistic implementation. Not the best performance, but short and readable.
    public IRootFinder.RootPaths FromDirectoryList(IEnumerable<string> directories) =>
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
            .Aggregate(new IRootFinder.RootPaths(), (roots, potentialRoot) =>
                roots.AddIfAncestorNotPresent(potentialRoot)
            );
}
