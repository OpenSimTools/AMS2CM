using System.IO.Abstractions.TestingHelpers;
using Core.API;
using Core.Games;
using FluentAssertions;

namespace Core.Tests.Games;

public class GameTest
{
    #region Setup

    private const string LibraryDirectory = @"C:\Library";
    private const string GameName = "Game";
    private const string AppId = "App";
    private static readonly string GameDirectory = Path.Combine(LibraryDirectory, GameName);
    private readonly MockFileSystem fs = new();
    private readonly Mock<ISteamLibraryLocator> steam = new(MockBehavior.Strict);

    public GameTest()
    {
        fs.Directory.CreateDirectory(GameDirectory);
    }

    #endregion

    [Fact]
    public void AbsolutePath_BypassesSteam()
    {
        var config = new GameConfig { Path = GameDirectory };

        var game = new Game(fs, config, steam.Object);

        game.InstallationDirectory.FullName.Should().Be(GameDirectory);
        game.InstallationDirectory.FileSystem.Should().BeSameAs(fs);
        steam.VerifyNoOtherCalls();
    }

    [Fact]
    public void RelativePath_UsesSteamLibrary()
    {
        var config = new GameConfig { Path = GameName, SteamId = AppId };
        steam.Setup(s => s.AppLibraryPath(AppId)).Returns(LibraryDirectory);

        var game = new Game(fs, config, steam.Object);

        game.InstallationDirectory.FullName.Should().Be(GameDirectory);
        steam.Verify(s => s.AppLibraryPath(AppId), Times.Once);
    }

    [Fact]
    public void MissingDirectory_Throws()
    {
        var config = new GameConfig { Path = @"C:\Missing" };

        var create = () => new Game(fs, config, steam.Object);

        create.Should().Throw<Exception>().WithMessage("The game directory does not exist");
    }
}
