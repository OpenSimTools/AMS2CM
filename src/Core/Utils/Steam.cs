using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using Microsoft.Win32;

namespace Core;


public class Steam
{
    private static readonly string[] RegistryPaths = {
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
    };

    private readonly IEnumerable<LibraryFolder> libraryFolders;

    public Steam()
    {
        var steamDir = FindSteamInstallationDirectory();
        if (steamDir == null)
        {
            libraryFolders = Array.Empty<LibraryFolder>();
        }
        else
        {
            var libraryFoldersPath = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
            libraryFolders = ReadLibraryFolders(libraryFoldersPath);
        }
    }

    private static string? FindSteamInstallationDirectory()
    {
        return RegistryPaths.Select(registryPath =>
            Registry.GetValue(registryPath, "InstallPath", null) as string
        ).FirstOrDefault(path => path != null && Directory.Exists(path));
    }

    public string? AppLibraryPath(string appId)
    {
        return libraryFolders.SingleOrDefault(lf => lf.Apps.ContainsKey(appId))?.Path;
    }

    private static IEnumerable<LibraryFolder> ReadLibraryFolders(string path)
    {
        var libraryFolders = File.ReadAllText(path);
        var vdfRoot = VdfConvert.Deserialize(libraryFolders);
        return vdfRoot.ToJson().Value
            .SelectMany(i => i)
            .Select(i => i.ToObject<LibraryFolder>()!);
    }

    private record LibraryFolder
    (
        string Path,
        Dictionary<string, string> Apps
    );
}
