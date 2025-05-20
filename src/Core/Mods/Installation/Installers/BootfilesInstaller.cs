using System.Collections.Immutable;
using Core.Games;
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

    private const string GeneratedBootfilesPackageName = $"{ModPackagesesUpdater.BootfilesPrefix}_generated";

    internal const string VehicleListRelativeDir = "vehicles";
    internal static readonly string TrackListRelativeDir = Path.Combine("tracks", "_data");
    internal static readonly string DrivelineRelativeDir = Path.Combine(VehicleListRelativeDir, "physics", "driveline");

    private readonly IEventHandler eventHandler;

    public BootfilesInstaller(IInstaller? bootfilesPackageInstaller,  IGame game, ITempDir tempDir, IEventHandler eventHandler, IConfig config) :
        base(PackageOrGenerated(bootfilesPackageInstaller, game, tempDir), game, tempDir, config)
    {
        this.eventHandler = eventHandler;
    }

    private static IInstaller PackageOrGenerated(IInstaller? bootfilesPackageInstaller, IGame game, ITempDir tempDir) =>
        bootfilesPackageInstaller ?? new GeneratedBootfilesInstaller(GeneratedBootfilesPackageName, game, tempDir);

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
            PostProcessor.AppendCrdFileEntries(new RootedPath(Game.InstallationDirectory, VehicleListRelativeDir),
                modConfigs.SelectMany(c => c.CrdFileEntries), WrapInComments);
            eventHandler.PostProcessingTracks();
            PostProcessor.AppendTrdFileEntries(new RootedPath(Game.InstallationDirectory, TrackListRelativeDir),
                modConfigs.SelectMany(c => c.TrdFileEntries), WrapInComments);
            eventHandler.PostProcessingDrivelines();
            PostProcessor.AppendDrivelineRecords(new RootedPath(Game.InstallationDirectory, DrivelineRelativeDir),
                modConfigs.SelectMany(c => c.DrivelineRecords), WrapInComments);
            eventHandler.PostProcessingEnd();
        }
        else
        {
            eventHandler.PostProcessingNotRequired();
        }
    }

    private static string WrapInComments(string content)
    {
        return $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{content}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";
    }

    private IReadOnlyList<ConfigEntries> CollectModConfigs()
    {
        var modsGamePath = Path.Combine(Game.InstallationDirectory, PostProcessor.GameSupportedModDirectory);
        var directoryInfo = new DirectoryInfo(modsGamePath);
        if (!directoryInfo.Exists)
            return Array.Empty<ConfigEntries>();
        return directoryInfo.GetDirectories("*").Select(modDir =>
            modDir.EnumerateFiles($"{modDir.Name}.xml").Any() ?
                ConfigEntries.Empty :
                new ConfigEntries
                (
                    FileLinesOrEmpty(modDir, PostProcessor.VehicleListFileName),
                    FileLinesOrEmpty(modDir, PostProcessor.TrackListFileName),
                    FileLinesOrEmpty(modDir, PostProcessor.DrivelineFileName)
                )
        ).ToImmutableList();
    }

    private static string[] FileLinesOrEmpty(DirectoryInfo parent, string fileName)
    {
        var filePath = Path.Combine(parent.FullName, fileName);
        return File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();
    }
}
