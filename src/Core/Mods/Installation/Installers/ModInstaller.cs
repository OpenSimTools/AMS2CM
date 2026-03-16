using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Installers;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Mods.Installation.Installers;

/**
 * Wrapper over mod installer that generates configuration for game or bootfiles.
 */
public class ModInstaller : BaseModInstaller
{
    public new interface IConfig : BaseModInstaller.IConfig
    {
        IEnumerable<string> ExcludedFromConfig { get; }
        bool GenerateModDetails { get; }
    }

    private readonly Matcher filesToConfigureMatcher;
    private readonly bool generateModDetails;

    private IReadOnlyCollection<string> bootfilesDependency = Array.Empty<string>();
    private readonly string bootfilesPackageName;

    private RootedPath modConfigPath;
    private string modName;

    internal ModInstaller(IInstaller inner, string tempDir, IConfig config,
        string gameInstallationDir, string bootfilesPackageName) :
        this(new FileSystem(), inner, tempDir, config, gameInstallationDir, bootfilesPackageName)
    {
    }

    internal ModInstaller(IFileSystem fileSystem, IInstaller inner, string tempDir, IConfig config,
        string gameInstallationDir, string bootfilesPackageName) :
        base(fileSystem, inner, tempDir, config)
    {
        this.bootfilesPackageName = bootfilesPackageName;
        filesToConfigureMatcher = Matchers.ExcludingPatterns(config.ExcludedFromConfig);
        generateModDetails = config.GenerateModDetails;

        var normalisedName = string.Concat(
            Path.GetFileNameWithoutExtension(inner.PackageName)
                .Where(char.IsAsciiLetterOrDigit));
        var hexFsHash = (inner.PackageFsHash ?? 0).ToString("x");
        modName = $"{normalisedName}_{hexFsHash}";

        modConfigPath = new RootedPath(gameInstallationDir, Path.Combine(GameSupportedModRelativeDir, modName));
    }

    public override IReadOnlySet<string> PackageDependencies =>
        Inner.PackageDependencies.Concat(bootfilesDependency).ToImmutableHashSet();

    protected override RootedPath VehicleListDir => modConfigPath;

    protected override RootedPath TrackListDir => modConfigPath;

    protected override RootedPath DrivelineDir => modConfigPath;

    protected override void Install(Action innerInstall, ProcessingCallbacks<RootedPath> callbacks)
    {
        innerInstall();

        var gameSupportedMod = FileEntriesToConfigure()
            .Any(p => p.StartsWith(GameSupportedModRelativeDir));
        var modConfig = gameSupportedMod
            ? ConfigEntries.Empty
            : new ConfigEntries(FindCrdFileEntries(), FindTrdFileEntries(), FindDrivelineRecords());

        if (modConfig.None())
        {
            return;
        }

        AppendCrdFileEntries(modConfig.CrdFileEntries, callbacks);
        AppendTrdFileEntries(modConfig.TrdFileEntries, callbacks);
        InsertDrivelineRecords(modConfig.DrivelineRecords, callbacks);
        if (generateModDetails && !modConfig.TrdFileEntries.Any())
        {
            SafeWriteAllText(modConfigPath.SubPath($"{modName}.xml"), ModManifest, callbacks);
        }
        else
        {
            bootfilesDependency = new[] { bootfilesPackageName };
        }
    }

    private List<string> FindCrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".crd"))
            .ToList();

    private List<string> FindTrdFileEntries() =>
        FileEntriesToConfigure()
            .Where(p => p.EndsWith(".trd"))
            .Select(fp => $"{Path.GetDirectoryName(fp)}{Path.DirectorySeparatorChar}@{Path.GetFileName(fp)}")
            .ToList();

    private IEnumerable<string> FileEntriesToConfigure() =>
        Inner.InstalledFiles
            .Select(rp => rp.Relative)
            .Where(p => filesToConfigureMatcher.Match(p).HasMatches);

    private List<string> FindDrivelineRecords()
    {
        var recordBlocks = new List<string>();
        if (!FileSystem.Directory.Exists(StagingFullPath))
        {
            return recordBlocks;
        }

        foreach (var configFile in FileSystem.Directory.EnumerateFiles(StagingFullPath))
        {
            var recordIndent = -1;
            var recordLines = new List<string>();
            foreach (var line in FileSystem.File.ReadAllLines(configFile))
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

    public string ModManifest =>
        @$"<?xml version=""1.0""?>
<Reflection>
    <class name=""BRTTIRefCount"" base=""root class"" />
    <class name=""BPersistent"" base=""BRTTIRefCount"">
        <prop name=""Name"" type=""String"" />
    </class>
    <class name=""ModDetails"" base=""BPersistent"">
        <prop name=""DisplayName"" type=""String"" />
    </class>
    <data class=""ModDetails"" id=""0x{PackageFsHash:x08}"">
        <prop name=""Name"" data=""{modName}"" />
        <prop name=""DisplayName"" data=""{PackageName}"" />
    </data>
</Reflection>";
}
