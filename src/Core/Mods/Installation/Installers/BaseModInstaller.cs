using System.Collections.Immutable;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public abstract class BaseModInstaller : IInstaller
{
    protected readonly IInstaller Inner;
    protected readonly DirectoryInfo StagingDir;
    private bool postProcessingDone;

    private readonly List<string> installedFiles = new();

    protected BaseModInstaller(IInstaller inner, ITempDir tempDir)
    {
        Inner = inner;
        StagingDir = new DirectoryInfo(Path.Combine(tempDir.BasePath, inner.PackageName));
        postProcessingDone = false;
    }

    public string PackageName => Inner.PackageName;

    public int? PackageFsHash => Inner.PackageFsHash;

    public IReadOnlyCollection<string> InstalledFiles =>
        Inner.InstalledFiles.Concat(installedFiles).ToImmutableArray();

    public IInstallation.State Installed =>
        Inner.Installed == IInstallation.State.Installed && !postProcessingDone
            ? IInstallation.State.PartiallyInstalled
            : Inner.Installed;

    public void Install(string dstPath, IBackupStrategy backupStrategy,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        Install(dstPath, () => Inner.Install(dstPath, backupStrategy, callbacks));

        postProcessingDone = true;
    }

    protected abstract void Install(string dstPath, Action innerInstall);

    protected void AddToInstalledFiles(RootedPath? installedFile)
    {
        if (installedFile is not null)
        {
            installedFiles.Add(installedFile.Relative);
        }
    }
}
