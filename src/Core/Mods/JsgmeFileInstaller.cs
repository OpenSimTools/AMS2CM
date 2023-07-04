using Core.Utils;

namespace Core.Mods;

public static class JsgmeFileInstaller
{
    public delegate void AfterFileCallback(string relativePath);
    public delegate bool BeforeFileCallback(string relativePath);

    private const string BackupFileSuffix = ".orig";
    public const string RemoveFileSuffix = "-remove";

    private static readonly string[] ExcludeCopySuffix =
    {
        BackupFileSuffix
    };

    /// <summary>
    /// Install mod directory.
    /// </summary>
    /// <param name="srcPath">Directory containing extracted mod archive</param>
    /// <param name="dstPath">Game directory</param>
    /// <param name="beforeFileCallback">Function to decide if a file should be installed</param>
    /// <param name="afterFileCallback">Callback to allow partial file installation to be detected</param>
    public static void InstallFiles(string srcPath, string dstPath, BeforeFileCallback beforeFileCallback, AfterFileCallback afterFileCallback) =>
        RecursiveMoveWithBackup(srcPath, dstPath,
            absoluteSrcFilePath => beforeFileCallback(Path.GetRelativePath(srcPath, absoluteSrcFilePath)),
            absoluteSrcFilePath => afterFileCallback(Path.GetRelativePath(srcPath, absoluteSrcFilePath))
        );

    private static void RecursiveMoveWithBackup(string srcPath, string dstPath, BeforeFileCallback beforeFileCallback, AfterFileCallback afterFileCallback)
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
                RecursiveMoveWithBackup(srcSubPath, dstSubPath, beforeFileCallback, afterFileCallback);
                continue;
            }

            if (!beforeFileCallback(srcSubPath))
                continue;

            if (File.Exists(dstSubPath))
            {
                BackupFile(dstSubPath);
            }

            if (!remove)
            {
                File.Move(srcSubPath, dstSubPath);
            }

            afterFileCallback(srcSubPath);
        }
    }

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(RemoveFileSuffix) ?
            (filePath.RemoveSuffix(RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }

    private static void BackupFile(string path)
    {
        var backupFile = BackupFileName(path);
        if (File.Exists(backupFile))
        {
            // TODO message: overwriting already installed file
            File.Delete(path);
        }
        else
        {
            File.Move(path, backupFile);
        }
    }

    /// <summary>
    /// Uninstall mod files.
    /// </summary>
    /// <param name="dstPath">Game directory</param>
    /// <param name="files">Perviously installed mod files</param>
    /// <param name="beforeFileCallback">Function to decide if a file backup should be restored</param>
    /// <param name="afterFileCallback">It is called for each uninstalled file</param>
    public static void UninstallFiles(string dstPath, IEnumerable<string> files, BeforeFileCallback beforeFileCallback, AfterFileCallback afterFileCallback)
    {
        var fileList = files.ToList(); // It must be enumerated twice
        foreach (var file in fileList)
        {
            var path = Path.Combine(dstPath, file);
            // Some mods have duplicate entries, so files might have been removed already
            if (File.Exists(path))
            {
                if (!beforeFileCallback(path))
                {
                    DeleteBackup(path);
                    afterFileCallback(file);
                    continue;
                }
                File.Delete(path);
            }

            RestoreFile(path);
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

    private static void RestoreFile(string path)
    {
        var backupFilePath = BackupFileName(path);
        if (File.Exists(backupFilePath))
        {
            File.Move(backupFilePath, path);
        }
    }

    private static void DeleteBackup(string path)
    {
        var backupFilePath = BackupFileName(path);
        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
        }
    }

    private static string BackupFileName(string originalFileName) => $"{originalFileName}{BackupFileSuffix}";
}