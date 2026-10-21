using System.IO.Abstractions;
using Core.Utils;
using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using Microsoft.Win32;

namespace Core.Games;

public static class Steam
{
    private static readonly string[] RegistryPaths =
    {
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
    };

    public static IDirectoryInfo MainInstallationPath(IFileSystem fileSystem)
    {
        try
        {
            return RegistryPaths
                .SelectNotNull(registryPath =>
                    Registry.GetValue(registryPath, "InstallPath", null) as string
                ).Distinct().Select(fileSystem.DirectoryInfo.New).Single(path => path.Exists);
        }
        catch (Exception e)
        {
            throw new Exception("Cannot find Steam installation path", e);
        }
    }

    public static IDirectoryInfo AppLibraryPath(IFileSystem fileSystem, string appId)
    {
        var steamDir = MainInstallationPath(fileSystem);
        var libraryFoldersPath = Path.Combine(steamDir.FullName, "steamapps", "libraryfolders.vdf");
        var gameLibraryFolders = ReadLibraryFolders(fileSystem.FileInfo.New(libraryFoldersPath))
            .Where(lf => lf.Apps.ContainsKey(appId)).ToArray();
        return gameLibraryFolders.Length switch
        {
            0 => throw new Exception($"Cannot find app ID {appId} in any Steam library"),
            1 => fileSystem.DirectoryInfo.New(gameLibraryFolders.Single().Path),
            var count => throw new Exception($"App ID {appId} found in {count} Steam libraries")
        };
    }

    private static IEnumerable<LibraryFolder> ReadLibraryFolders(IFileInfo path)
    {
        var libraryFolders = path.FileSystem.File.ReadAllText(path.FullName);
        var vdfRoot = VdfConvert.Deserialize(libraryFolders);
        return vdfRoot.ToJson().Value
            .SelectMany(_ => _)
            .Select(_ => _.ToObject<LibraryFolder>()!);
    }

    private record LibraryFolder(
        string Path,
        Dictionary<string, string> Apps
    );
}
