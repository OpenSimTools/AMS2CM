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
                var segmentsUntilRoot = dirSegments.TakeWhile(_ => !rootDirs.Contains(_)).ToArray();
                return dirSegments.Length == segmentsUntilRoot.Length
                    ? Array.Empty<string>()
                    : new[] { Path.Join(segmentsUntilRoot) };
            })
            .ToImmutableSortedSet()
            .Aggregate(new IRootFinder.RootPaths(), (roots, potentialRoot) =>
                roots.AddIfAncestorNotPresent(potentialRoot)
            );
}
