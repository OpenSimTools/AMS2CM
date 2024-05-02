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
        var insallationDirectory = FindInstallationDirectory(config);
        if (!Directory.Exists(insallationDirectory))
        {
            throw new Exception("The game directory does not exist");
        }

        this.config = config;
        InstallationDirectory = insallationDirectory;
    }

    public string InstallationDirectory
    {
        get;
        init;
    }

    public bool IsRunning => Process.GetProcesses().Any(_ => _.ProcessName == config.ProcessName);

    private static string FindInstallationDirectory(IConfig config)
    {
        if (Path.IsPathFullyQualified(config.Path))
        {
            return config.Path;
        }
        var steamLibraryPath = Steam.AppLibraryPath(config.SteamId);
        return Path.Combine(steamLibraryPath, config.Path);
    }
}
