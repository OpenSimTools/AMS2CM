using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods.Installation.Installers;

/**
 * Wrapper over mod installer that generates configuration for game or bootfiles.
 */
public class ModInstaller : BaseModInstaller
{
    public interface IConfig
    {
        IEnumerable<string> ExcludedFromConfig
        {
            get;
        }
    }

    private readonly Matcher filesToConfigureMatcher;

    internal ModInstaller(IInstaller inner, ITempDir tempDir, IConfig config) :
        base(inner, tempDir)
    {
        filesToConfigureMatcher = Matchers.ExcludingPatterns(config.ExcludedFromConfig);
    }

    // TODO Generate a better name
    private string NormalisedName => string.Concat(PackageName.Where(char.IsAsciiLetterOrDigit));

    protected override void Install(string dstPath, Action innerInstall)
    {
        innerInstall();

        GenerateModConfig(dstPath);
    }

    private void GenerateModConfig(string dstPath)
    {
        var gameSupportedMod = FileEntriesToConfigure()
            .Any(p => p.StartsWith(PostProcessor.GameSupportedModDirectory));
        var modConfig = gameSupportedMod
            ? ConfigEntries.Empty
            : new ConfigEntries(CrdFileEntries(), TrdFileEntries(), FindDrivelineRecords());
        WriteModConfigFiles(dstPath, modConfig);
    }

    private void WriteModConfigFiles(string dstPath, ConfigEntries modConfig)
    {
        // TODO remove in later bootfiles refactoring
        if (ModUtils.IsBootFiles(PackageName))
            return;
        if (modConfig.None())
            return;
        // TODO this can fail
        var modConfigDirPath = new RootedPath(dstPath, Path.Combine(PostProcessor.GameSupportedModDirectory, NormalisedName));
        Directory.CreateDirectory(modConfigDirPath.Full);
        AddToInstalledFiles(PostProcessor.AppendCrdFileEntries(modConfigDirPath, modConfig.CrdFileEntries));
        AddToInstalledFiles(PostProcessor.AppendTrdFileEntries(modConfigDirPath, modConfig.TrdFileEntries));
        AddToInstalledFiles(PostProcessor.AppendDrivelineRecords(modConfigDirPath, modConfig.DrivelineRecords));
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
        Inner.InstalledFiles.Where(_ => filesToConfigureMatcher.Match(_).HasMatches);

    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        if (!StagingDir.Exists)
        {
            return recordBlocks;
        }

        foreach (var configFile in StagingDir.EnumerateFiles())
        {
            var recordIndent = -1;
            var recordLines = new List<string>();
            foreach (var line in File.ReadAllLines(configFile.FullName))
            {
                // Read each line until we find one with RECORD
                if (recordIndent < 0)
                {
                    recordIndent = line.IndexOf("RECORD", StringComparison.InvariantCulture);
                }
                if (recordIndent < 0)
                {
                    continue;
                }

                // Once it finds a blank line, create a record block and start over
                if (string.IsNullOrWhiteSpace(line))
                {
                    recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
                    recordIndent = -1;
                    recordLines.Clear();
                    continue;
                }

                // Otherwise add the line to the current record lines
                var lineNoIndent = line.Substring(recordIndent).TrimEnd();
                recordLines.Add(lineNoIndent);
            }

            // Create a record block also if the file finshed on a record line
            if (recordIndent >= 0)
            {
                recordBlocks.Add(string.Join(Environment.NewLine, recordLines));
            }
        }

        return recordBlocks;
    }

}
