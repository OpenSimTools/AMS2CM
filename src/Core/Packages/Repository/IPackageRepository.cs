
namespace Core.Packages.Repository;

public interface IPackageRepository
{
    Package Upload(string sourceFilePath);
    string Enable(string packagePath);
    string Disable(string packagePath);
    IReadOnlyCollection<Package> ListEnabled();
    IReadOnlyCollection<Package> ListDisabled();
}

public record Package
(
    string Name,
    string FullPath,
    bool Enabled,
    int? FsHash
);
