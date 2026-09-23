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
public class ModPackagesUpdaterTest :
    PackagesUpdaterTestBase<PackagesUpdater.IEventHandler>,
    IModInstallerFactory<PackagesUpdater.IEventHandler>
{
    #region Setup

    private static readonly string GeneratedBootfilesName = "__generated";
    private static readonly string BootfilesPackageName = "__package";

    private class WrappedInstaller(IPackageInstaller inner) : IPackageInstaller
    {
        public string PackageName => $"({inner.PackageName})";
        public int? PackageVersionHash => inner.PackageVersionHash;
        public IReadOnlySet<string> PackageDependencies => inner.PackageDependencies;
        public IReadOnlySet<RootedPath> InstalledFiles => inner.InstalledFiles;
        public IInstallation.State Installed => inner.Installed;
        public DateTimeOffset InstallTime => inner.InstallTime;

        public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy,
            ProcessingCallbacks<RootedPath> callbacks) => inner.Install(destination, backupStrategy, callbacks);
        public IEnumerable<string> RelativeDirectoryPaths => inner.RelativeDirectoryPaths;
    }

    protected override IPackagesUpdater<PackagesUpdater.IEventHandler> NewPackagesUpdater(
        IBackupStrategyProvider<DateTimeOffset, PackagesUpdater.IEventHandler> backupStrategyProvider)
    {
        var bootfilesNamingMock = new Mock<IBootfilesNaming>();
        bootfilesNamingMock.Setup(m => m.IsBootfiles(BootfilesPackageName)).Returns(true);
        return new ModPackagesUpdater<PackagesUpdater.IEventHandler>(
            backupStrategyProvider, TestTimeProvider, bootfilesNamingMock.Object, this);
    }

    public IPackageInstaller ModInstaller(IPackageInstaller packageInstaller, IPackageInstaller bootfilesInstaller) =>
        new WrappedInstaller(packageInstaller);

    public IPackageInstaller BootfilesInstaller(IPackageInstaller? bootfilesPackageInstaller, PackagesUpdater.IEventHandler eventHandler) =>
        bootfilesPackageInstaller ?? InstallerOf(GeneratedBootfilesName);

    #endregion

    [Fact]
    public void Apply_AlwaysInstallsBootfilesPackage()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.UpdateCurrent(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            InstallerOf("I1"),            // 25%
            InstallerOf("I2"),            // 50%
            InstallerOf("I3"),            // 75%
            InstallerOf(BootfilesPackageName), // 100%
        ]);

        InstallationState.Should().BeEmpty();

        packages.Should().Equal("(I1)", "(I2)", "(I3)", BootfilesPackageName);
        progress.Should().Equal(0.25, 0.5, 0.75, 1.0);
    }

    [Fact]
    public void Apply_AlwaysInstallsGeneratedBootfiles()
    {
        var packages = new List<string>();
        var progress = new List<double>();
        EventHandlerMock.Setup(m => m.UpdateCurrent(It.IsAny<string>())).Callback<string>(packages.Add);
        EventHandlerMock.Setup(m => m.ProgressUpdate(It.IsAny<IPercent>()))
            .Callback<IPercent>(p => progress.Add(p.Percent));

        Apply([
            // Generated bootfiles 100%
        ]);

        InstallationState.Should().BeEmpty();

        packages.Should().Equal(GeneratedBootfilesName);
        progress.Should().Equal(1.0);
    }

    #region Utility Methods

    private IPackageInstaller InstallerOf(string name) =>
        new StaticFilesInstaller(TestFileSystem, TestTimeProvider, name, null, ReadOnlyDictionary<string, string>.Empty, []);

    #endregion
}
