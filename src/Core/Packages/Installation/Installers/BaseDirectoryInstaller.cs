using System.Collections.Immutable;
using System.IO.Abstractions;

namespace Core.Packages.Installation.Installers;

internal abstract class BaseDirectoryInstaller : BaseInstaller<IFileInfo>
{
    protected BaseDirectoryInstaller(IDirectoryInfo source, string packageName, int? packageVersionHash) :
        base(source.FileSystem, TimeProvider.System, packageName, packageVersionHash, ImmutableHashSet<string>.Empty)
    {
        Source = source;
    }

    protected IDirectoryInfo Source { get; }

    public override IEnumerable<string> RelativeDirectoryPaths =>
        Source.EnumerateDirectories("*", RecursiveEnumeration)
            .Select(_ => Path.GetRelativePath(Source.FullName, _.FullName));

    protected override void InstallAllFiles(InstallBody body)
    {
        foreach (var fileInfo in Source.EnumerateFiles("*", RecursiveEnumeration))
        {
            var relativePath = Path.GetRelativePath(Source.FullName, fileInfo.FullName);
            body(relativePath, fileInfo);
        }
    }

    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        // Not supported by the MockFileSystem
        // IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        RecurseSubdirectories = true,
    };
}
