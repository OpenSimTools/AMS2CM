using Core.API;
using Core.Games;
using Core.IO;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.State;
using Core.Tests.Base;
using Core.Utils;
using FluentAssertions;
using static Core.Packages.Repository.FileSystemRepository;

namespace Core.Tests.API;

[IntegrationTest]
public class ModManagerTest : AbstractFilesystemTest
{
    #region Initialisation

    private const string BootfilesPrefix = "BP";
    private const string DirAtRoot = "DirAtRoot";
    private const string FileExcludedFromInstall = "Excluded";
    private static readonly string GameSupportedModDirectory = Path.Combine("Mod", "Directory");

    private static readonly ModInstallConfig DefaultModInstallConfig = new();

    private static readonly string VehicleListRelativePath =
        Path.Combine(DefaultModInstallConfig.BootfilesVehicleListDir, DefaultModInstallConfig.VehicleListFileName);

    private static readonly string TrackListRelativePath =
        Path.Combine(DefaultModInstallConfig.BootfilesTrackListDir, DefaultModInstallConfig.TrackListFileName);

    private static readonly string DrivelineRelativePath =
        Path.Combine(DefaultModInstallConfig.BootfilesDrivelineDir, DefaultModInstallConfig.DrivelineFileName);

    private static readonly DateTime PastDate = DateTime.Today.AddDays(-1);

    private static readonly TimeSpan TimeTolerance = TimeSpan.FromMilliseconds(100);

    private readonly DirectoryInfo gameDir;

    private readonly Mock<IGame> gameMock = new();
    private readonly Mock<IPackageRepository> modRepositoryMock = new();
    private readonly Mock<ISafeFileDelete> safeFileDeleteMock = new();
    private readonly Mock<IEventHandler> eventHandlerMock = new();

    private readonly InMemoryStatePersistence persistedState;

    private readonly IModManager modManager;

    public ModManagerTest()
    {
        gameDir = TestDir.CreateSubdirectory("Game");

        var tempDir = new SubdirectoryTempDir(TestDir.FullName);

        persistedState = new InMemoryStatePersistence();
        var modInstallConfig = new ModInstallConfig
        {
            BootfilesPrefix = BootfilesPrefix,
            DirsAtRoot = [DirAtRoot],
            ExcludedFromInstall = [$"**\\{FileExcludedFromInstall}"],
            GameSupportedModDir = GameSupportedModDirectory
        };

        modManager = Init.CreateModManager(
            gameMock.Object,
            modRepositoryMock.Object,
            persistedState,
            safeFileDeleteMock.Object,
            tempDir,
            modInstallConfig);

        gameMock.Setup(m => m.InstallationDirectory).Returns(gameDir.FullName);
    }

    #endregion

    [Fact]
    public void FetchState_AlwaysReturnsModsInstalledOrNot()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["I"] = new(
                Time: PastDate, VersionHash: 101, Dependencies: [], Files: [], ShadowedBy: []),
        });
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns(
        [
            new Package(Name: "E", Location: "e/path", VersionHash: 102)
        ]);
        modRepositoryMock.Setup(m => m.ListDisabled()).Returns(
        [
            new Package(Name: "D", Location: "d/path", VersionHash: 103)
        ]);

        modManager.FetchState().Should().BeEquivalentTo(
        [
            new ModState("I", null, IsInstalled: true, IsEnabled: false, IsOutOfDate: false),
            new ModState("E", "e/path", IsInstalled: false, IsEnabled: true, IsOutOfDate: false),
            new ModState("D", "d/path", IsInstalled: false, IsEnabled: false, IsOutOfDate: false)
        ]);
    }

    [Fact]
    public void FetchState_MergesInstalledAndAvailable()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: PastDate, VersionHash: 999,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
            ["B"] = new(
                Time: PastDate, VersionHash: null,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
            ["C"] = new(
                Time: PastDate, VersionHash: null,
                Dependencies: [],
                Files: [],
                ShadowedBy: [])
        });
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns(
        [
            new Package(Name: "A", Location: "a/path", VersionHash: 101),
            new Package(Name: "B", Location: "b/path", VersionHash: 102)
        ]);
        modRepositoryMock.Setup(m => m.ListDisabled()).Returns(
        [
            new Package(Name: "C", Location: "c/path", VersionHash: 103),
            new Package(Name: "D", Location: "d/path", VersionHash: 104)
        ]);

        modManager.FetchState().Should().BeEquivalentTo(
        [
            new ModState("A", "a/path", IsInstalled: true, IsEnabled: true, IsOutOfDate: true),
            new ModState("B", "b/path", IsInstalled: null, IsEnabled: true, IsOutOfDate: true),
            new ModState("C", "c/path", IsInstalled: null, IsEnabled: false, IsOutOfDate: true),
            new ModState("D", "d/path", IsInstalled: false, IsEnabled: false, IsOutOfDate: false)
        ]);
    }

    [Fact]
    public void FetchState_PropagatesPartialOrMissingInstallationToDependants()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: PastDate, VersionHash: 101,
                Dependencies: ["Partial"],
                Files: [],
                ShadowedBy: []),
            ["B"] = new(
                Time: PastDate, VersionHash: 102,
                Dependencies: ["NotInstalled"],
                Files: [],
                ShadowedBy: []),
            ["Partial"] = new(
                Time: PastDate, VersionHash: null,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
        });
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([]);
        modRepositoryMock.Setup(m => m.ListDisabled()).Returns([]);

        modManager.FetchState().Should().BeEquivalentTo(
        [
            new ModState("A", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
            new ModState("B", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
            new ModState("Partial", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
        ]);
    }

    [Fact]
    public void FetchState_PropagatesPartialOrMissingInstallationToShadowed()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: PastDate, VersionHash: 101,
                Dependencies: [],
                Files: [],
                ShadowedBy: ["Partial"]),
            ["B"] = new(
                Time: PastDate, VersionHash: 102,
                Dependencies: [],
                Files: [],
                ShadowedBy: ["NotInstalled"]),
            ["Partial"] = new(
                Time: PastDate, VersionHash: null,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
        });
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([]);
        modRepositoryMock.Setup(m => m.ListDisabled()).Returns([]);

        modManager.FetchState().Should().BeEquivalentTo(
        [
            new ModState("A", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
            new ModState("B", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
            new ModState("Partial", null, IsInstalled: null, IsEnabled: false, IsOutOfDate: false),
        ]);
    }

    [Fact]
    public void FetchState_RemovesUnavailableBootfiles()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            [$"{BootfilesPrefix}_IU"] = new(
                Time: PastDate, VersionHash: 101,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
            [$"{BootfilesPrefix}_IE"] = new(
                Time: PastDate, VersionHash: 102,
                Dependencies: [],
                Files: [],
                ShadowedBy: []),
            [$"{BootfilesPrefix}_ID"] = new(
                Time: PastDate, VersionHash: 103,
                Dependencies: [],
                Files: [],
                ShadowedBy: [])
        });
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns(
        [
            new Package(Name: $"{BootfilesPrefix}_IE", Location: "ie/path", VersionHash: null),
            new Package(Name: $"{BootfilesPrefix}_UE", Location: "ue/path", VersionHash: null)
        ]);
        modRepositoryMock.Setup(m => m.ListDisabled()).Returns(
        [
            new Package(Name: $"{BootfilesPrefix}_ID", Location: "id/path", VersionHash: null),
            new Package(Name: $"{BootfilesPrefix}_UD", Location: "ud/path", VersionHash: null)
        ]);

        modManager.FetchState().Should().BeEquivalentTo(
        [
            new ModState($"{BootfilesPrefix}_IE", "ie/path", IsInstalled: true, IsEnabled: true, IsOutOfDate: true),
            new ModState($"{BootfilesPrefix}_UE", "ue/path", IsInstalled: false, IsEnabled: true, IsOutOfDate: false),
            new ModState($"{BootfilesPrefix}_ID", "id/path", IsInstalled: true, IsEnabled: false, IsOutOfDate: true),
            new ModState($"{BootfilesPrefix}_UD", "ud/path", IsInstalled: false, IsEnabled: false, IsOutOfDate: false),
        ]);
    }

    [Fact]
    public void Uninstall_FailsIfGameRunning()
    {
        gameMock.Setup(m => m.IsRunning).Returns(true);

        modManager.Invoking(m => m.UninstallAllMods(eventHandlerMock.Object))
            .Should().Throw<Exception>().WithMessage("*running*");

        persistedState.Should().HaveNotBeenWritten();
    }

    [Fact]
    public void Uninstall_DeletesCreatedFilesAndDirectories()
    {
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: PastDate, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    Path.Combine("X", "ModAFile"),
                    Path.Combine("Y", "ModAFile")
                ],
                ShadowedBy: []),
            ["B"] = new(
                Time: PastDate, VersionHash: 102,
                Dependencies: [],
                Files:
                [
                    Path.Combine("X", "ModBFile")
                ],
                ShadowedBy: [])
        });
        CreateFile(GamePath("Y", "ExistingFile"));

        modManager.UninstallAllMods(eventHandlerMock.Object);

        Directory.Exists(GamePath("X").Full).Should().BeFalse();
        File.Exists(GamePath("Y", "ModAFile").Full).Should().BeFalse();
        File.Exists(GamePath("Y", "ExistingFile").Full).Should().BeTrue();
        persistedState.Should().BeEmpty();
    }

    [Fact]
    public void Uninstall_SkipsFilesCreatedAfterInstallation()
    {
        var installationDateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            [""] = new(
                Time: installationDateTime.ToUniversalTime(), VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    "ModFile",
                    "RecreatedFile",
                    "AlreadyDeletedFile"
                ],
                ShadowedBy: [])
        });
        CreateFile(GamePath("ModFile")).CreationTime = installationDateTime;
        CreateFile(GamePath("RecreatedFile"));

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.Exists(GamePath("ModFile").Full).Should().BeFalse(); // FIXME
        File.Exists(GamePath("RecreatedFile").Full).Should().BeTrue();
        persistedState.Should().BeEmpty();
    }

    [Fact]
    public void Uninstall_StopsAfterAnyError()
    {
        // It must be after files are created
        var installationDateTime = DateTime.Now.AddMinutes(1);
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            ["A"] = new(
                Time: installationDateTime.ToUniversalTime(), VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    "ModAFile"
                ],
                ShadowedBy: []),
            ["B"] = new(
                Time: installationDateTime.ToUniversalTime(), VersionHash: 102,
                Dependencies: [],
                Files:
                [
                    "ModBFile1",
                    "ModBFile2"
                ],
                ShadowedBy: []),
            ["C"] = new(
                Time: installationDateTime.ToUniversalTime(), VersionHash: 103,
                Dependencies: [],
                Files:
                [
                    "ModCFile"
                ],
                ShadowedBy: [])
        });

        CreateFile(GamePath("ModAFile"));
        CreateFile(GamePath("ModBFile1"));
        using var _ = CreateFile(GamePath("ModBFile2")).OpenRead(); // Prevent deletion
        CreateFile(GamePath("ModCFile"));

        modManager.Invoking(m => m.UninstallAllMods(eventHandlerMock.Object))
            .Should().Throw<IOException>();

        persistedState.Should().Be(new SavedState(
            Installation: new Dictionary<string, PackageInstallationState>
                {
                    ["B"] = new(
                        Time: installationDateTime.ToUniversalTime(), VersionHash: null,
                        Dependencies: [],
                        Files:
                        [
                            "ModBFile2"
                        ],
                        ShadowedBy: []),
                    ["C"] = new(
                        Time: installationDateTime.ToUniversalTime(), VersionHash: 103,
                        Dependencies: [],
                        Files:
                        [
                            "ModCFile"
                        ],
                        ShadowedBy: [])
                }
            ));
    }


    [Fact]
    public void Uninstall_RestoresBackups()
    {
        // It must be after files are created
        var installationDateTime = DateTime.Now.AddMinutes(1);
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            [""] = new(
                Time: installationDateTime, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    "ModFile"
                ],
                ShadowedBy: [])
        });

        CreateFile(GamePath("ModFile"), "Mod");
        CreateFile(GamePath(BackupName("ModFile")), "Orig");

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath("ModFile").Full).Should().Be("Orig");
        File.Exists(GamePath(BackupName("ModFile")).Full).Should().BeFalse();
    }

    [Fact]
    public void Uninstall_SkipsRestoreIfModFileOverwritten()
    {
        // It must be after files are created
        var installationDateTime = DateTime.Now.AddMinutes(1);
        persistedState.InitModInstallationState(new Dictionary<string, PackageInstallationState>
        {
            [""] = new(
                Time: installationDateTime.ToUniversalTime(), VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    "ModFile"
                ],
                ShadowedBy: [])
        });

        CreateFile(GamePath("ModFile"), "Overwritten");
        File.SetCreationTime(GamePath("ModFile").Full, installationDateTime.AddHours(1));
        CreateFile(GamePath(BackupName("ModFile")), "Orig");

        modManager.UninstallAllMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath("ModFile").Full).Should().Be("Overwritten");
        File.Exists(GamePath(BackupName("ModFile")).Full).Should().BeFalse();
    }

    [Fact]
    public void Install_FailsIfGameRunning()
    {
        gameMock.Setup(m => m.IsRunning).Returns(true);

        modManager.Invoking(m => m.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<Exception>().WithMessage("*running*");

        persistedState.Should().HaveNotBeenWritten();
    }

    [Fact]
    public void Install_InstallsContentFromRootDirectories()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine("Foo", DirAtRoot, "A"),
                Path.Combine("Bar", DirAtRoot, "B"),
                Path.Combine("Bar", "C"),
                Path.Combine("Baz", "D"),
                Path.Combine("E")
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath(DirAtRoot, "A").Full).Should().BeTrue();
        File.Exists(GamePath(DirAtRoot, "B").Full).Should().BeTrue();
        File.Exists(GamePath("C").Full).Should().BeTrue();
        File.Exists(GamePath("D").Full).Should().BeFalse();
        File.Exists(GamePath("Baz", "D").Full).Should().BeFalse();
        File.Exists(GamePath("Baz").Full).Should().BeFalse();
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package101"] = new(
                Time: DateTime.UtcNow, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    Path.Combine(DirAtRoot, "A"),
                    Path.Combine(DirAtRoot, "B"),
                    "C"
                ],
                ShadowedBy: []),
        });
    }

    [Fact]
    public void InstallmSkipsBlacklistedFiles()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine("A", FileExcludedFromInstall),
                Path.Combine(DirAtRoot, "B"),
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath("A", FileExcludedFromInstall).Full).Should().BeFalse();
        File.Exists(GamePath(DirAtRoot, "B").Full).Should().BeTrue();
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package101"] = new(
                Time: DateTime.UtcNow, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    Path.Combine(DirAtRoot, "B")
                ],
                ShadowedBy: []),
        });
    }

    [Fact]
    public void Install_DeletesFilesWithSuffix()
    {
        var modFile = Path.Combine(DirAtRoot, "A");

        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [DeletionName(modFile)]),
        ]);
        CreateFile(GamePath(modFile), "Orig");

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.Exists(GamePath(modFile).Full).Should().BeFalse();
        File.ReadAllText(GamePath(BackupName(modFile)).Full).Should().Be("Orig");
    }

    [Fact]
    public void Install_GivesPriorityToFilesLaterInTheModList()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine(DirAtRoot, "A"),
                Path.Combine(DirAtRoot, "B")
            ]),
            CreateModArchive(102, [
                Path.Combine("X", DirAtRoot, "a")
            ]),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath(DirAtRoot, "A").Full).Should().Be("102");
        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package101"] = new(Time: DateTime.UtcNow, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    Path.Combine(DirAtRoot, "B")
                ],
                ShadowedBy: ["Package102"]),
            ["Package102"] = new(Time: DateTime.UtcNow, VersionHash: 102,
                Dependencies: [],
                Files:
                [
                    Path.Combine(DirAtRoot, "a")
                ],
                ShadowedBy: []),
        });
    }

    [Fact]
    public void Install_DuplicatesAreCaseInsensitive()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine("X", DirAtRoot, "A"),
                Path.Combine("Y", DirAtRoot, "a")
            ])
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(new Dictionary<string, PackageInstallationState>
        {
            ["Package101"] = new(Time: DateTime.UtcNow, VersionHash: 101,
                Dependencies: [],
                Files:
                [
                    Path.Combine(DirAtRoot, "A")
                ],
                ShadowedBy: []),
        });
    }

    [Fact]
    public void Install_StopsAfterAnyError()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine(DirAtRoot, "A")
            ]),
            CreateModArchive(102, [
                Path.Combine(DirAtRoot, "B1"),
                Path.Combine(DirAtRoot, "B2"),
                Path.Combine(DirAtRoot, "B3")
            ]),
            CreateModArchive(103, [
                Path.Combine(DirAtRoot, "C"),
            ]),
        ]);
        using var _ = CreateFile(GamePath(DirAtRoot, "B2")).OpenRead(); // Prevent overwrite

        modManager.Invoking(m => m.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<IOException>();

        File.ReadAllText(GamePath(DirAtRoot, "C").Full).Should().Be("103");
        File.ReadAllText(GamePath(DirAtRoot, "B1").Full).Should().Be("102");
        File.Exists(GamePath(DirAtRoot, "B3").Full).Should().BeFalse();
        File.Exists(GamePath(DirAtRoot, "A").Full).Should().BeFalse();
        persistedState.Should().Be(new SavedState(
            Installation: new Dictionary<string, PackageInstallationState>
                {
                    ["Package102"] = new(
                        Time: DateTime.UtcNow, VersionHash: null,
                        Dependencies: [],
                        Files:
                        [
                            Path.Combine(DirAtRoot, "B1"),
                            Path.Combine(DirAtRoot, "B2") // We don't know when it failed
                        ],
                        ShadowedBy: []),
                    ["Package103"] = new(
                        Time: DateTime.UtcNow, VersionHash: 103,
                        Dependencies: [],
                        Files:
                        [
                            Path.Combine(DirAtRoot, "C")
                        ],
                        ShadowedBy: []),
                }
            ));
    }

    [Fact]
    public void Install_PreventsFileCreationTimeInTheFuture()
    {
        var future = DateTime.Now.AddMinutes(1);
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                    Path.Combine(DirAtRoot, "A")
                ], extractedDir =>
                    File.SetCreationTime(Path.Combine(extractedDir, DirAtRoot, "A"), future)
            )
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.GetCreationTime(GamePath(DirAtRoot, "A").Full).Should().BeCloseTo(DateTime.Now, TimeTolerance);
    }

    [Fact]
    public void Install_PerformsBackups()
    {
        var modFile = Path.Combine(DirAtRoot, "A");
        var toBeDeleted = "B";

        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [modFile, DeletionName(toBeDeleted)]),
        ]);
        CreateFile(GamePath(modFile), "OrigA");
        CreateFile(GamePath(toBeDeleted), "OrigB");

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        File.ReadAllText(GamePath(BackupName(modFile)).Full).Should().Be("OrigA");
        File.ReadAllText(GamePath(BackupName(toBeDeleted)).Full).Should().Be("OrigB");
    }

    [Fact]
    public void Install_GameSupportedModsNeverRequireBootfiles()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine(DirAtRoot, "Vehicle.crd"),
                Path.Combine(DirAtRoot, "Track.trd"), // Tracks do not currently work in game
                Path.Combine(GameSupportedModDirectory, "Anything")
            ]),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package101"]);
        persistedState.For("Package101").Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Install_OldVehicleModsDoNotRequireBootfiles()
    {
        var drivelineRecord = $"RECORD foo";
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                    Path.Combine("Foo", DirAtRoot, "Vehicle.crd")
                ], extractedDir =>
                    File.WriteAllText(Path.Combine(extractedDir, "README.txt"), drivelineRecord)
            ),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package101"]);
        persistedState.For("Package101").Dependencies.Should().BeEmpty();

        var generatedConfigDir = $"Package101_{101:x}";
        File.ReadAllText(GamePath(GameSupportedModDirectory, generatedConfigDir,
            DefaultModInstallConfig.VehicleListFileName).Full).Should().Contain("Vehicle.crd");
        File.ReadAllText(GamePath(GameSupportedModDirectory, generatedConfigDir,
            DefaultModInstallConfig.DrivelineFileName).Full).Should().Contain(drivelineRecord);
        File.Exists(GamePath(GameSupportedModDirectory, generatedConfigDir, $"{generatedConfigDir}.xml")
            .Full).Should().BeTrue();
    }

    [Fact]
    public void Install_OldTrackModsAlwaysRequireBootfiles()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [
                Path.Combine(DirAtRoot, "Track.trd"),
                // Vehicles are not upgraded to game-supported mods if tracks are present
                Path.Combine(DirAtRoot, "Vehicle.crd")
            ]),
            CreateCustomBootfiles(900),
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package101", $"{BootfilesPrefix}900"]);
        persistedState.For("Package101").Dependencies.Should().Contain($"{BootfilesPrefix}900");

        var generatedConfigDir = $"Package101_{101:x}";
        File.ReadAllText(GamePath(GameSupportedModDirectory, generatedConfigDir,
            DefaultModInstallConfig.TrackListFileName).Full).Should().Contain("Track.trd");
        File.Exists(GamePath(GameSupportedModDirectory, generatedConfigDir, $"{generatedConfigDir}.xml")
            .Full).Should().BeFalse();

        File.ReadAllText(GamePath(TrackListRelativePath).Full).Should().Contain("Track.trd");
    }

    [Fact]
    public void Install_ExtractsBootfilesFromGameByDefault()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [Path.Combine(DirAtRoot, "Foo.trd")])
        ]);

        // Unfortunately, there is no easy way to create pak files!
        modManager.Invoking(m => m.InstallEnabledMods(eventHandlerMock.Object))
            .Should().Throw<DirectoryNotFoundException>();

        //CreateBootfileSources();
        //
        //modManager.InstallEnabledMods()
        //
        //persistedState.Should().HaveInstalled(["Package100", $"{BootfilesPrefix}_generated"]);
    }

    [Fact]
    public void Install_ChoosesLastOfMultipleCustomBootfiles()
    {
        modRepositoryMock.Setup(m => m.ListEnabled()).Returns([
            CreateModArchive(101, [Path.Combine(DirAtRoot, "Foo.trd")]),
            CreateCustomBootfiles(900),
            CreateCustomBootfiles(901)
        ]);

        modManager.InstallEnabledMods(eventHandlerMock.Object);

        persistedState.Should().HaveInstalled(["Package101", $"{BootfilesPrefix}901"]);
    }

    #region Utility methods

    private IPackage CreateModArchive(int versionHash, IEnumerable<string> relativePaths) =>
        CreateModArchive(versionHash, relativePaths, _ => { });

    private IPackage CreateModArchive(int versionHash, IEnumerable<string> relativePaths, Action<string> callback) =>
        CreateModPackage("Package", versionHash, relativePaths, callback);

    private IPackage CreateCustomBootfiles(int versionHash) =>
        CreateModPackage(BootfilesPrefix, versionHash, [
                Path.Combine(DirAtRoot, "OrTheyWontBeInstalled"),
                VehicleListRelativePath,
                TrackListRelativePath,
                DrivelineRelativePath,
            ], extractedDir =>
                File.AppendAllText(
                    Path.Combine(extractedDir, DrivelineRelativePath),
                    $"{Environment.NewLine}END")
        );

    private IPackage CreateModPackage(string packagePrefix, int versionHash, IEnumerable<string> relativePaths,
        Action<string> callback)
    {
        var modName = $"Mod{versionHash}";
        var modContentsDir = TestDir.CreateSubdirectory(modName).FullName;
        foreach (var relativePath in relativePaths)
        {
            CreateFile(new RootedPath(modContentsDir, relativePath), $"{versionHash}");
        }

        callback(modContentsDir);

        var packageName = $"{packagePrefix}{versionHash}";

        var package = new Mock<IPackage>();
        package.SetupGet(m => m.Name).Returns(packageName);
        package.SetupGet(m => m.Location).Returns(modContentsDir);
        package.SetupGet(m => m.VersionHash).Returns(versionHash);
        package.SetupGet(m => m.Installer).Returns(() =>
            new DirectoryInstaller(packageName, versionHash, modContentsDir));
        return package.Object;
    }

    // This can be removed once we introduce backup strategies
    private static string BackupName(string relativePath) =>
        $"{relativePath}.orig";

    // This can be removed once we hide it inside mod logic
    private static string DeletionName(string relativePath) =>
        $"{relativePath}{BaseInstaller.RemoveFileSuffix}";

    private RootedPath GamePath(params string[] segments) =>
        new(gameDir.FullName, Path.Combine(segments));

    private class InMemoryStatePersistence : IStatePersistence
    {
        // Avoids bootfiles checks on uninstall
        private static readonly SavedState SkipBootfilesCheck = new(
            Installation: new Dictionary<string, PackageInstallationState>
                {
                    ["INIT"] = new(Time: PastDate, VersionHash: null, Dependencies: [], Files: [],
                        ShadowedBy: []),
                }
            );

        private SavedState initState = SkipBootfilesCheck;
        private SavedState? savedState;

        public void InitModInstallationState(Dictionary<string, PackageInstallationState> modInstallationState) =>
            initState = new SavedState(Installation: modInstallationState);

        public SavedState ReadState() => savedState ?? initState;

        public void WriteState(SavedState state) => savedState = state;

        internal InMemoryStatePersistenceAssertions Should() => new(savedState);

        internal PackageInstallationState For(string packageName)
        {
            var state = savedState?.Installation[packageName];
            state.Should().NotBeNull();
            return state;
        }
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
            HaveInstalled(expected.Installation);
        }

        internal void HaveInstalled(IReadOnlyDictionary<string, PackageInstallationState> expected)
        {
            var writtenState = WrittenState();
            var actualMods = writtenState.Installation;
            var expectedMods = expected.Select(mod =>
            {
                var expectedTime = mod.Value.Time;
                var actualTime = writtenState.Installation.GetValueOrDefault(mod.Key)?.Time;
                if (actualTime is null)
                {
                    return mod;
                }

                ValidateDateTime(expectedTime, actualTime);
                return new KeyValuePair<string, PackageInstallationState>(mod.Key,
                    mod.Value with { Time = (DateTime)actualTime });
            });
            actualMods.Should().BeEquivalentTo(expectedMods);
        }

        internal void HaveInstalled(IEnumerable<string> expected)
        {
            var writtenState = WrittenState();
            writtenState.Installation.Keys.Should().BeEquivalentTo(expected);
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
