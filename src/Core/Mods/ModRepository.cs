namespace Core.Mods;

public class ModRepository : IModRepository
{
    private const string EnabledModsDirName = "Enabled";
    private const string DisabledModsSubdir = "Disabled";

    private readonly string enabledModsDir;
    private readonly string disabledModsDir;

    internal ModRepository(string modsDir)
    {
        var modsDirFullPath = Path.GetFullPath(modsDir);
        enabledModsDir = Path.Combine(modsDirFullPath, EnabledModsDirName);
        disabledModsDir = Path.Combine(modsDirFullPath, DisabledModsSubdir);
    }

    public ModPackage UploadMod(string sourceFilePath)
    {
        var fileName = Path.GetFileName(sourceFilePath);

        var isDisabled = ListDisabledMods().Where(_ => _.PackageName == fileName).Any();
        var destinationDirectoryPath = isDisabled ? disabledModsDir : enabledModsDir;
        var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);

        ExistingDirectoryOrCreate(destinationDirectoryPath);
        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

        return ModFilePackage(new FileInfo(destinationFilePath));
    }

    public string EnableMod(string packagePath)
    {
        return MoveMod(packagePath, enabledModsDir);
    }

    public string DisableMod(string packagePath)
    {
        return MoveMod(packagePath, disabledModsDir);
    }

    private static string MoveMod(string sourcePackagePath, string destinationParentPath)
    {
        ExistingDirectoryOrCreate(destinationParentPath);
        var destinationPackagePath = Path.Combine(destinationParentPath, Path.GetFileName(sourcePackagePath));
        if (Directory.Exists(sourcePackagePath))
        {
            Directory.Move(sourcePackagePath, destinationPackagePath);
        }
        else
        {
            File.Move(sourcePackagePath, destinationPackagePath);
        }
        return destinationPackagePath;
    }

    public IReadOnlyCollection<ModPackage> ListEnabledMods() => ListMods(enabledModsDir);

    public IReadOnlyCollection<ModPackage> ListDisabledMods() => ListMods(disabledModsDir);

    private IReadOnlyCollection<ModPackage> ListMods(string rootPath)
    {
        var directoryInfo = new DirectoryInfo(rootPath);
        if (directoryInfo.Exists)
        {
            var options = new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                RecurseSubdirectories = false,
            };
            return directoryInfo.GetFiles("*", options).Select(fileInfo => ModFilePackage(fileInfo))
                .Concat(directoryInfo.GetDirectories("*", options).Select(fileInfo => ModDirectoryPackage(fileInfo)))
                .ToList();
        }
        else
        {
            return Array.Empty<ModPackage>();
        }
    }

    private ModPackage ModFilePackage(FileInfo modFileInfo) =>
        new(
            PackageName: modFileInfo.Name,
            FullPath: modFileInfo.FullName,
            Enabled: IsEnabled(modFileInfo),
            FsHash: FsHash(modFileInfo)
        );

    private ModPackage ModDirectoryPackage(DirectoryInfo modDirectoryInfo) =>
        new(
            PackageName: $"{modDirectoryInfo.Name}{Path.DirectorySeparatorChar}",
            FullPath: modDirectoryInfo.FullName,
            Enabled: IsEnabled(modDirectoryInfo),
            FsHash: null
        );

    private bool IsEnabled(FileSystemInfo modFileSystemInfo) =>
        Directory.GetParent(modFileSystemInfo.FullName)?.FullName == enabledModsDir;

    /// <summary>
    /// Just a very simple has function to detect if the file might have changed.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private static int FsHash(FileInfo fileInfo)
    {
        return unchecked((int)(fileInfo.LastWriteTimeUtc.Ticks ^ fileInfo.Length));
    }

    private static void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
