using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Installers;

internal class DirectoryInstaller : BaseDirectoryInstaller
{
    public DirectoryInstaller(string packageName, int? packageVersionHash, IDirectoryInfo sourcePath) :
        base(sourcePath, packageName, packageVersionHash)
    {
    }

    protected override void InstallFile(RootedPath destinationPath, IFileInfo fileInfo)
    {
        fileInfo.CopyTo(destinationPath.Full, overwrite: true);
    }
}
