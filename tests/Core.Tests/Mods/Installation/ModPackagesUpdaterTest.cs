using System.Collections.ObjectModel;
using Core.Mods;
using Core.Mods.Installation;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation;

[IntegrationTest]
public class ModPackagesUpdaterTest : PackagesUpdaterTestBase<PackagesUpdater.IEventHandler>
{
    #region Initialisation

    private static readonly string GeneratedBootfilesName = "__generated";
    private static readonly string BootfilesPackageName = "__package";

    private class WrappedInstaller(IInstaller inner) : IInstaller
    {
        public string PackageName => $"({inner.PackageName})";
        public int? PackageFsHash => inner.PackageFsHash;
        public IReadOnlySet<string> PackageDependencies => inner.PackageDependencies;
        public IReadOnlySet<RootedPath> InstalledFiles => inner.InstalledFiles;
        public IInstallation.State Installed => inner.Installed;
        public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
            ProcessingCallbacks<RootedPath> callbacks) => inner.Install(destination, backupStrategy, callbacks);
        public IEnumerable<string> RelativeDirectoryPaths => inner.RelativeDirectoryPaths;
    }

    private class TestModInstallerFactory : IModInstallerFactory<PackagesUpdater.IEventHandler>
    {
        public IInstaller ModInstaller(IInstaller packageInstaller, IInstaller bootfilesInstaller) =>
            new WrappedInstaller(packageInstaller);

        public IInstaller BootfilesInstaller(IInstaller? bootfilesPackageInstaller, PackagesUpdater.IEventHandler eventHandler) =>
            bootfilesPackageInstaller ?? InstallerOf(GeneratedBootfilesName);
    }

    protected override IPackagesUpdater<PackagesUpdater.IEventHandler> NewPackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState, PackagesUpdater.IEventHandler> backupStrategyProvider,
        TimeProvider timeProvider)
    {
        var bootfilesNamingMock = new Mock<IBootfilesNaming>();
        bootfilesNamingMock.Setup(m => m.IsBootfiles(BootfilesPackageName)).Returns(true);
        return new ModPackagesUpdater<PackagesUpdater.IEventHandler>(
            installerFactory, backupStrategyProvider, timeProvider, bootfilesNamingMock.Object, new TestModInstallerFactory());
    }

    #endregion

    [Fact]
    public void Apply_AlwaysInstallsBootfilesPackage()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallCurrent(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            // Uninstall                          25%
            InstallerOf("I1"),                 // 50%
            InstallerOf("I2"),                 // 75%
            InstallerOf(BootfilesPackageName), // 100%
        ]);

        InstallationState.Should().BeEmpty();

        packages.Should().Equal("(I1)", "(I2)", BootfilesPackageName);
        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Apply_AlwaysInstallsGeneratedBootfiles()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.InstallCurrent(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            // Uninstall           50%
            // Generated bootfiles 100%
        ]);

        InstallationState.Should().BeEmpty();

        packages.Should().Equal(GeneratedBootfilesName);
        progress.Should().Equal(0.5, 1.0);
    }

    internal static IInstaller InstallerOf(string name) =>
        new StaticFilesInstaller(name, null, ReadOnlyDictionary<string, string>.Empty, []);
}
