using System.IO.Abstractions.TestingHelpers;
using Core.API;
using Core.Games;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation;
using Core.Utils;
using FluentAssertions;

using static Core.Mods.Installation.Installers.GeneratedBootfilesInstaller;

namespace Core.Tests.Mods.Installation;

[IntegrationTest]
public class ModReconciliationContextTest :
    ReconciliationIntegrationTestBase<ModReconciliationContext.IEventHandler>
{
    #region Setup

    private readonly Mock<Func<IPackageInstaller>> generateBootfiles = new();
    private static readonly ModInstallConfig Config = new()
    {
        DirsAtRoot = ["Files", "Configs"],
        GameSupportedModDir = "Configs",
        BootfilesVehicleListDir = "Files",
        GenerateModDetails = false
    };

    private const string GeneratedBootfilesName = "__generated";
    private const string BootfilesPackageName = "__package";
    private const string ModRequiringBootfiles = "ModRequiringBootfiles";

    private const string ModFile = @"Files\Mod.crd";
    private const string BootfilesFile = @"Files\Bootfiles";
    private const string GeneratedFile = @"Files\Generated";

    private readonly MockFileSystem sourceFileSystem = new();
    private readonly Mock<MoveFileBackupStrategy.IBackupFileNaming> sourceBackupFileNaming = new();

    private static IEnumerable<string> SourceFiles => SourceBackupNames.Keys;
    private static readonly Dictionary<string, string> SourceBackupNames = new()
    {
        [BootFlowPakFileName] = "first.bin",
        [BootSplashPakFileName] = "second.bin",
        [PhysicsPersistentPakFileName] = "third.bin"
    };
    private static readonly DateTime SourceTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ModReconciliationContextTest()
    {
        generateBootfiles.Setup(g => g())
            .Returns(() => TestPackage(GeneratedBootfilesName, 1, [GeneratedFile]).CreateInstaller());
    }

    protected override IReconciliationContext<ModReconciliationContext.IEventHandler>.FactoryFunc ContextFactory
    {
        get
        {
            var bootfilesNamingMock = new Mock<IBootfilesNaming>();
            bootfilesNamingMock.Setup(m => m.IsBootfiles(BootfilesPackageName)).Returns(true);
            bootfilesNamingMock.Setup(m => m.IsBootfiles(GeneratedBootfilesName)).Returns(true);
            bootfilesNamingMock.Setup(m => m.IsGeneratedBootfiles(GeneratedBootfilesName)).Returns(true);
            bootfilesNamingMock.SetupGet(m => m.GeneratedBootfilesName).Returns(GeneratedBootfilesName);
            return previousState => new TestModReconciliationContext(previousState, bootfilesNamingMock.Object, this);
        }
    }

    private sealed class TestModReconciliationContext(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IBootfilesNaming bootfilesNaming,
        ModReconciliationContextTest test)
        : ModReconciliationContext<ModReconciliationContext.IEventHandler, ModInstallConfig>(previousState,
            Mock.Of<IGame>(g => g.InstallationDirectory == test.DestinationDirectory),
            Mock.Of<ITempDir>(t => t.BaseDirectory == test.Directory.CreateDirectory("Temp")),
            bootfilesNaming, Config,
            Mock.Of<MoveFileBackupStrategy.IBackupFileNaming>(), Mock.Of<IFileUpdateTracker>())
    {
        protected override IPackageInstaller CreateGeneratedBootfilesInstaller(
            IReadOnlyDictionary<string, PackageInstallationState> state) => test.generateBootfiles.Object();
    }
    #endregion

    [Fact]
    public void PlanningBootfilesSwitchPreservesInstallationState()
    {
        var expected = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [GeneratedBootfilesName], Files: [ModFile], ShadowedBy: []),
            [GeneratedBootfilesName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [GeneratedFile], ShadowedBy: [])
        };
        InstallationState = CopyInstallationState(expected);
        var previous = InstallationState;

        PlanReconciliation([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        InstallationState.Should().BeEquivalentTo(expected);
        previous.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void CreatesBootfilesInstallerAfterOwnershipTransfer()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [@"Files\Shared"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        var bootfiles = TestPackage(BootfilesPackageName, 1, [BootfilesFile]);
        Mock.Get(bootfiles).Setup(p => p.CreateInstaller()).Callback(() =>
        {
            // Observe ownership at construction time, before bootfiles processing can change it.
            InstallationState["NewOwner"].Files.Should().Contain(@"Files\Shared");
            InstallationState[ModRequiringBootfiles].Files.Should().NotContain(@"Files\Shared");
            InstallationState[ModRequiringBootfiles].ShadowedBy.Should().Contain("NewOwner");
        }).Returns(() => TestPackage(BootfilesPackageName, 1, [BootfilesFile]).CreateInstaller());

        Reconcile([
            TestPackage("NewOwner", 1, [@"Files\Shared"]),
            DependentMod(2, [@"Files\Shared"]),
            bootfiles
        ]);

        Mock.Get(bootfiles).Verify(p => p.CreateInstaller(), Times.Once);
    }

    [Fact]
    public void RemovingLastDependentDoesNotConstructBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        var bootfiles = TestPackage(BootfilesPackageName, 1, [BootfilesFile]);

        Reconcile([
            bootfiles
        ]);

        Mock.Get(bootfiles).Verify(p => p.CreateInstaller(), Times.Never);
    }

    [Fact]
    public void RemovingLastDependentRemovesInstalledBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        InstalledPackages.Should().BeEmpty();
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void BootfilesConstructionFailureRetainsPartialState()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        var bootfiles = TestPackage(BootfilesPackageName, 1, [BootfilesFile]);
        Mock.Get(bootfiles).Setup(p => p.CreateInstaller()).Throws(new FileNotFoundException("Missing source"));

        var reconcile = () => Reconcile([
            DependentMod(2),
            bootfiles
        ]);

        reconcile.Should().Throw<FileNotFoundException>();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void InstallsGeneratedBootfilesIfModRequiresThem()
    {
        Reconcile([
            DependentMod()
        ]);

        InstalledPackages.Should().BeEquivalentTo([ModRequiringBootfiles, GeneratedBootfilesName]);
    }

    [Fact]
    public void InstallsBootfilesPackageIfModRequiresThem()
    {
        Reconcile([
            DependentMod(),
            TestPackage(BootfilesPackageName, files: [BootfilesFile])
        ]);

        InstalledPackages.Should().BeEquivalentTo(ModRequiringBootfiles, BootfilesPackageName);
        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void SkipsUnnecessaryBootfilesPackage()
    {
        var bootfiles = TestPackage(BootfilesPackageName, files: [BootfilesFile]);

        Reconcile([
            TestPackage("Unrelated"),
            bootfiles
        ]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Never);
        Mock.Get(bootfiles).Verify(p => p.CreateInstaller(), Times.Never);
    }

    [Fact]
    public void SkipsUnnecessaryGeneratedBootfiles()
    {
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);

        Reconcile([], events.Object);

        events.VerifyNoOtherCalls();
        generateBootfiles.Verify(g => g(), Times.Never);
    }

    [Fact]
    public void ReinstalledModUsesSelectedBootfilesDependency()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(2)
        ]);

        EventHandlerMock.Verify(m => m.UninstallingPackage(ModRequiringBootfiles));
        EventHandlerMock.Verify(m => m.InstallingPackage(ModRequiringBootfiles));
        InstallationState[ModRequiringBootfiles].Dependencies.Should().BeEquivalentTo(GeneratedBootfilesName);
    }

    [Fact]
    public void KeptModUsesSelectedBootfilesDependency()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(1)
        ]);

        EventHandlerMock.Verify(m => m.SkippingPackage(ModRequiringBootfiles));
        InstallationState[ModRequiringBootfiles].Dependencies.Should().BeEquivalentTo(GeneratedBootfilesName);
    }

    [Fact]
    public void RemappingBootfilesDependenciesPreservesPreviousState()
    {
        var expected = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        InstallationState = CopyInstallationState(expected);
        var previousState = InstallationState;

        Reconcile([
            DependentMod(1)
        ]);

        previousState.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void BootfilesAreReinstalledAfterDependentModChanges()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(2),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void DependentModUpdateRebuildsBootfilesConfiguration()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(2),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        DestinationDirectory.FileSystem.File.ReadAllText(DestinationPath(@"Files\vehiclelist.lst").Full)
            .Should().Contain(ModFile);
    }

    [Fact]
    public void BootfilesVersionChangeReinstallsBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 2, [BootfilesFile])
        ]);

        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        InstallationState[BootfilesPackageName].VersionHash.Should().Be(2);
        InstallationState[BootfilesPackageName].Partial.Should().BeFalse();
    }

    [Fact]
    public void BootfilesVersionChangeRebuildsModConfiguration()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        SeedModConfiguration();

        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 2, [BootfilesFile])
        ]);

        EventHandlerMock.Verify(e => e.PostProcessingVehicles(), Times.Once);
        DestinationDirectory.FileSystem.File.ReadAllText(DestinationPath(@"Files\vehiclelist.lst").Full)
            .Should().Contain(ModFile);
    }

    [Fact]
    public void SwitchingToPackagedBootfilesRemovesGeneratedBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [GeneratedBootfilesName], Files: [ModFile], ShadowedBy: []),
            [GeneratedBootfilesName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [GeneratedFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        InstalledPackages.Should().BeEquivalentTo(ModRequiringBootfiles, BootfilesPackageName);
        InstallationState[ModRequiringBootfiles].Dependencies.Should().Equal(BootfilesPackageName);
        EventHandlerMock.Verify(e => e.UninstallingPackage(GeneratedBootfilesName), Times.Once);
        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void AddingDependentInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["OtherDependent"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [@"Files\Other"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        // Process the existing dependent and the new dependent, leaving bootfiles pending.
        Reconcile([
            TestPackage("OtherDependent", 1, [@"Files\Other"], [BootfilesPackageName]),
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ], actionCount: 2);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RemovingDependentInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            ["OtherDependent"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [@"Files\Other"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        // Remove the unselected dependent before processing the remaining selections.
        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RepairingDependentInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: true,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        // Complete the dependent repair, leaving bootfiles reconciliation pending.
        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void UnrelatedPackageReconciliationLeavesBootfilesUnchanged(int versionHash)
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            ["Unrelated"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Unrelated"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        var original = InstallationState[BootfilesPackageName];

        Reconcile([
            DependentMod(1),
            TestPackage("Unrelated", versionHash),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        EventHandlerMock.Verify(e => e.SkippingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Never);
        EventHandlerMock.Verify(e => e.PostProcessingVehicles(), Times.Never);
        InstallationState[BootfilesPackageName].Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ReplacingShadowingModInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            ["Shadowing"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Shared"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\BootOnly"], ShadowedBy: ["Shadowing"])
        };

        // Keep the dependent and replace the shadowing mod, leaving bootfiles pending.
        Reconcile([
            DependentMod(1),
            TestPackage("Shadowing", 2, [@"Files\Shared"]),
            TestPackage(BootfilesPackageName, 1, [@"Files\Shared", @"Files\BootOnly"])
        ], actionCount: 2);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RemovingShadowingModInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            ["Shadowing"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Shared"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\BootOnly"], ShadowedBy: ["Shadowing"])
        };

        // Remove the unselected shadowing mod before processing the remaining selections.
        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [@"Files\Shared", @"Files\BootOnly"])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void EmptySelectionRemovesObsoleteBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([]);

        InstallationState.Should().BeEmpty();
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void BootfilesRemovalFailureRetainsPartialState()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        var failure = new IOException("Restore failed");
        BackupStrategyMock.Setup(b => b.RestoreBackup(DestinationPath(BootfilesFile))).Throws(failure);

        var reconcile = () => Reconcile([
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        reconcile.Should().Throw<IOException>().Which.Should().BeSameAs(failure);
        InstallationState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: true,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        });
    }

    [Fact]
    public void ConfigurationFailureRetainsPartialBootfilesState()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        BackupStrategyMock.Setup(b => b.AfterInstall(DestinationPath(@"Files\vehiclelist.lst")))
            .Throws(new IOException("Configuration failed"));

        var reconcile = () => Reconcile([
            DependentMod(2),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        reconcile.Should().Throw<IOException>();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
        InstallationState[BootfilesPackageName].Files.Should().Contain(BootfilesFile);
    }

    [Fact]
    public void UnknownBootfilesVersionIsReinstalled()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: null, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
        InstallationState[BootfilesPackageName].VersionHash.Should().Be(1);
        InstallationState[BootfilesPackageName].Partial.Should().BeFalse();
    }

    [Fact]
    public void ChangedShadowingReinstallsBootfiles()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            ["Shadowing"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Shared"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\BootOnly"], ShadowedBy: ["Shadowing"])
        };

        Reconcile([
            DependentMod(1),
            TestPackage("Shadowing", 2, [@"Files\Other"]),
            TestPackage(BootfilesPackageName, 1, [@"Files\Shared", @"Files\BootOnly"])
        ]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        InstallationState[BootfilesPackageName].Files.Should().Contain(@"Files\Shared");
    }

    [Fact]
    public void NewClaimPreventsBootfilesUninstallFromRestoringPath()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [@"Files\Other"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Shared", @"Files\BootOnly"], ShadowedBy: [])
        };

        Reconcile([
            DependentMod(2, [@"Files\Shared"]),
            TestPackage(BootfilesPackageName, 1, [@"Files\Shared", @"Files\BootOnly"])
        ]);

        BackupStrategyMock.Verify(b => b.RestoreBackup(DestinationPath(@"Files\Shared")), Times.Never);
    }

    [Fact]
    public void UpdatingDependentInvalidatesPackagedBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };

        // Complete the dependent update, leaving packaged bootfiles reconciliation pending.
        Reconcile([
            DependentMod(2),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void UpdatingDependentInvalidatesGeneratedBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [GeneratedBootfilesName], Files: [ModFile], ShadowedBy: []),
            [GeneratedBootfilesName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [GeneratedFile], ShadowedBy: [])
        };

        // Complete the dependent update, leaving generated bootfiles reconciliation pending.
        Reconcile([
            DependentMod(2)
        ], actionCount: 1);

        InstallationState[GeneratedBootfilesName].Partial.Should().BeTrue();
    }

    [Fact]
    public void FailedDependentRemovalRetainsBootfilesInvalidation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        BackupStrategyMock.Setup(b => b.RestoreBackup(DestinationPath(ModFile)))
            .Throws(new IOException("Restore failed"));

        var reconcile = () => Reconcile([
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        reconcile.Should().Throw<IOException>();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void FailedKnownDependentInstallationRetainsBootfilesInvalidation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["OtherDependent"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [@"Files\Other"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [BootfilesFile], ShadowedBy: [])
        };
        BackupStrategyMock.Setup(b => b.AfterInstall(DestinationPath(@"Configs\ModRequiringBootfiles_1\vehiclelist.lst")))
            .Throws(new IOException("Configuration failed"));

        var reconcile = () => Reconcile([
            TestPackage("OtherDependent", 1, [@"Files\Other"], [BootfilesPackageName]),
            TestPackage(ModRequiringBootfiles, 1, [ModFile], [BootfilesPackageName]),
            TestPackage(BootfilesPackageName, 1, [BootfilesFile])
        ]);

        reconcile.Should().Throw<IOException>();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void NewOwnerInvalidatesBootfilesBeforeBootfilesReconciliation()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: [ModFile], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: [@"Files\Shared", @"Files\BootOnly"], ShadowedBy: [])
        };

        // Install the new owner before processing the dependent or bootfiles.
        Reconcile([
            TestPackage("NewOwner", 1, [@"Files\Shared"]),
            DependentMod(1),
            TestPackage(BootfilesPackageName, 1, [@"Files\Shared", @"Files\BootOnly"])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void OwnershipLookupNormalizesSourcePaths()
    {
        SeedSources();
        var relative = Path.Combine(PakfilesDirectory, BootFlowPakFileName);
        var path = Path.Combine("Game", relative);
        sourceFileSystem.File.Move(path, BackupPath(BootFlowPakFileName));
        sourceFileSystem.AddFile(path, new MockFileData("Installed content") { CreationTime = SourceTimestamp });
        var expected = FileSystemHash.Hash32([
            sourceFileSystem.FileInfo.New(BackupPath(BootFlowPakFileName)),
            sourceFileSystem.FileInfo.New(Path.Combine("Game", PakfilesDirectory, BootSplashPakFileName)),
            sourceFileSystem.FileInfo.New(Path.Combine("Game", PakfilesDirectory, PhysicsPersistentPakFileName))
        ]);

        var installer = CreateGeneratedBootfilesInstaller(new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(SourceTimestamp), 1, false, [],
                [relative.Replace('\\', '/').ToLowerInvariant()], [])
        });

        installer.PackageVersionHash.Should().Be(expected);
    }

    [Fact]
    public void UpdatedOwnedSourceUsesLiveFile()
    {
        SeedSources();
        var relative = Path.Combine(PakfilesDirectory, BootFlowPakFileName);
        var path = Path.Combine("Game", relative);
        sourceFileSystem.AddFile(BackupPath(BootFlowPakFileName), "Old source");
        sourceFileSystem.File.SetCreationTimeUtc(path, SourceTimestamp.AddDays(1));
        var expected = FileSystemHash.Hash32([
            sourceFileSystem.FileInfo.New(path),
            sourceFileSystem.FileInfo.New(Path.Combine("Game", PakfilesDirectory, BootSplashPakFileName)),
            sourceFileSystem.FileInfo.New(Path.Combine("Game", PakfilesDirectory, PhysicsPersistentPakFileName))
        ]);

        var installer = CreateGeneratedBootfilesInstaller(new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(SourceTimestamp), 1, false, [], [relative], [])
        });

        installer.PackageVersionHash.Should().Be(expected);
    }

    [Fact]
    public void ConstructionReportsMissingBackup()
    {
        SeedSources();
        var relative = Path.Combine(PakfilesDirectory, BootFlowPakFileName);
        var state = new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(SourceTimestamp), 1, false, [], [relative], [])
        };

        var create = () => CreateGeneratedBootfilesInstaller(state);

        create.Should().Throw<FileNotFoundException>().Which.FileName.Should().Be(BackupPath(BootFlowPakFileName));
    }

    [Fact]
    public void GenerationReselectsRestoredSource()
    {
        SeedSources();
        var relative = Path.Combine(PakfilesDirectory, BootFlowPakFileName);
        var path = Path.Combine("Game", relative);
        sourceFileSystem.File.Move(path, BackupPath(BootFlowPakFileName));
        var state = new Dictionary<string, PackageInstallationState>
        {
            ["Bootfiles"] = new(new DateTimeOffset(SourceTimestamp), 1, false, [], [relative], [])
        };
        var installer = CreateGeneratedBootfilesInstaller(state);
        var backupStrategy = new MoveFileBackupStrategy(sourceFileSystem, sourceBackupFileNaming.Object, null);
        backupStrategy.RestoreBackup(new PathRoot(sourceFileSystem.DirectoryInfo.New("Game")).Combine(relative));
        state.Remove("Bootfiles");

        var install = () => installer.Install(new PathRoot(sourceFileSystem.DirectoryInfo.New("Destination")).Combine,
            Mock.Of<IBackupStrategy>(), new ProcessingCallbacks<RootedPath>());

        // The archive reader uses the real filesystem. Reaching the live path proves reselection,
        // even though the mock file cannot be opened by that reader.
        install.Should().Throw<IOException>().WithMessage($"*'{sourceFileSystem.Path.GetFullPath(path)}'*");
    }

    #region Utility Methods

    private IPackage DependentMod(int? versionHash = null, IReadOnlyCollection<string>? files = null) =>
        TestPackage(ModRequiringBootfiles, versionHash, [ModFile, .. files ?? []]);

    private IPackage TestPackage(string name, int? versionHash = null,
        IReadOnlyCollection<string>? files = null, IReadOnlyCollection<string>? dependencies = null) =>
        Package(name, versionHash, files ?? [Path.Combine("Files", name)], dependencies ?? []);

    private void SeedModConfiguration()
    {
        var path = DestinationPath(@"Configs\ModRequiringBootfiles_1\vehiclelist.lst").Full;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
        DestinationDirectory.FileSystem.File.WriteAllText(path, ModFile);
    }

    private void SeedSources()
    {
        sourceFileSystem.Directory.CreateDirectory("Backups");
        foreach (var name in SourceFiles)
        {
            var path = sourceFileSystem.Path.GetFullPath(Path.Combine("Game", PakfilesDirectory, name));
            sourceBackupFileNaming.Setup(n => n.ToBackup(path)).Returns(BackupPath(name));
            sourceFileSystem.AddFile(path, new MockFileData(name)
            {
                LastWriteTime = SourceTimestamp,
                CreationTime = SourceTimestamp
            });
        }
    }

    private IPackageInstaller CreateGeneratedBootfilesInstaller(
        IReadOnlyDictionary<string, PackageInstallationState> state) =>
        new SourceTestContext(this).CreateInstaller(state);

    private string BackupPath(string name) => sourceFileSystem.Path.GetFullPath(Path.Combine("Backups", SourceBackupNames[name]));

    private sealed class SourceTestContext(ModReconciliationContextTest test)
        : ModReconciliationContext<ModReconciliationContext.IEventHandler, ModInstallConfig>(
            new Dictionary<string, PackageInstallationState>(),
            Mock.Of<IGame>(g => g.InstallationDirectory == test.sourceFileSystem.DirectoryInfo.New("Game")),
            Mock.Of<ITempDir>(t => t.BaseDirectory == test.sourceFileSystem.Directory.CreateDirectory("Temp")),
            Mock.Of<IBootfilesNaming>(n => n.GeneratedBootfilesName == "Generated"),
            new ModInstallConfig(), test.sourceBackupFileNaming.Object, new CreationTimeFileUpdateTracker())
    {
        public IPackageInstaller CreateInstaller(IReadOnlyDictionary<string, PackageInstallationState> state) =>
            CreateGeneratedBootfilesInstaller(state);
    }

    #endregion
}
