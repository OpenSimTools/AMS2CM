using Core.Packages.Installation;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation;

[IntegrationTest]
public class PackageReconciliationContextTest : ReconciliationIntegrationTestBase<IReconciliationContext.IEventHandler>
{
    #region Setup

    private class TestException : Exception;

    protected override IReconciliationContext<IReconciliationContext.IEventHandler>.FactoryFunc ContextFactory =>
        PackageReconciliationContext<IReconciliationContext.IEventHandler>.Factory();

    #endregion

    [Fact]
    public void Reconcile_NoPackages()
    {
        Reconcile([]);

        InstallationState.Should().BeEmpty();
    }

    [Fact]
    public void Reconcile_InstallsSelectedPackages()
    {
        Reconcile([
            Package("A", versionHash: 42, files:
            [
                "AF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: false, Dependencies: [], Files:
            [
                "AF"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF")));
        BackupStrategyMock.Verify(m => m.AfterInstall(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.InstallingPackage("A"));
    }

    [Fact]
    public void Reconcile_UninstallsUnselectedPackages()
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

        Reconcile([]);

        InstallationState.Should().BeEmpty();

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF")));
        BackupStrategyMock.VerifyNoOtherCalls();

        EventHandlerMock.Verify(m => m.UninstallingPackage("A"));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public void Reconcile_UpdatesChangedPackages(int? previousVersionHash, int? currentVersionHash)
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: previousVersionHash, Partial: false, Dependencies: [], Files:
            [
                "AF",
                "AF1",
            ], ShadowedBy: [])
        };

        Reconcile([
            Package("A", versionHash: currentVersionHash, [
                "AF",
                "AF2"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: currentVersionHash, Partial: false,
                Dependencies: [], Files:
                [
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
    public void Reconcile_ReinstallsPartialPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: true, Dependencies: [], Files:
            [
                "AF1"
            ], ShadowedBy: [])
        };

        Reconcile([
            Package("A", versionHash: 1, [
                "AF1",
                "AF2"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files:
            [
                "AF1",
                "AF2"
            ], ShadowedBy: [])
        });

        BackupStrategyMock.Verify(m => m.RestoreBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF1")));
        BackupStrategyMock.Verify(m => m.PerformBackup(DestinationPath("AF2")));
    }

    [Fact]
    public void Reconcile_PreservesPackageDependencies()
    {
        Reconcile([
            Package("A", versionHash: 42, files:
            [
                "AF"
            ], dependencies: ["X"])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: false, Dependencies: ["X"], Files:
            [
                "AF"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Reconcile_FirstInstalledFilesTakePrecedence()
    {
        Reconcile([
            Package("A", versionHash: 1, files:
            [
                "AF1", "AF2"
            ]),
            Package("B", versionHash: 2, files:
            [
                "BF"
            ]),
            Package("C", versionHash: 3, files:
            [
                "AF1", "BF", "CF"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files:
            [
                "AF1", "AF2"
            ], ShadowedBy: []),
            ["B"] = new(Time: TestInstallationTimeUtc, VersionHash: 2, Partial: false, Dependencies: [], Files:
            [
                "BF"
            ], ShadowedBy: []),
            ["C"] = new(Time: TestInstallationTimeUtc, VersionHash: 3, Partial: false, Dependencies: [], Files:
            [
                "CF"
            ], ShadowedBy: ["A", "B"])
        });
    }

    [Fact]
    public void Reconcile_RestoresFilesPreviouslyShadowedByUninstalledPackage()
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

        Reconcile([
            Package("A", versionHash: 1, [
                "SF",
                "AF1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files:
            [
                "SF",
                "AF1"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Reconcile_DoesNotBackUpFilesShadowedInKeptPackages()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["B"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: ["Shared", "BF"], ShadowedBy: [])
        };

        Reconcile([
            Package("A", versionHash: 1, files: ["Shared"]),
            Package("B", versionHash: 1, files: ["Shared", "BF"])
        ]);

        InstallationState["A"].Files.Should().Equal("Shared");
        InstallationState["B"].Files.Should().Equal("BF");
        InstallationState["B"].ShadowedBy.Should().Equal("A");
        BackupStrategyMock.Verify(m => m.PerformBackup(It.IsAny<RootedPath>()), Times.Never);
        BackupStrategyMock.Verify(m => m.AfterInstall(DestinationPath("Shared")), Times.Once);
        BackupStrategyMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Reconcile_InstallStopsIfBackupFails()
    {
        BackupStrategyMock.Setup(m => m.PerformBackup(DestinationPath("Fail"))).Throws<TestException>();

        this.Invoking(m => m.Reconcile([
            Package("A", versionHash: 42, files:
            [
                "AF1", "Fail", "AF2"
            ])
        ])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 42, Partial: true, Dependencies: [], Files:
            [
                "AF1",
                "Fail" // We don't know where it failed, so we add it
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Reconcile_UninstallStopsIfBackupFails()
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

        this.Invoking(m => m.Reconcile([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 42, Partial: true, Dependencies: [], Files:
            [
                "Fail", // We don't know where it failed, so we leave it
                "AF2"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Reconcile_UninstallFailuresResultsInPartialInstallation()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: false, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Reconcile([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        });
    }

    [Fact]
    public void Reconcile_PartialPackagesStayPartial()
    {
        BackupStrategyMock.Setup(m => m.RestoreBackup(DestinationPath("Fail"))).Throws<TestException>();

        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        };

        this.Invoking(m => m.Reconcile([])).Should().Throw<TestException>();

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: null, Partial: true, Dependencies: [], Files:
            [
                "Fail"
            ], ShadowedBy: [])
        });
    }


    [Fact]
    public void Reconcile_UninstallRemovesEmptyDirectories()
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

        Reconcile([]);

        InstallationState.Should().BeEmpty();

        Directory.Exists(DestinationPath("D1").Full).Should().BeFalse();
        Directory.Exists(DestinationPath("").Full).Should().BeTrue();
    }

    [Fact]
    public void Reconcile_HandlesPriorityInversion()
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

        Reconcile([
            Package("B", versionHash: 0, [
                "BF",
                "Shared"
            ]),
            Package("A", versionHash: 0, [
                "AF",
                "Shared"
            ]),
            Package("Untouched", versionHash: 0, [
                "Not considered"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "AF"
            ], ShadowedBy: ["B"]),
            ["B"] = new(Time: TestInstallationTimeUtc, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
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
    public void Reconcile_HandlesFileDeletionOnUpgrade()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 0, Partial: false, Dependencies: [], Files:
            [
                "A1",
                "A2"
            ], ShadowedBy: []),
        };

        Reconcile([
            Package("A", versionHash: 1, [
                "A1"
            ])
        ]);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: TestInstallationTimeUtc, VersionHash: 1, Partial: false, Dependencies: [], Files:
            [
                "A1",
            ], ShadowedBy: []),
        });
    }

    [Fact]
    public void Reconcile_UninstallsAllPackagesBeforeInstallingOrKeepingPackages()
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
        var eventHandlerMock = new Mock<IReconciliationContext.IEventHandler>(MockBehavior.Strict);
        var sequence = new MockSequence();
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("Removed"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("A"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.UninstallingPackage("B"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("New"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("A"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.SkippingPackage("Kept"));
        eventHandlerMock.InSequence(sequence).Setup(m => m.InstallingPackage("B"));

        Reconcile([
            Package("New", versionHash: 1),
            Package("A", versionHash: 2),
            Package("Kept", versionHash: 1),
            Package("B", versionHash: 2)
        ], eventHandlerMock.Object);
    }

    [Fact]
    public void Reconcile_ExecutingOneActionPreservesLaterState()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: ["AF"], ShadowedBy: []),
            ["B"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: ["BF"], ShadowedBy: [])
        };

        Reconcile([
            Package("A", versionHash: 2, files: ["AF"]),
            Package("B", versionHash: 2, files: ["BF"])
        ], actionCount: 1);

        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["B"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: ["BF"], ShadowedBy: [])
        });
    }

    [Fact]
    public void Reconcile_RepeatedCallsUseIndependentState()
    {
        var initialState = InstallationState;

        Reconcile([
            Package("A", ["Shared"]),
        ]);
        var savedState = InstallationState;

        InstallationState = initialState;
        Reconcile([
            Package("B", ["Shared"])
        ]);

        savedState.Keys.Should().Equal("A");
        InstallationState.Keys.Should().Equal("B");

        EventHandlerMock.Verify(m => m.UninstallingPackage(It.IsAny<string>()), Times.Never);
    }
}
