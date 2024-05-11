using Core.Games;
using Core.Mods;
using Core.Utils;
using Core.State;
using static Core.IModManager;
using Core.IO;

namespace Core;

internal class ModManager : IModManager
{
    internal static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfiles.PakfilesDirectory,
        GeneratedBootfiles.PhysicsPersistentPakFileName
    );

    private readonly IGame game;
    private readonly IModRepository modRepository;
    private readonly IStatePersistence statePersistence;
    private readonly ISafeFileDelete safeFileDelete;
    private readonly ITempDir tempDir;

    private readonly ModInstaller modInstaller;

    internal ModManager(IGame game, IModRepository modRepository, IModFactory modFactory, IStatePersistence statePersistence, ISafeFileDelete safeFileDelete, ITempDir tempDir)
    {
        this.game = game;
        this.modRepository = modRepository;
        this.statePersistence = statePersistence;
        this.safeFileDelete = safeFileDelete;
        this.tempDir = tempDir;
        modInstaller = new ModInstaller(modFactory, tempDir);
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

        var bootfilesFailed = installedMods.Where(kv => BootfilesManager.IsBootFiles(kv.Key) && (kv.Value?.Partial ?? false)).Any();
        var isModInstalled = installedMods.SelectValues<string, InternalModInstallationState, bool?>(modInstallationState =>
            modInstallationState is null ? false : ((modInstallationState.Partial || bootfilesFailed) ? null : true)
        );
        var modsOutOfDate = installedMods.SelectValues((packageName, modInstallationState) =>
        {
            availableModPackages.TryGetValue(packageName, out var modPackage);
            return IsOutOfDate(modPackage, modInstallationState);
        });

        var allPackageNames = installedMods.Keys
            .Where(_ => !BootfilesManager.IsBootFiles(_))
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

    public void InstallEnabledMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        // Clean what left by a previous failed installation
        tempDir.Cleanup();
        if (RestoreOriginalState(eventHandler, cancellationToken))
        {
            InstallAllModFiles(eventHandler, cancellationToken);
        }
        tempDir.Cleanup();
    }

    public void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        RestoreOriginalState(eventHandler, cancellationToken);
    }

    private bool RestoreOriginalState(IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var previousInstallation = statePersistence.ReadState().Install;
        var modsLeft = new Dictionary<string, InternalModInstallationState>(previousInstallation.Mods);
        try
        {
            modInstaller.UninstallPackages(
                previousInstallation,
                game.InstallationDirectory,
                modInstallation =>
                {
                    if (modInstallation.Installed == IModInstallation.State.NotInstalled)
                    {
                        modsLeft.Remove(modInstallation.PackageName);
                    }
                    else
                    {
                        modsLeft[modInstallation.PackageName] = new InternalModInstallationState(
                            FsHash: modInstallation.PackageFsHash,
                            Partial: modInstallation.Installed == IModInstallation.State.PartiallyInstalled,
                            Files: modInstallation.InstalledFiles
                        );
                    }
                },
                eventHandler,
                cancellationToken);
        }
        finally
        {
            statePersistence.WriteState(new InternalState(
                Install: new(
                    Time: modsLeft.Any() ? previousInstallation.Time : null,
                    Mods: modsLeft
                )
            ));
        }
        // Success if everything was uninstalled
        return !modsLeft.Any();
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning)
        {
            throw new Exception("The game is running.");
        }
    }

    private void InstallAllModFiles(IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var installedFilesByMod = new Dictionary<string, InternalModInstallationState>();
        try
        {
            modInstaller.InstallPackages(
                modRepository.ListEnabledMods(),
                game.InstallationDirectory,
                modInstallation => installedFilesByMod.Add(modInstallation.PackageName, new(
                                FsHash: modInstallation.PackageFsHash,
                                Partial: modInstallation.Installed == IModInstallation.State.PartiallyInstalled,
                                Files: modInstallation.InstalledFiles
                            )),
                eventHandler,
                cancellationToken);
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
}