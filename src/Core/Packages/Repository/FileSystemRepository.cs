using System.IO.Abstractions;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Repository;

public class FileSystemRepository : IPackageRepository
{
    private class FilePackage(IFileInfo fileInfo) : IPackage
    {
        public string Name { get; } = fileInfo.Name;
        public int? VersionHash { get; } = FileSystemHash.Hash32(fileInfo);
        public string Location { get; } = fileInfo.FullName;
        public IPackageInstaller CreateInstaller() => new ArchiveInstaller(Name, VersionHash, Location);
    }

    private class DirectoryPackage(IDirectoryInfo directoryInfo) : IPackage
    {
        public string Name { get; } = $"{directoryInfo.Name}{directoryInfo.FileSystem.Path.DirectorySeparatorChar}";
        public int? VersionHash => null;
        public string Location { get; } = directoryInfo.FullName;
        public IPackageInstaller CreateInstaller() => new DirectoryInstaller(Name, VersionHash, Location);
    }

    internal const string EnabledSubdir = "Enabled";
    internal const string DisabledSubdir = "Disabled";

    private readonly string enabledDirPath;
    private readonly string disabledDirPath;
    private readonly IFileSystem fs;

    internal FileSystemRepository(string repositoryDir) : this(new FileSystem(), repositoryDir)
    {
    }

    internal FileSystemRepository(IFileSystem fs, string repositoryDir)
    {
        this.fs = fs;
        enabledDirPath = fs.Path.Combine(repositoryDir, EnabledSubdir);
        disabledDirPath = fs.Path.Combine(repositoryDir, DisabledSubdir);
    }

    public void Upload(string sourceFilePath)
    {
        var fileName = fs.Path.GetFileName(sourceFilePath);

        var isDisabled = ListDisabled().Any(f => f.Name == fileName);
        var destinationDirPath = isDisabled ? disabledDirPath : enabledDirPath;
        var destinationFilePath = fs.Path.Combine(destinationDirPath, fileName);

        ExistingDirectoryOrCreate(destinationDirPath);
        fs.File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
    }

    public string Enable(string packagePath)
    {
        return MovePackage(packagePath, enabledDirPath);
    }

    public string Disable(string packagePath)
    {
        return MovePackage(packagePath, disabledDirPath);
    }

    private string MovePackage(string sourceFilePath, string destinationParentPath)
    {
        ExistingDirectoryOrCreate(destinationParentPath);
        var destinationPackagePath = fs.Path.Combine(destinationParentPath, fs.Path.GetFileName(sourceFilePath));
        if (fs.Directory.Exists(sourceFilePath))
        {
            fs.Directory.Move(sourceFilePath, destinationPackagePath);
        }
        else
        {
            fs.File.Move(sourceFilePath, destinationPackagePath);
        }
        return destinationPackagePath;
    }

    public IReadOnlyCollection<IPackage> ListEnabled() => ListPackages(enabledDirPath);

    public IReadOnlyCollection<IPackage> ListDisabled() => ListPackages(disabledDirPath);

    private IReadOnlyCollection<IPackage> ListPackages(string rootPath)
    {
        var directoryInfo = fs.DirectoryInfo.New(rootPath);
        if (!directoryInfo.Exists)
        {
            return Array.Empty<IPackage>();
        }

        return directoryInfo.GetFiles().Where(IsVisible).Select(IPackage (fi) => new FilePackage(fi))
            .Concat(directoryInfo.GetDirectories().Where(IsVisible).Select(IPackage (di) => new DirectoryPackage(di)))
            .ToList();
    }

    private static bool IsVisible(IFileSystemInfo fsInfo) =>
        (fsInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;

    private void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!fs.Directory.Exists(directoryPath))
        {
            fs.Directory.CreateDirectory(directoryPath);
        }
    }
}
