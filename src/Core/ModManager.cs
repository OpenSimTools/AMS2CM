using System.Collections.Immutable;
using Core.Games;
using Core.Mods;
using Core.Utils;
using Core.State;
using SevenZip;
using static Core.IModManager;
using static Core.Mods.JsgmeFileInstaller;
using System.IO;

namespace Core;

internal class ModManager : IModManager
{
    private record ModReference(string RootPath, string FullPath)
    {
        public string PackageName => Path.GetRelativePath(RootPath, FullPath);
    }

    private record WorkPaths(
        string EnabledModArchivesDir,
        string DisabledModArchivesDir,
        string TempDir
    );

    private static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfiles.PakfilesDirectory,
        GeneratedBootfiles.PhysicsPersistentPakFileName
    );

    private const string EnabledModsDirName = "Enabled";
    private const string DisabledModsSubdir = "Disabled";
    private const string TempDirName = "Temp";

    private const string BootfilesPrefix = "__bootfiles";

    private readonly WorkPaths workPaths;
    private readonly IGame game;
    private readonly IModFactory modFactory;
    private readonly IStatePersistence statePersistence;

    public event LogHandler? Logs;
    public event ProgressHandler? Progress;

    internal ModManager(IGame game, string modsDir, IModFactory modFactory, IStatePersistence statePersistence)
    {
        this.game = game;
        this.modFactory = modFactory;
        this.statePersistence = statePersistence;
        workPaths = new WorkPaths(
            EnabledModArchivesDir: Path.Combine(modsDir, EnabledModsDirName),
            DisabledModArchivesDir: Path.Combine(modsDir, DisabledModsSubdir),
            TempDir: Path.Combine(modsDir, TempDirName)
        );
    }

    private static void AddToEnvionmentPath(string additionalPath)
    {
        const string pathEnvVar = "PATH";
        var env = Environment.GetEnvironmentVariable(pathEnvVar);
        if (env is not null && env.Contains(additionalPath))
        {
            return;
        }
        Environment.SetEnvironmentVariable(pathEnvVar, $"{env};{additionalPath}");
    }

    public List<ModState> FetchState()
    {
        var installedMods = statePersistence.ReadState().Install.Mods;
        var enabledModReferences = ListEnabledModPackages().ToDictionary(_ => _.PackageName);
        var disabledModReferences = ListDisabledModPackages().ToDictionary(_ => _.PackageName);

        var availableModPaths = enabledModReferences.Merge(disabledModReferences).SelectValues(_ => _.FullPath);
        var bootfilesFailed = installedMods.Where(kv => IsBootFiles(kv.Key) && (kv.Value?.Partial ?? false)).Any();
        var isModInstalled = installedMods.SelectValues<string, InternalModInstallationState, bool?>(modInstallationState =>
            modInstallationState is null ? false : ((modInstallationState.Partial || bootfilesFailed) ? null : true)
        );

        var allPackageNames = installedMods.Keys
            .Concat(enabledModReferences.Keys)
            .Concat(disabledModReferences.Keys)
            .Where(_ => !IsBootFiles(_))
            .Distinct();

        return allPackageNames
            .Select(packageName => {
                string? packagePath;
                bool? isInstalled;
                return new ModState(
                    ModName: Path.GetFileNameWithoutExtension(packageName),
                    PackageName: packageName,
                    PackagePath: availableModPaths.TryGetValue(packageName, out packagePath) ? packagePath : null,
                    IsInstalled: isModInstalled.TryGetValue(packageName, out isInstalled) ? isInstalled : false,
                    IsEnabled: enabledModReferences.Keys.Contains(packageName)
                );
            }).ToList();
    }

    public ModState AddNewMod(string packageFullPath)
    {
        var fileName = Path.GetFileName(packageFullPath);
        var isDisabled = ListDisabledModPackages().Where(_ => _.PackageName == fileName).Any();
        var destinationDirectoryPath = isDisabled ? workPaths.DisabledModArchivesDir : workPaths.EnabledModArchivesDir;
        var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);

        ExistingDirectoryOrCreate(destinationDirectoryPath);
        File.Copy(packageFullPath, destinationFilePath, overwrite: true);

        var modReference = new ModReference(destinationDirectoryPath, packageFullPath);
        var packageName = modReference.PackageName;
        return new ModState(
                ModName: Path.GetFileNameWithoutExtension(packageName),
                PackageName: packageName,
                PackagePath: modReference.FullPath,
                IsEnabled: !isDisabled,
                IsInstalled: false
            );
    }

    public string EnableMod(string packagePath)
    {
        return MoveMod(packagePath, workPaths.EnabledModArchivesDir);
    }

    public string DisableMod(string packagePath)
    {
        return MoveMod(packagePath, workPaths.DisabledModArchivesDir);
    }

    private string MoveMod(string packagePath, string destinationDirectoryPath)
    {
        ExistingDirectoryOrCreate(destinationDirectoryPath);
        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(packagePath));
        File.Move(packagePath, destinationFilePath);
        return destinationFilePath;
    }

    private static void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public void InstallEnabledMods(CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        if (RestoreOriginalState(cancellationToken))
        {
            CleanupTemp();
            InstallAllModFiles(cancellationToken);
            CleanupTemp();
        }
    }

    public void UninstallAllMods(CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        RestoreOriginalState(cancellationToken);
    }

    private void CleanupTemp()
    {
        if (Directory.Exists(workPaths.TempDir))
        {
            Directory.Delete(workPaths.TempDir, recursive: true);
        }
    }

    private bool RestoreOriginalState(CancellationToken cancellationToken)
    {
        var previousInstallation = statePersistence.ReadState().Install;
        if (previousInstallation.Mods.Any())
        {
            var modsLeft = new Dictionary<string, InternalModInstallationState>(previousInstallation.Mods);
            try
            {
                Logs?.Invoke($"Uninstalling mods:");
                foreach (var (modName, modInstallationState) in previousInstallation.Mods)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    Logs?.Invoke($"- {modName}");
                    var filesLeft = modInstallationState.Files.ToHashSet();
                    try
                    {
                        UninstallFiles(filesLeft, SkipCreatedAfter(previousInstallation.Time));
                    } finally
                    {
                        if (filesLeft.Any())
                        {
                            modsLeft[modName] = new InternalModInstallationState(
                                // Once partially uninstalled, it will stay that way unless fully uninstalled
                                Partial: modInstallationState.Partial || filesLeft.Count != modInstallationState.Files.Count,
                                Files: filesLeft
                            );
                        }
                        else
                        {
                            modsLeft.Remove(modName);
                        }
                    }
                }
            }
            finally
            {
                statePersistence.WriteState(new InternalState(
                    Install: new(
                        Time: DateTime.UtcNow,
                        Mods: modsLeft
                    )
                ));
            }
            return !modsLeft.Any(); // Success if everything was uninstalled
        }
        else
        {
            CheckNoBootfilesInstalled();
            Logs?.Invoke("No previously installed mods found. Skipping uninstall phase.");
            return true;
        }
    }

    private void UninstallFiles(ISet<string> files, BeforeFileCallback beforeFileCallback) =>
        JsgmeFileInstaller.UninstallFiles(
                game.InstallationDirectory,
                files,
                beforeFileCallback,
                p => files.Remove(p));

    private BeforeFileCallback SkipCreatedAfter(DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null)
        {
            return _ => true;
        }

        return path =>
        {
            var include = File.GetCreationTimeUtc(path) <= dateTimeUtc;
            if (!include)
            {
                Logs?.Invoke($"  Skipping modified file {path}");
            }
            return include;
        };
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning())
        {
            throw new Exception("The game is running.");
        }
    }

    private void CheckNoBootfilesInstalled()
    {
        if (!File.Exists(Path.Combine(game.InstallationDirectory, FileRemovedByBootfiles)))
        {
            throw new Exception("Bootfiles installed by another tool (e.g. JSGME) have been detected. Please uninstall all mods.");
        }
    }

    private void InstallAllModFiles(CancellationToken cancellationToken)
    {
        var modPackages = ListEnabledModPackages().Reverse();
        var modConfigs = new List<IMod.ConfigEntries>();
        var installedFilesByMod = new Dictionary<string, InternalModInstallationState>();
        var installedFiles = new HashSet<string>();
        try
        {
            if (modPackages.Any())
            {
                Logs?.Invoke("Installing mods:");
                var realModPackages = modPackages.Where(mp => !IsBootFiles(mp.PackageName)).ToList();
                // Increase by one in case bootfiles are needed
                var progress = Percent.OfTotal(realModPackages.Count + 2);
                Progress?.Invoke(progress.Increment());
                foreach (var modReference in modPackages)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    var packageName = modReference.PackageName;
                    Logs?.Invoke($"- {packageName}");
                    var mod = ExtractMod(packageName, modReference.FullPath);
                    try
                    {
                        mod.Install(game.InstallationDirectory, SkipDuplicates(installedFiles));
                        modConfigs.Add(mod.Config);
                    }
                    finally
                    {
                        installedFilesByMod.Add(mod.PackageName, new(
                            Partial: mod.Installed == IMod.InstalledState.PartiallyInstalled,
                            Files: mod.InstalledFiles
                        ));
                    }
                    Progress?.Invoke(progress.Increment());
                }

                if (modConfigs.Where(_ => _.NotEmpty()).Any())
                {
                    var bootfilesMod = BootfilesMod();
                    var postProcessingDone = false;
                    try
                    {
                        bootfilesMod.Install(game.InstallationDirectory, SkipDuplicates(installedFiles));
                        Logs?.Invoke("Post-processing:");
                        Logs?.Invoke("- Appending crd file entries");
                        PostProcessor.AppendCrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.CrdFileEntries));
                        Logs?.Invoke("- Appending trd file entries");
                        PostProcessor.AppendTrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.TrdFileEntries));
                        Logs?.Invoke("- Appending driveline records");
                        PostProcessor.AppendDrivelineRecords(game.InstallationDirectory, modConfigs.SelectMany(_ => _.DrivelineRecords));
                        postProcessingDone = true;
                    }
                    finally
                    {
                        installedFilesByMod.Add(bootfilesMod.PackageName, new(
                            Partial: bootfilesMod.Installed == IMod.InstalledState.PartiallyInstalled || !postProcessingDone,
                            Files: bootfilesMod.InstalledFiles
                        ));
                    }
                }
                else
                {
                    Logs?.Invoke("Post-processing not required");
                }
                Progress?.Invoke(progress.Increment());
            }
            else
            {
                Logs?.Invoke($"No mod archives found in {workPaths.EnabledModArchivesDir}");
                Progress?.Invoke(1.0);
            }
        }
        finally
        {
            statePersistence.WriteState(new InternalState(
                Install: new(
                    Time: DateTime.UtcNow,
                    Mods: installedFilesByMod
                )
            ));
        }
    }

    private BeforeFileCallback SkipDuplicates(ISet<string> allInstalledFiles)
    {
        return relativePath =>
        {
            var include = allInstalledFiles.Add(relativePath);
            if (!include)
            {
                Logs?.Invoke($"  Skipping duplicate {relativePath}");
            }
            return include;
        };
    }

    private bool IsBootFiles(string packageName) => packageName.StartsWith(BootfilesPrefix);

    private IMod ExtractMod(string packageName, string archivePath)
    {
        var extractionDir = Path.Combine(workPaths.TempDir, packageName);
        using var extractor = new SevenZipExtractor(archivePath);
        extractor.ExtractArchive(extractionDir);

        return modFactory.ManualInstallMod(packageName, extractionDir);
    }

    private IMod BootfilesMod()
    {
        var bootfilesArchives = Directory.EnumerateFiles(workPaths.EnabledModArchivesDir, $"{BootfilesPrefix}*.*");
        switch (bootfilesArchives.Count())
        {
            case 0:
                Logs?.Invoke("Extracting bootfiles from game");
                return modFactory.GeneratedBootfiles(workPaths.TempDir);
            case 1:
                var archivePath = bootfilesArchives.First();
                var packageName = Path.GetFileNameWithoutExtension(archivePath);
                Logs?.Invoke($"Extracting bootfiles from {packageName}");
                return ExtractMod(packageName, archivePath);
            default:
                Logs?.Invoke("Multiple bootfiles found:");
                foreach (var bf in bootfilesArchives)
                {
                    Logs?.Invoke($"- {bf}");
                }
                throw new Exception("Too many bootfiles found");
        }
    }

    private IReadOnlyCollection<ModReference> ListEnabledModPackages() => ListModPackages(workPaths.EnabledModArchivesDir);

    private IReadOnlyCollection<ModReference> ListDisabledModPackages() => ListModPackages(workPaths.DisabledModArchivesDir);

    private IReadOnlyCollection<ModReference> ListModPackages(string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            var options = new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                MaxRecursionDepth = 0,
            };
            return Directory.EnumerateFiles(rootPath, "*", options)
                .Select(modPath => new ModReference(rootPath, modPath))
                .ToList();
        }
        else
        {
            return Array.Empty<ModReference>();
        }
    }
}