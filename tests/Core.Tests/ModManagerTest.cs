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
    private readonly static TimeSpan TimeTolerance = TimeSpan.FromMilliseconds(100);

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
                    ["A"] = new (
                        FsHash: null, Partial: false, Files: [
                            Path.Combine("X", "ModAFile"),
                            Path.Combine("Y","ModAFile")
                        ]),
                    ["B"] = new(
                        FsHash: null, Partial: false, Files: [
                            Path.Combine("X", "ModBFile")
                        ])
                }
            )
        ));
        CreateGameFile(Path.Combine("Y", "ExistingFile"));

        modManager.UninstallAllMods();

        Assert.False(Directory.Exists(GamePath("X")));
        Assert.False(File.Exists(GamePath(Path.Combine("Y", "ModAFile"))));
        Assert.True(File.Exists(GamePath(Path.Combine("Y", "ExistingFile"))));
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
                    [""] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModFile",
                            "RecreatedFile",
                            "AlreadyDeletedFile"
                        ])
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
                    ["A"] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModAFile"
                        ]),
                    ["B"] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModBFile1",
                            "ModBFile2"
                        ]),
                    ["C"] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModCFile"
                        ])
                }
            )));

        CreateGameFile("ModAFile");
        CreateGameFile("ModBFile1");
        using var _ = CreateGameFile("ModBFile2").OpenRead(); // Prevent deletion
        CreateGameFile("ModCFile");

        Assert.Throws<IOException>(() => modManager.UninstallAllMods());

        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: null,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["B"] = new(
                        FsHash: null, Partial: true, Files: [
                            "ModBFile2"
                        ]),
                    ["C"] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModCFile"
                        ])
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
                    [""] = new(
                        FsHash: null, Partial: false, Files: [
                            "ModFile"
                        ])
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
            CreateModArchive(100, [
                Path.Combine("Foo", DirAtRoot, "A"),
                Path.Combine("Bar", DirAtRoot, "B"),
                Path.Combine("Bar", "C"),
                Path.Combine("Baz", "D")
            ])
        ]);

        modManager.InstallEnabledMods();

        Assert.True(File.Exists(GamePath(Path.Combine(DirAtRoot, "A"))));
        Assert.True(File.Exists(GamePath(Path.Combine(DirAtRoot, "B"))));
        Assert.True(File.Exists(GamePath("C")));
        Assert.False(File.Exists(GamePath("D")));
        Assert.False(File.Exists(GamePath(Path.Combine("Baz", "D"))));
        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: DateTime.Now,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["Package100"] = new(
                        FsHash: 100, Partial: false, Files: [
                            Path.Combine(DirAtRoot, "A"),
                            Path.Combine(DirAtRoot, "B"),
                            "C"
                        ]),
                }
            )));
    }

    [Fact]
    public void Install_DeletesFilesWithSuffix()
    {
        var modFile = Path.Combine(DirAtRoot, "A");

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
            CreateModArchive(100, [
                Path.Combine(DirAtRoot, "A")
            ]),
            CreateModArchive(200, [
                Path.Combine("Foo", DirAtRoot, "A")
            ])
        ]);

        modManager.InstallEnabledMods();

        Assert.Equal("200", File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "A"))));
        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: DateTime.Now,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["Package100"] = new(FsHash: 100, Partial: false, Files: []),
                    ["Package200"] = new(FsHash: 200, Partial: false, Files: [
                        Path.Combine(DirAtRoot, "A")
                    ]),
                }
            )));
    }

    [Fact]
    public void Install_StopsAfterAnyError()
    {
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [
                Path.Combine(DirAtRoot, "A")
            ]),
            CreateModArchive(200, [
                Path.Combine(DirAtRoot, "B1"),
                Path.Combine(DirAtRoot, "B2"),
                Path.Combine(DirAtRoot, "B3")
            ]),
            CreateModArchive(300, [
                Path.Combine(DirAtRoot, "C"),
            ]),
        ]);
        using var _ = CreateGameFile(Path.Combine(DirAtRoot, "B2")).OpenRead();  // Prevent overwrite

        Assert.Throws<IOException>(() => modManager.InstallEnabledMods());

        Assert.Equal("300", File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "C"))));
        Assert.Equal("200", File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "B1"))));
        Assert.False(File.Exists(GamePath(Path.Combine(DirAtRoot, "B3"))));
        Assert.False(File.Exists(GamePath(Path.Combine(DirAtRoot, "A"))));
        persistedState.AssertEqual(new InternalState(
            Install: new InternalInstallationState(
                Time: DateTime.Now,
                Mods: new Dictionary<string, InternalModInstallationState>
                {
                    ["Package200"] = new(
                        FsHash: 200, Partial: true, Files: [
                            Path.Combine(DirAtRoot, "B1")
                        ]),
                    ["Package300"] = new(
                        FsHash: 300, Partial: false, Files: [
                            Path.Combine(DirAtRoot, "C")
                        ]),
                }
            )));
    }

    [Fact]
    public void Install_PreventsFileCreationTimeInTheFuture()
    {
        var future = DateTime.Now.AddDays(1);
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [$@"{DirAtRoot}\A"],
                extractedDir => File.SetCreationTime($@"{extractedDir}\{DirAtRoot}\A", future))
        ]);

        modManager.InstallEnabledMods();

        AssertAboutNow(File.GetCreationTime(GamePath($@"{DirAtRoot}\A")));
    }

    [Fact]
    public void Install_PerformsBackups()
    {
        var modFile = Path.Combine(DirAtRoot, "A");
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
    public void Install_UsesBootfilesIfRequired()
    {
        // TODO
        // This includes new mod type not required, skins not required, cars and tracks required
        // We should be able to create bootfiles without game libraries
    }

    [Fact]
    public void Install_UsesCustomBootfilesIfPresentAndRequired()
    {
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [Path.Combine(DirAtRoot, "Foo.crd")]),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods();

        persistedState.AssertInstalled(["Package100", "__bootfiles900"]);
    }

    [Fact]
    public void Install_RejectsMultipleCustomBootfiles()
    {
        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([
            CreateModArchive(100, [Path.Combine(DirAtRoot, "Foo.crd")]),
            CreateCustomBootfiles(900),
            CreateCustomBootfiles(901)
        ]);

        var exception = Assert.Throws<Exception>(() => modManager.InstallEnabledMods());

        Assert.Contains("many bootfiles", exception.Message);
        persistedState.AssertInstalled(["Package100"]);
    }

    #region Utility methods

    private ModPackage CreateModArchive(int fsHash, IEnumerable<string> relativePaths) =>
        CreateModArchive(fsHash, relativePaths, _ => { });

    private ModPackage CreateModArchive(int fsHash, IEnumerable<string> relativePaths, Action<string> callback) =>
        CreateModPackage("Package", fsHash, relativePaths, _ => { });

    private ModPackage CreateCustomBootfiles(int fsHash) =>
        CreateModPackage(ModManager.BootfilesPrefix, fsHash, [
                Path.Combine(DirAtRoot, "OrTheyWontBeInstalled"),
                PostProcessor.VehicleListRelativePath,
                PostProcessor.TrackListRelativePath,
                PostProcessor.DrivelineRelativePath,
            ], _ => { });

    private ModPackage CreateModPackage(string packagePrefix, int fsHash, IEnumerable<string> relativePaths, Action<string> callback)
    {
        var modName = $"Mod{fsHash}";
        var modContentsDir = testDir.CreateSubdirectory(modName).FullName;
        foreach (var relativePath in relativePaths.DefaultIfEmpty("SevenZipRequiresAFile"))
        {
            CreateFile(Path.Combine(modContentsDir, relativePath), $"{fsHash}");
        }
        callback(modContentsDir);
        var archivePath = $@"{modsDir.FullName}\{modName}.7z";
        new SevenZipCompressor().CompressDirectory(modContentsDir, archivePath);
        return new ModPackage(modName, $"{packagePrefix}{fsHash}", archivePath, true, fsHash);
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

    private static void AssertAboutNow(DateTime actual) =>
        AssertEqualWithinToleration(DateTime.Now, actual);

    private static void AssertEqualWithinToleration(DateTime? expected, DateTime? actual) =>
        Assert.InRange(actual?.ToUniversalTime().Ticks ?? 0L,
            expected?.ToUniversalTime().Subtract(TimeTolerance).Ticks ?? 0L,
            expected?.ToUniversalTime().Add(TimeTolerance).Ticks ?? 0L);

    private class AssertState : IStatePersistence
    {
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
            AssertEqualWithinToleration(expected.Install.Time, savedState.Install.Time);
            AssertInstalled(expected.Install.Mods.Keys);
            foreach (var e in expected.Install.Mods)
            {
                var currentModState = savedState.Install.Mods[e.Key];
                var expectedModState = e.Value;
                Assert.Equal(expectedModState.FsHash, currentModState.FsHash);
                Assert.Equal(expectedModState.Partial, currentModState.Partial);
                Assert.Equal(expectedModState.Files.ToImmutableHashSet(), currentModState.Files.ToImmutableHashSet());
            };
        }

        internal void AssertInstalled(IEnumerable<string> expected)
        {
            Assert.Equal(expected.ToImmutableHashSet(), savedState?.Install.Mods.Keys.ToImmutableHashSet());
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