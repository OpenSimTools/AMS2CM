using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods;

public class ManualInstallMod : ExtractedMod
{
    internal static readonly string GameSupportedModDirectory = Path.Combine("UserData", "Mods");

    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot { get; }
        IEnumerable<string> ExcludedFromInstall { get; }
        IEnumerable<string> ExcludedFromConfig { get; }
    }

    private readonly Matcher filesToInstallMatcher;
    private readonly Matcher filesToConfigureMatcher;
    private readonly IRootFinder rootFinder;

    internal ManualInstallMod(string packageName, int packageFsHash, string extractedPath, IConfig config)
        : base(packageName, packageFsHash, extractedPath)
    {
        rootFinder = new RootFinderFromContainedDirs(config.DirsAtRoot);
        filesToInstallMatcher = MatcherExcluding(config.ExcludedFromInstall);
        filesToConfigureMatcher = MatcherExcluding(config.ExcludedFromConfig);
    }

    private static Matcher MatcherExcluding(IEnumerable<string> exclusions)
    {
        var matcher = new Matcher();
        matcher.AddInclude(@"**\*");
        matcher.AddExcludePatterns(exclusions);
        return matcher;
    }

    protected override IEnumerable<string> ExtractedRootDirs() =>
        rootFinder.FromFileList(AllFiles(extractedPath).Select(_ => _.FullName));

    protected override ConfigEntries GenerateConfig()
    {
        var gameSupportedMod = FileEntriesToConfigure()
            .Where(p => p.StartsWith(GameSupportedModDirectory))
            .Any();
        return gameSupportedMod
            ? ConfigEntries.Empty
            : new(CrdFileEntries(), TrdFileEntries(), FindDrivelineRecords());
    }

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

    protected override bool FileShouldBeInstalled(GamePath path) =>
        filesToInstallMatcher.Match(path.Relative).HasMatches;
}