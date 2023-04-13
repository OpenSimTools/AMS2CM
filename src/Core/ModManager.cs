using Core;
using Newtonsoft.Json;
using SevenZipExtractor;

namespace Core;

public record InstallPaths(
    string ModArchivesPath,
    string GamePath,
    string TempPath,
    string InstalledFilesPath
);

public class ModManager
{
    private const string Ams2SteamId = "1066890";
    private static readonly string Ams2InstallationDir = Path.Combine("steamapps", "common", "Automobilista 2");

    private const string ModsSubdir = "Mods";
    private const string EnabledModsSubdir = "Enabled";

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Formatting = Formatting.Indented };

    private readonly InstallPaths _installPaths;
    private readonly IPostProcessor _postProcessor;


    public static ModManager Init()
    {
        var ams2LibraryPath = Steam.AppLibraryPath(Ams2SteamId);
        if (ams2LibraryPath is null)
        {
            throw new Exception("Cannot find AMS2 on Steam");
        }

        var ams2InstallationDirectory = Path.Combine(ams2LibraryPath, Ams2InstallationDir);
        var modsDir = Path.Combine(ams2InstallationDirectory, ModsSubdir);
        var installPaths = new InstallPaths(
            ModArchivesPath: Path.Combine(modsDir, EnabledModsSubdir),
            GamePath: ams2InstallationDirectory,
            TempPath: Path.Combine(modsDir, "Temp", Guid.NewGuid().ToString()),
            InstalledFilesPath: Path.Combine(modsDir, "installed.json")
        );

        return new ModManager(installPaths);
    }

    private ModManager(InstallPaths installPaths)
    {
        _installPaths = installPaths;
        _postProcessor = new Ams2PostProcessor(installPaths.TempPath, installPaths.GamePath);
    }

    public void InstallEnabledMods()
    {
        RestoreOriginalState();

        if (!Directory.Exists(_installPaths.ModArchivesPath))
        {
            return;
        }

        // Install enabled mods
        var installedMods = InstallAllModFiles();
        WriteInstalledFiles(installedMods);
        _postProcessor.PerformPostProcessing(installedMods);

        // Cleanup
        if (Directory.Exists(_installPaths.TempPath))
        {
            Directory.Delete(_installPaths.TempPath, recursive: true);
        }
    }
    
    private void RestoreOriginalState()
    {
        var previouslyInstalledFiles = ReadPreviouslyInstalledFiles();
        if (!previouslyInstalledFiles.Any())
        {
            Console.WriteLine("No previously installed mods found. Skipping uninstall phase.");
            return;
        }
        Console.WriteLine($"Uninstalling mods:");
        foreach (var (modName, filePaths) in previouslyInstalledFiles)
        {
            Console.WriteLine($"- {modName}");
            JsgmeFileInstaller.RestoreOriginalState(_installPaths.GamePath, filePaths);
        }
    }

    private List<IMod> InstallAllModFiles()
    {
        var installedMods = new List<IMod>();
        var modArchives = Directory.EnumerateFiles(_installPaths.ModArchivesPath).ToList();
        if (!modArchives.Any())
        {
            Console.WriteLine($"No mod archives found in {_installPaths.ModArchivesPath}");
            return installedMods;
        }

        Console.WriteLine("Installing mods:");
        foreach (var filePath in modArchives)
        {
            var packageName = Path.GetFileNameWithoutExtension(filePath);

            Console.WriteLine($"- {packageName}");

            var extractionDir = Path.Combine(_installPaths.TempPath, packageName);
            using var archiveFile = new ArchiveFile(filePath);
            archiveFile.Extract(extractionDir);
            
            var mod = new ManualInstallMod(packageName, extractionDir);
            installedMods.Add(mod);
            mod.Install(_installPaths.GamePath);
        }

        return installedMods;
    }

    private Dictionary<string, IReadOnlyCollection<string>> ReadPreviouslyInstalledFiles() {
        if (!File.Exists(_installPaths.InstalledFilesPath))
        {
            return new Dictionary<string, IReadOnlyCollection<string>>();
        }

        return JsonConvert
            .DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(File.ReadAllText(_installPaths.InstalledFilesPath));
    }

    private void WriteInstalledFiles(IEnumerable<IMod> installedMods)
    {
        var installedFilesByMod = installedMods.ToDictionary(mod => mod.PackageName, mod => mod.InstalledFiles);
        File.WriteAllText(_installPaths.InstalledFilesPath, JsonConvert.SerializeObject(installedFilesByMod, JsonSerializerSettings));
    }
}