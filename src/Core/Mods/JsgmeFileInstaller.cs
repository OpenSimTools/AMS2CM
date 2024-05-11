using Core.Utils;

namespace Core.Mods;

public static class JsgmeFileInstaller
{
    public const string RemoveFileSuffix = "-remove";

    /// <summary>
    /// Install mod directory.
    /// </summary>
    /// <param name="srcPath">Directory containing extracted mod archive</param>
    /// <param name="dstPath">Game directory</param>
    /// <param name="callbacks">Relative file path processing callbacks</param>
    public static void InstallFiles(string srcPath, string dstPath, ProcessingCallbacks<string> callbacks) =>
        RecursiveMoveWithBackup(srcPath, srcPath, dstPath, callbacks);

    private static void RecursiveMoveWithBackup(string rootPath, string srcPath, string dstPath, ProcessingCallbacks<string> callbacks)
    {
        if (!Directory.Exists(dstPath))
        {
            Directory.CreateDirectory(dstPath);
        }

        foreach (var maybeSrcSubPath in Directory.GetFileSystemEntries(srcPath))
        {
            var (srcSubPath, remove) = NeedsRemoving(maybeSrcSubPath);

            var localName = Path.GetFileName(srcSubPath);

            var dstSubPath = Path.Combine(dstPath, localName);
            if (Directory.Exists(srcSubPath)) // Is directory
            {
                RecursiveMoveWithBackup(rootPath, srcSubPath, dstSubPath, callbacks);
                continue;
            }

            var relativePath = Path.GetRelativePath(rootPath, srcSubPath);
            if (!callbacks.Accept(relativePath))
            {
                callbacks.NotAccepted(relativePath);
                continue;
            }

            callbacks.Before(relativePath);

            if (!remove)
            {
                File.Move(srcSubPath, dstSubPath);
            }

            callbacks.After(relativePath);
        }
    }

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(RemoveFileSuffix) ?
            (filePath.RemoveSuffix(RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }

    /// <summary>
    /// Uninstall mod files.
    /// </summary>
    /// <param name="dstPath">Game directory</param>
    /// <param name="files">Perviously installed mod files</param>
    /// <param name="callbacks">Relative file path processing callbacks</param>
    public static void UninstallFiles(string dstPath, IEnumerable<string> files, ProcessingCallbacks<string> callbacks)
    {
        // *****************************************************************************************************
        // TODO All this should be moved to the ModInstaller since it simply restores the backup and deletes files
        // *****************************************************************************************************
        var fileList = files.ToList(); // It must be enumerated twice
        foreach (var relativePath in fileList)
        {
            var fullPath = Path.Combine(dstPath, relativePath);

            if (!callbacks.Accept(relativePath))
            {
                callbacks.NotAccepted(relativePath);
                continue;
            }

            callbacks.Before(relativePath);

            // Delete will fail if the parent directory does not exist
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            callbacks.After(relativePath);
        }
        DeleteEmptyDirectories(dstPath, fileList);
    }

    private static void DeleteEmptyDirectories(string dstRootPath, IEnumerable<string> filePaths) {
        var dirs = filePaths
            .Select(file => Path.Combine(dstRootPath, file))
            .SelectMany(dstFilePath => AncestorsUpTo(dstRootPath, dstFilePath))
            .Distinct()
            .OrderByDescending(name => name.Length);
        foreach (var dir in dirs)
        {
            // Some mods have duplicate entries, so files might have been removed already
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static IEnumerable<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path); dir is not null && dir.FullName != root; dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }
}