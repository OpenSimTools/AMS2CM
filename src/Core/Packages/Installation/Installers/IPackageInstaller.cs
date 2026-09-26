namespace Core.Packages.Installation.Installers;

public interface IPackageInstaller : IInstaller
{
    string PackageName { get; }
    int? PackageVersionHash { get; }
    IReadOnlySet<string> PackageDependencies { get; }
}
