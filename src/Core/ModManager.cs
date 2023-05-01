using Core.Games;
using Core.Mods;
using Newtonsoft.Json;
using SevenZipExtractor;

namespace Core;

public class ModManager
{
    private record WorkPaths(
        string EnabledModArchivesDir,
        string DisabledModArchivesDir,
        string TempDir,
        string CurrentStateFile
    );

    private static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfiles.PakfilesDirectory,
        GeneratedBootfiles.PhysicsPersistentPakFileName
    );

    private const string ModsDirName = "Mods";
    private const string EnabledModsDirName = "Enabled";
    private const string DisabledModsSubdir = "Disabled";
    private const string TempDirName = "Temp";
    private const string CurrentStateFileName = "installed.json";

    private const string BootfilesPrefix = "__bootfiles";

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Formatting = Formatting.Indented };

    private readonly WorkPaths workPaths;
    private readonly IGame game;
    private readonly IModFactory modFactory;

    public ModManager(IGame game, IModFactory modFactory)
    {

        this.game = game;
        this.modFactory = modFactory;
        var modsDir = Path.Combine(game.InstallationDirectory, ModsDirName);
        workPaths = new WorkPaths(
            EnabledModArchivesDir: Path.Combine(modsDir, EnabledModsDirName),
            DisabledModArchivesDir: Path.Combine(modsDir, DisabledModsSubdir),
            TempDir: Path.Combine(modsDir, TempDirName),
            CurrentStateFile: Path.Combine(modsDir, CurrentStateFileName)
        );
    }

    private static void AddToEnvionmentPath(string additionalPath)
    {
        const string pathEnvVar = "PATH";
        var env = Environment.GetEnvironmentVariable(pathEnvVar);
        if (env is not null && env.Contains(additionalPath))
        {
            return;
        }
        Environment.SetEnvironmentVariable(pathEnvVar, $"{env};{additionalPath}");
    }

    public List<ModState> FetchState()
    {
        var installedPackageNames = ReadPreviouslyInstalledFiles().Keys.Where(_ => !IsBootFiles(_)).ToHashSet();
        var enabledTuples = ListEnabledModPackages().Select(_ => (_, true));
        var disabledTuples = ListDisabledModPackages().Select(_ => (_, false));
        var availableTuples = enabledTuples.Concat(disabledTuples);
        var availableModsState = availableTuples.Select(_ =>
        {
            var packagePath = _._;
            var isEnabled = _.Item2;
            var packageName = PackageName(packagePath);
            return new ModState(
                PackageName: packageName,
                PackagePath: packagePath,
                IsEnabled:   isEnabled,
                IsInstalled: installedPackageNames.Contains(packageName)
            );
        });
        var availablePackageNames = availableModsState.Select(_ => _.PackageName);
        var unavailablePackageNames = installedPackageNames.Except(availablePackageNames);
        var unavailableModsState = unavailablePackageNames.Select(packageName => new ModState(
                PackageName: packageName,
                PackagePath: null,
                IsEnabled: null,
                IsInstalled: true
            ));
        return unavailableModsState.Concat(availableModsState).ToList();
    }

    public string EnableMod(string packagePath)
    {
        return InstallMod(packagePath, workPaths.EnabledModArchivesDir);
    }

    public string DisableMod(string packagePath)
    {
        return InstallMod(packagePath, workPaths.DisabledModArchivesDir);
    }

    public string InstallMod(string packagePath, string destinationDirectoryPath)
    {
        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(packagePath));
        File.Move(packagePath, destinationFilePath);
        return destinationFilePath;
    }

    public void InstallEnabledMods()
    {
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        CheckGameNotRunning();
        RestoreOriginalState();
        Cleanup();
        InstallAllModFiles();
        Cleanup();
    }

    private void Cleanup()
    {
        if (Directory.Exists(workPaths.TempDir))
        {
            Directory.Delete(workPaths.TempDir, recursive: true);
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
        var modPackages = ListEnabledModPackages();
        var modConfigs = new List<IMod.ConfigEntries>();
        var installedFilesByMod = new Dictionary<string, IReadOnlyCollection<string>>();
        try
        {
            if (modPackages.Any())
            {
                Console.WriteLine("Installing mods:");
                foreach (var packagePath in modPackages)
                {
                    var packageName = Path.GetFileNameWithoutExtension(packagePath);
                    if (IsBootFiles(packageName))
                    {
                        Console.WriteLine($"- {packageName} (skipped)");
                        continue;
                    }

                    Console.WriteLine($"- {packageName}");

                    var mod = ExtractMod(packageName, packagePath);
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
            else
            {
                Console.WriteLine($"No mod archives found in {workPaths.EnabledModArchivesDir}");
            }
        }
        finally
        {
            WriteInstalledFiles(installedFilesByMod);
        }
    }

    private bool IsBootFiles(string packageName) => packageName.StartsWith(BootfilesPrefix);

    private IMod ExtractMod(string packageName, string archivePath)
    {
        var extractionDir = Path.Combine(workPaths.TempDir, packageName);
        using var archiveFile = new ArchiveFile(archivePath);
        archiveFile.Extract(extractionDir);

        return modFactory.ManualInstallMod(packageName, extractionDir);
    }

    private IMod BootfilesMod()
    {
        var bootfilesArchives = Directory.EnumerateFiles(workPaths.EnabledModArchivesDir, $"{BootfilesPrefix}*.*");
        switch (bootfilesArchives.Count())
        {
            case 0:
                Console.WriteLine("Extracting bootfiles from game");
                return modFactory.GeneratedBootfiles(workPaths.TempDir);
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

    private IReadOnlyCollection<string> ListEnabledModPackages() => ListModPackages(workPaths.EnabledModArchivesDir);

    private IReadOnlyCollection<string> ListDisabledModPackages() => ListModPackages(workPaths.DisabledModArchivesDir);

    private IReadOnlyCollection<string> ListModPackages(string path)
    {
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path).ToList();
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    private string PackageName(string archivePath) => Path.GetFileNameWithoutExtension(archivePath);

    private Dictionary<string, IReadOnlyCollection<string>> ReadPreviouslyInstalledFiles() {
        if (!File.Exists(workPaths.CurrentStateFile))
        {
            return new Dictionary<string, IReadOnlyCollection<string>>();
        }

        return JsonConvert
            .DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(File.ReadAllText(workPaths.CurrentStateFile));
    }

    private void WriteInstalledFiles(Dictionary<string, IReadOnlyCollection<string>> filesByMod)
    {
        File.WriteAllText(workPaths.CurrentStateFile, JsonConvert.SerializeObject(filesByMod, JsonSerializerSettings));
    }
}