namespace Core.Mods;

public class ManualInstallMod : IMod
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
        "upgrade",
        "vehicles"
    };
    private const string ConfigExcludeModPrefix = "__";
    private static readonly string[] ConfigExcludeFile =
    {
        // IndyCar 2023
        "IR-18_2023_My_Team.crd",
        "IR-18_2023_Dale_Coyne_hr.crd"
    };
    
    private readonly string _extractedPath;
    private readonly List<string> _installedFiles = new();

    public ManualInstallMod(string packageName, string extractedPath)
    {
        PackageName = packageName;
        Config = new IMod.ConfigEntries(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        _extractedPath = extractedPath;
    }

    public string PackageName
    {
        get;
    }

    public IMod.InstalledState Installed
    {
        get;
        private set;
    }
    
    public IMod.ConfigEntries Config
    {
        get;
        private set;
    }

    public IReadOnlyCollection<string> InstalledFiles => _installedFiles;
    
    public void Install(string dstPath)
    {
        if (Installed != IMod.InstalledState.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IMod.InstalledState.PartiallyInstalled;

        foreach (var rootPath in FindModRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                relativeFilePath => _installedFiles.Add(relativeFilePath));
        }

        if (!PackageName.StartsWith(ConfigExcludeModPrefix))
        {
            Config = new IMod.ConfigEntries(CrdFileEntries(), TrdFileEntries(), FindDrivelineRecords());
        }
        
        Installed = IMod.InstalledState.Installed;
    }

    private IEnumerable<string> FindModRootDirs()
    {
        return FindRootContaining(_extractedPath, DirsAtRootLowerCase);
    }
    
    private static List<string> FindRootContaining(string path, string[] contained)
    {
        var roots = new List<string>();
        foreach (var subdir in Directory.GetDirectories(path))
        {
            var localName = Path.GetFileName(subdir).ToLowerInvariant();
            if (contained.Contains(localName))
            {
                return new List<string> {path};
            }
            roots.AddRange(FindRootContaining(subdir, contained));
        }

        return roots;
    }
    
    private List<string> CrdFileEntries()
    {
        return _installedFiles
            .Where(p => ConfigExcludeFile.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".crd"))
            .ToList();
    }
    
    private List<string> TrdFileEntries()
    {
        return _installedFiles
            .Where(p => ConfigExcludeFile.All(excluded => Path.GetFileName(p) != excluded))
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}")
            .ToList();
    }
    
    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        foreach (var fileAtModRoot in Directory.EnumerateFiles(_extractedPath))
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