using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Repository;
using Core.State;
using Core.Utils;

namespace Core.API;

internal class ModManager : IModManager
{
    private readonly IGame game;
    private readonly IPackageRepository packageRepository;
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly IStatePersistence statePersistence;
    private readonly ISafeFileDelete safeFileDelete;
    private readonly ITempDir tempDir;

    private readonly IReconciliationService<IEventHandler> reconciliationService;

    internal ModManager(
        IGame game,
        IPackageRepository packageRepository,
        IBootfilesNaming bootfilesNaming,
        IReconciliationService<IEventHandler> reconciliationService,
        IStatePersistence statePersistence,
        ISafeFileDelete safeFileDelete,
        ITempDir tempDir)
    {
        this.game = game;
        this.packageRepository = packageRepository;
        this.bootfilesNaming = bootfilesNaming;
        this.statePersistence = statePersistence;
        this.safeFileDelete = safeFileDelete;
        this.tempDir = tempDir;
        this.reconciliationService = reconciliationService;
    }

    private static void AddToEnvironmentPath(string additionalPath)
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
        var installedMods = statePersistence.ReadState().Installation;
        var enabledModPackages = packageRepository.ListEnabled().ToDictionary(p => p.Name);
        var disabledModPackages = packageRepository.ListDisabled().ToDictionary(p => p.Name);
        var availableModPackages = enabledModPackages.Merge(disabledModPackages);

        var isModInstalled = DependencyResolver
            .CollectValues(
                installedMods,
                s => s.Dependencies.Concat(s.ShadowedBy).ToArray(),
                s => s?.Partial ?? true)
            .SelectValues<string, IReadOnlySet<bool>, bool?>(
                partials => partials.Any(p => p) ? null : true);

        var modsOutOfDate = installedMods.SelectValues((packageName, modInstallationState) =>
        {
            availableModPackages.TryGetValue(packageName, out var modPackage);
            return IsOutOfDate(modPackage, modInstallationState);
        });

        var allPackageNames = installedMods.Keys.Where(packageName => !bootfilesNaming.IsBootfiles(packageName))
            .Concat(enabledModPackages.Keys)
            .Concat(disabledModPackages.Keys)
            .Distinct();

        return allPackageNames
            .Select(packageName => new ModState(
                PackageName: packageName,
                PackageLocation: availableModPackages.TryGetValue(packageName, out var modPackage) ? modPackage.Location : null,
                IsInstalled: isModInstalled.GetValueOrDefault(packageName, false),
                IsEnabled: enabledModPackages.ContainsKey(packageName),
                IsOutOfDate: modsOutOfDate.TryGetValue(packageName, out var isOutOfDate) && isOutOfDate
            )).ToList();
    }

    private static bool IsOutOfDate(IPackage? modPackage, PackageInstallationState? modInstallationState)
    {
        if (modPackage is null || modInstallationState is null)
        {
            return false;
        }
        var installedVersionHash = modInstallationState.VersionHash;
        if (installedVersionHash is null)
        {
            // When partially installed or for state backwards compatibility
            return true;
        }
        return installedVersionHash != modPackage.VersionHash;
    }

    public void AddNewMod(string packageFullPath)
    {
        if (IsDirectory(packageFullPath))
        {
            throw new Exception($"{packageFullPath} is a directory");
        }

        packageRepository.Upload(packageFullPath);
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
        // It shouldn't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvironmentPath(game.InstallationDirectory);

        // Clean what left by a previous failed installation
        tempDir.Cleanup();
        var modsInPriorityOrder = packageRepository.ListEnabled().Reverse().ToArray();
        UpdateMods(modsInPriorityOrder, eventHandler, cancellationToken);
        tempDir.Cleanup();
    }

    public void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken = default)
    {
        CheckGameNotRunning();
        UpdateMods(Array.Empty<IPackage>(), eventHandler, cancellationToken);
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning)
        {
            throw new Exception("The game is running.");
        }
    }

    private void UpdateMods(IReadOnlyCollection<IPackage> packages, IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        reconciliationService.Reconcile(
            statePersistence.ReadState().Installation,
            packages,
            game.InstallationDirectory,
            nextState =>
                statePersistence.WriteState(new SavedState(
                    Installation: nextState
                )),
            eventHandler,
            cancellationToken);
    }
}
