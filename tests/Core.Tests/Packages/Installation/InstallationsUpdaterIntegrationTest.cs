using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.State;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Tests.Packages.Installation;

public class InstallationsUpdaterIntegrationTest : AbstractFilesystemTest
{
    #region Initialisation

    private class TestException : Exception {}

    private readonly Mock<IBackupStrategy> backupStrategyMock;
    private readonly Mock<InstallationsUpdater.IEventHandler> eventHandlerMock = new();
    private readonly DateTime fakeUtcInstallationDate = DateTime.Today.AddDays(10).ToUniversalTime();
    private readonly TimeSpan fakeLocalTimeOffset = TimeSpan.FromHours(3);
    private readonly InstallationsUpdater installationsUpdater;
    private readonly Dictionary<string, PackageInstallationState?> recordedState = new();

    public InstallationsUpdaterIntegrationTest()
    {
        backupStrategyMock = new Mock<IBackupStrategy>();
        var backupStrategyProviderMock = new Mock<IBackupStrategyProvider<PackageInstallationState>>();
        backupStrategyProviderMock.Setup(m => m.BackupStrategy(It.IsAny<PackageInstallationState>()))
            .Returns(backupStrategyMock.Object);
        installationsUpdater = new InstallationsUpdater(
            backupStrategyProviderMock.Object,
            new FakeTimeProvider(fakeUtcInstallationDate.WithOffset(fakeLocalTimeOffset)));
    }

    #endregion

    [Fact]
    public void Apply_NoMods()
    {
        installationsUpdater.Apply(
            new Dictionary<string, PackageInstallationState>(),
            [],
            "",
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEmpty();
    }

    [Fact]
    public void Apply_UninstallsMods()
    {
        installationsUpdater.Apply(
            new Dictionary<string, PackageInstallationState>{
                ["A"] = new(
                        Time: null,
                        FsHash: 42,
                        Partial: false,
                        Files: ["AF"])
            },
            [],
            TestDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState?>{
            ["A"] = null,
        });

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

        installationsUpdater.Invoking(_ => _.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(
                        Time: null,
                        FsHash: 42,
                        Partial: false,
                        Files: ["AF1", "Fail", "AF2"])
            },
            [],
            TestDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None)).Should().Throw<TestException>();

        recordedState["A"]?.Files.Should().BeEquivalentTo(["Fail", "AF2"]);
    }

    [Fact]
    public void Apply_InstallsMods()
    {
        installationsUpdater.Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", 42, [
                    "AF"
                ])
            ],
            TestDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(fakeUtcInstallationDate, 42, false, ["AF"])
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

        installationsUpdater.Invoking(_ => _.Apply(
            new Dictionary<string, PackageInstallationState>(),
            [
                InstallerOf("A", 42, [
                    "AF1", "Fail", "AF2"
                ])
            ],
            TestDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None)).Should().Throw<TestException>();

        recordedState["A"]?.Files.Should().BeEquivalentTo([
            "AF1",
            "Fail" // We don't know where it failed, so we add it
        ]);
    }

    [Fact]
    public void Apply_UpdatesMods()
    {
        var endState = new Dictionary<string, IInstallation>();
        installationsUpdater.Apply(
            new Dictionary<string, PackageInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 1, Partial: false, Files: [
                    "AF",
                    "AF1",
                ])
            },
            [
                InstallerOf("A", 2, [
                    "AF",
                    "AF2"
                ])
            ],
            TestDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(fakeUtcInstallationDate, 2, false, ["AF", "AF2"])
        });
    }

    #region Utility methods

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

    private void RecordState(string packageName, PackageInstallationState? state)
    {
        recordedState[packageName] = state;
    }

    #endregion
}
