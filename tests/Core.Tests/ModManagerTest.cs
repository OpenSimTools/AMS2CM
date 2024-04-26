namespace Core.Tests;

using Core.Games;
using Core.IO;
using Core.Mods;
using Core.State;
using Moq;

public class ModManagerTest : IDisposable
{
    private const string ModRootDir = "RootDir";

    private readonly DirectoryInfo testDir;
    private readonly DirectoryInfo gameDir;

    private readonly Mock<IGame> gameMock = new();
    private readonly Mock<IModRepository> modRepositoryMock = new();
    private readonly Mock<IStatePersistence> statePersistenceMock = new();
    private readonly Mock<ISafeFileDelete> safeFileDeleteMock = new();
    private readonly Mock<ITempDir> tempDirMock = new();
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
            statePersistenceMock.Object,
            safeFileDeleteMock.Object,
            tempDirMock.Object);

        gameMock.Setup(_ => _.InstallationDirectory).Returns(gameDir.FullName);
    }

    public void Dispose()
    {
        testDir.Delete(recursive: true);
    }

    [Fact]
    public void FailsIfGameRunning()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(true);

        Assert.Throws<Exception>(() =>
            modManager.UninstallAllMods()
        );
        Assert.Throws<Exception>(() =>
            modManager.InstallEnabledMods()
        );
    }

    [Fact]
    public void FailsIfBootfilesWereInstalledByAnotherTool()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        statePersistenceMock.Setup(_ => _.ReadState()).Returns(InternalState.Empty);

        Assert.Throws<Exception>(() =>
            modManager.UninstallAllMods()
        );

        Assert.Throws<Exception>(() =>
            modManager.InstallEnabledMods()
        );
    }

    [Fact]
    public void FailsIfBootfilesRemovedByAnotherTool()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        statePersistenceMock.Setup(_ => _.ReadState()).Returns(InternalState.Empty);

        var exception = Assert.Throws<Exception>(() => modManager.UninstallAllMods());
        Assert.Contains("another tool", exception.Message);

        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([]);
        exception = Assert.Throws<Exception>(() => modManager.InstallEnabledMods());
        Assert.Contains("another tool", exception.Message);
    }


    [Fact]
    public void Succeeds_ToDo()
    {
        gameMock.Setup(_ => _.IsRunning).Returns(false);
        statePersistenceMock.Setup(_ => _.ReadState()).Returns(InternalState.Empty);
        //statePersistenceMock.Setup(_ => _.ReadState()).Returns(new InternalState
        //(
        //    Install: new InternalInstallationState(
        //        Time: null,
        //        Mods: new Dictionary<string, InternalModInstallationState> { }
        //    )
        //));
        CreateGameFile(ModManager.FileRemovedByBootfiles);

        modManager.UninstallAllMods();

        modRepositoryMock.Setup(_ => _.ListEnabledMods()).Returns([]);
        modManager.InstallEnabledMods();
    }

    private void CreateGameFile(string relativePath, string content = "")
    {
        var fullPath = Path.GetFullPath(relativePath, gameDir.FullName);
        var parentDirFullPath = Path.GetDirectoryName(fullPath);
        if (parentDirFullPath is not null)
        {
            Directory.CreateDirectory(parentDirFullPath);
        }
        File.WriteAllText(fullPath, content);
    }
}