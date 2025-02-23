using System.Collections.Immutable;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods.Installation.Installers;

/**
 * Wrapper over mod installer that generates configuration for game or bootfiles.
 */
public class ModInstaller : IInstaller
{
    public interface IConfig
    {
        IEnumerable<string> ExcludedFromConfig
        {
            get;
        }
    }

    public static readonly string GameSupportedModDirectory = Path.Combine("UserData", "Mods");

    private readonly IInstaller inner;
    private bool postProcessingDone;
    private readonly Matcher filesToConfigureMatcher;
    private readonly List<string> installedFiles = new();

    // FIXME how do I share the staging dir with a BaseInstaller?
    private readonly DirectoryInfo stagingDir;

    internal ModInstaller(IInstaller inner, ITempDir tempDir, IConfig config)
    {
        this.inner = inner;
        postProcessingDone = false;
        filesToConfigureMatcher = Matchers.ExcludingPatterns(config.ExcludedFromConfig);
        stagingDir = new DirectoryInfo(Path.Combine(tempDir.BasePath, inner.PackageName));
    }

    public string PackageName => inner.PackageName;

    public IInstallation.State Installed =>
        inner.Installed == IInstallation.State.Installed && !postProcessingDone
            ? IInstallation.State.PartiallyInstalled
            : inner.Installed;

    public IReadOnlyCollection<string> InstalledFiles => inner.InstalledFiles.Concat(installedFiles).ToImmutableArray();

    public int? PackageFsHash => inner.PackageFsHash;

    // TODO Generate a better name
    private string NormalisedName => string.Concat(PackageName.Where(char.IsAsciiLetterOrDigit));

    public void Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
    {
        inner.Install(dstPath, backupStrategy, callbacks);

        GenerateModConfig(dstPath);

        postProcessingDone = true;
    }

    private void GenerateModConfig(string dstPath)
    {
        var gameSupportedMod = FileEntriesToConfigure()
            .Any(p => p.StartsWith(GameSupportedModDirectory));
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
        var modConfigDirPath = new RootedPath(dstPath, Path.Combine(GameSupportedModDirectory, NormalisedName));
        Directory.CreateDirectory(modConfigDirPath.Full);
        AddToInstalledFiles(PostProcessor.AppendCrdFileEntries(modConfigDirPath, modConfig.CrdFileEntries));
        AddToInstalledFiles(PostProcessor.AppendTrdFileEntries(modConfigDirPath, modConfig.TrdFileEntries));
        AddToInstalledFiles(PostProcessor.AppendDrivelineRecords(modConfigDirPath, modConfig.DrivelineRecords));
    }

    private void AddToInstalledFiles(RootedPath? installedFile)
    {
        if (installedFile is not null)
        {
            installedFiles.Add(installedFile.Relative);
        }
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
        inner.InstalledFiles.Where(_ => filesToConfigureMatcher.Match(_).HasMatches);

    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        if (!stagingDir.Exists)
        {
            return recordBlocks;
        }

        foreach (var configFile in stagingDir.EnumerateFiles())
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
