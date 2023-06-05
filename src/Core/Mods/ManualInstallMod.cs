using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods;

public class ManualInstallMod : ExtractedMod
{
    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot { get; }
        IEnumerable<string> ExcludedFromConfig { get; }
    }

    private readonly Matcher filesToConfigureMatcher;
    private readonly List<string> dirsAtRootLowerCase;

    internal ManualInstallMod(string packageName, string extractedPath, IConfig config)
        : base(packageName, extractedPath)
    {
        dirsAtRootLowerCase = config.DirsAtRoot.Select(dir => dir.ToLowerInvariant()).ToList();
        filesToConfigureMatcher = MatcherExcluding(config.ExcludedFromConfig);
    }

    private static Matcher MatcherExcluding(IEnumerable<string> exclusions)
    {
        var matcher = new Matcher();
        matcher.AddInclude(@"**\*");
        matcher.AddExcludePatterns(exclusions);
        return matcher;
    }

    protected override IEnumerable<string> ExtractedRootDirs()
    {
        return FindRootContaining(extractedPath, dirsAtRootLowerCase);
    }

    private static List<string> FindRootContaining(string path, IEnumerable<string> contained)
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

    private List<string> CrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".crd"))
            .ToList();
    
    private List<string> TrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}")
            .ToList();

    private IEnumerable<string> FileEntriesToConfigure() =>
        installedFiles.Where(_ => filesToConfigureMatcher.Match(_).HasMatches);

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
                var lineNoIndent = line.Substring(recordIndent).TrimEnd();
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