namespace Core.Mods;

internal class ModRepository
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

    public ModPackage UploadMod(string packageFullPath)
    {
        var fileName = Path.GetFileName(packageFullPath);

        var isDisabled = ListDisabledMods().Where(_ => _.PackageName == fileName).Any();
        var destinationDirectoryPath = isDisabled ? disabledModArchivesDir : enabledModArchivesDir;
        var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);

        ExistingDirectoryOrCreate(destinationDirectoryPath);
        File.Copy(packageFullPath, destinationFilePath, overwrite: true);

        return ModFrom(destinationDirectoryPath, packageFullPath);
    }

    public string EnableMod(string packagePath)
    {
        return MoveMod(packagePath, enabledModArchivesDir);
    }

    public string DisableMod(string packagePath)
    {
        return MoveMod(packagePath, disabledModArchivesDir);
    }

    private string MoveMod(string packagePath, string destinationDirectoryPath)
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
        if (Directory.Exists(rootPath))
        {
            var options = new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                MaxRecursionDepth = 0,
            };
            return Directory.EnumerateFiles(rootPath, "*", options)
                .Select(modFullPath => ModFrom(rootPath, modFullPath))
                .ToList();
        }
        else
        {
            return Array.Empty<ModPackage>();
        }
    }

    private static ModPackage ModFrom(string rootPath, string modFullPath)
    {
        var packageName = Path.GetRelativePath(rootPath, modFullPath);
        var name = Path.GetFileNameWithoutExtension(packageName);
        return new ModPackage
        (
            Name: name,
            PackageName: packageName,
            FullPath: modFullPath,
            Enabled: Path.GetDirectoryName(rootPath) == EnabledModsDirName
        );
    }

    private static void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}

internal record ModPackage
(
    string Name,
    string PackageName, // TODO: rename to ID
    string FullPath, // TODO: remove once all references are gone
    bool Enabled
);