using System.IO.Compression;
using Core.API;
using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.State;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.API;

public class ModManagerIntegrationTest : AbstractFilesystemTest
{
    #region Initialisation

    private const string DirAtRoot = "DirAtRoot";
    private const string FileExcludedFromInstall = "Excluded";

    private static readonly string VehicleListRelativePath = Path.Combine(BootfilesInstaller.VehicleListRelativeDir, PostProcessor.VehicleListFileName);
    private static readonly string TrackListRelativePath = Path.Combine(BootfilesInstaller.TrackListRelativeDir, PostProcessor.TrackListFileName);
    private static readonly string DrivelineRelativePath = Path.Combine(BootfilesInstaller.DrivelineRelativeDir, PostProcessor.DrivelineFileName);

    // Randomness ensures that at least some test runs will fail if it's used
    private static readonly DateTime? ValueNotUsed = Random.Shared.Next() > 0 ? DateTime.MaxValue : DateTime.MinValue;

    private static readonly TimeSpan TimeTolerance = TimeSpan.FromMilliseconds(100);

    private readonly DirectoryInfo gameDir;
    private readonly DirectoryInfo modsDir;

    private readonly Mock<IGame> gameMock = new();
    private readonly Mock<IPackageRepository> modRepositoryMock = new();
    private readonly Mock<ISafeFileDelete> safeFileDeleteMock = new();
    private readonly Mock<IEventHandler> eventHandlerMock = new();

    private readonly InMemoryStatePersistence persistedState;

    private readonly ModManager modManager;

    public ModManagerIntegrationTest()
    {
        gameDir = testDir.CreateSubdirectory("Game");
        modsDir = testDir.CreateSubdirectory("Packages");

        var tempDir = new SubdirectoryTempDir(testDir.FullName);

        persistedState = new InMemoryStatePersistence();
        var modInstallConfig = new ModInstallConfig
        {
            DirsAtRoot = [DirAtRoot],
            ExcludedFromInstall = [$"**\\{FileExcludedFromInstall}"]
        };

        var modPackagesUpdater = Init.CreateModPackagesUpdater(modInstallConfig, gameMock.Object, tempDir);

        modManager = new ModManager(
            gameMock.Object,
            modRepositoryMock.Object,
            modPackagesUpdater,
            persistedState,
            safeFileDeleteMock.Object,
            tempDir);

        gameMock.Setup(_ => _.InstallationDirectory).Returns(gameDir.FullName);
    }

    #endregion

    [Fact]
    public void Uninstall_FailsIfGameRunning()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(true);

        modManager.Invoking(_ => _.UninstallAllMods(eventHandlerMock.Object))
            .Should().Throw<Exception>().WithMessage("*running*");

        persistedState.Should().HaveNotBeenWritten();
    }

    [Fact]
    public void Uninstall_DeletesCreatedFilesAndDirectories()
    {
        persistedState.InitState(new SavedState
        (
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    ["A"] = new(
                        Time: null, FsHash: null, Partial: false, Files: [
                            Path.Combine("X", "ModAFile"),
                            Path.Combine("Y", "ModAFile")
                        ]),
                    ["B"] = new(
                        Time: null, FsHash: null, Partial: false, Files: [
                            Path.Combine("X", "ModBFile")
                        ])
                }
            )
        ));
        CreateGameFile(Path.Combine("Y", "ExistingFile"));

        modManager.UninstallAllMods(eventHandlerMock.Object);

        Directory.Exists(GamePath("X")).Should().BeFalse();
        File.Exists(GamePath(Path.Combine("Y", "ModAFile"))).Should().BeFalse();
        File.Exists(GamePath(Path.Combine("Y", "ExistingFile"))).Should().BeTrue();
        persistedState.Should().BeEmpty();
    }

    [Fact]
    public void Uninstall_SkipsFilesCreatedAfterInstallation()
    {
        var installationDateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        persistedState.InitState(new SavedState
        (
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    [""] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModFile",
                            "RecreatedFile",
                            "AlreadyDeletedFile"
                        ])
                }
            )
        ));
        CreateGameFile("ModFile").CreationTime = installationDateTime;
        CreateGameFile("RecreatedFile");

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.Exists(GamePath("ModFile")).Should().BeFalse(); // FIXME
        File.Exists(GamePath("RecreatedFile")).Should().BeTrue();
        persistedState.Should().BeEmpty();
    }

    [Fact]
    public void Uninstall_StopsAfterAnyError()
    {
        // It must be after files are created
        var installationDateTime = DateTime.Now.AddMinutes(1);
        persistedState.InitState(new SavedState(
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    ["A"] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModAFile"
                        ]),
                    ["B"] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModBFile1",
                            "ModBFile2"
                        ]),
                    ["C"] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModCFile"
                        ])
                }
            )));

        CreateGameFile("ModAFile");
        CreateGameFile("ModBFile1");
        using var _ = CreateGameFile("ModBFile2").OpenRead(); // Prevent deletion
        CreateGameFile("ModCFile");

        modManager.Invoking(_ => _.UninstallAllMods(eventHandlerMock.Object))
            .Should().Throw<IOException>();

        persistedState.Should().Be(new SavedState(
            Install: new InstallationState(
                Time: installationDateTime.ToUniversalTime(),
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    ["B"] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: true, Files: [
                            "ModBFile2"
                        ]),
                    ["C"] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModCFile"
                        ])
                }
            )));
    }


    [Fact]
    public void Uninstall_RestoresBackups()
    {
        persistedState.InitState(new SavedState(
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    [""] = new(
                        Time: null, FsHash: null, Partial: false, Files: [
                            "ModFile"
                        ])
                }
            )));

        CreateGameFile("ModFile", "Mod");
        CreateGameFile(BackupName("ModFile"), "Orig");

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath("ModFile")).Should().Be("Orig");
        File.Exists(GamePath(BackupName("ModFile"))).Should().BeFalse();
    }

    [Fact]
    public void Uninstall_SkipsRestoreIfModFileOverwritten()
    {
        // It must be after files are created
        var installationDateTime = DateTime.Now.AddMinutes(1);
        persistedState.InitState(new SavedState(
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    [""] = new(
                        Time: installationDateTime.ToUniversalTime(), FsHash: null, Partial: false, Files: [
                            "ModFile"
                        ])
                }
            )));

        CreateGameFile("ModFile", "Overwritten");
        File.SetCreationTime(GamePath("ModFile"), installationDateTime.AddHours(1));
        CreateGameFile(BackupName("ModFile"), "Orig");

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath("ModFile")).Should().Be("Overwritten");
        File.Exists(GamePath(BackupName("ModFile"))).Should().BeFalse();
    }

    [Fact]
    public void Install_FailsIfGameRunning()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(true);

        modManager.Invoking(_ => _.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<Exception>().WithMessage("*running*");

        persistedState.Should().HaveNotBeenWritten();
    }

    [Fact]
    public void Install_InstallsContentFromRootDirectories()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine("Foo", DirAtRoot, "A"),
                Path.Combine("Bar", DirAtRoot, "B"),
                Path.Combine("Bar", "C"),
                Path.Combine("Baz", "D")
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath(Path.Combine(DirAtRoot, "A"))).Should().BeTrue();
        File.Exists(GamePath(Path.Combine(DirAtRoot, "B"))).Should().BeTrue();
        File.Exists(GamePath("C")).Should().BeTrue();
        File.Exists(GamePath("D")).Should().BeFalse();
        File.Exists(GamePath(Path.Combine("Baz", "D"))).Should().BeFalse();
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package100"] = new(
                    Time: DateTime.UtcNow, FsHash: 100, Partial: false, Files: [
                        Path.Combine(DirAtRoot, "A"),
                        Path.Combine(DirAtRoot, "B"),
                        "C"
                    ]),
        });
    }

    [Fact]
    public void Install_SkipsBlacklistedFiles()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine("A", FileExcludedFromInstall),
                Path.Combine(DirAtRoot, "B"),
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath(Path.Combine("A", FileExcludedFromInstall))).Should().BeFalse();
        File.Exists(GamePath(Path.Combine(DirAtRoot, "B"))).Should().BeTrue();
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package100"] = new(
                    Time: DateTime.UtcNow, FsHash: 100, Partial: false, Files: [
                        Path.Combine(DirAtRoot, "B")
                    ]),
        });
    }

    [Fact]
    public void Install_DeletesFilesWithSuffix()
    {
        var modFile = Path.Combine(DirAtRoot, "A");

        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [DeletionName(modFile)]),
        ]);
        CreateGameFile(modFile, "Orig");

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath(modFile)).Should().BeFalse();
        File.ReadAllText(GamePath(BackupName(modFile))).Should().Be("Orig");
    }

    [Fact]
    public void Install_GivesPriotiryToFilesLaterInTheModList()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine(DirAtRoot, "A")
            ]),
            CreateModArchive(200, [
                Path.Combine("X", DirAtRoot, "a")
            ]),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "A"))).Should().Be("200");
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package100"] = new(Time: DateTime.UtcNow, FsHash: 100, Partial: false, Files: []),
            ["Package200"] = new(Time: DateTime.UtcNow, FsHash: 200, Partial: false, Files: [
                    Path.Combine(DirAtRoot, "a")
                ]),
        });
    }

    [Fact]
    public void Install_DuplicatesAreCaseInsensitive()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine("X", DirAtRoot, "A"),
                Path.Combine("Y", DirAtRoot, "a")
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package100"] = new(Time: DateTime.UtcNow, FsHash: 100, Partial: false, Files: [
                    Path.Combine(DirAtRoot, "A")
                ]),
        });
    }

    [Fact]
    public void Install_StopsAfterAnyError()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
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

        modManager.Invoking(_ => _.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<IOException>();

        File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "C"))).Should().Be("300");
        File.ReadAllText(GamePath(Path.Combine(DirAtRoot, "B1"))).Should().Be("200");
        File.Exists(GamePath(Path.Combine(DirAtRoot, "B3"))).Should().BeFalse();
        File.Exists(GamePath(Path.Combine(DirAtRoot, "A"))).Should().BeFalse();
        persistedState.Should().Be(new SavedState(
            Install: new InstallationState(
                Time: DateTime.UtcNow,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    ["Package200"] = new(
                        Time: DateTime.UtcNow, FsHash: 200, Partial: true, Files: [
                            Path.Combine(DirAtRoot, "B1")
                        ]),
                    ["Package300"] = new(
                        Time: DateTime.UtcNow, FsHash: 300, Partial: false, Files: [
                            Path.Combine(DirAtRoot, "C")
                        ]),
                }
            )));
    }

    [Fact]
    public void Install_PreventsFileCreationTimeInTheFuture()
    {
        var future = DateTime.Now.AddMinutes(1);
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine(DirAtRoot, "A")
            ], extractedDir =>
                File.SetCreationTime(Path.Combine(extractedDir, DirAtRoot, "A"), future)
            )
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.GetCreationTime(GamePath($@"{DirAtRoot}\A")).Should().BeCloseTo(DateTime.Now, TimeTolerance);
    }

    [Fact]
    public void Install_PerformsBackups()
    {
        var modFile = Path.Combine(DirAtRoot, "A");
        var toBeDeleted = "B";

        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [modFile, DeletionName(toBeDeleted)]),
        ]);
        CreateGameFile(modFile, "OrigA");
        CreateGameFile(toBeDeleted, "OrigB");

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath(BackupName(modFile))).Should().Be("OrigA");
        File.ReadAllText(GamePath(BackupName(toBeDeleted))).Should().Be("OrigB");
    }

    [Fact]
    public void Install_OldVehiclesRequireBootfiles()
    {
        var drivelineRecord = $"RECORD foo";
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine("Foo", DirAtRoot, "Vehicle.crd")
            ], extractedDir =>
                File.WriteAllText(Path.Combine(extractedDir, "README.txt"), drivelineRecord)
            ),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package100", "__bootfiles900"]);
        File.ReadAllText(GamePath(VehicleListRelativePath)).Should().Contain("Vehicle.crd"); // <----------------------- THIS
        File.ReadAllText(GamePath(DrivelineRelativePath)).Should().Contain(drivelineRecord);
    }

    [Fact]
    public void Install_NewVehiclesDoNotRequireBootfiles()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [
                Path.Combine(DirAtRoot, "Vehicle.crd"),
                Path.Combine(PostProcessor.GameSupportedModDirectory, "Anything")
            ]),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package100"]);
    }

    [Fact]
    public void Install_AllTracksRequireBootfiles()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [Path.Combine(DirAtRoot, "Track.trd")]),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package100", "__bootfiles900"]);
        File.ReadAllText(GamePath(TrackListRelativePath)).Should().Contain("Track.trd");
    }

    [Fact]
    public void Install_ExtractsBootfilesFromGameByDefault()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [Path.Combine(DirAtRoot, "Foo.crd")])
        ]);

        // Unfortunately, there is no easy way to create pak files!
        modManager.Invoking(_ => _.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<DirectoryNotFoundException>();

        //CreateBootfileSources();
        //
        //modManager.InstallEnabledMods()
        //
        //persistedState.Should().HaveInstalled(["Package100", "__bootfiles"]);
    }

    [Fact]
    public void Install_ChoosesLastOfMultipleCustomBootfiles()
    {
        modRepositoryMock.Setup(_ => _.ListEnabled()).Returns([
            CreateModArchive(100, [Path.Combine(DirAtRoot, "Foo.crd")]),
            CreateCustomBootfiles(900),
            CreateCustomBootfiles(901)
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package100", "__bootfiles901"]);
    }

    #region Utility methods

    private Package CreateModArchive(int fsHash, IEnumerable<string> relativePaths) =>
        CreateModArchive(fsHash, relativePaths, _ => { });

    private Package CreateModArchive(int fsHash, IEnumerable<string> relativePaths, Action<string> callback) =>
        CreateModPackage("Package", fsHash, relativePaths, callback);

    private Package CreateCustomBootfiles(int fsHash) =>
        CreateModPackage(ModUtils.BootfilesPrefix, fsHash, [
                Path.Combine(DirAtRoot, "OrTheyWontBeInstalled"),
            VehicleListRelativePath,
            TrackListRelativePath,
            DrivelineRelativePath,
        ], extractedDir =>
            File.AppendAllText(
                Path.Combine(extractedDir, DrivelineRelativePath),
                $"{Environment.NewLine}END")
            );

    private Package CreateModPackage(string packagePrefix, int fsHash, IEnumerable<string> relativePaths, Action<string> callback)
    {
        var modName = $"Mod{fsHash}";
        var modContentsDir = testDir.CreateSubdirectory(modName).FullName;
        foreach (var relativePath in relativePaths.DefaultIfEmpty("SevenZipRequiresAFile"))
        {
            CreateFile(Path.Combine(modContentsDir, relativePath), $"{fsHash}");
        }
        callback(modContentsDir);
        var archivePath = $@"{modsDir.FullName}\{modName}.zip";
        // TODO LibArchive.Net does not support compression yet
        ZipFile.CreateFromDirectory(modContentsDir, archivePath);
        return new Package($"{packagePrefix}{fsHash}", archivePath, true, fsHash);
    }

    private FileInfo CreateGameFile(string relativePath, string content = "") =>
        CreateFile(GamePath(relativePath), content);

    // This can be removed once we introduce backup strategies
    private string BackupName(string relativePath) =>
        $"{relativePath}.orig";

    // This can be removed once we hide it inside mod logic
    private string DeletionName(string relativePath) =>
        $"{relativePath}{BaseInstaller.RemoveFileSuffix}";

    private string GamePath(string relativePath) =>
        Path.GetFullPath(relativePath, gameDir.FullName);

    private class InMemoryStatePersistence : IStatePersistence
    {
        // Avoids bootfiles checks on uninstall
        private static readonly SavedState SkipBootfilesCheck = new(
            Install: new(
                Time: ValueNotUsed,
                Mods: new Dictionary<string, PackageInstallationState>
                {
                    ["INIT"] = new(Time: null, FsHash: null, Partial: false, Files: []),
                }
            ));

        private SavedState initState = SkipBootfilesCheck;
        private SavedState? savedState;

        public void InitState(SavedState state) => initState = state;

        public SavedState ReadState() => savedState ?? initState;

        public void WriteState(SavedState state) => savedState = state;

        internal InMemoryStatePersistenceAssertions Should() => new(savedState);
    }

    private class InMemoryStatePersistenceAssertions
    {
        private readonly SavedState? savedState;

        internal InMemoryStatePersistenceAssertions(SavedState? savedState)
        {
            this.savedState = savedState;
        }

        internal void Be(SavedState expected)
        {
            var writtenState = WrittenState();
            ValidateDateTime(expected.Install.Time, writtenState.Install.Time);
            HaveInstalled(expected.Install.Mods);
        }

        internal void HaveInstalled(IReadOnlyDictionary<string, PackageInstallationState> expected)
        {
            var writtenState = WrittenState();
            var actualMods = writtenState.Install.Mods;
            var expectedMods = expected.Select(mod =>
            {
                var expectedTime = mod.Value.Time;
                var actualTime = writtenState.Install.Mods.GetValueOrDefault(mod.Key)?.Time;
                if (actualTime is null)
                {
                    return mod;
                }
                ValidateDateTime(expectedTime, actualTime);
                return new KeyValuePair<string, PackageInstallationState>(mod.Key, mod.Value with { Time = actualTime });
            });
            actualMods.Should().BeEquivalentTo(expectedMods);
        }

        internal void HaveInstalled(IEnumerable<string> expected)
        {
            var writtenState = WrittenState();
            writtenState.Install.Mods.Keys.Should().BeEquivalentTo(expected);
        }

        private SavedState WrittenState()
        {
            savedState.Should().NotBeNull("State was not written");
            return savedState!;
        }

        /// <summary>
        /// Not a great solution, but .NET doesn't natively provide support for mocking the clock!
        /// </summary>
        private void ValidateDateTime(DateTime? expected, DateTime? actual) =>
            (actual ?? DateTime.MinValue).Should().BeCloseTo((expected ?? DateTime.MinValue), TimeTolerance);

        internal void BeEmpty()
        {
            savedState.Should().BeEquivalentTo(SavedState.Empty());
        }

        internal void HaveNotBeenWritten()
        {
            savedState.Should().BeNull();
        }
    }

    #endregion
}
