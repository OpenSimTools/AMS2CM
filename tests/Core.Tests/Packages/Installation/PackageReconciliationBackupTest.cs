using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.State;
using Core.State.JsonFile;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

[IntegrationTest]
public class PackageReconciliationBackupTest
{
    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly FakeTimeProvider time = new(DateTimeOffset.UtcNow.AddDays(10));
    private readonly IDirectoryInfo destination;
    private readonly ReconciliationService<ReconciliationService.IEventHandler> service;
    private IReadOnlyDictionary<string, PackageInstallationState> state = new Dictionary<string, PackageInstallationState>();

    public PackageReconciliationBackupTest()
    {
        destination = fs.Directory.CreateDirectory("Game");
        var backups = new SkipUpdatedBackupStrategy.Provider<ReconciliationService.IEventHandler>(fs,
            new CreationTimeFileUpdateTracker(),
            new SuffixBackupStrategy.Provider<DateTimeOffset, ReconciliationService.IEventHandler>(fs));
        service = new ReconciliationService<ReconciliationService.IEventHandler>(backups,
            PackageReconciliationContext<ReconciliationService.IEventHandler>.Factory());
    }

    #endregion

    [Fact]
    public void LaterUpdatePreservesEarlierClaim()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF"), Package("B", 1, "BF", "Shared"));

        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 2, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("A:2");
        state["A"].Files.Should().Contain("Shared");
        state["B"].Files.Should().NotContain("Shared");
        state["B"].ShadowedBy.Should().Equal("A");
    }

    [Fact]
    public void KeptPackageYieldsToEarlierUpdate()
    {
        Reconcile(Package("A", 1, "AF"), Package("B", 1, "BF", "Shared"));

        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("A:2");
        state["B"].Files.Should().Equal("BF");
        state["B"].ShadowedBy.Should().Equal("A");
    }

    [Fact]
    public void RemovingUpdatedPackagesRestoresOriginal()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF"), Package("B", 1, "BF", "Shared"));
        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 2, "BF", "Shared"));

        Reconcile();

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("Original");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void RemovingUpdatedPackagesRemovesModAddedFile()
    {
        Reconcile(Package("A", 1, "AF"), Package("B", 1, "BF", "Shared"));
        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 2, "BF", "Shared"));

        Reconcile();

        fs.File.Exists(PathOf("Shared")).Should().BeFalse();
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void LaterUpdateRespectsEarlierDeletion()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF"), Package("B", 1, "BF", "Shared"));

        Reconcile(Package("A", 2, "AF", "Shared-remove"), Package("B", 2, "BF", "Shared"));

        fs.File.Exists(PathOf("Shared")).Should().BeFalse();
        fs.File.ReadAllText(PathOf("Shared.orig")).Should().Be("Original");
        state["A"].Files.Should().Contain("Shared");
        state["B"].Files.Should().NotContain("Shared");
    }

    [Fact]
    public void DroppedFileIsSuppliedByShadowedPackage()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF", "Shared"), Package("B", 1, "BF", "Shared"));

        Reconcile(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        state["B"].Files.Should().Contain("Shared");
        state["B"].ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void InterruptedUpdateRecoversShadowedFile()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF", "Shared"), Package("B", 1, "BF", "Shared"));
        ReconcileFirstAction(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));
        ReloadState();

        Reconcile(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        state["B"].Files.Should().Contain("Shared");
        state["B"].Partial.Should().BeFalse();
        state["B"].ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void InterruptedRemovalRecoversShadowedFile()
    {
        Reconcile(Package("A", 1, "AF", "Shared"), Package("B", 1, "BF", "Shared"));
        ReconcileFirstAction(Package("B", 1, "BF", "Shared"));
        ReloadState();

        Reconcile(Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        state["B"].Partial.Should().BeFalse();
        state["B"].ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void RemovingRecoveredPackagesRestoresOriginal()
    {
        fs.AddFile(PathOf("Shared"), "Original");
        Reconcile(Package("A", 1, "AF", "Shared"), Package("B", 1, "BF", "Shared"));
        ReconcileFirstAction(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));
        ReloadState();
        Reconcile(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        Reconcile();

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("Original");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    #region Utility Methods

    private string PathOf(string file) => Path.Combine(destination.FullName, file);

    private IPackage Package(string name, int version, params string[] files)
    {
        var package = new Mock<IPackage>();
        package.Setup(p => p.CreateInstaller()).Returns(() =>
            new StaticFilesInstaller(fs, time, name, version, files.ToDictionary(f => f, _ => $"{name}:{version}"), []));
        return package.Object;
    }

    private void Reconcile(params IPackage[] packages) =>
        service.Reconcile(state, packages, destination, updated => state = updated,
            Mock.Of<ReconciliationService.IEventHandler>(), CancellationToken.None);

    private void ReconcileFirstAction(params IPackage[] packages)
    {
        using var cancellation = new CancellationTokenSource();
        var events = new Mock<ReconciliationService.IEventHandler>();
        events.Setup(e => e.ProgressUpdate(It.IsAny<IPercent?>())).Callback(cancellation.Cancel);
        service.Reconcile(state, packages, destination, updated => state = updated,
            events.Object, cancellation.Token);
    }

    private void ReloadState()
    {
        var persistence = new JsonFileStatePersistence(fs.Directory.CreateTempSubdirectory());
        persistence.WriteState(new SavedState(state));
        state = persistence.ReadState().Installation;
    }

    #endregion
}
