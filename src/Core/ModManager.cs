using Core.Games;
using Core.Mods;
using Core.Utils;
using Core.State;
using SevenZip;
using static Core.IModManager;
using static Core.Mods.JsgmeFileInstaller;
using Core.IO;

namespace Core;

internal class ModManager : IModManager
{
    internal static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfiles.PakfilesDirectory,
        GeneratedBootfiles.PhysicsPersistentPakFileName
    );

    internal const string BootfilesPrefix = "__bootfiles";

    private readonly IGame game;
    private readonly IModRepository modRepository;
    private readonly IModFactory modFactory;
    private readonly IStatePersistence statePersistence;
    private readonly ISafeFileDelete safeFileDelete;
    private readonly ITempDir tempDir;

    public event LogHandler? Logs;
    public event ProgressHandler? Progress;

    internal ModManager(IGame game, IModRepository modRepository, IModFactory modFactory, IStatePersistence statePersistence, ISafeFileDelete safeFileDelete, ITempDir tempDir)
    {
        this.game = game;
        this.modRepository = modRepository;
        this.modFactory = modFactory;
        this.statePersistence = statePersistence;
        this.safeFileDelete = safeFileDelete;
        this.tempDir = tempDir;
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
        var enabledModPackages = modRepository.ListEnabledMods().ToDictionary(_ => _.PackageName);
        var disabledModPackages = modRepository.ListDisabledMods().ToDictionary(_ => _.PackageName);
        var availableModPackages = enabledModPackages.Merge(disabledModPackages);

        var bootfilesFailed = installedMods.Where(kv => IsBootFiles(kv.Key) && (kv.Value?.Partial ?? false)).Any();
        var isModInstalled = installedMods.SelectValues<string, InternalModInstallationState, bool?>(modInstallationState =>
            modInstallationState is null ? false : ((modInstallationState.Partial || bootfilesFailed) ? null : true)
        );
        var modsOutOfDate = installedMods.SelectValues((packageName, modInstallationState) =>
        {
            availableModPackages.TryGetValue(packageName, out var modPackage);
            return IsOutOfDate(modPackage, modInstallationState);
        });

        var allPackageNames = installedMods.Keys
            .Where(_ => !IsBootFiles(_))
            .Concat(enabledModPackages.Keys)
            .Concat(disabledModPackages.Keys)
            .Distinct();

        return allPackageNames
            .Select(packageName => {
                return new ModState(
                    ModName: Path.GetFileNameWithoutExtension(packageName),
                    PackageName: packageName,
                    PackagePath: availableModPackages.TryGetValue(packageName, out var modPackage) ? modPackage.FullPath : null,
                    IsInstalled: isModInstalled.TryGetValue(packageName, out var isInstalled) ? isInstalled : false,
                    IsEnabled: enabledModPackages.ContainsKey(packageName),
                    IsOutOfDate: modsOutOfDate.TryGetValue(packageName, out var isOutOfDate) && isOutOfDate
                );
            }).ToList();
    }

    private static bool IsOutOfDate(ModPackage? modPackage, InternalModInstallationState? modInstallationState)
    {
        if (modPackage is null || modInstallationState is null)
        {
            return false;
        }
        var installedFsHash = modInstallationState.FsHash;
        if (installedFsHash is null)
        {
            // When partially installed or for state backwards compatibility
            return true;
        }
        return installedFsHash != modPackage.FsHash;
    }

    public ModState AddNewMod(string packageFullPath)
    {
        if (IsDirectory(packageFullPath))
        {
            throw new Exception($"{packageFullPath} is a directory");
        }

        var modPackage = modRepository.UploadMod(packageFullPath);
        statePersistence.ReadState().Install.Mods.TryGetValue(modPackage.PackageName, out var modInstallationState);

        return new ModState(
                ModName: modPackage.Name,
                PackageName: modPackage.PackageName,
                PackagePath: modPackage.FullPath,
                IsEnabled: modPackage.Enabled,
                IsInstalled: false,
                IsOutOfDate: IsOutOfDate(modPackage, modInstallationState)
            );
    }

    public void DeleteMod(string packagePath) =>
        safeFileDelete.SafeDelete(packagePath);

    private static bool IsDirectory(string path) =>
        File.GetAttributes(path).HasFlag(FileAttributes.Directory);

    public string EnableMod(string packagePath)
    {
        return modRepository.EnableMod(packagePath);
    }

    public string DisableMod(string packagePath)
    {
        return modRepository.DisableMod(packagePath);
    }

    public void InstallEnabledMods(CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        if (RestoreOriginalState(cancellationToken))
        {
            // Clean what left by a previous failed installation
            tempDir.Cleanup();

            InstallAllModFiles(cancellationToken);
            tempDir.Cleanup();
        }
    }

    public void UninstallAllMods(CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        RestoreOriginalState(cancellationToken);
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
                                FsHash: null,
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
                        Time: modsLeft.Count > 0 ? previousInstallation.Time : null,
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
        if (game.IsRunning)
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
        var modPackages = modRepository.ListEnabledMods().Where(_ => !IsBootFiles(_.PackageName)).Reverse();
        var modConfigs = new List<ConfigEntries>();
        var installedFilesByMod = new Dictionary<string, InternalModInstallationState>();
        var installedFiles = new HashSet<string>();
        bool SkipAlreadyInstalled(string file) => installedFiles.Add(file.ToLowerInvariant());
        try
        {
            if (modPackages.Any())
            {
                Logs?.Invoke("Installing mods:");
                // Increase by one in case bootfiles are needed and another one to show that something is happening
                var progress = Percent.OfTotal(modPackages.Count() + 2);
                Progress?.Invoke(progress.Increment());

                foreach (var modPackage in modPackages)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    Logs?.Invoke($"- {modPackage.PackageName}");
                    var mod = ExtractMod(modPackage);
                    try
                    {
                        var modConfig = mod.Install(game.InstallationDirectory, SkipAlreadyInstalled);
                        modConfigs.Add(modConfig);
                    }
                    finally
                    {
                        installedFilesByMod.Add(mod.PackageName, new(
                            FsHash: mod.PackageFsHash,
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
                        bootfilesMod.Install(game.InstallationDirectory, SkipAlreadyInstalled);
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
                            FsHash: bootfilesMod.PackageFsHash,
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
                Logs?.Invoke($"No mod archives to install");
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

    private static bool IsBootFiles(string packageName) => packageName.StartsWith(BootfilesPrefix);

    private IMod ExtractMod(ModPackage modPackage)
    {
        var extractionDir = Path.Combine(tempDir.BasePath, modPackage.PackageName);
        using var extractor = new SevenZipExtractor(modPackage.FullPath);
        extractor.ExtractArchive(extractionDir);

        return modFactory.ManualInstallMod(modPackage.PackageName, modPackage.FsHash, extractionDir);
    }

    private IMod BootfilesMod()
    {
        var bootfilesPackages = modRepository.ListEnabledMods().Where(_ => IsBootFiles(_.PackageName));
        switch (bootfilesPackages.Count())
        {
            case 0:
                Logs?.Invoke("Extracting bootfiles from game");
                return modFactory.GeneratedBootfiles(tempDir.BasePath);
            case 1:
                var modPackage = bootfilesPackages.First();
                Logs?.Invoke($"Extracting bootfiles from {modPackage.PackageName}");
                return ExtractMod(modPackage);
            default:
                Logs?.Invoke("Multiple bootfiles found:");
                foreach (var bf in bootfilesPackages)
                {
                    Logs?.Invoke($"- {bf.Name}");
                }
                throw new Exception("Too many bootfiles found");
        }
    }
}