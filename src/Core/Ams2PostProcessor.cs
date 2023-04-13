namespace Core;

public class Ams2PostProcessor : IPostProcessor
{
    private const string ExcludeModPostProcessingPrefix = "__";
    private static readonly string[] ExcludeFilePostProcessing =
    {
        // IndyCar 2023
        "IR-18_2023_My_Team.crd",
        "IR-18_2023_Dale_Coyne_hr.crd"
    };

    private readonly string _extractionPath;
    private readonly string _driveLineFilePath;
    private readonly string _vehicleListFilePath;
    private readonly string _trackListFilePath;

    public Ams2PostProcessor(string extractionPath, string gamePath)
    {
        _extractionPath = extractionPath;
        _driveLineFilePath = Path.Combine(gamePath, "vehicles", "physics", "driveline", "driveline.rg");
        _vehicleListFilePath = Path.Combine(gamePath, "vehicles", "vehiclelist.lst");
        _trackListFilePath = Path.Combine(gamePath, "tracks", "_data", "tracklist.lst");
    }
    
    public void PerformPostProcessing(List<IMod> installedMods)
    {
        Console.WriteLine("Post-processing:");

        var crdFileEntries = new List<string>();
        var trdFileEntries = new List<string>();
        var recordBlocks = new List<string>();

        foreach (var mod in installedMods)
        {
            if (mod.PackageName.StartsWith(ExcludeModPostProcessingPrefix))
            {
                Console.WriteLine($"- {mod.PackageName} (skipped)");
                continue;
            }
            Console.WriteLine($"- {mod.PackageName}");

            crdFileEntries.AddRange(CrdFileEntries(mod.InstalledFiles));
            trdFileEntries.AddRange(TrdFileEntries(mod.InstalledFiles));

            var modExtractionPath = Path.Combine(_extractionPath, mod.PackageName);
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
        if (!entriesBlock.Any())
        {
            return;
        }

        var f = File.AppendText(path);
        f.Write(entriesBlock);
        f.Close();
    }

    private IEnumerable<string> FindRecordBlocks(string modName)
    {
        var recordBlocks = new List<string>();
        var modRoot = Path.Combine(_extractionPath, modName);
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
                {
                    continue;
                }

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
            {
                recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
            }
        }

        return recordBlocks;
    }
    
    private void InsertRecordBlocks(IEnumerable<string> recordBlocks)
    {
        var recordsBlock = string.Join(null, recordBlocks.Select(rb => $"{rb}{Environment.NewLine}{Environment.NewLine}"));
        if (!recordsBlock.Any())
        {
            return;
        }

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

}