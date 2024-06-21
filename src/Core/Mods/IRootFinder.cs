namespace Core.Mods;

public interface IRootFinder
{
    RootPaths FromDirectoryList(IEnumerable<string> directories);

    /// <remarks>
    /// This could be made mutable using a builder pattern, but it wasn't
    /// because of how simple it is and how it is used in the software.
    /// </remarks>
    public record RootPaths()
    {
        internal List<string> Roots = new();

        public string? GetPathFromRoot(string path)
        {
            foreach (var root in Roots)
            {
                // Empty path is ancestor of any path
                if (root.Length == 0)
                {
                    return path;
                }
                // Adding directory separator prevents substring match in directory name
                if ($"{path}{Path.DirectorySeparatorChar}".StartsWith($"{root}{Path.DirectorySeparatorChar}"))
                {
                    return Path.GetRelativePath(root, path);
                }
            }
            return null;
        }

        public RootPaths AddIfAncestorNotPresent(string path)
        {
            if (GetPathFromRoot(path) is null)
            {
                Roots.Add(path);
            }
            return this;
        }
    }
}