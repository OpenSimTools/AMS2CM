namespace Core.Packages.Repository;

public class FileSystemRepository : IPackageRepository
{
    private const string EnabledSubdir = "Enabled";
    private const string DisabledSubdir = "Disabled";

    private readonly string enabledDirPath;
    private readonly string disabledDirPath;

    internal FileSystemRepository(string repositoryDir)
    {
        enabledDirPath = Path.Combine(repositoryDir, EnabledSubdir);
        disabledDirPath = Path.Combine(repositoryDir, DisabledSubdir);
    }

    public Package Upload(string sourceFilePath)
    {
        var fileName = Path.GetFileName(sourceFilePath);

        var isDisabled = ListDisabled().Any(_ => _.Name == fileName);
        var destinationDirPath = isDisabled ? disabledDirPath : enabledDirPath;
        var destinationFilePath = Path.Combine(destinationDirPath, fileName);

        ExistingDirectoryOrCreate(destinationDirPath);
        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

        return FilePackage(new FileInfo(destinationFilePath));
    }

    public string Enable(string packagePath)
    {
        return MovePackage(packagePath, enabledDirPath);
    }

    public string Disable(string packagePath)
    {
        return MovePackage(packagePath, disabledDirPath);
    }

    private static string MovePackage(string sourceFilePath, string destinationParentPath)
    {
        ExistingDirectoryOrCreate(destinationParentPath);
        var destinationPackagePath = Path.Combine(destinationParentPath, Path.GetFileName(sourceFilePath));
        if (Directory.Exists(sourceFilePath))
        {
            Directory.Move(sourceFilePath, destinationPackagePath);
        }
        else
        {
            File.Move(sourceFilePath, destinationPackagePath);
        }
        return destinationPackagePath;
    }

    public IReadOnlyCollection<Package> ListEnabled() => ListPackages(enabledDirPath);

    public IReadOnlyCollection<Package> ListDisabled() => ListPackages(disabledDirPath);

    private IReadOnlyCollection<Package> ListPackages(string rootPath)
    {
        var directoryInfo = new DirectoryInfo(rootPath);
        if (directoryInfo.Exists)
        {
            var options = new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                RecurseSubdirectories = false,
            };
            return directoryInfo.GetFiles("*", options).Select(fileInfo => FilePackage(fileInfo))
                .Concat(directoryInfo.GetDirectories("*", options).Select(fileInfo => DirectoryPackage(fileInfo)))
                .ToList();
        }
        else
        {
            return Array.Empty<Package>();
        }
    }

    private Package FilePackage(FileInfo fileInfo) =>
        new(
            Name: fileInfo.Name,
            FullPath: fileInfo.FullName,
            Enabled: IsEnabled(fileInfo),
            FsHash: FsHash(fileInfo)
        );

    private Package DirectoryPackage(DirectoryInfo directoryInfo) =>
        new(
            Name: $"{directoryInfo.Name}{Path.DirectorySeparatorChar}",
            FullPath: directoryInfo.FullName,
            Enabled: IsEnabled(directoryInfo),
            FsHash: null
        );

    private bool IsEnabled(FileSystemInfo fileSystemInfo) =>
        Directory.GetParent(fileSystemInfo.FullName)?.FullName == enabledDirPath;

    /// <summary>
    /// Just a very simple has function to detect if the file might have changed.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private static int FsHash(FileInfo fileInfo)
    {
        return unchecked((int)(fileInfo.LastWriteTimeUtc.Ticks ^ fileInfo.Length));
    }

    private static void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
