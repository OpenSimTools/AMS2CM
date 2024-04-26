namespace Core.Games;

public interface IGame
{
    string InstallationDirectory
    {
        get;
    }

    bool IsRunning
    {
        get;
    }
}
