using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Tests.Packages.Installation.Installers;

internal class StaticFilesInstaller : BaseInstaller<string>
{
    private readonly IReadOnlyDictionary<string, string> files;
    private readonly bool createFiles;

    internal StaticFilesInstaller(string packageName, int? packageFsHash, IReadOnlyDictionary<string, string> files,
        IReadOnlyCollection<string> packageDependencies) :
        base(packageName, packageFsHash, packageDependencies.ToImmutableHashSet())
    {
        createFiles = false;
        this.files = files;
    }

    internal StaticFilesInstaller(IFileSystem fs, string packageName, int? packageFsHash, IReadOnlyDictionary<string, string> files,
        IReadOnlyCollection<string> packageDependencies) :
        base(fs, packageName, packageFsHash, packageDependencies.ToImmutableHashSet())
    {
        createFiles = true;
        this.files = files;
    }

    protected override void InstallAllFiles(InstallBody body)
    {
        foreach (var file in files)
        {
            body(file.Key, file.Value);
        }
    }

    protected override void InstallFile(RootedPath destinationPath, string fileContents)
    {
        if (!createFiles)
            return;

        var parent = Path.GetDirectoryName(destinationPath.Full);
        if (parent != null)
        {
            FileSystem.Directory.CreateDirectory(parent);
        }
        FileSystem.File.WriteAllText(destinationPath.Full, fileContents);
    }

    public override IEnumerable<string> RelativeDirectoryPaths =>
        files.Keys.SelectNotNull(Path.GetDirectoryName).ToImmutableHashSet();
}
