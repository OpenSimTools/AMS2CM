using System.Collections.Immutable;
using System.Linq;
using Core.Games;
using Core.Mods;
using Core.Utils;
using Newtonsoft.Json;
using SevenZipExtractor;
using static Core.IModManager;
using static Core.Mods.JsgmeFileInstaller;

namespace Core;

public class ModManager : IModManager
{
    private record WorkPaths(
        string EnabledModArchivesDir,
        string DisabledModArchivesDir,
        string TempDir,
        string StateFile,
        string OldStateFile
    );

    private record InternalState(
        InternalInstallationState Install
    )
    {
        public static InternalState Empty() => new(
            Install: InternalInstallationState.Empty()
        );
    };

    private record InternalInstallationState(
        DateTime? Time,
        IReadOnlyDictionary<string, InternalModInstallationState> Mods
    )
    {
        public static InternalInstallationState Empty() => new (
            Time: null,
            Mods: ImmutableDictionary.Create<string, InternalModInstallationState>()
        );
    };

    private record InternalModInstallationState(
        bool Partial,
        IReadOnlyCollection<string> Files
    );

    private static readonly string FileRemovedByBootfiles = Path.Combine(
        GeneratedBootfiles.PakfilesDirectory,
        GeneratedBootfiles.PhysicsPersistentPakFileName
    );

    private const string ModsDirName = "Mods";
    private const string EnabledModsDirName = "Enabled";
    private const string DisabledModsSubdir = "Disabled";
    private const string TempDirName = "Temp";
    private const string StateFileName = "state.json";
    private const string OldStateFileName = "installed.json";

    private const string BootfilesPrefix = "__bootfiles";

    private static readonly JsonSerializerSettings JsonSerializerSettings = new() {
        Formatting = Formatting.None,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    private readonly WorkPaths workPaths;
    private readonly IGame game;
    private readonly IModFactory modFactory;

    public event LogHandler? Logs;

    public ModManager(IGame game, IModFactory modFactory)
    {
        this.game = game;
        this.modFactory = modFactory;
        var modsDir = Path.Combine(game.InstallationDirectory, ModsDirName);
        workPaths = new WorkPaths(
            EnabledModArchivesDir: Path.Combine(modsDir, EnabledModsDirName),
            DisabledModArchivesDir: Path.Combine(modsDir, DisabledModsSubdir),
            TempDir: Path.Combine(modsDir, TempDirName),
            StateFile: Path.Combine(modsDir, StateFileName),
            OldStateFile: Path.Combine(modsDir, OldStateFileName)
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
        var installedMods = ReadState().Install.Mods;
        var enabledModPaths = ListEnabledModPackages().ToDictionary(PackageName);
        var disabledModPaths = ListDisabledModPackages().ToDictionary(PackageName);

        var bootfilesFailed = installedMods.Where(kv => IsBootFiles(kv.Key) && (kv.Value?.Partial ?? false)).Any();
        var availableModPaths = enabledModPaths.Merge(disabledModPaths);
        var isModInstalled = installedMods.SelectValues<string, InternalModInstallationState, bool?>(modInstallationState =>
            modInstallationState is null ? false : ((modInstallationState.Partial || bootfilesFailed) ? null : true)
        );

        var allModNames = installedMods.Keys
            .Concat(enabledModPaths.Keys)
            .Concat(disabledModPaths.Keys)
            .Where(_ => !IsBootFiles(_))
            .Distinct();

        return allModNames
            .Select(packageName => {
                string? packagePath;
                bool? isInstalled;
                return new ModState(
                    PackageName: packageName,
                    PackagePath: availableModPaths.TryGetValue(packageName, out packagePath) ? packagePath : null,
                    IsInstalled: isModInstalled.TryGetValue(packageName, out isInstalled) ? isInstalled : false,
                    IsEnabled: enabledModPaths.Keys.Contains(packageName)
                );
            }).ToList();
    }

    public ModState EnableNewMod(string packagePath)
    {
        var destinationDirectoryPath = workPaths.EnabledModArchivesDir;
        ExistingDirectoryOrCreate(destinationDirectoryPath);
        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(packagePath));
        File.Copy(packagePath, destinationFilePath);
        return new ModState(
                PackageName: PackageName(destinationFilePath),
                PackagePath: destinationFilePath,
                IsEnabled: true,
                IsInstalled: false
            );
    }

    public string EnableMod(string packagePath)
    {
        return MoveMod(packagePath, workPaths.EnabledModArchivesDir);
    }

    public string DisableMod(string packagePath)
    {
        return MoveMod(packagePath, workPaths.DisabledModArchivesDir);
    }

    private string MoveMod(string packagePath, string destinationDirectoryPath)
    {
        ExistingDirectoryOrCreate(destinationDirectoryPath);
        var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(packagePath));
        File.Move(packagePath, destinationFilePath);
        return destinationFilePath;
    }

    private static void ExistingDirectoryOrCreate(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public void InstallEnabledMods(CancellationToken cancellationToken = default)
    {
        // It shoulnd't be needed, but some systems seem to want to load oo2core
        // even when Mermaid and Kraken compression are not used in pak files!
        AddToEnvionmentPath(game.InstallationDirectory);

        CheckGameNotRunning();
        RestoreOriginalState();
        Cleanup();
        InstallAllModFiles(cancellationToken);
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
        var previousInstallation = ReadState().Install;
        if (previousInstallation.Mods.Any())
        {
            Logs?.Invoke($"Uninstalling mods:");
            foreach (var (modName, modInstallationState) in previousInstallation.Mods)
            {
                Logs?.Invoke($"- {modName}");
                JsgmeFileInstaller.RestoreOriginalState(
                    game.InstallationDirectory,
                    modInstallationState.Files,
                    SkipCreatedAfter(previousInstallation.Time)
                );
            }
        }
        else
        {
            CheckNoBootfilesInstalled();
            Logs?.Invoke("No previously installed mods found. Skipping uninstall phase.");
        }
    }

    private ShouldSkipFile SkipCreatedAfter(DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null)
        {
            return _ => false;
        }

        return path =>
        {
            var exclude = File.GetCreationTimeUtc(path) > dateTimeUtc;
            if (exclude)
            {
                Logs?.Invoke($"  Skipping {path}");
            }
            return exclude;
        };
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

    private void InstallAllModFiles(CancellationToken cancellationToken)
    {
        var modPackages = ListEnabledModPackages();
        var modConfigs = new List<IMod.ConfigEntries>();
        var installedFilesByMod = new Dictionary<string, InternalModInstallationState>();
        try
        {
            if (modPackages.Any())
            {
                Logs?.Invoke("Installing mods:");
                foreach (var packagePath in modPackages)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var packageName = Path.GetFileNameWithoutExtension(packagePath);
                    if (IsBootFiles(packageName))
                    {
                        Logs?.Invoke($"- {packageName} (skipped)");
                        continue;
                    }

                    Logs?.Invoke($"- {packageName}");

                    var mod = ExtractMod(packageName, packagePath);
                    try
                    {
                        mod.Install(game.InstallationDirectory);
                        modConfigs.Add(mod.Config);
                    }
                    catch (Exception e)
                    {
                        Logs?.Invoke($"  Error: {e.Message}");
                    }
                    installedFilesByMod.Add(mod.PackageName, new(
                        Partial: mod.Installed == IMod.InstalledState.PartiallyInstalled,
                        Files: mod.InstalledFiles
                    ));
                }

                if (modConfigs.Where(_ => _.NotEmpty()).Any())
                {
                    var bootfilesMod = BootfilesMod();
                    var postProcessingDone = false;
                    try
                    {
                        bootfilesMod.Install(game.InstallationDirectory);
                        Logs?.Invoke("Post-processing:");
                        Logs?.Invoke("- Appending crd file entries");
                        PostProcessor.AppendCrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.CrdFileEntries));
                        Logs?.Invoke("- Appending trd file entries");
                        PostProcessor.AppendTrdFileEntries(game.InstallationDirectory, modConfigs.SelectMany(_ => _.TrdFileEntries));
                        Logs?.Invoke("- Appending driveline records");
                        PostProcessor.AppendDrivelineRecords(game.InstallationDirectory, modConfigs.SelectMany(_ => _.DrivelineRecords));
                        postProcessingDone = true;
                    }
                    catch (Exception e)
                    {
                        Logs?.Invoke($"  Error: {e.Message}");
                    }
                    installedFilesByMod.Add(bootfilesMod.PackageName, new(
                        Partial: bootfilesMod.Installed == IMod.InstalledState.PartiallyInstalled || !postProcessingDone,
                        Files: bootfilesMod.InstalledFiles
                    ));
                }
                else
                {
                    Logs?.Invoke("Post-processing not required");
                }
            }
            else
            {
                Logs?.Invoke($"No mod archives found in {workPaths.EnabledModArchivesDir}");
            }
        }
        finally
        {
            var newState = new InternalState(
                Install: new(
                    Time: DateTime.UtcNow,
                    Mods: installedFilesByMod
                )
            );
            WriteState(newState);
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
                Logs?.Invoke("Extracting bootfiles from game");
                return modFactory.GeneratedBootfiles(workPaths.TempDir);
            case 1:
                var archivePath = bootfilesArchives.First();
                var packageName = Path.GetFileNameWithoutExtension(archivePath);
                Logs?.Invoke($"Extracting bootfiles from {packageName}");
                return ExtractMod(packageName, archivePath);
            default:
                Logs?.Invoke("Multiple bootfiles found:");
                foreach (var bf in bootfilesArchives)
                {
                    Logs?.Invoke($"- {bf}");
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
            var options = new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                MaxRecursionDepth = 0,
            };
            return Directory.EnumerateFiles(path, "*", options).ToList();
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    private string PackageName(string archivePath) => Path.GetFileNameWithoutExtension(archivePath);

    private InternalState ReadState()
    {
        if (File.Exists(workPaths.StateFile))
        {
            var contents = File.ReadAllText(workPaths.StateFile);
            return JsonConvert.DeserializeObject<InternalState>(contents);
        }
        // Fallback to old state when new state is not present
        if (File.Exists(workPaths.OldStateFile))
        {
            var contents = File.ReadAllText(workPaths.OldStateFile);
            var oldState = JsonConvert.DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(contents);
            var installTime = File.GetLastWriteTimeUtc(workPaths.OldStateFile);
            return new InternalState(
                Install: new(
                    Time: installTime,
                    Mods: oldState.AsEnumerable().ToDictionary(
                        kv => kv.Key,
                        kv => new InternalModInstallationState(false, kv.Value)
                    )
                )
            );
        }
        return InternalState.Empty();
    }

    private void WriteState(InternalState state)
    {
        // Write old state if it exists
        if (File.Exists(workPaths.OldStateFile))
        {
            var oldState = state.Install.Mods.ToDictionary(kv => kv.Key, kv => kv.Value.Files);
            File.WriteAllText(workPaths.OldStateFile, JsonConvert.SerializeObject(oldState, JsonSerializerSettings));
        }
        File.WriteAllText(workPaths.StateFile, JsonConvert.SerializeObject(state, JsonSerializerSettings));
    }
}