
namespace Core.Mods;

public interface IModRepository
{
    ModPackage UploadMod(string sourceFilePath);
    string EnableMod(string packagePath);
    string DisableMod(string packagePath);
    IReadOnlyCollection<ModPackage> ListEnabledMods();
    IReadOnlyCollection<ModPackage> ListDisabledMods();
}

public record ModPackage
(
    string Name,
    string PackageName, // TODO: rename to ID
    string FullPath,
    bool Enabled,
    int? FsHash
);