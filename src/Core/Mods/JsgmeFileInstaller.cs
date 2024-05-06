using Core.Backup;
using Core.Utils;

namespace Core.Mods;

public static class JsgmeFileInstaller
{
    private static readonly IBackupStrategy backupStrategy = new SuffixBackupStrategy();

    public const string RemoveFileSuffix = "-remove";

    private static readonly string[] ExcludeCopySuffix =
    {
        SuffixBackupStrategy.BackupSuffix
    };

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
            if (ExcludeCopySuffix.Any(suffix => localName.EndsWith(suffix)))
            {
                // TODO message: blacklisted
                continue;
            }

            var dstSubPath = Path.Combine(dstPath, localName);
            if (Directory.Exists(srcSubPath)) // Is directory
            {
                RecursiveMoveWithBackup(rootPath, srcSubPath, dstSubPath, callbacks);
                continue;
            }

            var relativePath = Path.GetRelativePath(rootPath, srcSubPath);
            if (!callbacks.Accept(relativePath))
                continue;

            backupStrategy.PerformBackup(dstSubPath);

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
    /// <param name="beforeFileCallback">Function to decide if a file backup should be restored</param>
    /// <param name="afterFileCallback">It is called for each uninstalled file</param>
    public static void UninstallFiles(string dstPath, IEnumerable<string> files, Predicate<string> beforeFileCallback, Action<string> afterFileCallback)
    {
        // *****************************************************************************************************
        // TODO All this should be moved to the ModManager since it simply restores the backup and deletes files
        // *****************************************************************************************************
        var fileList = files.ToList(); // It must be enumerated twice
        foreach (var file in fileList)
        {
            var path = Path.Combine(dstPath, file);
            // Some mods have duplicate entries, so files might have been removed already
            if (File.Exists(path))
            {
                if (!beforeFileCallback(path))
                {
                    backupStrategy.DeleteBackup(path);
                    afterFileCallback(file);
                    continue;
                }
                File.Delete(path);
            }

            backupStrategy.RestoreBackup(path);
            afterFileCallback(file);
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