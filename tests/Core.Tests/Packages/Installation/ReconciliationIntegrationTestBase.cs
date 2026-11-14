using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

public abstract class ReconciliationIntegrationTestBase<TEventHandler>
    where TEventHandler : class, IReconciliationContext.IEventHandler
{
    #region Setup

    protected readonly DateTimeOffset ValueNotUsed = DateTimeOffset.MinValue;

    protected readonly Mock<IBackupStrategy> BackupStrategyMock = new();
    protected readonly Mock<TEventHandler> EventHandlerMock = new();

    protected readonly DateTimeOffset TestInstallationTimeUtc;
    private readonly FakeTimeProvider testTimeProvider;

    private readonly IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider;

    protected IReadOnlyDictionary<string, PackageInstallationState> InstallationState =
        ReadOnlyDictionary<string, PackageInstallationState>.Empty;

    protected IEnumerable<string> InstalledPackages => InstallationState.Keys;

    private readonly MockFileSystem fs;
    protected IDirectoryInfo DestinationDirectory { get; }
    protected readonly IDirectory Directory;

    protected ReconciliationIntegrationTestBase()
    {
        fs = new MockFileSystem();
        Directory = fs.Directory;
        DestinationDirectory = Directory.CreateTempSubdirectory();
        TestInstallationTimeUtc = DateTimeOffset.Now.AddDays(10).ToUniversalTime();
        var fakeLocalTimeOffset = TimeSpan.FromHours(3);
        testTimeProvider = new FakeTimeProvider(TestInstallationTimeUtc.ToOffset(fakeLocalTimeOffset));
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<DateTimeOffset, TEventHandler>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<DateTimeOffset>(), It.IsAny<TEventHandler>()))
            .Returns(BackupStrategyMock.Object);
        backupStrategyProvider = backupStrategyProviderMock.Object;
    }

    protected abstract IReconciliationContext<TEventHandler>.FactoryFunc ContextFactory { get; }

    #endregion

    #region Utility Methods

    protected RootedPath DestinationPath(string relativePath) =>
        new PathRoot(DestinationDirectory).Combine(relativePath);

    protected void Reconcile(IPackage[] packages, TEventHandler? eventHandler = null) =>
        Reconcile(packages, int.MaxValue, eventHandler);

    protected void Reconcile(IPackage[] packages, int actionCount,
        TEventHandler? eventHandler = null)
    {
        foreach (var action in PlanReconciliation(packages, eventHandler).Take(actionCount))
        {
            action();
        }
    }

    protected IReadOnlyCollection<Action> PlanReconciliation(IPackage[] packages, TEventHandler? eventHandler = null)
    {
        var context = ContextFactory(InstallationState);
        var actions = context.ReconciliationActions(packages, DestinationDirectory, backupStrategyProvider,
            eventHandler ?? EventHandlerMock.Object);
        InstallationState = context.CurrentState;
        return actions;
    }

    protected static Dictionary<string, PackageInstallationState> CopyInstallationState(
        IReadOnlyDictionary<string, PackageInstallationState> state) =>
        state.ToDictionary(p => p.Key, p => p.Value with
        {
            Dependencies = p.Value.Dependencies.ToArray(),
            Files = p.Value.Files.ToArray(),
            ShadowedBy = p.Value.ShadowedBy.ToArray()
        });

    protected IPackage Package(string name, IReadOnlyCollection<string> files) =>
        Package(name, null, files);

    protected IPackage Package(string name, int? versionHash, IReadOnlyCollection<string> files) =>
        Package(name, versionHash, files, Array.Empty<string>());

    protected IPackage Package(string name, int? versionHash,
        IReadOnlyCollection<string> files, IReadOnlyCollection<string> dependencies) =>
        Package(new StaticFilesInstaller(fs, testTimeProvider,
            name, versionHash, files.ToDictionary(f => f, _ => ""), dependencies));

    private static IPackage Package(StaticFilesInstaller installer)
    {
        var package = new Mock<IPackage>();
        package.SetupGet(p => p.Name).Returns(installer.PackageName);
        package.SetupGet(p => p.VersionHash).Returns(installer.PackageVersionHash);
        package.Setup(p => p.CreateInstaller()).Returns(installer);
        return package.Object;
    }

    #endregion
}
