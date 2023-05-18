using System.Diagnostics;
using Core.Mods;
using Newtonsoft.Json;
using SevenZipExtractor;

namespace Core;

internal record InstallPaths(
    string ModArchivesPath,
    string GamePath,
    string TempPath,
    string InstalledFilesPath
);

public class ModManager
{
    private const string Ams2SteamId = "1066890";
    private const string Ams2ProcessName = "AMS2AVX";
    private static readonly string Ams2InstallationDir = Path.Combine("steamapps", "common", "Automobilista 2");
    private static readonly string FileRemovedByBootfiles = Path.Combine("Pakfiles", "PHYSICSPERSISTENT.bff");

    private const string ModsSubdir = "Mods";
    private const string EnabledModsSubdir = "Enabled";

    private const string BootfilesPrefix = "__bootfiles";

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Formatting = Formatting.Indented };

    private readonly InstallPaths installPaths;

    public static ModManager Init()
    {
        var ams2LibraryPath = Steam.AppLibraryPath(Ams2SteamId);
        if (ams2LibraryPath is null)
        {
            throw new Exception("Cannot find AMS2 on Steam");
        }

        var ams2InstallationDirectory = Path.Combine(ams2LibraryPath, Ams2InstallationDir);
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(ams2InstallationDirectory);
        var modsDir = Path.Combine(ams2InstallationDirectory, ModsSubdir);
        var installPaths = new InstallPaths(
            ModArchivesPath: Path.Combine(modsDir, EnabledModsSubdir),
            GamePath: ams2InstallationDirectory,
            TempPath: Path.Combine(modsDir, "Temp", Guid.NewGuid().ToString()),
            InstalledFilesPath: Path.Combine(modsDir, "installed.json")
        );

        return new ModManager(installPaths);
    }

    private static void AddToEnvionmentPath(string additionalPath)
    {
        var env = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH",  $"{env};{additionalPath}");
    }

    private ModManager(InstallPaths installPaths)
    {
        this.installPaths = installPaths;
    }

    public void InstallEnabledMods()
    {
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
                JsgmeFileInstaller.RestoreOriginalState(installPaths.GamePath, filePaths);
            }
        }
        else
        {
            CheckNoBootfilesInstalled();
            Console.WriteLine("No previously installed mods found. Skipping uninstall phase.");
        }
    }

    private static void CheckGameNotRunning()
    {
        if (Process.GetProcesses().Any(_ => _.ProcessName == Ams2ProcessName))
        {
            throw new Exception("The game is running.");
        }
    }

    private void CheckNoBootfilesInstalled()
    {
        if (!File.Exists(Path.Combine(installPaths.GamePath, FileRemovedByBootfiles)))
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
                        mod.Install(installPaths.GamePath);
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
                    bootfilesMod.Install(installPaths.GamePath);
                    installedFilesByMod.Add(bootfilesMod.PackageName, bootfilesMod.InstalledFiles);

                    Console.WriteLine("Post-processing:");
                    Console.WriteLine("- Appending crd file entries");
                    PostProcessor.AppendCrdFileEntries(installPaths.GamePath, modConfigs.SelectMany(_ => _.CrdFileEntries));
                    Console.WriteLine("- Appending trd file entries");
                    PostProcessor.AppendTrdFileEntries(installPaths.GamePath, modConfigs.SelectMany(_ => _.TrdFileEntries));
                    Console.WriteLine("- Appending driveline records");
                    PostProcessor.AppendDrivelineRecords(installPaths.GamePath, modConfigs.SelectMany(_ => _.DrivelineRecords));
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

        return new ManualInstallMod(packageName, extractionDir);
    }

    private IMod BootfilesMod()
    {
        var bootfilesArchives = Directory.EnumerateFiles(installPaths.ModArchivesPath, $"{BootfilesPrefix}*.*");
        switch (bootfilesArchives.Count())
        {
            case 0:
                Console.WriteLine("Extracting bootfiles from game");
                return GenerateBootfiles();
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

    private IMod GenerateBootfiles()
    {
        return new GeneratedBootfiles(installPaths.GamePath, installPaths.TempPath);
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