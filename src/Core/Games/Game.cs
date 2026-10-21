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
    private readonly IFileSystem fileSystem;

    public Game(IFileSystem fileSystem, IConfig config)
    {
        this.fileSystem = fileSystem;
        var insallationDirectory = FindInstallationDirectory(config);
        if (!insallationDirectory.Exists)
        {
            throw new Exception("The game directory does not exist");
        }

        this.config = config;
        InstallationDirectory = insallationDirectory;
    }

    public IDirectoryInfo InstallationDirectory { get; init; }

    public bool IsRunning => Process.GetProcesses().Any(_ => _.ProcessName == config.ProcessName);

    private IDirectoryInfo FindInstallationDirectory(IConfig config)
    {
        if (Path.IsPathFullyQualified(config.Path))
        {
            return fileSystem.DirectoryInfo.New(config.Path);
        }

        var steamLibraryPath = Steam.AppLibraryPath(fileSystem, config.SteamId);
        return fileSystem.DirectoryInfo.New(Path.Combine(steamLibraryPath.FullName, config.Path));
    }
}
