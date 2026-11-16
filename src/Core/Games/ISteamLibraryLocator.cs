namespace Core.Games;

public interface ISteamLibraryLocator
{
    string AppLibraryPath(string appId);
}
