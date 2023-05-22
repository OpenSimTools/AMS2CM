using System.Diagnostics;
using Core.Games;
using Core.Mods;
using Newtonsoft.Json;
using SevenZipExtractor;

namespace Core;

internal record InstallPaths(
    string ModArchivesPath,
    string TempPath,
    string InstalledFilesPath
);

public class ModManager
{
    private static readonly string FileRemovedByBootfiles = Path.Combine("Pakfiles", "PHYSICSPERSISTENT.bff");

    private const string ModsSubdir = "Mods";
    private const string EnabledModsSubdir = "Enabled";

    private const string BootfilesPrefix = "__bootfiles";

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Formatting = Formatting.Indented };

    private readonly InstallPaths installPaths;
    private readonly IGame game;
    private readonly IModFactory modFactory;

    public ModManager(IGame game, IModFactory modFactory)
    {
        this.game = game;
        this.modFactory = modFactory;
        var modsDir = Path.Combine(game.InstallationDirectory, ModsSubdir);
        installPaths = new InstallPaths(
            ModArchivesPath: Path.Combine(modsDir, EnabledModsSubdir),
            TempPath: Path.Combine(modsDir, "Temp", Guid.NewGuid().ToString()),
            InstalledFilesPath: Path.Combine(modsDir, "installed.json")
        );
    }

    private static void AddToEnvionmentPath(string additionalPath)
    {
        var env = Environment.GetEnvironmentVariable("PATH");
        if (env is not null && env.Contains(additionalPath))
        {
            return;
        }
        Environment.SetEnvironmentVariable("PATH", $"{env};{additionalPath}");
    }

    public void InstallEnabledMods()
    {
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        CheckGameNotRunning();
        RestoreOriginalState();
        InstallAllModFiles();
        Cleanup();
    }

    private void Cleanup()
    {
        if (Directory.Exists(installPaths.TempPath))
        {
            Directory.Delete(installPaths.TempPath, recursive: true);
        }
    }

    private void RestoreOriginalState()
    {
        var previouslyInstalledFiles = ReadPreviouslyInstalledFiles();
        if (previouslyInstalledFiles.Any())
        {
            Console.WriteLine($"Uninstalling mods:");
            foreach (var (modName, filePaths) in previouslyInstalledFiles)
            {
                Console.WriteLine($"- {modName}");
                JsgmeFileInstaller.RestoreOriginalState(game.InstallationDirectory, filePaths);
            }
        }
        else
        {
            CheckNoBootfilesInstalled();
            Console.WriteLine("No previously installed mods found. Skipping uninstall phase.");
        }
    }

    private void CheckGameNotRunning()
    {
        if (game.IsRunning())
        {
            throw new Exception("The game is running.");
        }
    }

    private void CheckNoBootfilesInstalled()
    {
        if (!File.Exists(Path.Combine(game.InstallationDirectory, FileRemovedByBootfiles)))
        {
            throw new Exception("Bootfiles installed by another tool (e.g. JSGME) have been detected. Please uninstall all mods.");
        }
    }

    private void InstallAllModFiles()
    {
        if (!Directory.Exists(installPaths.ModArchivesPath))
        {
            Console.WriteLine($"No mod archives found in {installPaths.ModArchivesPath}");
            return;
        }
        var modConfigs = new List<IMod.ConfigEntries>();
        var modArchives = Directory.EnumerateFiles(installPaths.ModArchivesPath).ToList();
        var installedFilesByMod = new Dictionary<string, IReadOnlyCollection<string>>();
        try
        {
            if (!modArchives.Any())
            {
                Console.WriteLine($"No mod archives found in {installPaths.ModArchivesPath}");
            }
            else
            {
                Console.WriteLine("Installing mods:");
                foreach (var archivePath in modArchives)
                {
                    var packageName = Path.GetFileNameWithoutExtension(archivePath);
                    if (packageName.StartsWith(BootfilesPrefix))
                    {
                        Console.WriteLine($"- {packageName} (skipped)");
                        continue;
                    }

                    Console.WriteLine($"- {packageName}");

                    var mod = ExtractMod(packageName, archivePath);
                    try
                    {
                        mod.Install(game.InstallationDirectory);
                        modConfigs.Add(mod.Config);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"  Error: {e.Message}");
                    }
                    // Add even partially installed files
                    installedFilesByMod.Add(mod.PackageName, mod.InstalledFiles);
                }

                if (modConfigs.Where(_ => _.NotEmpty()).Any())
                {
                    var bootfilesMod = BootfilesMod();
                    bootfilesMod.Install(game.InstallationDirectory);
                    installedFilesByMod.Add(bootfilesMod.PackageName, bootfilesMod.InstalledFiles);

                    Console.WriteLine("Post-processing:");
                    Console.WriteLine("- Appending crd file entries");
                    PostProcessor.AppendCrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.CrdFileEntries));
                    Console.WriteLine("- Appending trd file entries");
                    PostProcessor.AppendTrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.TrdFileEntries));
                    Console.WriteLine("- Appending driveline records");
                    PostProcessor.AppendDrivelineRecords(game.InstallationDirectory, modConfigs.SelectMany(_ => _.DrivelineRecords));
                }
                else
                {
                    Console.WriteLine("Post-processing not required");
                }
            }
        }
        finally
        {
            WriteInstalledFiles(installedFilesByMod);
        }
    }

    private IMod ExtractMod(string packageName, string archivePath)
    {
        var extractionDir = Path.Combine(installPaths.TempPath, packageName);
        using var archiveFile = new ArchiveFile(archivePath);
        archiveFile.Extract(extractionDir);

        return modFactory.ManualInstallMod(packageName, extractionDir);
    }

    private IMod BootfilesMod()
    {
        var bootfilesArchives = Directory.EnumerateFiles(installPaths.ModArchivesPath, $"{BootfilesPrefix}*.*");
        switch (bootfilesArchives.Count())
        {
            case 0:
                Console.WriteLine("Extracting bootfiles from game");
                return modFactory.GeneratedBootfiles(installPaths.TempPath);
            case 1:
                var archivePath = bootfilesArchives.First();
                var packageName = Path.GetFileNameWithoutExtension(archivePath);
                Console.WriteLine($"Extracting bootfiles from {packageName}");
                return ExtractMod(packageName, archivePath);
            default:
                Console.WriteLine("Multiple bootfiles found:");
                foreach (var bf in bootfilesArchives)
                {
                    Console.WriteLine($"- {bf}");
                }
                throw new Exception("Too many bootfiles found");
        }
    }

    private Dictionary<string, IReadOnlyCollection<string>> ReadPreviouslyInstalledFiles() {
        if (!File.Exists(installPaths.InstalledFilesPath))
        {
            return new Dictionary<string, IReadOnlyCollection<string>>();
        }

        return JsonConvert
            .DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(File.ReadAllText(installPaths.InstalledFilesPath));
    }

    private void WriteInstalledFiles(Dictionary<string, IReadOnlyCollection<string>> filesByMod)
    {
        File.WriteAllText(installPaths.InstalledFilesPath, JsonConvert.SerializeObject(filesByMod, JsonSerializerSettings));
    }
}