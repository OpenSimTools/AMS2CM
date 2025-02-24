using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
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

    internal const string VehicleListRelativeDir = "vehicles";
    internal static readonly string TrackListRelativeDir = Path.Combine("tracks", "_data");
    internal static readonly string DrivelineRelativeDir = Path.Combine("vehicles", "physics", "driveline");

    private readonly IEventHandler eventHandler;

    public BootfilesInstaller(IInstaller inner, ITempDir tempDir, IEventHandler eventHandler) :
        base(inner, tempDir)
    {
        this.eventHandler = eventHandler;
    }

    protected override void Install(string dstPath, Action innerInstall)
    {
        var modConfigs = CollectModConfigs(dstPath);
        if (modConfigs.Any(c => c.Any()))
        {
            eventHandler.PostProcessingStart();
            // TODO
            var packageNameIfNotGenerated =
                PackageName != GeneratedBootfilesInstaller.VirtualPackageName ? PackageName : null;
            eventHandler.ExtractingBootfiles(packageNameIfNotGenerated);
            innerInstall();
            eventHandler.PostProcessingVehicles();
            PostProcessor.AppendCrdFileEntries(new RootedPath(dstPath, VehicleListRelativeDir),
                modConfigs.SelectMany(c => c.CrdFileEntries));
            eventHandler.PostProcessingTracks();
            PostProcessor.AppendTrdFileEntries(new RootedPath(dstPath, TrackListRelativeDir),
                modConfigs.SelectMany(c => c.TrdFileEntries));
            eventHandler.PostProcessingDrivelines();
            PostProcessor.AppendDrivelineRecords(new RootedPath(dstPath, DrivelineRelativeDir),
                modConfigs.SelectMany(c => c.DrivelineRecords));
            eventHandler.PostProcessingEnd();
        }
        else
        {
            eventHandler.PostProcessingNotRequired();
        }
    }

    private static IReadOnlyList<ConfigEntries> CollectModConfigs(string dstPath)
    {
        var modsGamePath = Path.Combine(dstPath, PostProcessor.GameSupportedModDirectory);
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
