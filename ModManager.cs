using Newtonsoft.Json;
using SevenZipExtractor;

namespace AMS2CM;

using ModFileList = Dictionary<string, IReadOnlyCollection<string>>;

public class ModManager
{
    private const string Ams2SteamId = "1066890";
    private static readonly string Ams2InstallationDir = Path.Combine("steamapps", "common", "Automobilista 2");

    private const string BackupFileSuffix = ".orig";
    private const string JSGMERemoveFileSuffix = "-remove";

    private const string ExcludeModPostProcessingPrefix = "__";
    private static readonly string[] ExcludeFilePostProcessing =
    {
        // IndyCar 2023
        "IR-18_2023_My_Team.crd",
        "IR-18_2023_Dale_Coyne_hr.crd"
    };
    private static readonly string[] ExcludeCopySuffix =
    {
        BackupFileSuffix
    };

    private static readonly string[] DirsAtRootLowerCase =
        {
            "cameras",
            "characters",
            "effects",
            "gui",
            "pakfiles",
            "render",
            "text",
            "tracks",
            "upgrade",
            "vehicles"
        };

    private const string ModsSubdir = "Mods";
    private const string EnabledModsSubdir = "Enabled";
    private static readonly string DisabledModsSubdir = "Disabled";
    
    private static readonly JsonSerializerSettings JsonSerializerSettings = new() { Formatting = Formatting.Indented };

    private readonly string _tempPath;
    private readonly string _enabledModsPath;
    private readonly string _installedListFilePath;
    private readonly string _ams2InstallationDirectory;
    private readonly string _driveLineFilePath;
    private readonly string _vehicleListFilePath;
    private readonly string _trackListFilePath;

    public static ModManager Init()
    {
        var ams2LibraryPath = Steam.AppLibraryPath(Ams2SteamId);
        if (ams2LibraryPath is null)
            throw new Exception("Cannot find AMS2 on Steam");
        var ams2InstallationDirectory = Path.Combine(ams2LibraryPath, Ams2InstallationDir);
        return new ModManager(ams2InstallationDirectory);
    }

    private ModManager(string ams2InstallationDirectory)
    {
        var modsDir = Path.Combine(ams2InstallationDirectory, ModsSubdir);
        _tempPath = Path.Combine(modsDir, "Temp", Guid.NewGuid().ToString());
        _enabledModsPath = Path.Combine(modsDir, EnabledModsSubdir);
        _installedListFilePath = Path.Combine(modsDir, "installed.json");
        _ams2InstallationDirectory = ams2InstallationDirectory;
        _driveLineFilePath = Path.Combine(ams2InstallationDirectory, "vehicles", "physics", "driveline", "driveline.rg");
        _vehicleListFilePath = Path.Combine(ams2InstallationDirectory, "vehicles", "vehiclelist.lst");
        _trackListFilePath = Path.Combine(ams2InstallationDirectory, "tracks", "_data", "tracklist.lst");
    }

    public void InstallEnabledMods()
    {
        RestoreOriginalState();

        if (!Directory.Exists(_enabledModsPath))
            return;

        // Install enabled mods
        var installedFiles = InstallAllModFiles();
        WriteInstalledFiles(installedFiles);
        PerformPostProcessing(installedFiles);

        // Cleanup
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }

    private void RestoreOriginalState()
    {
        Console.WriteLine($"Restoring original state");
        foreach (var filePath in ReadPreviouslyInstalledFiles())
        {
            var dstFilePath = Path.Combine(_ams2InstallationDirectory, filePath);
            if (File.Exists(dstFilePath))
                File.Delete(dstFilePath);
            RestoreFile(dstFilePath);
        }
    }

    private ModFileList InstallAllModFiles()
    {
        var installedFiles = new ModFileList();
        var modArchives = Directory.EnumerateFiles(_enabledModsPath);
        foreach (var filePath in modArchives)
        {
            var modName = Path.GetFileNameWithoutExtension(filePath);

            Console.WriteLine($"Installing mod {modName}");

            var extractionDir = Path.Combine(_tempPath, modName);
            using var archiveFile = new ArchiveFile(filePath);
            archiveFile.Extract(extractionDir);

            var modRootDir = FindModRootDir(extractionDir);
            if (modRootDir is null)
                continue;
            Console.WriteLine($"Contents found at {modRootDir}");

            var installedModFiles = MoveAllWithBackup(modRootDir, _ams2InstallationDirectory);
            var relativeModFiles = installedModFiles.Select(fp => Path.GetRelativePath(modRootDir, fp)).ToList();
            installedFiles.Add(modName, relativeModFiles);
        }

        return installedFiles;
    }

    private static IReadOnlyCollection<string> MoveAllWithBackup(string srcPath, string dstPath)
    {
        var movedFiles = new List<string>();
        if (!Directory.Exists(dstPath))
            Directory.CreateDirectory(dstPath);
        foreach (var maybeSrcSubPath in Directory.GetFileSystemEntries(srcPath))
        {
            var (srcSubPath, remove) = NeedsRemoving(maybeSrcSubPath);
            var localName = Path.GetFileName(srcSubPath);
            if (ExcludeCopySuffix.Any(suffix => localName.EndsWith(suffix)))
            {
                Console.WriteLine($"Skipping {localName}");
                continue;
            }

            var dstSubPath = Path.Combine(dstPath, localName);
            if (Directory.Exists(srcSubPath)) // Is directory
            {
                movedFiles.AddRange(MoveAllWithBackup(srcSubPath, dstSubPath));
                continue;
            }

            if (File.Exists(dstSubPath))
                BackupFile(dstSubPath);
            if (!remove)
                File.Move(srcSubPath, dstSubPath);
            movedFiles.Add(srcSubPath);
        }

        return movedFiles;
    }

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(JSGMERemoveFileSuffix) ?
            (filePath.RemoveSuffix(JSGMERemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }

    private static string? FindModRootDir(string path)
    {
        foreach (var dirPath in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            var dirName = Path.GetFileName(dirPath).ToLowerInvariant();
            if (DirsAtRootLowerCase.Contains(dirName))
            {
                return Path.GetDirectoryName(dirPath);
            }
        }

        Console.WriteLine("No content found");
        return null;
    }

    private void PerformPostProcessing(ModFileList filesToInstall)
    {
        Console.WriteLine("Post processing");

        var crdFileEntries = new List<string>();
        var trdFileEntries = new List<string>();
        var recordBlocks = new List<string>();

        foreach (var (modName, files) in filesToInstall)
        {
            if (modName.StartsWith(ExcludeModPostProcessingPrefix))
            {
                Console.WriteLine($"- {modName} (skipped)");
                continue;
            }
            Console.WriteLine($"- {modName}");

            crdFileEntries.AddRange(CrdFileEntries(files));
            trdFileEntries.AddRange(TrdFileEntries(files));

            var modExtractionPath = Path.Combine(_tempPath, modName);
            recordBlocks.AddRange(FindRecordBlocks(modExtractionPath));
        }

        AppendCrdFileEntries(crdFileEntries);
        AppendTrdFileEntries(trdFileEntries);
        InsertRecordBlocks(recordBlocks);
    }
    
    private static IEnumerable<string> CrdFileEntries(IEnumerable<string> modFiles)
    {
        return modFiles
            .Where(p => ExcludeFilePostProcessing.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".crd"));
    }
    
    private static IEnumerable<string> TrdFileEntries(IEnumerable<string> modFiles)
    {
        return modFiles
            .Where(p => ExcludeFilePostProcessing.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}");
    }
    
    private void AppendCrdFileEntries(IEnumerable<string> crdFileEntries)
    {
        AppendEntryList(_vehicleListFilePath, crdFileEntries);
    }

    private void AppendTrdFileEntries(IEnumerable<string> trdFileEntries)
    {
        AppendEntryList(_trackListFilePath, trdFileEntries);
    }

    private static void AppendEntryList(string path, IEnumerable<string> entries)
    {
        var entriesBlock = string.Join(null, entries.Select(f => $"{Environment.NewLine}{f}"));
        if (!entriesBlock.Any()) return;

        var f = File.AppendText(path);
        f.Write(entriesBlock);
        f.Close();
    }

    private IEnumerable<string> FindRecordBlocks(string modName)
    {
        var recordBlocks = new List<string>();
        var modRoot = Path.Combine(_tempPath, modName);
        foreach (var fileAtModRoot in Directory.EnumerateFiles(modRoot))
        {
            var recordIndent = -1;
            var recordLines = new List<string>();
            foreach (var line in File.ReadAllLines(fileAtModRoot))
            {
                if (recordIndent < 0)
                {
                    recordIndent = line.IndexOf("RECORD", StringComparison.InvariantCulture);
                }

                if (recordIndent < 0)
                    continue;
                if (string.IsNullOrWhiteSpace(line))
                {
                    recordIndent = -1;
                    recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
                    recordLines.Clear();
                    continue;
                }
                var lineNoIndent = line.Substring(recordIndent);
                recordLines.Add(lineNoIndent);
            }

            if (recordIndent >= 0)
                recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
        }

        return recordBlocks;
    }
    
    private void InsertRecordBlocks(IEnumerable<string> recordBlocks)
    {
        var recordsBlock = string.Join(null, recordBlocks.Select(rb => $"{rb}{Environment.NewLine}{Environment.NewLine}"));
        if (!recordsBlock.Any()) return;

        var contents = File.ReadAllText(_driveLineFilePath);
        var endIndex = contents.LastIndexOf("END", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            Console.WriteLine("Could not find insertion point in driveline file");
            return;
        }
        var newContents = contents.Insert(endIndex, recordsBlock);
        File.WriteAllText(_driveLineFilePath, newContents);
    }

    private IEnumerable<string> ReadPreviouslyInstalledFiles() {
        if (!File.Exists(_installedListFilePath))
            return Array.Empty<string>();
        return JsonConvert
            .DeserializeObject<ModFileList>(File.ReadAllText(_installedListFilePath))
            .Values.SelectMany(_ => _);
    }

    private void WriteInstalledFiles(ModFileList installedFiles)
    {
        File.WriteAllText(_installedListFilePath, JsonConvert.SerializeObject(installedFiles, JsonSerializerSettings));
    }
    
    private static void BackupFile(string path)
    {
        var backupFile = BackupFileName(path);
        if (File.Exists(backupFile))
        {
            Console.WriteLine($"Backup file already exists: {backupFile}");
            File.Delete(path);
        }
        else
        {
            File.Move(path, backupFile);
        }
    }
    
    private static void RestoreFile(string path)
    {
        var backupFilePath = BackupFileName(path);
        if (File.Exists(backupFilePath))
            File.Move(backupFilePath, path);
    }

    private static string BackupFileName(string originalFileName) => $"{originalFileName}{BackupFileSuffix}";
}