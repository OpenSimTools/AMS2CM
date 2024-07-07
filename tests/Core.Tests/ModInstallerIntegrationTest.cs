using Core.Backup;
using Core.Mods;
using Core.State;
using Core.Utils;

namespace Core.Tests;

public class ModInstallerIntegrationTest : AbstractFilesystemTest
{
    #region Initialisation

    private record InstallationResult(
        string PackageName,
        int? PackageFsHash,
        HashSet<string> InstalledFiles,
        IInstallation.State Installed
    )
    {
        internal InstallationResult(IInstallation installation) : this(
            installation.PackageName,
            installation.PackageFsHash,
            installation.InstalledFiles.ToHashSet(),
            installation.Installed) { }
    }

    private readonly static string PathNotUsed = "NotUsed";
    private readonly static bool EnabledNotUsed = Random.Shared.NextDouble() < 0.5;

    private readonly Mock<IInstallationFactory> installationFactoryMock;
    private readonly Mock<IBackupStrategy> backupStrategyMock;
    private readonly Mock<ModInstaller.IConfig> config;
    private readonly ModInstaller modInstaller;

    private readonly Mock<ModInstaller.IEventHandler> eventHandlerMock;

    private readonly Dictionary<string, InstallationResult> recordedState;

    public ModInstallerIntegrationTest()
    {
        installationFactoryMock = new Mock<IInstallationFactory>();
        backupStrategyMock = new Mock<IBackupStrategy>();
        config = new Mock<ModInstaller.IConfig>();
        modInstaller = new ModInstaller(
            installationFactoryMock.Object,
            backupStrategyMock.Object,
            config.Object);
        eventHandlerMock = new Mock<ModInstaller.IEventHandler>();
        recordedState = new();
    }

    #endregion

    [Fact]
    public void Apply_NoMods()
    {
        modInstaller.Apply(
            new Dictionary<string, InternalModInstallationState>(),
            [],
            "",
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        AssertRecordedState([]);

        eventHandlerMock.Verify(_ => _.UninstallNoMods());
        eventHandlerMock.Verify(_ => _.InstallNoMods());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UninstallsMods()
    {
        // TODO Introduce interface to delete file as part of restoring backup
        CreateTestFile("AF");

        modInstaller.Apply(
            new Dictionary<string, InternalModInstallationState>{
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

        AssertRecordedState([
            new("A", 42, [], IInstallation.State.NotInstalled)
        ]);

        backupStrategyMock.Verify(_ => _.RestoreBackup(TestPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        // TODO see above
        Assert.False(File.Exists(TestPath("AF")));

        eventHandlerMock.Verify(_ => _.UninstallStart());
        eventHandlerMock.Verify(_ => _.UninstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.UninstallEnd());
        eventHandlerMock.Verify(_ => _.InstallNoMods());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_InstallsMods()
    {
        modInstaller.Apply(
            new Dictionary<string, InternalModInstallationState>(),
            [
                PackageInstalling("A", 42, [
                    "AF"
                ])
            ],
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        AssertRecordedState([
            new("A", 42, ["AF"], IInstallation.State.Installed)
        ]);

        backupStrategyMock.Verify(_ => _.IsBackupFile("AF"));
        backupStrategyMock.Verify(_ => _.PerformBackup(TestPath("AF")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(_ => _.UninstallNoMods());
        eventHandlerMock.Verify(_ => _.InstallStart());
        eventHandlerMock.Verify(_ => _.InstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.PostProcessingNotRequired());
        eventHandlerMock.Verify(_ => _.InstallEnd());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Apply_UpdatesMods()
    {
        var endState = new Dictionary<string, IInstallation>();
        modInstaller.Apply(
            new Dictionary<string, InternalModInstallationState>
            {
                ["A"] = new(Time: null, FsHash: 1, Partial: false, Files: [
                    "AF",
                    "AF1",
                ])
            },
            [
                PackageInstalling("A", 2, [
                    "AF",
                    "AF2"
                ])
            ],
            testDir.FullName,
            RecordState,
            eventHandlerMock.Object,
            CancellationToken.None);

        AssertRecordedState([
            new("A", 2, ["AF", "AF2"], IInstallation.State.Installed)
        ]);

        backupStrategyMock.Verify(_ => _.RestoreBackup(TestPath("AF")));
        backupStrategyMock.Verify(_ => _.RestoreBackup(TestPath("AF1")));
        backupStrategyMock.Verify(_ => _.IsBackupFile("AF"));
        backupStrategyMock.Verify(_ => _.PerformBackup(TestPath("AF")));
        backupStrategyMock.Verify(_ => _.IsBackupFile("AF2"));
        backupStrategyMock.Verify(_ => _.PerformBackup(TestPath("AF2")));
        backupStrategyMock.VerifyNoOtherCalls();

        eventHandlerMock.Verify(_ => _.UninstallStart());
        eventHandlerMock.Verify(_ => _.UninstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.UninstallEnd());
        eventHandlerMock.Verify(_ => _.InstallStart());
        eventHandlerMock.Verify(_ => _.InstallCurrent("A"));
        eventHandlerMock.Verify(_ => _.PostProcessingNotRequired());
        eventHandlerMock.Verify(_ => _.InstallEnd());
        eventHandlerMock.Verify(_ => _.ProgressUpdate(It.IsAny<IPercent>()));
        eventHandlerMock.VerifyNoOtherCalls();
    }

    #region Utility methods

    private ModPackage PackageInstalling(string name, int? fsHash, IReadOnlyCollection<string> files)
    {
        var unusedPath = $@"Some\Unused\Path\{name}";
        var unusedEnabled = Random.Shared.NextDouble() < 0.5;
        var package = new ModPackage(name, unusedPath, unusedEnabled, fsHash);
        var installer = new StaticFilesInstaller(name, fsHash, new SubdirectoryTempDir(testDir.FullName), files);
        installationFactoryMock.Setup(_ => _.ModInstaller(package)).Returns(installer);
        return package;
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

        public override void Dispose()
        {
        }

        // Install everything from the root directory

        private readonly static string DirAtRoot = "X";

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

    private void AssertRecordedState(InstallationResult[] expectedState)
    {
        // Xunit doesn't seem to compare correctly InstalledFiles if inside a record
        // Message is terrible as it doesn't set the context.
        // Maybe Fluent Assertions works better.
        var expectedDict = expectedState.ToDictionary(_ => _.PackageName);
        Assert.Equal(expectedDict.Keys, recordedState.Keys);
        foreach (var key in expectedDict.Keys)
        {
            var expected = expectedDict[key];
            var actual = recordedState[key];
            Assert.Equal(expected.PackageName, actual.PackageName);
            Assert.Equal(expected.PackageFsHash, actual.PackageFsHash);
            Assert.Equal(expected.InstalledFiles, actual.InstalledFiles);
            Assert.Equal(expected.Installed, actual.Installed);
        }
    }

    #endregion
}
