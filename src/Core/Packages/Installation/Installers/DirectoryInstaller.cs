using Core.Utils;

namespace Core.Packages.Installation.Installers;

internal class DirectoryInstaller : BaseDirectoryInstaller
{
    public DirectoryInstaller(string packageName, int? packageVersionHash, string sourcePath) :
        base(packageName, packageVersionHash)
    {
        Source = new DirectoryInfo(sourcePath);
    }

    protected override DirectoryInfo Source
    {
        get;
    }

    protected override void InstallFile(RootedPath destinationPath, FileInfo fileInfo)
    {
        File.Copy(fileInfo.FullName, destinationPath.Full);
    }
}
