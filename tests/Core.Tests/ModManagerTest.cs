namespace Core.Tests;

using Core.Games;
using Core.IO;
using Core.Mods;
using Core.State;
using Moq;
using SevenZip;
using System;
using System.Collections.Immutable;

public class ModManagerTest : IDisposable
{
    #region Initialisation

    private const string DirAtRoot = "DirAtRoot";

    private readonly DirectoryInfo testDir;
    private readonly DirectoryInfo gameDir;
    private readonly DirectoryInfo modsDir;

    private readonly Mock<IGame> gameMock = new();
    private readonly Mock<IModRepository> modRepositoryMock = new();
    private readonly Mock<ISafeFileDelete> safeFileDeleteMock = new();

    private readonly AssertState persistedState;
    private readonly ModFactory modFactory;

    private readonly ModManager modManager;

    public ModManagerTest()
    {
        testDir = Directory.CreateTempSubdirectory(GetType().Name);
        gameDir = testDir.CreateSubdirectory("Game");
        modsDir = testDir.CreateSubdirectory("Packages");

        var tempDir = new SubdirectoryTempDir(testDir.FullName);

        persistedState = new AssertState();
        modFactory = new ModFactory(
            new ModInstallConfig { DirsAtRoot = [DirAtRoot] },
            gameMock.Object);

        modManager = new ModManager(
            gameMock.Object,
            modRepositoryMock.Object,
            modFactory,
            persistedState,
            safeFileDeleteMock.Object,
            tempDir);

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
    public void Uninstall_FailsIfBootfilesInstalledByAnotherToolAndNothingToUninstall()
    {
        persistedState.InitState(InternalState.Empty());

        var exception = Assert.Throws<Exception>(() => modManager.UninstallAllMods());

        Assert.Contains("another tool", exception.Message);
        persistedState.AssertNotWritten();
    }

    [Fact]
    public void Uninstall_DeletesCreatedFilesAndDirectories()
    {
        persistedState.InitState(new InternalState
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
        var installationDateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        persistedState.InitState(new InternalState
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
        persistedState.InitState(new InternalState(
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
    public void Uninstall_RestoresBackups()
    {
        persistedState.InitState(new InternalState(
            Install: new(
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    [""] = new(FsHash: null, Partial: false, Files: ["ModFile"])
                }
            )));

        CreateGameFile("ModFile", "Mod");
        CreateGameFile(BackupName("ModFile"), "Orig");

        modManager.UninstallAllMods();

        Assert.Equal("Orig", File.ReadAllText(GamePath("ModFile")));
        Assert.False(File.Exists(GamePath(BackupName("ModFile"))));
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
        persistedState.InitState(InternalState.Empty());

        var exception = Assert.Throws<Exception>(() => modManager.InstallEnabledMods());
        Assert.Contains("another tool", exception.Message);
        persistedState.AssertNotWritten();
    }

    [Fact]
    public void Install_InstallsContentFromRootDirectories()
    {
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [$@"Foo\{DirAtRoot}\A", $@"Bar\{DirAtRoot}\B", @"Bar\C", @"Baz\D"])
        ]);

        modManager.InstallEnabledMods();

        Assert.True(File.Exists(GamePath($@"{DirAtRoot}\A")));
        Assert.True(File.Exists(GamePath($@"{DirAtRoot}\B")));
        Assert.True(File.Exists(GamePath(@"C")));
        Assert.False(File.Exists(GamePath(@"D")));
        Assert.False(File.Exists(GamePath(@"Baz\D")));
        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: DateTime.Now,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["Package100"] = new(FsHash: 100, Partial: false, Files: [$@"{DirAtRoot}\A", $@"{DirAtRoot}\B", @"C"]),
                }
            )));
    }

    [Fact]
    public void Install_DeletesFilesWithSuffix()
    {
        var modFile = $@"{DirAtRoot}\A";

        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [DeletionName(modFile)]),
        ]);
        CreateGameFile(modFile, "Orig");

        modManager.InstallEnabledMods();

        Assert.False(File.Exists(GamePath(modFile)));
        Assert.Equal("Orig", File.ReadAllText(GamePath(BackupName(modFile))));
    }

    [Fact]
    public void Install_GivesPriotiryToFilesLaterInTheModList()
    {
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [$@"{DirAtRoot}\A"]),
            CreateModArchive(200, [$@"Foo\{DirAtRoot}\A"])
        ]);

        modManager.InstallEnabledMods();

        Assert.Equal("200", File.ReadAllText(GamePath($@"{DirAtRoot}\A")));
        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: DateTime.Now,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["Package100"] = new(FsHash: 100, Partial: false, Files: []),
                    ["Package200"] = new(FsHash: 200, Partial: false, Files: [$@"{DirAtRoot}\A"]),
                }
            )));
    }

    [Fact]
    public void Install_StopsAfterAnyError()
    {
        // TODO
    }

    [Fact]
    public void Install_PreventsFileCreationTimeInTheFuture()
    {
        // TODO
    }

    [Fact]
    public void Install_PerformsBackups()
    {
        var modFile = $@"{DirAtRoot}\A";
        var toBeDeleted = "B";

        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [modFile, DeletionName(toBeDeleted)]),
        ]);
        CreateGameFile(modFile, "OrigA");
        CreateGameFile(toBeDeleted, "OrigB");

        modManager.InstallEnabledMods();

        Assert.Equal("OrigA", File.ReadAllText(GamePath(BackupName(modFile))));
        Assert.Equal("OrigB", File.ReadAllText(GamePath(BackupName(toBeDeleted))));
    }

    [Fact]
    public void Install_ConfiguresBootfilesIfRequired()
    {
        // TODO
        // This includes new mod type not required, skins not required, cars and tracks required
        // We should be able to create bootfiles without game libraries
    }

    [Fact]
    public void Install_UsesCustomBootfilesIfPresentAndRequired()
    {
        // TODO
    }

    [Fact]
    public void Install_RejectsMultipleCustomBootfiles()
    {
        // TODO
    }

    #region Utility methods

    private ModPackage CreateModArchive(int fsHash, IEnumerable<string> relativePaths)
    {
        var modName = $"Mod{fsHash}";
        var modContentsDir = testDir.CreateSubdirectory(modName).FullName;
        foreach (var relativePath in relativePaths)
        {
            CreateFile(Path.Combine(modContentsDir, relativePath), $"{fsHash}");
        }
        var archivePath = $@"{modsDir.FullName}\{modName}.7z";
        new SevenZipCompressor().CompressDirectory(modContentsDir, archivePath);
        return new ModPackage(modName, $"Package{fsHash}", archivePath, true, fsHash);
    }

    private IDisposable CreateReadOnlyGameFile(string relativePath)
    {
        var fileInfo = CreateGameFile(relativePath);
        fileInfo.Attributes |= FileAttributes.ReadOnly;
        return Cleanup(() => fileInfo.Attributes &= ~FileAttributes.ReadOnly);
    }

    private FileInfo CreateGameFile(string relativePath, string content = "") =>
        CreateFile(GamePath(relativePath), content);

    private FileInfo CreateFile(string fullPath, string content = "")
    {
        var parentDirFullPath = Path.GetDirectoryName(fullPath);
        if (parentDirFullPath is not null)
        {
            Directory.CreateDirectory(parentDirFullPath);
        }
        File.WriteAllText(fullPath, content);
        return new FileInfo(fullPath);
    }

    // This can be removed once we introduce backup strategies
    private string BackupName(string relativePath) =>
        $"{relativePath}.orig";

    // This can be removed once we hide it inside mod logic
    private string DeletionName(string relativePath) =>
        $"{relativePath}{JsgmeFileInstaller.RemoveFileSuffix}";

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
        private readonly static TimeSpan TimeTolerance = TimeSpan.FromMilliseconds(100);
        // Avoids bootfiles checks on uninstall
        private readonly static InternalState SkipBootfilesCheck = new InternalState(
            Install: new (
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["INIT"] = new (FsHash: null, Partial: false, Files: []),
                }
            ));

        private InternalState initState = SkipBootfilesCheck;
        private InternalState? savedState;

        public void InitState(InternalState state) => initState = state;

        public InternalState ReadState() => savedState ?? initState;

        public void WriteState(InternalState state) => savedState = state;

        internal void AssertEqual(InternalState expected)
        {
            Assert.NotNull(savedState);
            // Not a great solution, but .NET doesn't natively provide support for mocking the clock
            Assert.InRange(
                savedState.Install.Time?.ToUniversalTime().Ticks ?? 0L,
                expected.Install.Time?.ToUniversalTime().Subtract(TimeTolerance).Ticks ?? 0L,
                expected.Install.Time?.ToUniversalTime().Add(TimeTolerance).Ticks ?? 0L);
            Assert.Equal(expected.Install.Mods.Keys.ToImmutableHashSet(), savedState.Install.Mods.Keys.ToImmutableHashSet());
            foreach (var e in expected.Install.Mods)
            {
                var currentModState = savedState.Install.Mods[e.Key];
                var expectedModState = e.Value;
                Assert.Equal(expectedModState.FsHash, currentModState.FsHash);
                Assert.Equal(expectedModState.Partial, currentModState.Partial);
                Assert.Equal(expectedModState.Files.ToImmutableHashSet(), currentModState.Files.ToImmutableHashSet());
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