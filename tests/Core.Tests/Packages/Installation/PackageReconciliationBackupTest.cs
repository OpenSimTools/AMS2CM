using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

[IntegrationTest]
public class PackageReconciliationBackupTest
{
    #region Setup

    private static readonly DateTimeOffset OriginalFileTime = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset PreviousInstallationTime = OriginalFileTime.AddDays(1);
    private static readonly DateTimeOffset InstallationTime = PreviousInstallationTime.AddDays(1);

    private readonly MockFileSystem fs = new();
    private readonly FakeTimeProvider time = new(InstallationTime);
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
    public void Reconcile_LaterUpdatePreservesEarlierClaim()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF", "Shared"], [])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "B:1", PreviousInstallationTime);
        AddFile("Shared.orig", "Original", OriginalFileTime);

        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 2, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("A:2");
        fs.File.ReadAllText(PathOf("Shared.orig")).Should().Be("Original");
        state["A"].Files.Should().Contain("Shared");
        state["B"].Files.Should().NotContain("Shared");
        state["B"].ShadowedBy.Should().Equal("A");
    }

    [Fact]
    public void Reconcile_UpdateDoesNotBackUpInstalledFile()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF", "Shared"], [])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "B:1", PreviousInstallationTime);

        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 2, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("A:2");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void Reconcile_KeptPackageYieldsToEarlierUpdate()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF", "Shared"], [])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "B:1", PreviousInstallationTime);

        Reconcile(Package("A", 2, "AF", "Shared"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("A:2");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
        state["B"].Files.Should().Equal("BF");
        state["B"].ShadowedBy.Should().Equal("A");
    }

    [Fact]
    public void Reconcile_RemovalRestoresOriginal()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(InstallationTime, 2, false, [], ["AF", "Shared"], []),
            ["B"] = new(InstallationTime, 2, false, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:2", InstallationTime);
        AddFile("BF", "B:2", InstallationTime);
        AddFile("Shared", "A:2", InstallationTime);
        AddFile("Shared.orig", "Original", OriginalFileTime);

        Reconcile();

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("Original");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void Reconcile_RemovalDeletesFileWithoutBackup()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(InstallationTime, 2, false, [], ["AF", "Shared"], []),
            ["B"] = new(InstallationTime, 2, false, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:2", InstallationTime);
        AddFile("BF", "B:2", InstallationTime);
        AddFile("Shared", "A:2", InstallationTime);

        Reconcile();

        fs.File.Exists(PathOf("Shared")).Should().BeFalse();
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void Reconcile_LaterUpdateRespectsEarlierDeletion()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF", "Shared"], [])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "B:1", PreviousInstallationTime);
        AddFile("Shared.orig", "Original", OriginalFileTime);

        Reconcile(Package("A", 2, "AF", "Shared-remove"), Package("B", 2, "BF", "Shared"));

        fs.File.Exists(PathOf("Shared")).Should().BeFalse();
        fs.File.ReadAllText(PathOf("Shared.orig")).Should().Be("Original");
        state["A"].Files.Should().Contain("Shared");
        state["B"].Files.Should().NotContain("Shared");
    }

    [Fact]
    public void Reconcile_DroppedFileIsSuppliedByShadowedPackage()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF", "Shared"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "A:1", PreviousInstallationTime);
        AddFile("Shared.orig", "Original", OriginalFileTime);

        Reconcile(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        fs.File.ReadAllText(PathOf("Shared.orig")).Should().Be("Original");
        state["B"].Files.Should().Contain("Shared");
        state["B"].ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void Reconcile_InterruptedUpdateLeavesRecoverableState()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF", "Shared"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "A:1", PreviousInstallationTime);
        AddFile("Shared.orig", "Original", OriginalFileTime);

        ReconcileFirstAction(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        state.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(InstallationTime, 2, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, true, [], ["BF"], ["A"])
        });
        fs.File.ReadAllText(PathOf("AF")).Should().Be("A:2");
        fs.File.ReadAllText(PathOf("BF")).Should().Be("B:1");
        fs.File.ReadAllText(PathOf("Shared")).Should().Be("Original");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void Reconcile_RecoversAfterInterruptedUpdate()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(InstallationTime, 2, false, [], ["AF"], []),
            ["B"] = new(PreviousInstallationTime, 1, true, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:2", InstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "Original", OriginalFileTime);

        Reconcile(Package("A", 2, "AF"), Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        fs.File.ReadAllText(PathOf("Shared.orig")).Should().Be("Original");
        state["B"].Should().BeEquivalentTo(new PackageInstallationState(
            InstallationTime, 1, false, [], ["BF", "Shared"], []));
    }

    [Fact]
    public void Reconcile_InterruptedRemovalLeavesRecoverableState()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(PreviousInstallationTime, 1, false, [], ["AF", "Shared"], []),
            ["B"] = new(PreviousInstallationTime, 1, false, [], ["BF"], ["A"])
        };
        AddFile("AF", "A:1", PreviousInstallationTime);
        AddFile("BF", "B:1", PreviousInstallationTime);
        AddFile("Shared", "A:1", PreviousInstallationTime);

        ReconcileFirstAction(Package("B", 1, "BF", "Shared"));

        state.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["B"] = new(PreviousInstallationTime, 1, true, [], ["BF"], ["A"])
        });
        fs.File.ReadAllText(PathOf("BF")).Should().Be("B:1");
        fs.File.Exists(PathOf("AF")).Should().BeFalse();
        fs.File.Exists(PathOf("Shared")).Should().BeFalse();
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
    }

    [Fact]
    public void Reconcile_RecoversAfterInterruptedRemoval()
    {
        state = new Dictionary<string, PackageInstallationState>
        {
            ["B"] = new(PreviousInstallationTime, 1, true, [], ["BF"], ["A"])
        };
        AddFile("BF", "B:1", PreviousInstallationTime);

        Reconcile(Package("B", 1, "BF", "Shared"));

        fs.File.ReadAllText(PathOf("BF")).Should().Be("B:1");
        fs.File.ReadAllText(PathOf("Shared")).Should().Be("B:1");
        fs.File.Exists(PathOf("Shared.orig")).Should().BeFalse();
        state["B"].Should().BeEquivalentTo(new PackageInstallationState(
            InstallationTime, 1, false, [], ["BF", "Shared"], []));
    }

    #region Utility Methods

    private void AddFile(string file, string contents, DateTimeOffset creationTime) =>
        fs.AddFile(PathOf(file), new MockFileData(contents) { CreationTime = creationTime });

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

    #endregion
}
