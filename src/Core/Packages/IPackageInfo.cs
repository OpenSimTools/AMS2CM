namespace Core.Packages;

public interface IPackageInfo
{
    string PackageName { get; }
    int? PackageFsHash { get; }
}
