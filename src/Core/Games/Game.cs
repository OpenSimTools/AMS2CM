using System.Diagnostics;
using System.IO.Abstractions;

namespace Core.Games;

public class Game : IGame
{
    public interface IConfig
    {
        string SteamId { get; }

        string Path { get; }

        string ProcessName { get; }
    }

    private readonly IConfig config;

    public Game(IFileSystem fs, IConfig config, ISteamLibraryLocator steamLibraryLocator)
    {
        var installationDirectory = FindInstallationDirectory(config, steamLibraryLocator);
        if (!fs.Directory.Exists(installationDirectory))
        {
            throw new Exception("The game directory does not exist");
        }

        this.config = config;
        InstallationDirectory = fs.DirectoryInfo.New(installationDirectory);
    }

    public IDirectoryInfo InstallationDirectory { get; init; }

    public bool IsRunning => Process.GetProcesses().Any(p => p.ProcessName == config.ProcessName);

    private static string FindInstallationDirectory(IConfig config, ISteamLibraryLocator steamLibraryLocator)
    {
        if (Path.IsPathFullyQualified(config.Path))
        {
            return config.Path;
        }

        var steamLibraryPath = steamLibraryLocator.AppLibraryPath(config.SteamId);
        return Path.Combine(steamLibraryPath, config.Path);
    }
}
