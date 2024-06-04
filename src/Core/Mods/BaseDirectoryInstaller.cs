namespace Core.Mods;

internal abstract class BaseDirectoryInstaller : BaseInstaller<FileInfo>
{
    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        MatchType = MatchType.Win32,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        RecurseSubdirectories = true,
    };

    public BaseDirectoryInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config) :
        base(packageName, packageFsHash, tempDir, config)
    {
    }

    protected abstract DirectoryInfo Source { get; }

    protected override IEnumerable<string> RelativeDirectoryPaths =>
        Source.EnumerateDirectories("*", RecursiveEnumeration)
            .Select(_ => Path.GetRelativePath(Source.FullName, _.FullName));

    protected override void InstalAllFiles(InstallBody body)
    {
        foreach (var fileInfo in Source.EnumerateFiles("*", RecursiveEnumeration))
        {
            var relativePath = Path.GetRelativePath(Source.FullName, fileInfo.FullName);
            body(relativePath, fileInfo);
        }
    }
}
