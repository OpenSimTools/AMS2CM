namespace Core.Mods;

internal class ManualInstallMod : ExtractedMod
{
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
        "userdata",
        "upgrade",
        "vehicles"
    };

    private static readonly string[] ConfigExcludeFile =
    {
        // IndyCar 2023
        "IR-18_2023_My_Team.crd",
        "IR-18_2023_Dale_Coyne_hr.crd"
    };

    public ManualInstallMod(string packageName, string extractedPath)
        : base(packageName, extractedPath)
    {
    }

    protected override IEnumerable<string> ExtractedRootDirs()
    {
        return FindRootContaining(extractedPath, DirsAtRootLowerCase);
    }

    private static List<string> FindRootContaining(string path, string[] contained)
    {
        var roots = new List<string>();
        foreach (var subdir in Directory.GetDirectories(path))
        {
            var localName = Path.GetFileName(subdir).ToLowerInvariant();
            if (contained.Contains(localName))
            {
                return new List<string> { path };
            }
            roots.AddRange(FindRootContaining(subdir, contained));
        }

        return roots;
    }

    protected override IMod.ConfigEntries GenerateConfig() =>
        new(CrdFileEntries(), TrdFileEntries(), FindDrivelineRecords());

    private List<string> CrdFileEntries()
    {
        return installedFiles
            .Where(p => ConfigExcludeFile.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".crd"))
            .ToList();
    }
    
    private List<string> TrdFileEntries()
    {
        return installedFiles
            .Where(p => ConfigExcludeFile.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}")
            .ToList();
    }
    
    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        foreach (var fileAtModRoot in Directory.EnumerateFiles(extractedPath))
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
}