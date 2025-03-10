using System.Collections.Immutable;
using Core.Games;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods.Installation.Installers;

public abstract class BaseModInstaller : IInstaller
{
    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot
        {
            get;
        }

        IEnumerable<string> ExcludedFromInstall
        {
            get;
        }
    }

    protected readonly IInstaller Inner;
    protected readonly IGame Game;
    protected readonly DirectoryInfo StagingDir;

    private readonly Lazy<IRootFinder.RootPaths> rootPaths;
    private readonly Matcher filesToInstallMatcher;

    private bool postProcessingDone;

    private readonly List<RootedPath> localInstalledFiles = new();

    protected BaseModInstaller(IInstaller inner, IGame game, ITempDir tempDir, IConfig config)
    {
        Inner = inner;
        Game = game;
        StagingDir = new DirectoryInfo(Path.Combine(tempDir.BasePath, inner.PackageName));
        var rootFinder = new ContainedDirsRootFinder(config.DirsAtRoot);
        rootPaths = new Lazy<IRootFinder.RootPaths>(
            () => rootFinder.FromDirectoryList(Inner.RelativeDirectoryPaths));
        filesToInstallMatcher = Matchers.ExcludingPatterns(config.ExcludedFromInstall);
        postProcessingDone = false;
    }

    public string PackageName => Inner.PackageName;

    public int? PackageFsHash => Inner.PackageFsHash;

    public IReadOnlyCollection<RootedPath> InstalledFiles =>
        Inner.InstalledFiles
            .Concat(localInstalledFiles)
            .Where(RootIsNotStagingDir)
            .ToImmutableArray();

    public IInstallation.State Installed =>
        Inner.Installed == IInstallation.State.Installed && !postProcessingDone
            ? IInstallation.State.PartiallyInstalled
            : Inner.Installed;

    public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        Install(() => Inner.Install(
            ConfigToStagingDir(destination),
            backupStrategy,
            IgnoreForStagedFiles(callbacks.AndAccept(Whitelisted))));

        postProcessingDone = true;
    }

    public IEnumerable<string> RelativeDirectoryPaths =>
        Inner.RelativeDirectoryPaths.SelectNotNull(rootPaths.Value.GetPathFromRoot);

    protected abstract void Install(Action innerInstall);

    protected void AddToInstalledFiles(RootedPath? installedFile)
    {
        if (installedFile is not null)
        {
            localInstalledFiles.Add(installedFile);
        }
    }

    private IInstaller.Destination ConfigToStagingDir(IInstaller.Destination destination) =>
        pathInPackage =>
        {
            var relativePathFromRoot = rootPaths.Value.GetPathFromRoot(pathInPackage);
            return relativePathFromRoot is null
                ? new RootedPath(StagingDir.FullName, pathInPackage)
                // If part of a game root, return the destination relative to that root
                : destination(relativePathFromRoot);
        };

    private bool Whitelisted(RootedPath path) =>
        filesToInstallMatcher.Match(path.Relative).HasMatches;

    private ProcessingCallbacks<RootedPath> IgnoreForStagedFiles(ProcessingCallbacks<RootedPath> callbacks) =>
        callbacks with
        {
            // Do not call nested functions if extracted to staging directory
            Accept = AlwaysAllowStagedFiles(callbacks.Accept),
            Before = IgnoreForStagedFiles(callbacks.Before),
            After = IgnoreForStagedFiles(callbacks.After)
        };

    private Predicate<RootedPath> AlwaysAllowStagedFiles(Predicate<RootedPath> predicate) => rp =>
        RootIsStagingDir(rp) || predicate(rp);

    private Action<RootedPath> IgnoreForStagedFiles(Action<RootedPath> action) => rp =>
    {
        if (RootIsNotStagingDir(rp))
        {
            action(rp);
        }
    };

    private bool RootIsNotStagingDir(RootedPath rp) =>
        rp.Root != StagingDir.FullName;

    private bool RootIsStagingDir(RootedPath rp) =>
        rp.Root == StagingDir.FullName;
}
