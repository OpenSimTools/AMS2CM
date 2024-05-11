using System.Collections.Immutable;
using Core.Mods;
using Core.State;
using Core.Utils;
using SevenZip;

namespace Core;

public class ModInstaller
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

    public ModInstaller(IModFactory modFactory, ITempDir tempDir)
    {
        this.modFactory = modFactory;
        this.tempDir = tempDir;
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
            foreach (var (packageName, modInstallationState) in currentState.Mods)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var filesLeft = modInstallationState.Files.ToHashSet();
                try
                {
                    JsgmeFileInstaller.UninstallFiles(
                        installDir,
                        filesLeft,
                        SkipCreatedAfter(eventHandler, currentState.Time),
                        p => filesLeft.Remove(p));
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

    private Predicate<string> SkipCreatedAfter(IEventHandler eventHandler, DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null)
        {
            return _ => true;
        }

        return path =>
        {
            var proceed = !File.Exists(path) || File.GetCreationTimeUtc(path) <= dateTimeUtc;
            if (!proceed)
            {
                eventHandler.UninstallSkipModified(path);
            }
            return proceed;
        };
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
        var installedFiles = new HashSet<string>();
        bool SkipAlreadyInstalled(string file) => installedFiles.Add(file.ToLowerInvariant());
        var installCallbacks = new ProcessingCallbacks<string>
        {
            Accept = SkipAlreadyInstalled,
            //Before = TODO move backup here!
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

        public ConfigEntries Install(string dstPath, ProcessingCallbacks<string> callbacks) => inner.Install(dstPath, callbacks);

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