using Core.Packages.Installation.Installers;

namespace Core.Packages;

public interface IPackage
{
    string Name { get; }
    int? VersionHash { get; }
    string? Location { get; }
    IPackageInstaller Installer { get; }
}
