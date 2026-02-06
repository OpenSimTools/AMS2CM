using Core.Mods.Installation;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation;

public class ModPackagesUpdaterTest : PackagesUpdaterTestBase<PackagesUpdater.IEventHandler>
{
    #region Initialisation

    // Randomness ensures that at least some test runs will fail if it's used
    private static readonly DateTime ValueNotUsed = Random.Shared.Next() > 0 ? DateTime.MaxValue : DateTime.MinValue;

    private static readonly string GeneratedBootfilesName = "__generated";
    private static readonly string BootfilesPackageName = "__package";

    private class WrappedInstaller(IInstaller inner) : IInstaller
    {
        public string PackageName => $"({inner.PackageName})";
        public int? PackageFsHash => inner.PackageFsHash;
        public IReadOnlyCollection<string> PackageDependencies => inner.PackageDependencies;
        public IReadOnlyCollection<RootedPath> InstalledFiles => inner.InstalledFiles;
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
            bootfilesPackageInstaller ?? InstallerOf(GeneratedBootfilesName, fsHash: null, []);

        public bool IsBootFiles(string packageName) =>
            packageName == BootfilesPackageName || packageName == GeneratedBootfilesName;
    }

    protected override IPackagesUpdater<PackagesUpdater.IEventHandler> NewPackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState, PackagesUpdater.IEventHandler> backupStrategyProvider,
        TimeProvider timeProvider) {
        return new ModPackagesUpdater<PackagesUpdater.IEventHandler>(
            installerFactory, backupStrategyProvider, timeProvider, new TestModInstallerFactory());
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
            // Uninstall                                            25%
            InstallerOf("I1", fsHash: null, []),                 // 50%
            InstallerOf("I2", fsHash: null, []),                 // 75%
            InstallerOf(BootfilesPackageName, fsHash: null, []), // 100%
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
}
