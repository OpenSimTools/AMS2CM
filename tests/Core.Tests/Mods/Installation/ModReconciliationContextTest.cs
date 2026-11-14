using System.IO.Abstractions.TestingHelpers;
using Core.API;
using Core.Games;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.State;
using Core.State.JsonFile;
using Core.Tests.Packages.Installation;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation;

[IntegrationTest]
public class ModReconciliationContextTest :
    ReconciliationIntegrationTestBase<ModReconciliationContext.IEventHandler>
{
    #region Setup

    private readonly Mock<Action> configure = new();
    private readonly Mock<Action> configureMod = new();
    private readonly Mock<Action<IReadOnlyDictionary<string, PackageInstallationState>>> creatingBootfiles = new();

    private const string GeneratedBootfilesName = "__generated";
    private const string BootfilesPackageName = "__package";
    private const string ModRequiringBootfiles = "ModRequiringBootfiles";

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
            Mock.Of<IGame>(), Mock.Of<ITempDir>(), bootfilesNaming, new ModInstallConfig(),
            Mock.Of<MoveFileBackupStrategy.IBackupFileNaming>())
    {
        protected override IPackageInstaller CreateModInstaller(IPackageInstaller packageInstaller,
            string bootfilesPackageName) =>
            new Dictionary<string, IPackageInstaller>
            {
                [ModRequiringBootfiles] = new PostProcessingInstaller(
                    new InstallerWithBootfilesDependency(packageInstaller, bootfilesPackageName), test.configureMod.Object)
            }.GetValueOrDefault(packageInstaller.PackageName, packageInstaller);
        protected override IPackageInstaller CreateBootfilesInstaller(IPackage? bootfilesPackage,
            IReadOnlyDictionary<string, PackageInstallationState> state,
            ModReconciliationContext.IEventHandler eventHandler)
        {
            test.creatingBootfiles.Object(state);
            return new PostProcessingInstaller(bootfilesPackage?.CreateInstaller() ??
                test.Installer(GeneratedBootfilesName, 1), test.configure.Object);
        }
    }

    private class InstallerWithBootfilesDependency(IPackageInstaller inner, string bootfilesName)
        : IPackageInstaller
    {
        private readonly HashSet<string> dependencies = [.. inner.PackageDependencies];
        public string PackageName => inner.PackageName;
        public int? PackageVersionHash => inner.PackageVersionHash;

        public IReadOnlySet<string> PackageDependencies => dependencies;

        public IReadOnlySet<RootedPath> InstalledFiles => inner.InstalledFiles;
        public IInstallation.State Installed => inner.Installed;
        public DateTimeOffset InstallTime => inner.InstallTime;

        public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
            ProcessingCallbacks<RootedPath> callbacks)
        {
            inner.Install(destination, backupStrategy, callbacks);
            dependencies.Add(bootfilesName);
        }

        public IEnumerable<string> RelativeDirectoryPaths => inner.RelativeDirectoryPaths;
    }

    private sealed class PostProcessingInstaller(IPackageInstaller inner, Action configure) : IPackageInstaller
    {
        public string PackageName => inner.PackageName;
        public int? PackageVersionHash => inner.PackageVersionHash;
        public IReadOnlySet<string> PackageDependencies => inner.PackageDependencies;
        public IReadOnlySet<RootedPath> InstalledFiles => inner.InstalledFiles;
        public IInstallation.State Installed { get; private set; } = IInstallation.State.NotInstalled;
        public DateTimeOffset InstallTime => inner.InstallTime;
        public IEnumerable<string> RelativeDirectoryPaths => inner.RelativeDirectoryPaths;
        public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
            ProcessingCallbacks<RootedPath> callbacks)
        {
            Installed = IInstallation.State.PartiallyInstalled;
            inner.Install(destination, backupStrategy, callbacks);
            configure();
            Installed = IInstallation.State.Installed;
        }
    }
    #endregion

    [Fact]
    public void PlanningDoesNotCreateBootfilesInstaller()
    {
        var bootfiles = Package(BootfilesPackageName);

        Reconcile([Package(ModRequiringBootfiles), bootfiles], actionCount: 0);

        creatingBootfiles.Verify(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()), Times.Never);
        Mock.Get(bootfiles).Verify(p => p.CreateInstaller(), Times.Never);
        EventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void BootfilesConstructionObservesUpdatedOwnership()
    {
        Reconcile([Package(ModRequiringBootfiles, 1, ["Shared"]), Package(BootfilesPackageName, 1)]);
        creatingBootfiles.Invocations.Clear();
        creatingBootfiles.Setup(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()))
            .Callback<IReadOnlyDictionary<string, PackageInstallationState>>(state =>
            {
                state["Earlier"].Files.Should().Contain("Shared");
                state[ModRequiringBootfiles].Files.Should().NotContain("Shared");
                state[ModRequiringBootfiles].ShadowedBy.Should().Contain("Earlier");
            });

        Reconcile([Package("Earlier", 1, ["Shared"]),
            Package(ModRequiringBootfiles, 2, ["Shared", "Other"]), Package(BootfilesPackageName, 1)]);

        creatingBootfiles.Verify(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()), Times.Once);
    }

    [Fact]
    public void RemovingLastDependentDoesNotConstructBootfiles()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        creatingBootfiles.Invocations.Clear();
        var bootfiles = Package(BootfilesPackageName, 1);

        Reconcile([bootfiles]);

        creatingBootfiles.Verify(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()), Times.Never);
        Mock.Get(bootfiles).Verify(p => p.CreateInstaller(), Times.Never);
        InstalledPackages.Should().BeEmpty();
    }

    [Fact]
    public void BootfilesConstructionFailureDoesNotUninstallExistingBootfiles()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var previous = InstallationState[BootfilesPackageName];
        creatingBootfiles.Setup(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()))
            .Throws(new FileNotFoundException("Missing source"));

        var reconcile = () => Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);

        reconcile.Should().Throw<FileNotFoundException>();
        InstallationState[BootfilesPackageName].Should().Be(previous with { Partial = true });
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Never);
    }

    [Fact]
    public void Reconcile_InstallsGeneratedBootfilesIfModRequiresThem()
    {
        Reconcile([
            Package(ModRequiringBootfiles)
        ]);

        InstalledPackages.Should().BeEquivalentTo([ModRequiringBootfiles, GeneratedBootfilesName]);
    }

    [Fact]
    public void Reconcile_InstallsBootfilesPackageIfModRequiresThem()
    {
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.InstallingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.InstallingPackage(BootfilesPackageName));

        Reconcile([
            Package(ModRequiringBootfiles),
            Package(BootfilesPackageName)
        ], events.Object);

        InstalledPackages.Should().BeEquivalentTo(ModRequiringBootfiles, BootfilesPackageName);
        events.VerifyAll();
    }

    [Fact]
    public void Reconcile_SkipsUnnecessaryBootfilesPackage()
    {
        var packages = new List<string>();
        EventHandlerMock.Setup(m => m.InstallingPackage(It.IsAny<string>())).Callback<string>(packages.Add);

        Reconcile([
            Package("I1"),
            Package("I2"),
            Package("I3"),
            Package(BootfilesPackageName)
        ]);

        packages.Should().NotContain(BootfilesPackageName);
        creatingBootfiles.Verify(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()), Times.Never);
    }

    [Fact]
    public void Reconcile_SkipsUnnecessaryGeneratedBootfiles()
    {
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);

        Reconcile([], events.Object);

        events.VerifyNoOtherCalls();
        creatingBootfiles.Verify(c => c(It.IsAny<IReadOnlyDictionary<string, PackageInstallationState>>()), Times.Never);
    }

    [Fact]
    public void Reconcile_AlignsBootfilesDependenciesWithCurrent()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            [ModRequiringBootfiles] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: FilesNotUsed, ShadowedBy: []),
            ["Untouched"] = new(Time: ValueNotUsed, VersionHash: 2, Partial: false,
                Dependencies: [BootfilesPackageName], Files: ["F2"], ShadowedBy: []),
            [BootfilesPackageName] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [], Files: FilesNotUsed, ShadowedBy: [])
        };
        var previousState = InstallationState;

        Reconcile([
            Package(ModRequiringBootfiles, versionHash: 3, files: ["F3"]),
            Package("Untouched", versionHash: 2, files: FilesNotUsed)
        ]);

        EventHandlerMock.Verify(m => m.UninstallingPackage(ModRequiringBootfiles));
        EventHandlerMock.Verify(m => m.InstallingPackage(ModRequiringBootfiles));
        EventHandlerMock.Verify(m => m.SkippingPackage("Untouched"));
        EventHandlerMock.Verify(m => m.InstallingPackage(GeneratedBootfilesName));

        InstallationState.Keys.Should().BeEquivalentTo(ModRequiringBootfiles, "Untouched", GeneratedBootfilesName);
        InstallationState[ModRequiringBootfiles].Dependencies.Should().BeEquivalentTo(GeneratedBootfilesName);
        InstallationState["Untouched"].Dependencies.Should().BeEquivalentTo(GeneratedBootfilesName);

        previousState["Untouched"].Dependencies.Should().BeEquivalentTo(BootfilesPackageName);
    }

    [Fact]
    public void Reconcile_PlanningPreservesUnprocessedBootfilesDependencies()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["Untouched"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: FilesNotUsed, ShadowedBy: [])
        };
        var previousState = InstallationState;

        Reconcile([
            Package("Untouched", versionHash: 1)
        ], actionCount: 0);

        InstallationState["Untouched"].Dependencies.Should().Equal(BootfilesPackageName);
        previousState["Untouched"].Dependencies.Should().Equal(BootfilesPackageName);
    }

    [Fact]
    public void BootfilesAreReinstalledAfterDependentModChanges()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        EventHandlerMock.Invocations.Clear();

        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.UninstallingPackage(BootfilesPackageName), Times.Once);
        configure.Verify(c => c(), Times.Exactly(2));
    }

    [Fact]
    public void BootfilesReplacementCompletesConfiguration()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.SkippingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(BootfilesPackageName));
        events.InSequence(sequence).Setup(e => e.InstallingPackage(BootfilesPackageName));
        var strictConfigure = new Mock<Action>(MockBehavior.Strict);
        strictConfigure.InSequence(sequence).Setup(c => c());
        configure.Setup(c => c()).Callback(strictConfigure.Object);

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 2)], events.Object);

        strictConfigure.Verify(c => c(), Times.Once);
        events.VerifyAll();
        InstallationState[BootfilesPackageName].VersionHash.Should().Be(2);
        InstallationState[BootfilesPackageName].Partial.Should().BeFalse();
    }

    [Fact]
    public void UnchangedBootfilesAreSkipped()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.SkippingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.SkippingPackage(BootfilesPackageName));

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)], events.Object);

        events.VerifyAll();
    }

    [Fact]
    public void SwitchingToPackagedBootfilesRemovesGeneratedBootfiles()
    {
        Reconcile([Package(ModRequiringBootfiles, 1)]);
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(GeneratedBootfilesName));
        events.InSequence(sequence).Setup(e => e.SkippingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.InstallingPackage(BootfilesPackageName));

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)], events.Object);

        events.VerifyAll();
    }

    [Fact]
    public void AddingDependentInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package("OtherDependent", 1, ["Other"], [BootfilesPackageName]),
            Package(BootfilesPackageName, 1)]);

        Reconcile([Package("OtherDependent", 1, ["Other"], [BootfilesPackageName]),
            Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)], actionCount: 2);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RemovingDependentInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package(ModRequiringBootfiles, 1),
            Package("OtherDependent", 1, ["Other"], [BootfilesPackageName]), Package(BootfilesPackageName, 1)]);

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RepairingDependentInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        InstallationState = new Dictionary<string, PackageInstallationState>(InstallationState)
        {
            [ModRequiringBootfiles] = InstallationState[ModRequiringBootfiles] with { Partial = true }
        };

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void UnrelatedChangesDoNotReinstallOrConfigureBootfiles(int versionHash)
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package("Unrelated", 1), Package(BootfilesPackageName, 1)]);
        var original = InstallationState[BootfilesPackageName];
        configure.Invocations.Clear();
        EventHandlerMock.Invocations.Clear();

        Reconcile([Package(ModRequiringBootfiles, 1), Package("Unrelated", versionHash),
            Package(BootfilesPackageName, 1)]);

        EventHandlerMock.Verify(e => e.SkippingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Never);
        configure.Verify(c => c(), Times.Never);
        InstallationState[BootfilesPackageName].Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ReplacingShadowingModInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package("Shadowing", 1, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])]);

        Reconcile([Package(ModRequiringBootfiles, 1), Package("Shadowing", 2, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])], actionCount: 2);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RemovingShadowingModInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package("Shadowing", 1, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])]);

        Reconcile([Package(ModRequiringBootfiles, 1),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void RemovingLastDependentRemovesBootfiles()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        configure.Invocations.Clear();
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(BootfilesPackageName));

        Reconcile([Package(BootfilesPackageName, 1)], events.Object);

        InstallationState.Should().BeEmpty();
        configure.Verify(c => c(), Times.Never);
        events.VerifyAll();
    }

    [Fact]
    public void EmptySelectionRemovesObsoleteBootfiles()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(BootfilesPackageName));

        Reconcile([], events.Object);

        events.VerifyAll();
    }

    [Fact]
    public void BootfilesUninstallFailurePropagates()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        BackupStrategyMock.Setup(b => b.RestoreBackup(DestinationPath(BootfilesPackageName)))
            .Throws(new IOException("Restore failed"));
        var sequence = new MockSequence();
        var events = new Mock<ModReconciliationContext.IEventHandler>(MockBehavior.Strict);
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(ModRequiringBootfiles));
        events.InSequence(sequence).Setup(e => e.UninstallingPackage(BootfilesPackageName));

        var reconcile = () => Reconcile([], events.Object);

        reconcile.Should().Throw<IOException>();
        events.VerifyAll();
    }

    [Fact]
    public void ConfigurationFailureRetainsPartialBootfilesState()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        configure.Setup(c => c()).Throws(new IOException("Configuration failed"));

        var reconcile = () => Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);

        reconcile.Should().Throw<IOException>();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
        InstallationState[BootfilesPackageName].Files.Should().Contain(BootfilesPackageName);
    }

    [Fact]
    public void UnknownBootfilesVersionIsReinstalledOnce()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var state = new Dictionary<string, PackageInstallationState>(InstallationState)
        {
            [BootfilesPackageName] = InstallationState[BootfilesPackageName] with { VersionHash = null }
        };
        InstallationState = state;
        EventHandlerMock.Invocations.Clear();

        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        EventHandlerMock.Verify(e => e.SkippingPackage(BootfilesPackageName), Times.Once);
    }

    [Fact]
    public void ChangedShadowingReinstallsBootfiles()
    {
        Reconcile([
            Package(ModRequiringBootfiles, 1, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ]);
        EventHandlerMock.Invocations.Clear();

        Reconcile([
            Package(ModRequiringBootfiles, 2, ["Other"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ]);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        InstallationState[BootfilesPackageName].Files.Should().Contain("Shared");
    }

    [Fact]
    public void NewClaimPreventsBootfilesUninstallFromRestoringPath()
    {
        Reconcile([
            Package(ModRequiringBootfiles, 1, ["Other"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ]);
        BackupStrategyMock.Invocations.Clear();

        Reconcile([
            Package(ModRequiringBootfiles, 2, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ]);

        BackupStrategyMock.Verify(b => b.RestoreBackup(DestinationPath("Shared")), Times.Never);
        InstallationState[BootfilesPackageName].Files.Should().NotContain("Shared");
        InstallationState[BootfilesPackageName].ShadowedBy.Should().Contain(ModRequiringBootfiles);
    }

    [Fact]
    public void FirstActionRemovesStaleBootfilesOwnership()
    {
        Reconcile([
            Package(ModRequiringBootfiles, 1, ["Other"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ]);

        Reconcile([
            Package(ModRequiringBootfiles, 2, ["Shared"]),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])
        ], actionCount: 1);

        InstallationState[BootfilesPackageName].Files.Should().NotContain("Shared");
        InstallationState[BootfilesPackageName].ShadowedBy.Should().Contain(ModRequiringBootfiles);
    }

    [Fact]
    public void InterruptedDependentUpdateRebuildsBootfilesAfterReload()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var previous = InstallationState[BootfilesPackageName];

        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)], actionCount: 1);

        InstallationState[BootfilesPackageName].Should().Be(previous with { Partial = true });
        VerifyBootfilesRecovery([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);
        EventHandlerMock.Verify(e => e.SkippingPackage(ModRequiringBootfiles), Times.Once);
    }

    [Fact]
    public void InterruptedDependentUpdateRebuildsGeneratedBootfilesAfterReload()
    {
        Reconcile([Package(ModRequiringBootfiles, 1)]);

        Reconcile([Package(ModRequiringBootfiles, 2)], actionCount: 1);
        ReloadState();
        EventHandlerMock.Invocations.Clear();
        Reconcile([Package(ModRequiringBootfiles, 2)]);

        EventHandlerMock.Verify(e => e.SkippingPackage(ModRequiringBootfiles), Times.Once);
        EventHandlerMock.Verify(e => e.InstallingPackage(GeneratedBootfilesName), Times.Once);
        InstallationState[GeneratedBootfilesName].Partial.Should().BeFalse();
    }

    [Fact]
    public void RecoveredBootfilesAreSkippedOnNextRun()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)], actionCount: 1);
        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);
        EventHandlerMock.Invocations.Clear();
        configure.Invocations.Clear();

        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)]);

        EventHandlerMock.Verify(e => e.SkippingPackage(BootfilesPackageName), Times.Once);
        configure.Verify(c => c(), Times.Never);
    }

    [Fact]
    public void PlanningDependentUpdatePreservesBootfilesState()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        var previous = InstallationState[BootfilesPackageName];

        Reconcile([Package(ModRequiringBootfiles, 2), Package(BootfilesPackageName, 1)], actionCount: 0);

        InstallationState[BootfilesPackageName].Should().Be(previous);
    }

    [Fact]
    public void FailedDependentRemovalRetainsBootfilesInvalidation()
    {
        Reconcile([Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);
        BackupStrategyMock.Setup(b => b.RestoreBackup(DestinationPath(ModRequiringBootfiles)))
            .Throws(new IOException("Restore failed"));

        var reconcile = () => Reconcile([Package(BootfilesPackageName, 1)]);

        reconcile.Should().Throw<IOException>();
        ReloadState();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void FailedNewDependentInstallationRetainsBootfilesInvalidation()
    {
        Reconcile([Package("OtherDependent", 1, ["Other"], [BootfilesPackageName]),
            Package(BootfilesPackageName, 1)]);
        configureMod.Setup(c => c()).Throws(new IOException("Configuration failed"));

        var reconcile = () => Reconcile([Package("OtherDependent", 1, ["Other"], [BootfilesPackageName]),
            Package(ModRequiringBootfiles, 1), Package(BootfilesPackageName, 1)]);

        reconcile.Should().Throw<IOException>();
        ReloadState();
        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    [Fact]
    public void NewOwnerInvalidatesBootfilesBeforePostProcessing()
    {
        Reconcile([Package(ModRequiringBootfiles, 1),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])]);

        Reconcile([Package("NewOwner", 1, ["Shared"]), Package(ModRequiringBootfiles, 1),
            Package(BootfilesPackageName, 1, ["Shared", "BootOnly"])], actionCount: 1);

        InstallationState[BootfilesPackageName].Partial.Should().BeTrue();
    }

    #region Utility Methods

    private void VerifyBootfilesRecovery(IPackage[] packages)
    {
        ReloadState();
        EventHandlerMock.Invocations.Clear();

        Reconcile(packages);

        EventHandlerMock.Verify(e => e.InstallingPackage(BootfilesPackageName), Times.Once);
        InstallationState[BootfilesPackageName].Partial.Should().BeFalse();
    }

    private void ReloadState()
    {
        var fs = new MockFileSystem();
        var persistence = new JsonFileStatePersistence(fs.Directory.CreateTempSubdirectory());
        persistence.WriteState(new SavedState(InstallationState));
        InstallationState = persistence.ReadState().Installation;
    }

    #endregion
}
