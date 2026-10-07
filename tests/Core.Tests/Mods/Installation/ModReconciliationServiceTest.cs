using Core.Mods;
using Core.Mods.Installation;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation;

[IntegrationTest]
public class ModReconciliationServiceTest :
    ReconciliationServiceTestBase<PackageReconciliationService.IEventHandler>,
    IModInstallerFactory<PackageReconciliationService.IEventHandler>
{
    #region Setup

    private static readonly string GeneratedBootfilesName = "__generated";
    private static readonly string BootfilesPackageName = "__package";
    private static readonly string ModRequiringBootfiles = "ModRequiringBootfiles";

    protected override IReconciliationService<PackageReconciliationService.IEventHandler> NewService(
        IBackupStrategyProvider<DateTimeOffset, PackageReconciliationService.IEventHandler> backupStrategyProvider)
    {
        var bootfilesNamingMock = new Mock<IBootfilesNaming>();
        bootfilesNamingMock.Setup(m => m.IsBootfiles(BootfilesPackageName)).Returns(true);
        bootfilesNamingMock.Setup(m => m.IsGeneratedBootfiles(GeneratedBootfilesName)).Returns(true);
        return new ModReconciliationService<PackageReconciliationService.IEventHandler>(
            backupStrategyProvider, bootfilesNamingMock.Object, this);
    }

    public IPackageInstaller ModInstaller(IPackageInstaller packageInstaller, IPackageInstaller bootfilesInstaller) =>
        packageInstaller.PackageName == ModRequiringBootfiles
            ? new InstallerWithBootfilesDependency(packageInstaller, bootfilesInstaller)
            : packageInstaller;

    public IPackageInstaller BootfilesInstaller(IPackageInstaller? bootfilesPackageInstaller, PackageReconciliationService.IEventHandler eventHandler) =>
        bootfilesPackageInstaller ?? InstallerOf(GeneratedBootfilesName);

    private class InstallerWithBootfilesDependency(IPackageInstaller inner, IPackageInstaller bootfiles) : IPackageInstaller
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
    public void Apply_InstallsGeneratedBootfilesIfModRequiresThem()
    {
        Apply([
            InstallerOf(ModRequiringBootfiles)
        ]);

        InstalledPackages.Should().BeEquivalentTo([ModRequiringBootfiles, GeneratedBootfilesName]);
    }

    [Fact]
    public void Apply_InstallsBootfilesPackageIfModRequiresThem()
    {
        Apply([
            InstallerOf(ModRequiringBootfiles),
            InstallerOf(BootfilesPackageName)
        ]);

        InstalledPackages.Should().BeEquivalentTo([ModRequiringBootfiles, BootfilesPackageName]);
    }

    [Fact]
    public void Apply_AlwaysCallsBootfilesPackageInstaller()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallingPackage(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            InstallerOf("I1"),           // 25%
            InstallerOf("I2"),           // 50%
            InstallerOf("I3"),           // 75%
            InstallerOf(BootfilesPackageName) // 100%
        ]);

        packages.Should().Contain(BootfilesPackageName);
        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Apply_AlwaysCallsGeneratedBootfilesInstaller()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallingPackage(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            // Generated bootfiles 100%
        ]);

        packages.Should().Equal(GeneratedBootfilesName);
        progress.Should().Equal(1.0);
    }

    [Fact]
    public void Apply_AlignsBootfilesDepsWithCurrent()
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

        Apply([
            InstallerOf(ModRequiringBootfiles, versionHash: 3, files: ["F3"]),
            InstallerOf("Untouched", versionHash: 2, files: FilesNotUsed)
        ]);

        EventHandlerMock.Verify(m => m.UninstallingPackage(ModRequiringBootfiles));
        EventHandlerMock.Verify(m => m.InstallingPackage(ModRequiringBootfiles));
        EventHandlerMock.Verify(m => m.SkippingPackage("Untouched"));
        EventHandlerMock.Verify(m => m.InstallingPackage(GeneratedBootfilesName));

        InstallationState.Keys.Should().BeEquivalentTo(ModRequiringBootfiles, "Untouched", GeneratedBootfilesName);
        InstallationState[ModRequiringBootfiles].Dependencies.Should().BeEquivalentTo([GeneratedBootfilesName]);
        InstallationState["Untouched"].Dependencies.Should().BeEquivalentTo([GeneratedBootfilesName]);
    }
}
