namespace Core.Packages.Repository;

public interface IPackageRepository
{
    void Upload(string sourceFilePath);
    string Enable(string packagePath);
    string Disable(string packagePath);
    IReadOnlyCollection<IPackage> ListEnabled();
    IReadOnlyCollection<IPackage> ListDisabled();
}
