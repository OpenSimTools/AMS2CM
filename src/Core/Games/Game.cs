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
        this.config = config;
        InstallationDirectory = FindInstallationDirectory(config);
    }

    public string InstallationDirectory
    {
        get;
        init;
    }

    public bool IsRunning() => Process.GetProcesses().Any(_ => _.ProcessName == config.ProcessName);

    private static string FindInstallationDirectory(IConfig config)
    {
        if (Path.IsPathFullyQualified(config.Path))
        {
            return config.Path;
        }

        var gameLibraryPath = Steam.AppLibraryPath(config.SteamId) ??
            throw new Exception("Cannot find game on Steam");

        return Path.Combine(gameLibraryPath, config.Path);
    }
}
