using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public class BootfilesInstaller : BaseModInstaller
{
    public interface IEventHandler
    {
        void PostProcessingNotRequired();
        void PostProcessingStart();
        void ExtractingBootfiles(string? packageName);
        void PostProcessingVehicles();
        void PostProcessingTracks();
        void PostProcessingDrivelines();
        void PostProcessingEnd();
    }

    private const string GeneratedBootfilesPackageName = $"{ModInstallerFactory.BootfilesPrefix}_generated";

    internal const string VehicleListRelativeDir = "vehicles";
    internal static readonly string TrackListRelativeDir = Path.Combine("tracks", "_data");
    internal static readonly string DrivelineRelativeDir = Path.Combine(VehicleListRelativeDir, "physics", "driveline");

    private readonly RootedPath gameInstallationPath;
    private readonly IEventHandler eventHandler;

    public BootfilesInstaller(IInstaller? bootfilesPackageInstaller, string tempDir, IConfig config,
        string gameInstallationDir, IEventHandler eventHandler) :
        this(new FileSystem(), bootfilesPackageInstaller, tempDir, config, gameInstallationDir, eventHandler)
    {
    }

    public BootfilesInstaller(IFileSystem fileSystem, IInstaller? bootfilesPackageInstaller,
        string tempDir, IConfig config, string gameInstallationDir, IEventHandler eventHandler) :
        base(fileSystem, PackageOrGenerated(bootfilesPackageInstaller, gameInstallationDir, tempDir), tempDir, config)
    {
        gameInstallationPath = new RootedPath(gameInstallationDir);
        this.eventHandler = eventHandler;
    }

    private static IInstaller PackageOrGenerated(IInstaller? bootfilesPackageInstaller,
        string gameInstallationDirectory, string tempDir) =>
        bootfilesPackageInstaller ?? new GeneratedBootfilesInstaller(GeneratedBootfilesPackageName,
            gameInstallationDirectory, tempDir);

    // Bootfiles cannot have dependencies.
    public override IReadOnlyCollection<string> PackageDependencies => Array.Empty<string>();

    protected override void Install(Action innerInstall)
    {
        var modConfigs = CollectModConfigs();
        if (modConfigs.Any(c => c.Any()))
        {
            eventHandler.PostProcessingStart();
            var packageNameIfNotGenerated = PackageName != GeneratedBootfilesPackageName ? PackageName : null;
            eventHandler.ExtractingBootfiles(packageNameIfNotGenerated);
            innerInstall();
            eventHandler.PostProcessingVehicles();
            AppendCrdFileEntries(modConfigs.SelectMany(c => c.CrdFileEntries));
            eventHandler.PostProcessingTracks();
            AppendTrdFileEntries(modConfigs.SelectMany(c => c.TrdFileEntries));
            eventHandler.PostProcessingDrivelines();
            AppendDrivelineRecords(modConfigs.SelectMany(c => c.DrivelineRecords));
            eventHandler.PostProcessingEnd();
        }
        else
        {
            eventHandler.PostProcessingNotRequired();
        }
    }

    protected override RootedPath VehicleListDir => gameInstallationPath.SubPath(VehicleListRelativeDir);

    protected override RootedPath TrackListDir => gameInstallationPath.SubPath(TrackListRelativeDir);

    protected override RootedPath DrivelineDir => gameInstallationPath.SubPath(DrivelineRelativeDir);

    protected override string WrapConfigBlock(string configBlock) =>
        $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{configBlock}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";

    private IReadOnlyList<ConfigEntries> CollectModConfigs()
    {
        var modsGamePath = gameInstallationPath.SubPath(GameSupportedModDirectory);
        var directoryInfo = new DirectoryInfo(modsGamePath.Full);
        if (!directoryInfo.Exists)
            return Array.Empty<ConfigEntries>();
        return directoryInfo.GetDirectories("*").Select(modDir =>
            modDir.EnumerateFiles($"{modDir.Name}.xml").Any() ?
                ConfigEntries.Empty :
                new ConfigEntries
                (
                    FileLinesOrEmpty(modDir, VehicleListFileName),
                    FileLinesOrEmpty(modDir, TrackListFileName),
                    FileLinesOrEmpty(modDir, DrivelineFileName)
                )
        ).ToImmutableList();
    }

    private static string[] FileLinesOrEmpty(DirectoryInfo parent, string fileName)
    {
        var filePath = Path.Combine(parent.FullName, fileName);
        return File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();
    }
}
