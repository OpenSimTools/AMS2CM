using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public interface IReconciliationAction
{
    string PackageName { get; }
    int? PackageVersionHash { get; }
    IReadOnlySet<string> PackageDependencies { get; }
    DateTimeOffset InstallTime { get; }
    IInstallation.State InstallState { get; }
    IReadOnlySet<string> InstalledFiles { get; }

    void Execute(ProcessingCallbacks<RootedPath> callbacks);
}

internal abstract class ReconciliationAction<TEventHandler>
    where TEventHandler : PackageReconciliationService.IEventHandler
{
    public class Factory(
        IDirectoryInfo installDir,
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler)
    {
        internal IDirectoryInfo InstallDir { get; } = installDir;
        internal PathRoot InstallRoot { get; } = new(installDir);

        internal IBackupStrategyProvider<DateTimeOffset, TEventHandler> BackupStrategyProvider { get; } =
            backupStrategyProvider;

        internal TEventHandler EventHandler { get; } = eventHandler;

        public IReconciliationAction Install(IPackageInstaller installer) =>
            new InstallAction(installer, this);

        public IReconciliationAction Uninstall(string packageName, PackageInstallationState state) =>
            new UninstallAction(packageName, state, this);

        public IReconciliationAction Keep(string packageName, PackageInstallationState state) =>
            new KeepAction(packageName, state, this);
    }

    private class InstallAction : ReconciliationAction<TEventHandler>, IReconciliationAction
    {
        private readonly IPackageInstaller installer;

        internal InstallAction(IPackageInstaller installer,
            Factory factory) : base(installer.InstallTime, factory)
        {
            this.installer = installer;
        }

        public void Execute(ProcessingCallbacks<RootedPath> callbacks)
        {
            EventHandler.InstallingPackage(PackageName);
            installer.Install(InstallRoot.Combine, BackupStrategy, callbacks);
        }

        public string PackageName => installer.PackageName;
        public int? PackageVersionHash => installer.PackageVersionHash;
        public IReadOnlySet<string> PackageDependencies => installer.PackageDependencies;

        public IInstallation.State InstallState => installer.Installed;

        public IReadOnlySet<string> InstalledFiles => installer.InstalledFiles
            .Where(rp => rp.Root == InstallRoot)
            .Select(rp => rp.Relative)
            .ToImmutableHashSet();
    }

    private class UninstallAction : StateBasedAction, IReconciliationAction
    {
        internal UninstallAction(
            string packageName,
            PackageInstallationState originalState,
            Factory factory) : base(packageName, originalState, factory)
        {
        }

        public void Execute(ProcessingCallbacks<RootedPath> callbacks)
        {
            EventHandler.UninstallingPackage(PackageName);
            CurrentInstallState = IInstallation.State.PartiallyInstalled;
            var filesToUninstall = CurrentInstalledFiles
                .Select(InstallRoot.Combine).ToImmutableList();
            foreach (var gamePath in filesToUninstall)
            {
                BackupStrategy.RestoreBackup(gamePath);
                CurrentInstalledFiles.Remove(gamePath.Relative);
            }

            CurrentInstallState = IInstallation.State.NotInstalled;
            DeleteEmptyDirectories(filesToUninstall);
        }

        private void DeleteEmptyDirectories(IReadOnlyCollection<RootedPath> filePaths)
        {
            var dirs = filePaths
                .SelectMany(AncestorsUpToRoot)
                .Distinct()
                .OrderByDescending(name => name.Length);
            foreach (var dir in dirs)
            {
                // Some packages have duplicate entries, so files might have been removed already
                if (InstallDir.FileSystem.Directory.Exists(dir) &&
                    !InstallDir.FileSystem.Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    InstallDir.FileSystem.Directory.Delete(dir);
                }
            }
        }

        private List<string> AncestorsUpToRoot(RootedPath rp)
        {
            var ancestors = new List<string>();
            for (var dir = rp.Parent(); dir?.Parent() is not null; dir = dir.Parent())
            {
                ancestors.Add(dir.Full);
            }
            return ancestors;
        }
    }

    private class KeepAction : StateBasedAction, IReconciliationAction
    {
        internal KeepAction(
            string packageName,
            PackageInstallationState originalState,
            Factory factory) : base(packageName, originalState, factory)
        {
        }

        public void Execute(ProcessingCallbacks<RootedPath> callbacks)
        {
            EventHandler.SkippingPackage(PackageName);
            CurrentInstallState = IInstallation.State.PartiallyInstalled;
            var callbacksAndRemoveShadowed = callbacks.AndNotAccepted(rp => CurrentInstalledFiles.Remove(rp.Relative));
            foreach (var relativePath in CurrentInstalledFiles.ToImmutableList())
            {
                callbacksAndRemoveShadowed.Wrap(() => { }, InstallRoot.Combine(relativePath));
            }

            CurrentInstallState = IInstallation.State.Installed;
        }
    }

    private abstract class StateBasedAction : ReconciliationAction<TEventHandler>
    {
        private readonly PackageInstallationState originalState;
        protected readonly HashSet<string> CurrentInstalledFiles;
        protected IInstallation.State CurrentInstallState;

        protected StateBasedAction(
            string packageName,
            PackageInstallationState state,
            Factory factory) : base(state.Time, factory)
        {
            PackageName = packageName;
            originalState = state;
            CurrentInstalledFiles = [.. state.Files.Select(path => InstallRoot.Combine(path).Relative)];
            CurrentInstallState =
                state.Partial ? IInstallation.State.PartiallyInstalled : IInstallation.State.Installed;
        }

        public string PackageName { get; }
        public int? PackageVersionHash => originalState.VersionHash;
        public IReadOnlySet<string> PackageDependencies => originalState.Dependencies.ToImmutableHashSet();

        public IInstallation.State InstallState => CurrentInstallState;
        public IReadOnlySet<string> InstalledFiles => CurrentInstalledFiles.ToImmutableHashSet();
    }

    protected ReconciliationAction(
        DateTimeOffset installTime,
        Factory factory)
    {
        InstallDir = factory.InstallDir;
        InstallRoot = factory.InstallRoot;
        EventHandler = factory.EventHandler;
        BackupStrategy = factory.BackupStrategyProvider.BackupStrategy(installTime, EventHandler);
        InstallTime = installTime;
    }

    protected readonly IDirectoryInfo InstallDir;
    protected readonly PathRoot InstallRoot;
    protected readonly TEventHandler EventHandler;
    protected readonly IBackupStrategy BackupStrategy;

    public DateTimeOffset InstallTime { get; }
}
