using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

internal class ReplacementInstaller : IPackageInstaller
{
    internal class Factory<TEventHandler>(
        string installDir,
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler)
    {
        public IPackageInstaller Uninstall(string packageName, PackageInstallationState state) =>
            WithBehaviour(Behaviour.Uninstall, packageName, state);

        public IPackageInstaller Keep(string packageName, PackageInstallationState state) =>
            WithBehaviour(Behaviour.Keep, packageName, state);

        private IPackageInstaller WithBehaviour(Behaviour behaviour, string packageName,
            PackageInstallationState state)
        {
            var backupStrategy = backupStrategyProvider.BackupStrategy(state.Time, eventHandler);
            return new ReplacementInstaller(behaviour, packageName, state, installDir, backupStrategy);
        }
    }

    private readonly IBackupStrategy backupStrategy;
    public IReadOnlySet<RootedPath> InstalledFiles => filesStillInstalled.ToImmutableHashSet();
    private readonly HashSet<RootedPath> filesStillInstalled;
    public IInstallation.State Installed { get; private set; }
    public DateTimeOffset InstallTime { get; }
    public IEnumerable<string> RelativeDirectoryPaths => Array.Empty<string>();
    public string PackageName { get; }
    public int? PackageVersionHash { get; }
    public IReadOnlySet<string> PackageDependencies { get; }

    private enum Behaviour
    {
        Uninstall,
        Keep
    }

    private readonly Behaviour behaviour;

    private ReplacementInstaller(Behaviour behaviour, string packageName, PackageInstallationState packageInstallationState, string installDir, IBackupStrategy backupStrategy)
    {
        this.behaviour = behaviour;
        this.backupStrategy = backupStrategy;
        Installed = packageInstallationState.Partial ?
            IInstallation.State.PartiallyInstalled :
            IInstallation.State.Installed;
        InstallTime = packageInstallationState.Time;
        filesStillInstalled = packageInstallationState.Files
            .Select(relativePath => new RootedPath(installDir, relativePath))
            .ToHashSet();
        PackageName = packageName;
        PackageVersionHash = packageInstallationState.VersionHash;
        PackageDependencies = packageInstallationState.Dependencies.ToHashSet();
    }

    public void Install(IInstaller.Destination destination,
        IBackupStrategy _,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        switch (behaviour)
        {
            case Behaviour.Keep:
                var callbacksAndRemoveShadowed = callbacks.AndNotAccepted(rp => filesStillInstalled.Remove(rp));
                foreach (var gamePath in filesStillInstalled.ToImmutableList())
                {
                    callbacksAndRemoveShadowed.Wrap(() => {}, gamePath);
                }
                break;
            case Behaviour.Uninstall:
                Installed = IInstallation.State.PartiallyInstalled;
                var filesToUninstall = filesStillInstalled.ToImmutableList();
                foreach (var gamePath in filesToUninstall)
                {
                    backupStrategy.RestoreBackup(gamePath);
                    filesStillInstalled.Remove(gamePath);
                }
                Installed = IInstallation.State.NotInstalled;
                DeleteEmptyDirectories(filesToUninstall);
                break;
        }
    }

    private static void DeleteEmptyDirectories(IReadOnlyCollection<RootedPath> filePaths)
    {
        var dirs = filePaths
            .SelectMany(file => AncestorsUpTo(file.Root, file.Full))
            .Distinct()
            .OrderByDescending(name => name.Length);
        foreach (var dir in dirs)
        {
            // Some packages have duplicate entries, so files might have been removed already
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static List<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path);
             dir is not null && dir.FullName != root;
             dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }
}
