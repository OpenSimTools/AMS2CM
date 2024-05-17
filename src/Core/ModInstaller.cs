using System.Collections.Immutable;
using Core.Backup;
using Core.Mods;
using Core.State;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;
using SevenZip;

namespace Core;

public class ModInstaller : IModInstaller
{
    public interface IConfig
    {
        IEnumerable<string> ExcludedFromInstall { get; }
    }

    public interface IEventHandler : IProgress
    {
        void InstallNoMods();
        void InstallStart();
        void InstallCurrent(string packageName);
        void InstallEnd();

        void PostProcessingNotRequired();
        void PostProcessingStart();
        void ExtractingBootfiles(string? packageName);
        void ExtractingBootfilesErrorMultiple(IReadOnlyCollection<string> bootfilesPackageNames);
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

    private readonly IModFactory modFactory;
    private readonly ITempDir tempDir;
    private readonly Matcher filesToInstallMatcher;
    private readonly IBackupStrategy backupStrategy;

    public ModInstaller(IModFactory modFactory, ITempDir tempDir, IConfig config)
    {
        this.modFactory = modFactory;
        this.tempDir = tempDir;
        filesToInstallMatcher = Matchers.ExcludingPatterns(config.ExcludedFromInstall);
        backupStrategy = new SuffixBackupStrategy();
    }

    public void UninstallPackages(
        InternalInstallationState currentState,
        string installDir,
        Action<IModInstallation> afterUninstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (currentState.Mods.Any())
        {
            eventHandler.UninstallStart();
            var skipCreatedAfter = SkipCreatedAfter(eventHandler, currentState.Time);
            var uninstallCallbacks = new ProcessingCallbacks<GamePath>
            {
                Accept = gamePath =>
                {
                    return skipCreatedAfter(gamePath);
                },
                After = gamePath =>
                {
                    backupStrategy.RestoreBackup(gamePath.Full);
                },
                NotAccepted = gamePath =>
                {
                    backupStrategy.DeleteBackup(gamePath.Full);
                }
            };
            foreach (var (packageName, modInstallationState) in currentState.Mods)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var filesLeft = modInstallationState.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                try
                {
                    UninstallFiles(
                        installDir,
                        filesLeft,
                        uninstallCallbacks
                            .AndAfter(_ => filesLeft.Remove(_.Relative))
                            .AndNotAccepted(_ => filesLeft.Remove(_.Relative))
                    );
                }
                finally
                {
                    var installationState = IModInstallation.State.NotInstalled;
                    if (filesLeft.Count != 0)
                    {
                        // Once partially uninstalled, it will stay that way unless fully uninstalled
                        if (modInstallationState.Partial || filesLeft.Count != modInstallationState.Files.Count)
                        {
                            installationState = IModInstallation.State.PartiallyInstalled;
                        }
                        else
                        {
                            installationState = IModInstallation.State.Installed;
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

    private static Predicate<GamePath> SkipCreatedAfter(IEventHandler eventHandler, DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null)
        {
            return _ => true;
        }

        return gamePath =>
        {
            var proceed = !File.Exists(gamePath.Full) || File.GetCreationTimeUtc(gamePath.Full) <= dateTimeUtc;
            if (!proceed)
            {
                eventHandler.UninstallSkipModified(gamePath.Full);
            }
            return proceed;
        };
    }

    private static void UninstallFiles(string dstPath, IEnumerable<string> files, ProcessingCallbacks<GamePath> callbacks)
    {
        var fileList = files.ToList(); // It must be enumerated twice
        foreach (var relativePath in fileList)
        {
            var gamePath = new GamePath(dstPath, relativePath);

            if (!callbacks.Accept(gamePath))
            {
                callbacks.NotAccepted(gamePath);
                continue;
            }

            callbacks.Before(gamePath);

            // Delete will fail if the parent directory does not exist
            if (File.Exists(gamePath.Full))
            {
                File.Delete(gamePath.Full);
            }

            callbacks.After(gamePath);
        }
        DeleteEmptyDirectories(dstPath, fileList);
    }

    private static void DeleteEmptyDirectories(string dstRootPath, IEnumerable<string> filePaths)
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

    private static IEnumerable<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path); dir is not null && dir.FullName != root; dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }

    public void InstallPackages(
        IReadOnlyCollection<ModPackage> packages,
        string installDir,
        Action<IModInstallation> afterInstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var modPackages = packages.Where(_ => !BootfilesManager.IsBootFiles(_.PackageName)).Reverse();

        var modConfigs = new List<ConfigEntries>();
        var installedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installCallbacks = new ProcessingCallbacks<GamePath>
        {
            Accept = gamePath =>
                Whitelisted(gamePath) &&
                !backupStrategy.IsBackupFile(gamePath.Relative) &&
                !installedFiles.Contains(gamePath.Relative),
            Before = gamePath =>
            {
                backupStrategy.PerformBackup(gamePath.Full);
                installedFiles.Add(gamePath.Relative);
            }
        };

        // Increase by one in case bootfiles are needed and another one to show that something is happening
        var progress = new PercentOfTotal(modPackages.Count() + 2);
        if (modPackages.Any())
        {
            eventHandler.InstallStart();
            eventHandler.ProgressUpdate(progress.IncrementDone());

            foreach (var modPackage in modPackages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.InstallCurrent(modPackage.PackageName);
                var mod = ExtractMod(modPackage);
                try
                {
                    var modConfig = mod.Install(installDir, installCallbacks);
                    modConfigs.Add(modConfig);
                }
                finally
                {
                    afterInstall(mod);
                }
                eventHandler.ProgressUpdate(progress.IncrementDone());
            }

            if (modConfigs.Where(_ => _.NotEmpty()).Any())
            {
                eventHandler.PostProcessingStart();
                var bootfilesMod = CreateBootfilesMod(packages, eventHandler);
                try
                {
                    bootfilesMod.Install(installDir, installCallbacks);
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
            eventHandler.ProgressUpdate(progress.IncrementDone());
        }
        else
        {
            eventHandler.InstallNoMods();
        }
        eventHandler.ProgressUpdate(progress.DoneAll());
    }

    private Predicate<GamePath> Whitelisted =>
        gamePath => filesToInstallMatcher.Match(gamePath.Relative).HasMatches;

    private BootfilesMod CreateBootfilesMod(IReadOnlyCollection<ModPackage> packages, IEventHandler eventHandler)
    {
        var bootfilesPackages = packages
            .Where(_ => BootfilesManager.IsBootFiles(_.PackageName));
        switch (bootfilesPackages.Count())
        {
            case 0:
                eventHandler.ExtractingBootfiles(null);
                return new BootfilesMod(modFactory.GeneratedBootfiles(tempDir.BasePath));
            case 1:
                var modPackage = bootfilesPackages.First();
                eventHandler.ExtractingBootfiles(modPackage.PackageName);
                return new BootfilesMod(ExtractMod(modPackage));
            default:
                var bootfilesPackageNames = bootfilesPackages.Select(_ => _.PackageName).ToImmutableList();
                eventHandler.ExtractingBootfilesErrorMultiple(bootfilesPackageNames);
                throw new Exception("Too many bootfiles found");
        }
    }

    private IMod ExtractMod(ModPackage modPackage)
    {
        var extractionDir = Path.Combine(tempDir.BasePath, modPackage.PackageName);
        using var extractor = new SevenZipExtractor(modPackage.FullPath);
        extractor.ExtractArchive(extractionDir);
        return modFactory.ManualInstallMod(modPackage.PackageName, modPackage.FsHash, extractionDir);
    }

    private class BootfilesMod : IMod
    {
        private readonly IMod inner;
        private bool postProcessingDone;


        public BootfilesMod(IMod inner)
        {
            this.inner = inner;
            postProcessingDone = false;
        }

        public string PackageName => inner.PackageName;

        public IModInstallation.State Installed =>
            inner.Installed == IModInstallation.State.Installed && !postProcessingDone
                ? IModInstallation.State.PartiallyInstalled
                : inner.Installed;

        public IReadOnlyCollection<string> InstalledFiles => inner.InstalledFiles;

        public int? PackageFsHash => inner.PackageFsHash;

        public ConfigEntries Install(string dstPath, ProcessingCallbacks<GamePath> callbacks) => inner.Install(dstPath, callbacks);

        public void PostProcessing(string dstPath, IReadOnlyList<ConfigEntries> modConfigs, IEventHandler eventHandler)
        {
            eventHandler.PostProcessingVehicles();
            PostProcessor.AppendCrdFileEntries(dstPath, modConfigs.SelectMany(_ => _.CrdFileEntries));
            eventHandler.PostProcessingTracks();
            PostProcessor.AppendTrdFileEntries(dstPath, modConfigs.SelectMany(_ => _.TrdFileEntries));
            eventHandler.PostProcessingDrivelines();
            PostProcessor.AppendDrivelineRecords(dstPath, modConfigs.SelectMany(_ => _.DrivelineRecords));
            postProcessingDone = true;
        }
    }
}