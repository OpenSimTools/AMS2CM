using Core.API;
using Core.Games;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation;

[IntegrationTest]
public class ModReconciliationContextTest :
    ReconciliationIntegrationTestBase<IEventHandler>
{
    #region Setup

    private const string GeneratedBootfilesName = "__generated";
    private const string BootfilesPackageName = "__package";
    private const string ModRequiringBootfiles = "ModRequiringBootfiles";

    protected override IReconciliationContext<IEventHandler>.FactoryFunc ContextFactory
    {
        get
        {
            var bootfilesNamingMock = new Mock<IBootfilesNaming>();
            bootfilesNamingMock.Setup(m => m.IsBootfiles(BootfilesPackageName)).Returns(true);
            bootfilesNamingMock.Setup(m => m.IsGeneratedBootfiles(GeneratedBootfilesName)).Returns(true);
            return previousState => new TestModReconciliationContext(previousState, bootfilesNamingMock.Object, this);
        }
    }

    private sealed class TestModReconciliationContext(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IBootfilesNaming bootfilesNaming,
        ModReconciliationContextTest test)
        : ModReconciliationContext<IEventHandler, ModInstallConfig>(previousState,
            Mock.Of<IGame>(), Mock.Of<ITempDir>(), bootfilesNaming, new ModInstallConfig())
    {
        protected override IPackageInstaller CreateModInstaller(IPackageInstaller packageInstaller,
            IPackageInstaller bootfilesInstaller) =>
            packageInstaller.PackageName == ModRequiringBootfiles
                ? new InstallerWithBootfilesDependency(packageInstaller, bootfilesInstaller)
                : packageInstaller;

        // Note: the real bootfiles installer will skip installation when not needed, but this will not.
        protected override IPackageInstaller CreateBootfilesInstaller(IPackageInstaller? bootfilesPackageInstaller,
            IEventHandler eventHandler) =>
            bootfilesPackageInstaller ?? test.Installer(GeneratedBootfilesName);
    }

    private class InstallerWithBootfilesDependency(IPackageInstaller inner, IPackageInstaller bootfiles)
        : IPackageInstaller
    {
        public string PackageName => inner.PackageName;
        public int? PackageVersionHash => inner.PackageVersionHash;

        public IReadOnlySet<string> PackageDependencies =>
            inner.PackageDependencies.Append(bootfiles.PackageName).ToHashSet();

        public IReadOnlySet<RootedPath> InstalledFiles => inner.InstalledFiles;
        public IInstallation.State Installed => inner.Installed;
        public DateTimeOffset InstallTime => inner.InstallTime;

        public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
            ProcessingCallbacks<RootedPath> callbacks) => inner.Install(destination, backupStrategy, callbacks);

        public IEnumerable<string> RelativeDirectoryPaths => inner.RelativeDirectoryPaths;
    }

    #endregion

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
        Reconcile([
            Package(ModRequiringBootfiles),
            Package(BootfilesPackageName)
        ]);

        InstalledPackages.Should().BeEquivalentTo(ModRequiringBootfiles, BootfilesPackageName);
    }

    [Fact]
    public void Reconcile_AlwaysCallsBootfilesPackageInstaller()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallingPackage(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Reconcile([
            Package("I1"),                // 25%
            Package("I2"),                // 50%
            Package("I3"),                // 75%
            Package(BootfilesPackageName) // 100%
        ]);

        packages.Should().Contain(BootfilesPackageName);
        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Reconcile_AlwaysCallsGeneratedBootfilesInstaller()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallingPackage(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Reconcile([
            // Generated bootfiles 100%
        ]);

        packages.Should().Equal(GeneratedBootfilesName);
        progress.Should().Equal(1.0);
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
    public void Reconcile_CancellationPreservesUnprocessedBootfilesDependencies()
    {
        InstallationState = new Dictionary<string, PackageInstallationState>
        {
            ["Untouched"] = new(Time: ValueNotUsed, VersionHash: 1, Partial: false,
                Dependencies: [BootfilesPackageName], Files: FilesNotUsed, ShadowedBy: [])
        };
        var previousState = InstallationState;

        Reconcile([
            Package("Untouched", versionHash: 1)
        ], new CancellationToken(canceled: true));

        InstallationState["Untouched"].Dependencies.Should().Equal(BootfilesPackageName);
        previousState["Untouched"].Dependencies.Should().Equal(BootfilesPackageName);
    }
}
