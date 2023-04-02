using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using Microsoft.Win32;

namespace AMS2CM;


public static class Steam
{
    private static readonly string[] RegistryPaths = {
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
    };
    
    public static string? MainInstallationDirectory()
    {
        return RegistryPaths.Select(registryPath =>
            Registry.GetValue(registryPath, "InstallPath", null) as string
        ).FirstOrDefault(path => path != null && Directory.Exists(path));
    }

    public static string? AppLibraryPath(string appId)
    {
        var steamDir = MainInstallationDirectory();
        if (steamDir == null)
            return null;
        var libraryFoldersPath = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
        var libraryFolders = _readLibraryFolders(libraryFoldersPath);
        return libraryFolders.SingleOrDefault(lf => lf.Apps.ContainsKey(appId))?.Path;
    }

    private static IEnumerable<LibraryFolder> _readLibraryFolders(string path)
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
