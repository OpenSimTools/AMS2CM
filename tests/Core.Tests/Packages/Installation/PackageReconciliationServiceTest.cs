using System.Collections.ObjectModel;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

[IntegrationTest]
public class PackageReconciliationServiceTest : ReconciliationServiceTestBase<PackageReconciliationService.IEventHandler>
{
    #region Setup

    private class TestException : Exception;

    protected override IReconciliationService<PackageReconciliationService.IEventHandler> NewService(
        IBackupStrategyProvider<DateTimeOffset, PackageReconciliationService.IEventHandler> backupStrategyProvider) =>
        new PackageReconciliationService<PackageReconciliationService.IEventHandler>(backupStrategyProvider);

    #endregion

    [Fact]
    public void Apply_NoPackages()
    {
        Apply([]);

        InstallationState.Should().BeEmpty();

        EventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()), Times.Never);
    }

    [Fact]
    public void Apply_TracksProgress()
    {
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["U1"] =
                new(Time: ValueNotUsed, VersionHash: null, Partial: false, Dependencies: [], Files: FilesNotUsed, ShadowedBy: []),
            // 20%
            ["U2"] = new(Time: ValueNotUsed, VersionHash: null, Partial: false, Dependencies: [], Files: FilesNotUsed,
                ShadowedBy: [])
            // 40%
        };

        Apply([
            InstallerOf("I1"),
            // 60%
            InstallerOf("I2"),
            // 80%
            InstallerOf("I3"),
            // 100%
        ]);

        progress.Should().Equal(0.2, 0.4, 0.6, 0.8, 1.0);
    }

    [Fact]
    public void Apply_InstallsSelectedPackages()
    {
        Apply([
            InstallerOf("A", versionHash: 42, files: [
                "AF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: false, Dependencies: [], Files: [
                "AF"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF")));
        BackupStrategyMock.Verify(m => m.AfterInstall(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.ReconciliationStart());
        EventHandlerMock.Verify(m => m.InstallingPackage("A"));
        EventHandlerMock.Verify(m => m.ReconciliationEnd());
        EventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        EventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UninstallsUnselectedPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: ValueNotUsed,
                VersionHash: 42,
                Partial: false,
                Dependencies: [],
                Files: ["AF"],
                ShadowedBy: [])
        };

        Apply([]);

        InstallationState.Should().BeEmpty();

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.ReconciliationStart());
        EventHandlerMock.Verify(m => m.UninstallingPackage("A"));
        EventHandlerMock.Verify(m => m.ReconciliationEnd());
        EventHandlerMock.Verify(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        EventHandlerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public void Apply_UpdatesChangedPackages(int? previousVersionHash, int? currentVersionHash)
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: previousVersionHash, Partial: false, Dependencies: [], Files:
            [
                "AF",
                "AF1",
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("A", versionHash: currentVersionHash, [
                "AF",
                "AF2"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: currentVersionHash, Partial: false, Dependencies: [], Files: [
                "AF",
                "AF2"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF2")));
        EventHandlerMock.Verify(m => m.UninstallingPackage("A"), Times.Once);
        EventHandlerMock.Verify(m => m.InstallingPackage("A"), Times.Once);
        EventHandlerMock.Verify(m => m.SkippingPackage("A"), Times.Never);
    }

    [Fact]
    public void Apply_ReinstallsPartialPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: true, Dependencies: [], Files:
            [
                "AF1"
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("A", versionHash: 1, [
                "AF1",
                "AF2"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files: [
                "AF1",
                "AF2"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF2")));
    }

    [Fact]
    public void Apply_PreservesPackageDependencies()
    {
        Apply([
            InstallerOf("A", versionHash: 42, files: [
                "AF"
            ], dependencies: ["X"])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: false, Dependencies: ["X"], Files: [
                "AF"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_FirstInstalledFilesTakePrecedence()
    {
        Apply([
            InstallerOf("A", versionHash: 1, files: [
                "AF1", "AF2"
            ]),
            InstallerOf("B", versionHash: 2, files: [
                "BF"
            ]),
            InstallerOf("C", versionHash: 3, files: [
                "AF1", "BF", "CF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files: [
                "AF1", "AF2"
            ], ShadowedBy: []),
            ["B"] = new(Time: TestInstallationTimeUtc, VersionHash: 2, Partial: false, Dependencies: [], Files: [
                "BF"
            ], ShadowedBy: []),
            ["C"] = new(Time: TestInstallationTimeUtc, VersionHash: 3, Partial: false, Dependencies: [], Files: [
                "CF"
            ], ShadowedBy: ["A", "B"])
        });
    }

    [Fact]
    public void Apply_RestoresFilesPreviouslyShadowedByUninstalledPackage()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false, Dependencies: [], Files:
            [
                "AF1",
            ], ShadowedBy: ["B"]),
            ["B"] = new(Time: ValueNotUsed, VersionHash: 2, Partial: false, Dependencies: [], Files:
            [
                "SF", // SF in A was shadowed by B
                "BF1",
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("A", versionHash: 1, [
                "SF",
                "AF1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files: [
                "SF",
                "AF1"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_InstallStopsIfBackupFails()
    {
        BackupStrategyMock.Setup(m => m.PerformBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Apply([
            InstallerOf("A", versionHash: 42, files: [
                "AF1", "Fail", "AF2"
            ])
        ])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: true, Dependencies: [], Files: [
                "AF1",
                "Fail" // We don't know where it failed, so we add it
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_UninstallStopsIfBackupFails()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 42, Partial: false, Dependencies: [], Files:
            [
                "AF1",
                "Fail",
                "AF2"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 42, Partial: true, Dependencies: [], Files: [
                "Fail", // We don't know where it failed, so we leave it
                "AF2"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallFailuresResultsInPartialInstallation()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: false, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Apply_PartialPackagesStayPartial()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Apply([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files: [
                "Fail"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Apply_UninstallRemovesEmptyDirectories()
    {
        var subDir = Path.Combine("D1", "D2");
        Directory.CreateDirectory(DestinationPath(subDir).Full);

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files:
            [
                Path.Combine(subDir, "F1")
            ], ShadowedBy: [])
        };

        Apply([]);

        InstallationState.Should().BeEmpty();

        Directory.Exists(DestinationPath("D1").Full).Should().BeFalse();
    }

    [Fact]
    public void Apply_HandlesPriorityInversion()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "AF",
                "Shared"
            ], ShadowedBy: []),
            ["B"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "BF"
            ], ShadowedBy: ["A"]),
            ["Untouched"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "UF"
            ], ShadowedBy: [])
        };

        Apply([
            InstallerOf("B", versionHash: 0, [
                "BF",
                "Shared"
            ]),
            InstallerOf("A", versionHash: 0, [
                "AF",
                "Shared"
            ]),
            InstallerOf("Untouched", versionHash: 0, [
                "Not considered"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files: [
                "AF"
            ], ShadowedBy: ["B"]),
            ["B"] = new(Time: TestInstallationTimeUtc, VersionHash: 0, Partial: false, Dependencies: [], Files: [
                "BF",
                "Shared"
            ], ShadowedBy: []),
            ["Untouched"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "UF"
            ], ShadowedBy: [])
        });

        EventHandlerMock.Verify(m => m.SkippingPackage("Untouched"));
    }

    [Fact]
    public void Apply_HandlesFileDeletionOnUpgrade()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "A1",
                "A2"
            ], ShadowedBy: []),
        };

        Apply([
            InstallerOf("A", versionHash: 1, [
                "A1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files: [
                "A1",
            ], ShadowedBy: []),
        });
    }

    [Fact]
    public void Apply_UninstallsAllPackagesBeforeInstallingOrKeepingPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["Removed"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: FilesNotUsed, ShadowedBy: []),
            ["A"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: FilesNotUsed, ShadowedBy: []),
            ["B"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: FilesNotUsed, ShadowedBy: []),
            ["Kept"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: FilesNotUsed, ShadowedBy: [])
        };

        // InSequence not verified in Loose mode!
        var eventHandlerMock = new Mock<PackageReconciliationService.IEventHandler>(MockBehavior.Strict);
        eventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()));
        var sequence = new MockSequence();
        eventHandlerMock.InSequence(sequence).Setup(m => m.ReconciliationStart());
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("Removed"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("A"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("B"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("New"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("A"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.SkippingPackage("Kept"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("B"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.ReconciliationEnd());

        Apply([
            InstallerOf("New", versionHash: 1),
            InstallerOf("A", versionHash: 2),
            InstallerOf("Kept", versionHash: 1),
            InstallerOf("B", versionHash: 2)
        ], eventHandlerMock.Object);
    }
}

public abstract class ReconciliationServiceTestBase<TEventHandler> where TEventHandler : class
{
    // Randomness ensures that at least some test runs will fail if it's used
    protected readonly DateTimeOffset ValueNotUsed = Random.Shared.Next() > 0 ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue;
    protected readonly IReadOnlyCollection<string> FilesNotUsed = [Guid.NewGuid().ToString()];

    protected readonly Mock<IBackupStrategy> BackupStrategyMock = new();
    protected readonly Mock<TEventHandler> EventHandlerMock = new();

    protected readonly DateTimeOffset TestInstallationTimeUtc;
    private readonly FakeTimeProvider TestTimeProvider;
    private readonly MockFileSystem TestFileSystem = new();

    protected IReadOnlyDictionary<string, PackageInstallationState>? InstallationState;
    protected IEnumerable<string> InstalledPackages =>
        InstallationState?.Keys ?? Enumerable.Empty<string>();

    private readonly string destinationDir = Path.GetRandomFileName();

    protected ReconciliationServiceTestBase()
    {
        TestInstallationTimeUtc = DateTimeOffset.Now.AddDays(10).ToUniversalTime();
        var fakeLocalTimeOffset = TimeSpan.FromHours(3);
        TestTimeProvider = new FakeTimeProvider(TestInstallationTimeUtc.ToOffset(fakeLocalTimeOffset));
    }

    protected RootedPath DestinationPath(string relativePath) => new(destinationDir, relativePath);

    protected abstract IReconciliationService<TEventHandler> NewService(
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider);

    protected void Apply(IPackageInstaller[] installers, TEventHandler? eventHandler = null)
    {
        var packages = installers.Select(installer =>
        {
            var package = new Mock<IPackage>();
            package.SetupGet(p => p.Name).Returns(installer.PackageName);
            package.SetupGet(p => p.VersionHash).Returns(installer.PackageVersionHash);
            package.SetupGet(p => p.Installer).Returns(installer);
            return package.Object;
        }).ToArray();
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<DateTimeOffset, TEventHandler>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<DateTimeOffset>(), It.IsAny<TEventHandler>()))
            .Returns(BackupStrategyMock.Object);
        var service = NewService(
            backupStrategyProviderMock.Object);
        service.Reconcile(
            InstallationState ?? ReadOnlyDictionary<string, PackageInstallationState>.Empty,
            packages,
            destinationDir,
            newState => InstallationState = newState,
            eventHandler ?? EventHandlerMock.Object,
            CancellationToken.None);
    }

    protected IPackageInstaller InstallerOf(string name, int? versionHash = null) =>
        new StaticFilesInstaller(TestFileSystem, TestTimeProvider, name, versionHash, new Dictionary<string, string>
        {
            [name] = "Contents"
        }, []);

    protected IPackageInstaller InstallerOf(string name, int? versionHash, IReadOnlyCollection<string> files) =>
        InstallerOf(name, versionHash, files, Array.Empty<string>());

    protected IPackageInstaller InstallerOf(string name, int? versionHash,
        IReadOnlyCollection<string> files, IReadOnlyCollection<string> dependencies) =>
        new StaticFilesInstaller(TestFileSystem, TestTimeProvider,
            name, versionHash, files.ToDictionary(f => f, _ => ""), dependencies);
}
