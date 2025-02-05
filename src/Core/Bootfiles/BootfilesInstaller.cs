using System.Collections.Immutable;
using Core.Backup;
using Core.Mods;

namespace Core.Bootfiles;
public class BootfilesInstaller : IInstaller
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

    private readonly IInstaller inner;
    private readonly IEventHandler eventHandler;
    private bool postProcessingDone;

    public BootfilesInstaller(IInstaller inner, IEventHandler eventHandler)
    {
        this.inner = inner;
        this.eventHandler = eventHandler;
        postProcessingDone = false;
    }

    public string PackageName => inner.PackageName;

    public IInstallation.State Installed =>
        inner.Installed == IInstallation.State.Installed && !postProcessingDone
            ? IInstallation.State.PartiallyInstalled
            : inner.Installed;

    public IReadOnlyCollection<string> InstalledFiles => inner.InstalledFiles;

    public int? PackageFsHash => inner.PackageFsHash;

    public void Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
    {

        var modConfigs = CollectModConfigs(dstPath);
        if (modConfigs.Any(c => c.Any()))
        {
            eventHandler.PostProcessingStart();
            // TODO
            var packageNameIfNotGenerated =
                PackageName != GeneratedBootfilesInstaller.VirtualPackageName ? PackageName : null;
            eventHandler.ExtractingBootfiles(packageNameIfNotGenerated);
            inner.Install(dstPath, backupStrategy, callbacks);
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
        postProcessingDone = true;
    }

    private static IReadOnlyList<ConfigEntries> CollectModConfigs(string dstPath)
    {
        var modsGamePath = Path.Combine(dstPath, BaseInstaller.GameSupportedModDirectory);
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
