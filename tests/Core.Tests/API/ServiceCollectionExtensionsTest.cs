using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.API;
using Core.Games;
using Core.IO;
using Core.Packages.Repository;
using Core.SoftwareUpdates;
using Core.State;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests.API;

[IntegrationTest]
public class ServiceCollectionExtensionsTest
{
    #region Setup

    private const string GameDirectory = @"C:\Game";
    private const string ModsDirectory = "Packages";
    private readonly MockFileSystem fs = new();

    public ServiceCollectionExtensionsTest()
    {
        fs.Directory.CreateDirectory(GameDirectory);
    }

    #endregion

    [Fact]
    public void AddCoreServices_ResolvesApplicationServices()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IModManager>().Should().NotBeNull();
        provider.GetRequiredService<IUpdateChecker>().Should().NotBeNull();
    }

    [Fact]
    public void Repository_UsesConfiguredModsDirectory()
    {
        var packagePath = Path.Combine(GameDirectory, ModsDirectory, "Enabled", "Package");
        fs.Directory.CreateDirectory(packagePath);
        using var provider = BuildProvider();

        var packages = provider.GetRequiredService<IPackageRepository>().ListEnabled();

        packages.Should().ContainSingle().Which.Location.Should().Be(packagePath);
    }

    [Fact]
    public void Persistence_UsesConfiguredModsDirectory()
    {
        var modsPath = Path.Combine(GameDirectory, ModsDirectory);
        fs.Directory.CreateDirectory(modsPath);
        using var provider = BuildProvider();

        provider.GetRequiredService<IStatePersistence>().WriteState(SavedState.Empty());

        fs.File.Exists(Path.Combine(modsPath, "state.json")).Should().BeTrue();
    }

    [Fact]
    public void TemporaryDirectory_UsesConfiguredModsDirectory()
    {
        using var provider = BuildProvider();

        var directory = provider.GetRequiredService<ITempDir>().BaseDirectory;

        directory.FullName.Should().Be(Path.Combine(GameDirectory, ModsDirectory, "Temp"));
        directory.FileSystem.Should().BeSameAs(fs);
    }

    [Fact]
    public void AddCoreServices_PreservesSteamLibraryLocatorOverride()
    {
        var locator = Mock.Of<ISteamLibraryLocator>();
        var services = new ServiceCollection().AddSingleton(locator);

        services.AddCoreServices(new Config());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISteamLibraryLocator>().Should().BeSameAs(locator);
    }

    [Fact]
    public void AddCoreServices_PreservesSafeFileDeleteOverride()
    {
        var deletion = Mock.Of<ISafeFileDelete>();
        var services = new ServiceCollection().AddSingleton(deletion);

        services.AddCoreServices(new Config());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISafeFileDelete>().Should().BeSameAs(deletion);
    }

    #region Utility Methods

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem>(fs);
        services.AddCoreServices(new Config
        {
            Game = new GameConfig { Path = GameDirectory },
            ModsDirectory = ModsDirectory
        });
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    #endregion
}
