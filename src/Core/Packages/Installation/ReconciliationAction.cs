using System.Collections.Immutable;
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

internal abstract class ReconciliationAction<TEventHandler> where TEventHandler : PackageReconciliationService.IEventHandler
{
    public class Factory(
        string installDir,
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler)
    {
        internal string InstallDir { get; } = installDir;
        internal IBackupStrategyProvider<DateTimeOffset, TEventHandler> BackupStrategyProvider { get; } = backupStrategyProvider;
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

        private static IInstaller.Destination InstallTo(string destDir) =>
            relativePath => new RootedPath(destDir, relativePath);

        public void Execute(ProcessingCallbacks<RootedPath> callbacks)
        {
            EventHandler.InstallingPackage(PackageName);
            installer.Install(InstallTo(InstallDir), BackupStrategy, callbacks);
        }

        public string PackageName => installer.PackageName;
        public int? PackageVersionHash => installer.PackageVersionHash;
        public IReadOnlySet<string> PackageDependencies => installer.PackageDependencies;

        public IInstallation.State InstallState => installer.Installed;
        public IReadOnlySet<string> InstalledFiles => installer.InstalledFiles
                    .Where(rp => rp.Root == InstallDir)
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
                .Select(relativePath => new RootedPath(InstallDir, relativePath)).ToImmutableList();
            foreach (var gamePath in filesToUninstall)
            {
                BackupStrategy.RestoreBackup(gamePath);
                CurrentInstalledFiles.Remove(gamePath.Relative);
            }
            CurrentInstallState = IInstallation.State.NotInstalled;
            DeleteEmptyDirectories(filesToUninstall);
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
                callbacksAndRemoveShadowed.Wrap(() => {}, new RootedPath(InstallDir, relativePath));
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
            CurrentInstalledFiles = [.. state.Files];
            CurrentInstallState = state.Partial ? IInstallation.State.PartiallyInstalled : IInstallation.State.Installed;
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
        EventHandler = factory.EventHandler;
        BackupStrategy = factory.BackupStrategyProvider.BackupStrategy(installTime, EventHandler);
        InstallTime = installTime;
    }

    protected readonly string InstallDir;
    protected readonly TEventHandler EventHandler;
    protected readonly IBackupStrategy BackupStrategy;

    public DateTimeOffset InstallTime { get; }
}
