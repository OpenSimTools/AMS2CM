using System.Diagnostics;

namespace Core.Games;

public class Game : IGame
{
    public interface IConfig
    {
        string SteamId
        {
            get;
        }

        string Path
        {
            get;
        }

        string ProcessName
        {
            get;
        }
    }

    private readonly IConfig config;

    public Game(IConfig config)
    {
        var maybeInsallationDirectory = FindInstallationDirectory(config);
        if (maybeInsallationDirectory is null || !Directory.Exists(maybeInsallationDirectory))
        {
            throw new Exception("Cannot find game directory");
        }

        this.config = config;
        InstallationDirectory = maybeInsallationDirectory;
    }

    public string InstallationDirectory
    {
        get;
        init;
    }

    public bool IsRunning => Process.GetProcesses().Any(_ => _.ProcessName == config.ProcessName);

    private static string? FindInstallationDirectory(IConfig config)
    {
        if (Path.IsPathFullyQualified(config.Path))
        {
            return config.Path;
        }
        var gameLibraryPath = Steam.AppLibraryPath(config.SteamId);
        return gameLibraryPath is null ? null : Path.Combine(gameLibraryPath, config.Path);
    }
}
