using Core.Bootfiles;
using Core.Games;
using Core.IO;
using Core.Mods;
using Core.State;
using Core.Utils;
using static Core.API.IModManager;

namespace Core.API;

internal class ModManager : IModManager
{
    internal static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfilesInstaller.PakfilesDirectory,
        GeneratedBootfilesInstaller.PhysicsPersistentPakFileName
    );

    private readonly IGame game;
    private readonly IModRepository modRepository;
    private readonly IStatePersistence statePersistence;
    private readonly ISafeFileDelete safeFileDelete;
    private readonly ITempDir tempDir;

    private readonly IModInstaller modInstaller;

    internal ModManager(IGame game, IModRepository modRepository, IModInstaller modInstaller, IStatePersistence statePersistence, ISafeFileDelete safeFileDelete, ITempDir tempDir)
    {
        this.game = game;
        this.modRepository = modRepository;
        this.statePersistence = statePersistence;
        this.safeFileDelete = safeFileDelete;
        this.tempDir = tempDir;
        this.modInstaller = modInstaller;
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
        var isModInstalled = installedMods.SelectValues<string, ModInstallationState, bool?>(modInstallationState =>
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
            .Select(packageName =>
            {
                return new ModState(
                    PackageName: packageName,
                    PackagePath: availableModPackages.TryGetValue(packageName, out var modPackage) ? modPackage.FullPath : null,
                    IsInstalled: isModInstalled.TryGetValue(packageName, out var isInstalled) ? isInstalled : false,
                    IsEnabled: enabledModPackages.ContainsKey(packageName),
                    IsOutOfDate: modsOutOfDate.TryGetValue(packageName, out var isOutOfDate) && isOutOfDate
                );
            }).ToList();
    }

    private static bool IsOutOfDate(ModPackage? modPackage, ModInstallationState? modInstallationState)
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
        UpdateMods(modRepository.ListEnabledMods(), eventHandler, cancellationToken);
        tempDir.Cleanup();
    }

    public void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        UpdateMods(Array.Empty<ModPackage>(), eventHandler, cancellationToken);
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning)
        {
            throw new Exception("The game is running.");
        }
    }

    private void UpdateMods(IReadOnlyCollection<ModPackage> packages, IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var previousState = statePersistence.ReadState().Install.Mods;
        var currentState = new Dictionary<string, ModInstallationState>(previousState);
        try
        {
            modInstaller.Apply(
                previousState,
                packages,
                game.InstallationDirectory,
                modInstallation =>
                {
                    switch (modInstallation.Installed)
                    {
                    case IInstallation.State.Installed:
                    case IInstallation.State.PartiallyInstalled:
                        currentState.Upsert(modInstallation.PackageName,
                            existing => existing with
                            {
                                Partial = modInstallation.Installed == IInstallation.State.PartiallyInstalled,
                                Files = modInstallation.InstalledFiles
                            },
                            () => new ModInstallationState(
                                Time: DateTime.Now,
                                FsHash: modInstallation.PackageFsHash,
                                Partial: modInstallation.Installed == IInstallation.State.PartiallyInstalled,
                                Files: modInstallation.InstalledFiles
                            ));
                        break;
                    case IInstallation.State.NotInstalled:
                        currentState.Remove(modInstallation.PackageName);
                        break;
                    }
                },
                eventHandler,
                cancellationToken);
        }
        finally
        {
            statePersistence.WriteState(new SavedState(
                Install: new(
                    Time: currentState.Values.Max(_ => _.Time),
                    Mods: currentState
                )
            ));
        }
    }
}
