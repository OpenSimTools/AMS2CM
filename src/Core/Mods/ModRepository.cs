namespace Core.Mods;

public class ModRepository : IModRepository
{
    private const string EnabledModsDirName = "Enabled";
    private const string DisabledModsSubdir = "Disabled";

    private readonly string enabledModArchivesDir;
    private readonly string disabledModArchivesDir;

    internal ModRepository(string modsDir)
    {
        enabledModArchivesDir = Path.Combine(modsDir, EnabledModsDirName);
        disabledModArchivesDir = Path.Combine(modsDir, DisabledModsSubdir);
    }

    public ModPackage UploadMod(string sourceFilePath)
    {
        var fileName = Path.GetFileName(sourceFilePath);

        var isDisabled = ListDisabledMods().Where(_ => _.PackageName == fileName).Any();
        var destinationDirectoryPath = isDisabled ? disabledModArchivesDir : enabledModArchivesDir;
        var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);

        ExistingDirectoryOrCreate(destinationDirectoryPath);
        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

        return ModPackageFrom(destinationDirectoryPath, new FileInfo(destinationFilePath));
    }

    public string EnableMod(string packagePath)
    {
        return MoveMod(packagePath, enabledModArchivesDir);
    }

    public string DisableMod(string packagePath)
    {
        return MoveMod(packagePath, disabledModArchivesDir);
    }

    private static string MoveMod(string packagePath, string destinationDirectoryPath)
    {
        ExistingDirectoryOrCreate(destinationDirectoryPath);
        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(packagePath));
        File.Move(packagePath, destinationFilePath);
        return destinationFilePath;
    }

    public IReadOnlyCollection<ModPackage> ListEnabledMods() => ListMods(enabledModArchivesDir);

    public IReadOnlyCollection<ModPackage> ListDisabledMods() => ListMods(disabledModArchivesDir);

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
                MaxRecursionDepth = 0,
            };
            return directoryInfo.GetFiles("*", options)
                .Select(fileInfo => ModPackageFrom(rootPath, fileInfo))
                .ToList();
        }
        else
        {
            return Array.Empty<ModPackage>();
        }
    }

    private static ModPackage ModPackageFrom(string rootPath, FileInfo modFileInfo)
    {
        return new ModPackage
        (
            Name: Path.GetFileNameWithoutExtension(modFileInfo.Name),
            PackageName: modFileInfo.Name,
            FullPath: modFileInfo.FullName,
            Enabled: Path.GetDirectoryName(rootPath) == EnabledModsDirName,
            FsHash: FsHash(modFileInfo)
        );
    }

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