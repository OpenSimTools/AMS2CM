using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages.Installation;
using Core.Packages.Repository;
using Core.State;
using Core.Utils;

namespace Core.API;

internal class ModManager : IModManager
{
    private readonly IGame game;
    private readonly IPackageRepository packageRepository;
    private readonly IStatePersistence statePersistence;
    private readonly ISafeFileDelete safeFileDelete;
    private readonly ITempDir tempDir;

    private readonly ModPackagesesUpdater packagesesUpdater;

    internal ModManager(IGame game, IPackageRepository packageRepository, ModPackagesesUpdater packagesesUpdater, IStatePersistence statePersistence, ISafeFileDelete safeFileDelete, ITempDir tempDir)
    {
        this.game = game;
        this.packageRepository = packageRepository;
        this.statePersistence = statePersistence;
        this.safeFileDelete = safeFileDelete;
        this.tempDir = tempDir;
        this.packagesesUpdater = packagesesUpdater;
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
        var enabledModPackages = packageRepository.ListEnabled().ToDictionary(_ => _.Name);
        var disabledModPackages = packageRepository.ListDisabled().ToDictionary(_ => _.Name);
        var availableModPackages = enabledModPackages.Merge(disabledModPackages);

        var bootfilesFailed = installedMods.Where(kv => ModPackagesesUpdater.IsBootFiles(kv.Key) && (kv.Value?.Partial ?? false)).Any();
        var isModInstalled = installedMods.SelectValues<string, PackageInstallationState, bool?>(modInstallationState =>
            modInstallationState is null ? false : ((modInstallationState.Partial || bootfilesFailed) ? null : true)
        );
        var modsOutOfDate = installedMods.SelectValues((packageName, modInstallationState) =>
        {
            availableModPackages.TryGetValue(packageName, out var modPackage);
            return IsOutOfDate(modPackage, modInstallationState);
        });

        var allPackageNames = installedMods.Keys
            .Where(_ => !ModPackagesesUpdater.IsBootFiles(_))
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

    private static bool IsOutOfDate(Package? modPackage, PackageInstallationState? modInstallationState)
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

        var modPackage = packageRepository.Upload(packageFullPath);
        statePersistence.ReadState().Install.Mods.TryGetValue(modPackage.Name, out var modInstallationState);

        return new ModState(
                PackageName: modPackage.Name,
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
        return packageRepository.Enable(packagePath);
    }

    public string DisableMod(string packagePath)
    {
        return packageRepository.Disable(packagePath);
    }

    public void InstallEnabledMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        // Clean what left by a previous failed installation
        tempDir.Cleanup();
        var modsInPriorityOrder = packageRepository.ListEnabled().Reverse();
        UpdateMods(modsInPriorityOrder, eventHandler, cancellationToken);
        tempDir.Cleanup();
    }

    public void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        UpdateMods(Array.Empty<Package>(), eventHandler, cancellationToken);
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning)
        {
            throw new Exception("The game is running.");
        }
    }

    private void UpdateMods(IEnumerable<Package> packages, IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        packagesesUpdater.Apply(
            statePersistence.ReadState().Install.Mods,
            packages,
            game.InstallationDirectory,
            nextState =>
                statePersistence.WriteState(new SavedState(
                    Install: new InstallationState(
                        Time: nextState.Values.Max(_ => _.Time),
                        Mods: nextState
                    )
                )),
            eventHandler,
            cancellationToken);
    }
}
