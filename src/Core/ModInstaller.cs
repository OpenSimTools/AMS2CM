using System.Collections.Immutable;
using Core.Backup;
using Core.Mods;
using Core.State;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core;

public class ModInstaller : IModInstaller
{
    public interface IEventHandler : IProgress
    {
        void InstallNoMods();
        void InstallStart();
        void InstallCurrent(string packageName);
        void InstallEnd();

        void PostProcessingNotRequired();
        void PostProcessingStart();
        void ExtractingBootfiles(string? packageName);
        void PostProcessingVehicles();
        void PostProcessingTracks();
        void PostProcessingDrivelines();
        void PostProcessingEnd();

        void UninstallNoMods();
        void UninstallStart();
        void UninstallCurrent(string packageName);
        void UninstallSkipModified(string filePath);
        void UninstallEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }

    private readonly IInstallationFactory installationFactory;
    private readonly IModBackupStrategyProvider modBackupStrategyProvider;

    public ModInstaller(
        IInstallationFactory installationFactory,
        IBackupStrategy  backupStrategy)
    {
        this.installationFactory = installationFactory;
        modBackupStrategyProvider = new SkipUpdatedBackupStrategy.Provider(backupStrategy);
    }

    public void Apply(
        IReadOnlyDictionary<string, ModInstallationState> currentState,
        IReadOnlyCollection<ModPackage> toInstall,
        string installDir,
        Action<IInstallation> afterCallback,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        UninstallPackages(currentState, installDir, afterCallback, eventHandler, cancellationToken);
        InstallPackages(toInstall, installDir, afterCallback, eventHandler, cancellationToken);
    }

    private void UninstallPackages(
        IReadOnlyDictionary<string, ModInstallationState> currentState,
        string installDir,
        Action<IInstallation> afterUninstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (currentState.Any())
        {
            eventHandler.UninstallStart();
            foreach (var (packageName, modInstallationState) in currentState)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var backupStrategy = modBackupStrategyProvider.BackupStrategy(modInstallationState);
                var filesLeft = modInstallationState.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var relativePath in modInstallationState.Files)
                    {
                        var gamePath = new RootedPath(installDir, relativePath);
                        if (!backupStrategy.RestoreBackup(gamePath))
                        {
                            eventHandler.UninstallSkipModified(gamePath.Relative);
                        }
                        filesLeft.Remove(gamePath.Relative);
                    }
                    DeleteEmptyDirectories(installDir, modInstallationState.Files);
                }
                finally
                {
                    var installationState = IInstallation.State.NotInstalled;
                    if (filesLeft.Count != 0)
                    {
                        // Once partially uninstalled, it will stay that way unless fully uninstalled
                        if (modInstallationState.Partial || filesLeft.Count != modInstallationState.Files.Count)
                        {
                            installationState = IInstallation.State.PartiallyInstalled;
                        }
                        else
                        {
                            installationState = IInstallation.State.Installed;
                        }
                    }

                    afterUninstall(new ModInstallation(
                        packageName,
                        installationState,
                        filesLeft,
                        modInstallationState.FsHash
                    ));
                }
            }
            eventHandler.UninstallEnd();
        }
        else
        {
            eventHandler.UninstallNoMods();
        }
    }

    private static void DeleteEmptyDirectories(string dstRootPath, IReadOnlyCollection<string> filePaths)
    {
        var dirs = filePaths
            .Select(file => Path.Combine(dstRootPath, file))
            .SelectMany(dstFilePath => AncestorsUpTo(dstRootPath, dstFilePath))
            .Distinct()
            .OrderByDescending(name => name.Length);
        foreach (var dir in dirs)
        {
            // Some mods have duplicate entries, so files might have been removed already
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static List<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path); dir is not null && dir.FullName != root; dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }

    private void InstallPackages(
        IReadOnlyCollection<ModPackage> toInstall,
        string installDir,
        Action<IInstallation> afterInstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var modPackages = toInstall.Where(p => !BootfilesManager.IsBootFiles(p.PackageName)).Reverse().ToImmutableArray();

        var modConfigs = new List<ConfigEntries>();
        var installedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installCallbacks = new ProcessingCallbacks<RootedPath>
        {
            Accept = gamePath => !installedFiles.Contains(gamePath.Relative),
            Before = gamePath => installedFiles.Add(gamePath.Relative),
        };

        // Increase by one in case bootfiles are needed and another one to show that something is happening
        var progress = new PercentOfTotal(modPackages.Length + 2);
        if (modPackages.Any())
        {
            eventHandler.InstallStart();
            eventHandler.ProgressUpdate(progress.IncrementDone());

            foreach (var modPackage in modPackages.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
            {
                eventHandler.InstallCurrent(modPackage.PackageName);
                var backupStrategy = modBackupStrategyProvider.BackupStrategy(null);
                var mod = installationFactory.ModInstaller(modPackage);
                try
                {
                    var modConfig = mod.Install(installDir, backupStrategy, installCallbacks);
                    modConfigs.Add(modConfig);
                }
                finally
                {
                    afterInstall(mod);
                }
                eventHandler.ProgressUpdate(progress.IncrementDone());
            }

            if (modConfigs.Any(c => c.NotEmpty()))
            {
                eventHandler.PostProcessingStart();
                var bootfilesMod = CreateBootfilesMod(toInstall, eventHandler);
                try
                {
                    var backupStrategy = modBackupStrategyProvider.BackupStrategy(null);
                    bootfilesMod.Install(installDir, backupStrategy, installCallbacks);
                    bootfilesMod.PostProcessing(installDir, modConfigs, eventHandler);
                }
                finally
                {
                    afterInstall(bootfilesMod);
                }
                eventHandler.PostProcessingEnd();
            }
            else
            {
                eventHandler.PostProcessingNotRequired();
            }
            eventHandler.InstallEnd();
            eventHandler.ProgressUpdate(progress.IncrementDone());
        }
        else
        {
            eventHandler.InstallNoMods();
        }
        eventHandler.ProgressUpdate(progress.DoneAll());
    }

    private BootfilesMod CreateBootfilesMod(IReadOnlyCollection<ModPackage> packages, IEventHandler eventHandler)
    {
        var bootfilesPackage = packages.FirstOrDefault(p => BootfilesManager.IsBootFiles(p.PackageName));
        if (bootfilesPackage is null)
        {
            eventHandler.ExtractingBootfiles(null);
            return new BootfilesMod(installationFactory.GeneratedBootfilesInstaller());
        }
        eventHandler.ExtractingBootfiles(bootfilesPackage.PackageName);
        return new BootfilesMod(installationFactory.ModInstaller(bootfilesPackage));
    }

    private class BootfilesMod : IInstaller
    {
        private readonly IInstaller inner;
        private bool postProcessingDone;

        public BootfilesMod(IInstaller inner)
        {
            this.inner = inner;
            postProcessingDone = false;
        }

        public string PackageName => inner.PackageName;

        public IInstallation.State Installed =>
            inner.Installed == IInstallation.State.Installed && !postProcessingDone
                ? IInstallation.State.PartiallyInstalled
                : inner.Installed;

        public IReadOnlyCollection<string> InstalledFiles => inner.InstalledFiles;

        public int? PackageFsHash => inner.PackageFsHash;

        public ConfigEntries Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
        {
            inner.Install(dstPath, backupStrategy, callbacks);
            return ConfigEntries.Empty;
        }

        public void PostProcessing(string dstPath, IReadOnlyList<ConfigEntries> modConfigs, IEventHandler eventHandler)
        {
            eventHandler.PostProcessingVehicles();
            PostProcessor.AppendCrdFileEntries(dstPath, modConfigs.SelectMany(c => c.CrdFileEntries));
            eventHandler.PostProcessingTracks();
            PostProcessor.AppendTrdFileEntries(dstPath, modConfigs.SelectMany(c => c.TrdFileEntries));
            eventHandler.PostProcessingDrivelines();
            PostProcessor.AppendDrivelineRecords(dstPath, modConfigs.SelectMany(c => c.DrivelineRecords));
            postProcessingDone = true;
        }
    }
}
