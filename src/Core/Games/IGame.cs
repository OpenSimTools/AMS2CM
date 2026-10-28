using System.IO.Abstractions;

namespace Core.Games;

public interface IGame
{
    IDirectoryInfo InstallationDirectory { get; }

    bool IsRunning { get; }
}
