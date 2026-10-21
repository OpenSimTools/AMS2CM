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
        EnumerateDirectories().Skip(1)
            .Select(_ => Path.GetRelativePath(Source.FullName, _.FullName));

    protected override void InstallAllFiles(InstallBody body)
    {
        foreach (var directory in EnumerateDirectories())
        {
            foreach (var fileInfo in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Where(IsVisible))
            {
                var relativePath = Path.GetRelativePath(Source.FullName, fileInfo.FullName);
                body(relativePath, fileInfo);
            }
        }
    }

    private IEnumerable<IDirectoryInfo> EnumerateDirectories()
    {
        var pending = new Queue<IDirectoryInfo>();
        pending.Enqueue(Source);
        while (pending.TryDequeue(out var directory))
        {
            yield return directory;
            foreach (var child in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Where(IsVisible))
            {
                pending.Enqueue(child);
            }
        }
    }

    private static bool IsVisible(IFileSystemInfo entry) =>
        (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
}
