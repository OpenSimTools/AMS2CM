using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Packages.Installation;

public class InstallationsUpdaterIntegrationTest : AbstractFilesystemTest
{
    #region Initialisation

    private class TestException : Exception {}

    private record InstallationResult(
        int? PackageFsHash,
        HashSet<string> InstalledFiles,
        IInstallation.State Installed
    )
    {
        internal InstallationResult(IInstallation installation) : this(
            installation.PackageFsHash,
            installation.InstalledFiles.ToHashSet(),
            installation.Installed) { }
    }

    private readonly Mock<IBackupStrategy> backupStrategyMock;
    private readonly InstallationsUpdater installationsUpdater;

    private readonly Mock<InstallationsUpdater.IEventHandler> eventHandlerMock = new();

    private readonly Dictionary<string, InstallationResult> recordedState = new();

    public InstallationsUpdaterIntegrationTest()
    {
        backupStrategyMock = new Mock<IBackupStrategy>();
        installationsUpdater = new InstallationsUpdater(backupStrategyMock.Object);
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
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, InstallationResult>{
            ["A"] = new(42, [], IInstallation.State.NotInstalled),
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
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None)).Should().Throw<TestException>();

        recordedState["A"].InstalledFiles.Should().BeEquivalentTo(["Fail", "AF2"]);
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
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, InstallationResult>
        {
            ["A"] = new(42, ["AF"], IInstallation.State.Installed)
        });

        backupStrategyMock.Verify(_ => _.PerformBackup(TestPath("AF")));
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
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None)).Should().Throw<TestException>();

        recordedState["A"].InstalledFiles.Should().BeEquivalentTo(["AF1"]);
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
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        recordedState.Should().BeEquivalentTo(new Dictionary<string, InstallationResult>
        {
            ["A"] = new(2, ["AF", "AF2"], IInstallation.State.Installed)
        });
    }

    #region Utility methods

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files)
    {
        return new StaticFilesInstaller(name, fsHash, new SubdirectoryTempDir(testDir.FullName), files);
    }

    private class StaticFilesInstaller : BaseInstaller<object>
    {
        private static readonly object NoContext = new();
        private readonly IReadOnlyCollection<string> files;

        internal StaticFilesInstaller(string packageName, int? packageFsHash, ITempDir tempDir, IReadOnlyCollection<string> files) :
            base(packageName, packageFsHash, tempDir, Config())
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

        private static BaseInstaller.IConfig Config()
        {
            var mock = new Mock<BaseInstaller.IConfig>();
            mock.Setup(_ => _.DirsAtRoot).Returns([DirAtRoot]);
            return mock.Object;
        }

        protected override IEnumerable<string> RelativeDirectoryPaths => [DirAtRoot];
    }

    private void RecordState(IInstallation state)
    {
        recordedState[state.PackageName] = new InstallationResult(state);
    }

    #endregion
}
