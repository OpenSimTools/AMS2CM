namespace Core.Mods;

public static class JsgmeFileInstaller
{
    private const string BackupFileSuffix = ".orig";
    public const string RemoveFileSuffix = "-remove";

    private static readonly string[] ExcludeCopySuffix =
    {
        BackupFileSuffix
    };

    public static void InstallFiles(string srcPath, string dstPath, Action<string> fileInstalledCallback)
    {
        RecursiveMoveWithBackup(srcPath, dstPath, absoluteSrcFilePath => fileInstalledCallback(Path.GetRelativePath(srcPath, absoluteSrcFilePath)));
    }

    private static void RecursiveMoveWithBackup(string srcPath, string dstPath, Action<string> fileInstalledCallback)
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
                RecursiveMoveWithBackup(srcSubPath, dstSubPath, fileInstalledCallback);
                continue;
            }

            if (File.Exists(dstSubPath))
            {
                BackupFile(dstSubPath);
            }

            if (!remove)
            {
                File.Move(srcSubPath, dstSubPath);
            }

            fileInstalledCallback(srcSubPath);
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

    public static void RestoreOriginalState(string dstPath, IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            var dstFilePath = Path.Combine(dstPath, file);
            if (File.Exists(dstFilePath))
            {
                File.Delete(dstFilePath);
            }

            RestoreFile(dstFilePath);
        }
    }
    
    private static void RestoreFile(string path)
    {
        var backupFilePath = BackupFileName(path);
        if (File.Exists(backupFilePath))
        {
            File.Move(backupFilePath, path);
        }
    }

    private static string BackupFileName(string originalFileName) => $"{originalFileName}{BackupFileSuffix}";
}