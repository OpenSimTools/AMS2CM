using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

public abstract class ReconciliationIntegrationTestBase<TEventHandler>
    where TEventHandler : class, ReconciliationService.IEventHandler
{
    // Randomness ensures that at least some test runs will fail if it's used
    protected readonly DateTimeOffset ValueNotUsed =
        Random.Shared.Next() > 0 ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue;

    protected readonly IReadOnlyCollection<string> FilesNotUsed = [Guid.NewGuid().ToString()];

    protected readonly Mock<IBackupStrategy> BackupStrategyMock = new();
    protected readonly Mock<TEventHandler> EventHandlerMock = new();

    protected readonly DateTimeOffset TestInstallationTimeUtc;
    private readonly FakeTimeProvider testTimeProvider;

    private readonly ReconciliationService<TEventHandler> service;

    protected IReadOnlyDictionary<string, PackageInstallationState> InstallationState =
        ReadOnlyDictionary<string, PackageInstallationState>.Empty;

    protected IEnumerable<string> InstalledPackages => InstallationState.Keys;

    private readonly MockFileSystem fs;
    private readonly IDirectoryInfo destinationDir;
    protected readonly IDirectory Directory;

    protected ReconciliationIntegrationTestBase()
    {
        fs = new MockFileSystem();
        Directory = fs.Directory;
        destinationDir = Directory.CreateTempSubdirectory();
        TestInstallationTimeUtc = DateTimeOffset.Now.AddDays(10).ToUniversalTime();
        var fakeLocalTimeOffset = TimeSpan.FromHours(3);
        testTimeProvider = new FakeTimeProvider(TestInstallationTimeUtc.ToOffset(fakeLocalTimeOffset));
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<DateTimeOffset, TEventHandler>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<DateTimeOffset>(), It.IsAny<TEventHandler>()))
            .Returns(BackupStrategyMock.Object);
        service = new ReconciliationService<TEventHandler>(backupStrategyProviderMock.Object,
            previousState => ContextFactory(previousState));
    }

    protected abstract IReconciliationContext<TEventHandler>.FactoryFunc ContextFactory { get; }

    protected RootedPath DestinationPath(string relativePath) =>
        new PathRoot(destinationDir).Combine(relativePath);

    protected void Reconcile(IPackage[] packages, TEventHandler? eventHandler = null) =>
        Reconcile(packages, CancellationToken.None, eventHandler);

    protected void Reconcile(IPackage[] packages, CancellationToken cancellationToken,
        TEventHandler? eventHandler = null)
    {
        service.Reconcile(
            InstallationState,
            packages,
            destinationDir,
            newState => InstallationState = newState,
            eventHandler ?? EventHandlerMock.Object,
            cancellationToken);
    }

    protected IPackage Package(string name, IReadOnlyCollection<string> files) =>
        Package(name, null, files);

    protected IPackage Package(string name, int? versionHash, IReadOnlyCollection<string> files) =>
        Package(name, versionHash, files, Array.Empty<string>());

    protected IPackage Package(string name, int? versionHash,
        IReadOnlyCollection<string> files, IReadOnlyCollection<string> dependencies) =>
        Package(new StaticFilesInstaller(fs, testTimeProvider,
            name, versionHash, files.ToDictionary(f => f, _ => ""), dependencies));

    protected IPackage Package(string name, int? versionHash = null) =>
        Package(Installer(name, versionHash));

    private static IPackage Package(IPackageInstaller installer)
    {
        var package = new Mock<IPackage>();
        package.SetupGet(p => p.Name).Returns(installer.PackageName);
        package.SetupGet(p => p.VersionHash).Returns(installer.PackageVersionHash);
        package.Setup(p => p.CreateInstaller()).Returns(installer);
        return package.Object;
    }

    protected IPackageInstaller Installer(string name, int? versionHash = null) =>
        new StaticFilesInstaller(fs, testTimeProvider, name, versionHash,
            new Dictionary<string, string> { [name] = "Contents" }, []);

}
