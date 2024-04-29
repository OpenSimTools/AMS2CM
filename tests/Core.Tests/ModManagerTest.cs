namespace Core.Tests;

using Core.Games;
using Core.IO;
using Core.Mods;
using Core.State;
using Moq;
using System;
using System.Collections.Immutable;

public class ModManagerTest : IDisposable
{
    #region Initialisation

    private const string ModRootDir = "RootDir";

    private readonly DirectoryInfo testDir;
    private readonly DirectoryInfo gameDir;

    private readonly Mock<IGame> gameMock = new();
    private readonly Mock<IModRepository> modRepositoryMock = new();
    private readonly Mock<ISafeFileDelete> safeFileDeleteMock = new();
    private readonly Mock<ITempDir> tempDirMock = new();

    private readonly AssertState persistedState = new AssertState();

    private readonly ModManager modManager;

    public ModManagerTest()
    {
        testDir = Directory.CreateTempSubdirectory(GetType().Name);
        gameDir = testDir.CreateSubdirectory("GameDir");

        modManager = new ModManager(
            gameMock.Object,
            modRepositoryMock.Object,
            new ModFactory(
                new ModInstallConfig { DirsAtRoot = new[] { ModRootDir } },
                gameMock.Object),
            persistedState,
            safeFileDeleteMock.Object,
            tempDirMock.Object);

        gameMock.Setup(_ => _.InstallationDirectory).Returns(gameDir.FullName);
    }

    public void Dispose()
    {
        testDir.Delete(recursive: true);
    }

    #endregion

    [Fact]
    public void Uninstall_FailsIfGameRunning()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(true);

        var exception = Assert.Throws<Exception>(() =>
            modManager.UninstallAllMods()
        );

        Assert.Contains("running", exception.Message);
        persistedState.AssertNotWritten();
    }

    [Fact]
    public void Uninstall_FailsIfBootfilesInstalledByAnotherTool()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);

        var exception = Assert.Throws<Exception>(() => modManager.UninstallAllMods());

        Assert.Contains("another tool", exception.Message);
        persistedState.AssertNotWritten();
    }

    [Fact]
    public void Uninstall_DeletesCreatedFilesAndDirectories()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        persistedState.WriteState(new InternalState
        (
            Install: new (
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["A"] = new (FsHash: null, Partial: false, Files: [@"X\ModAFile", @"Y\ModAFile"]),
                    ["B"] = new(FsHash: null, Partial: false, Files: [@"X\ModBFile"])
                }
            )
        ));
        CreateGameFile(@"Y\ExistingFile");

        modManager.UninstallAllMods();

        Assert.False(Directory.Exists(GamePath(@"X")));
        Assert.False(File.Exists(GamePath(@"Y\ModAFile")));
        Assert.True(File.Exists(GamePath(@"Y\ExistingFile")));
        persistedState.AssertEmpty();
    }

    [Fact]
    public void Uninstall_SkipsFilesCreatedAfterInstallation()
    {
        var installationDateTime = DateTime.Now.AddDays(-1);
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        persistedState.WriteState(new InternalState
        (
            Install: new(
                Time: installationDateTime,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    [""] = new(FsHash: null, Partial: false, Files: ["ModFile", "RecreatedFile", "AlreadyDeletedFile"])
                }
            )
        ));
        CreateGameFile("ModFile").CreationTime = installationDateTime;
        CreateGameFile("RecreatedFile");

        modManager.UninstallAllMods();

        Assert.False(File.Exists(GamePath("ModFile")));
        Assert.True(File.Exists(GamePath("RecreatedFile")));
        persistedState.AssertEmpty();
    }

    [Fact]
    public void Uninstall_StopsAfterAnyError()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        persistedState.WriteState(new InternalState(
            Install: new(
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["A"] = new(FsHash: null, Partial: false, Files: ["ModAFile"]),
                    ["B"] = new(FsHash: null, Partial: false, Files: ["ModBFile1", "ModBFile2"]),
                    ["C"] = new(FsHash: null, Partial: false, Files: ["ModCFile"])
                }
            )));

        CreateGameFile("ModAFile");
        CreateGameFile("ModBFile1");
        using var _ = CreateReadOnlyGameFile("ModBFile2");
        CreateGameFile("ModCFile");

        Assert.Throws<UnauthorizedAccessException>(() => modManager.UninstallAllMods());

        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["B"] = new(FsHash: null, Partial: true, Files: ["ModBFile2"]),
                    ["C"] = new(FsHash: null, Partial: false, Files: ["ModCFile"])
                }
            )));
    }

    [Fact]
    public void Install_FailsIfGameRunning()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(true);

        var exception = Assert.Throws<Exception>(() =>
            modManager.InstallEnabledMods()
        );

        Assert.Contains("running", exception.Message);
        persistedState.AssertNotWritten();
    }

    [Fact]
    public void Install_FailsIfBootfilesInstalledByAnotherTool()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([]);

        var exception = Assert.Throws<Exception>(() => modManager.InstallEnabledMods());
        Assert.Contains("another tool", exception.Message);
        persistedState.AssertNotWritten();
    }

    #region Utility methods

    private IDisposable CreateReadOnlyGameFile(string relativePath)
    {
        var fileInfo = CreateGameFile(relativePath);
        fileInfo.Attributes |= FileAttributes.ReadOnly;
        return Cleanup(() => fileInfo.Attributes &= ~FileAttributes.ReadOnly);
    }

    private FileInfo CreateGameFile(string relativePath, string content = "")
    {
        var fullPath = GamePath(relativePath);
        var parentDirFullPath = Path.GetDirectoryName(fullPath);
        if (parentDirFullPath is not null)
        {
            Directory.CreateDirectory(parentDirFullPath);
        }
        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }

    private string GamePath(string relativePath) =>
        Path.GetFullPath(relativePath, gameDir.FullName);

    private IDisposable Cleanup(Action action) => new DisposableAction(action);

    // Similar to Rx.NET Disposable.Create
    private class DisposableAction : IDisposable
    {
        private readonly Action action;

        public DisposableAction(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            action.Invoke();
        }
    }

    private class AssertState : IStatePersistence
    {
        private InternalState? savedState;

        public InternalState ReadState() =>
            savedState ?? InternalState.Empty();

        public void WriteState(InternalState state) =>
            savedState = state;

        internal void AssertEqual(InternalState expected)
        {
            Assert.NotNull(savedState);
            Assert.Equal(expected.Install.Time, savedState.Install.Time);
            Assert.Equal(expected.Install.Mods.Keys, savedState.Install.Mods.Keys);
            foreach (var e in expected.Install.Mods)
            {
                var currentModState = savedState.Install.Mods[e.Key];
                var expectedModState = e.Value;
                Assert.Equal(expectedModState.FsHash, currentModState.FsHash);
                Assert.Equal(expectedModState.Partial, currentModState.Partial);
                Assert.Equal(expectedModState.Files.ToImmutableList(), currentModState.Files.ToImmutableList());
            };
        }

        internal void AssertEmpty()
        {
            AssertEqual(InternalState.Empty());
        }

        internal void AssertNotWritten()
        {
            Assert.Null(savedState);
        }
    }

    #endregion
}