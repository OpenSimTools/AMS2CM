using System.IO.Abstractions;
using Core.API;
using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Repository;
using Core.SoftwareUpdates;
using Core.State;
using Core.State.JsonFile;
using Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;

namespace Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, Config config)
    {
        services.TryAddSingleton(config.Game);
        services.TryAddSingleton(config.Updates);
        services.TryAddSingleton(config.ModInstall);
        services.TryAddSingleton<PrefixBootfilesNaming.IConfig>(p => p.GetRequiredService<ModInstallConfig>());
        services.TryAddSingleton<IFileSystem, FileSystem>();
        services.TryAddSingleton<ISteamLibraryLocator, Steam>();
        services.TryAddSingleton<IGame, Game>();
        services.TryAddSingleton<IPackageRepository>(p => new FileSystemRepository(ModsDirectory(p, config)));
        services.TryAddSingleton<IStatePersistence>(p => new JsonFileStatePersistence(ModsDirectory(p, config)));
        services.TryAddSingleton<ITempDir>(p => new SubdirectoryTempDir(ModsDirectory(p, config)));
        services.TryAddSingleton<ISafeFileDelete, WindowsRecyclingBin>();
        services.TryAddSingleton<IBootfilesNaming, PrefixBootfilesNaming>();
        services.TryAddSingleton<IFileUpdateTracker, CreationTimeFileUpdateTracker>();
        services.TryAddSingleton<SuffixBackupStrategy.Provider<DateTimeOffset, IEventHandler>>();
        services.TryAddSingleton<IBackupStrategyProvider<DateTimeOffset, IEventHandler>>(p =>
            new SkipUpdatedBackupStrategy.Provider<IEventHandler>(
                p.GetRequiredService<IFileSystem>(),
                p.GetRequiredService<IFileUpdateTracker>(),
                p.GetRequiredService<SuffixBackupStrategy.Provider<DateTimeOffset, IEventHandler>>()));
        services.TryAddSingleton<IReconciliationContext<IEventHandler>.FactoryFunc>(p =>
            ModReconciliationContext<IEventHandler, ModInstallConfig>.Factory(
                p.GetRequiredService<IGame>(),
                p.GetRequiredService<ITempDir>(),
                p.GetRequiredService<IBootfilesNaming>(),
                p.GetRequiredService<ModInstallConfig>(),
                SuffixBackupStrategy.Naming,
                p.GetRequiredService<IFileUpdateTracker>()));
        services.TryAddSingleton<IReconciliationService<IEventHandler>, ReconciliationService<IEventHandler>>();
        services.TryAddSingleton<IModManager>(p => new ModManager(
            p.GetRequiredService<IGame>(),
            p.GetRequiredService<IPackageRepository>(),
            p.GetRequiredService<IBootfilesNaming>(),
            p.GetRequiredService<IReconciliationService<IEventHandler>>(),
            p.GetRequiredService<IStatePersistence>(),
            p.GetRequiredService<ISafeFileDelete>(),
            p.GetRequiredService<ITempDir>()));
        services.TryAddSingleton<IGitHubClient>(p => new GitHubClient(
            new ProductHeaderValue(p.GetRequiredService<GitHubUpdateChecker.IConfig>().GitHubClientApp)));
        services.TryAddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        return services;
    }

    private static IDirectoryInfo ModsDirectory(IServiceProvider provider, Config config) =>
        provider.GetRequiredService<IFileSystem>().DirectoryInfo.New(Path.Combine(
            provider.GetRequiredService<IGame>().InstallationDirectory.FullName, config.ModsDirectory));
}
