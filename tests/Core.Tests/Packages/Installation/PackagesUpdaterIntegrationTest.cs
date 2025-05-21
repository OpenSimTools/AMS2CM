using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

public class PackagesUpdaterIntegrationTest : AbstractFilesystemTest
{
    #region Initialisation

    private class TestException : Exception {}

    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<PackagesUpdater.IEventHandler> eventHandlerMock = new();
    private readonly DateTime fakeUtcInstallationDate = DateTime.Today.AddDays(10).ToUniversalTime();
    private readonly TimeSpan fakeLocalTimeOffset = TimeSpan.FromHours(3);
    private IReadOnlyDictionary<string, PackageInstallationState>? recordedState;

    private class InstallerForPackage : IInstallerFactory
    {
        private readonly IReadOnlyCollection<IInstaller> installers;

        internal InstallerForPackage(IReadOnlyCollection<IInstaller> installers)
        {
            this.installers = installers;
        }

        public IInstaller PackageInstaller(Package package) =>
            installers.First(installer => installer.PackageName == package.Name);
    }

    #endregion

    [Fact]
    public void Apply_NoMods()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>(),
            []
        );

        recordedState.Should().BeEmpty();
    }

    [Fact]
    public void Apply_UninstallsMods()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>{
                ["A"] = new(
                        Time: null,
                        FsHash: 42,
                        Partial: false,
                        Dependencies: [],
                        Files: ["AF"])
            },
            []
        );

        recordedState.Should().BeEmpty();

        backupStrategyMock.Verify(_ => _.RestoreBackup(TestPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(_ => _.UninstallStart());
        eventHandlerMock.Verify(_ => _.UninstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.UninstallSkipModified("AF"));
        eventHandlerMock.Verify(_ => _.UninstallEnd());
        eventHandlerMock.Verify(_ => _.InstallNoPackages());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UninstallStopsIfBackupFails()
    {
        backupStrategyMock.Setup(_ => _.RestoreBackup(TestPath("Fail"))).Throws<TestException>();

        this.Invoking(_ => _.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(
                        Time: null,
                        FsHash: 42,
                        Partial: false,
                        Dependencies: [],
                        Files: ["AF1", "Fail", "AF2"])
            },
            []
        )).Should().Throw<TestException>();

        recordedState["A"]?.Files.Should().BeEquivalentTo(["Fail", "AF2"]);
    }

    [Fact]
    public void Apply_InstallsMods()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", 42, [
                    "AF"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(fakeUtcInstallationDate, 42, false, [], ["AF"])
        });

        backupStrategyMock.Verify(_ => _.PerformBackup(TestPath("AF")));
        backupStrategyMock.Verify(_ => _.AfterInstall(TestPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(_ => _.UninstallNoPackages());
        eventHandlerMock.Verify(_ => _.InstallStart());
        eventHandlerMock.Verify(_ => _.InstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.InstallEnd());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_InstallStopsIfBackupFails()
    {
        backupStrategyMock.Setup(_ => _.PerformBackup(TestPath("Fail"))).Throws<TestException>();

        this.Invoking(_ => _.Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", 42, [
                    "AF1", "Fail", "AF2"
                ])
            ]
        )).Should().Throw<TestException>();

        recordedState["A"]?.Files.Should().BeEquivalentTo([
            "AF1",
            "Fail" // We don't know where it failed, so we add it
        ]);
    }

    [Fact]
    public void Apply_UpdatesMods()
    {
        Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 1, Partial: false, Dependencies: [], Files: [
                    "AF",
                    "AF1",
                ])
            },
            [
                InstallerOf("A", 2, [
                    "AF",
                    "AF2"
                ])
            ]
        );

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(fakeUtcInstallationDate, 2, false, [], ["AF", "AF2"])
        });
    }

    #region Utility methods

    private void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> oldState,
        IReadOnlyCollection<IInstaller> installers)
    {
        var packages = installers.Select(i => new Package(i.PackageName, "", true, null));
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<PackageInstallationState>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<PackageInstallationState>()))
            .Returns(backupStrategyMock.Object);
        var packagesUpdater = new PackagesUpdater<PackagesUpdater.IEventHandler>(
            new InstallerForPackage(installers),
            backupStrategyProviderMock.Object,
            new FakeTimeProvider(fakeUtcInstallationDate.WithOffset(fakeLocalTimeOffset)));
        packagesUpdater.Apply(
            oldState,
            packages,
            TestDir.FullName,
            newState => recordedState = newState,
            eventHandlerMock.Object,
            CancellationToken.None);
    }

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files)
    {
        return new StaticFilesInstaller(name, fsHash, files);
    }

    private class StaticFilesInstaller : BaseInstaller<object>
    {
        private static readonly object NoContext = new();
        private readonly IReadOnlyCollection<string> files;

        internal StaticFilesInstaller(string packageName, int? packageFsHash, IReadOnlyCollection<string> files) :
            base(packageName, packageFsHash)
        {
            this.files = files;
        }

        protected override void InstalAllFiles(InstallBody body)
        {
            foreach (var file in files)
            {
                body(file, NoContext);
            }
        }

        protected override void InstallFile(RootedPath destinationPath, object context)
        {
            // Do not install any file for real
        }

        // Install everything from the root directory

        private static readonly string DirAtRoot = "X";

        public override IEnumerable<string> RelativeDirectoryPaths => [DirAtRoot];
    }

    #endregion
}
