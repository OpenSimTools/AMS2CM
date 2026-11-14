using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public class BootfilesInstaller : BaseModInstaller
{
    public new interface IConfig : BaseModInstaller.IConfig
    {
        string BootfilesVehicleListDir { get; }
        string BootfilesTrackListDir { get; }
        string BootfilesDrivelineDir { get; }
    }

    public interface IEventHandler
    {
        void ExtractingBootfiles(string? packageName);
        void PostProcessingVehicles();
        void PostProcessingTracks();
        void PostProcessingDrivelines();
    }

    private readonly PathRoot gameRoot;
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly IEventHandler eventHandler;

    public BootfilesInstaller(IPackageInstaller bootfilesPackageInstaller, IDirectoryInfo tempDir,
        IConfig config, IDirectoryInfo gameInstallationDir, IBootfilesNaming bootfilesNaming,
        IEventHandler eventHandler) :
        base(bootfilesPackageInstaller, tempDir, config)
    {
        gameRoot = new PathRoot(gameInstallationDir);
        VehicleListDir = gameRoot.Combine(config.BootfilesVehicleListDir);
        TrackListDir = gameRoot.Combine(config.BootfilesTrackListDir);
        DrivelineDir = gameRoot.Combine(config.BootfilesDrivelineDir);
        this.bootfilesNaming = bootfilesNaming;
        this.eventHandler = eventHandler;
    }

    // Bootfiles cannot have dependencies.
    public override IReadOnlySet<string> PackageDependencies => ImmutableHashSet<string>.Empty;

    protected override void Install(Action innerInstall, IBackupStrategy backupStrategy,
        ProcessingCallbacks<RootedPath> callbacks)
    {
        var modConfigs = CollectModConfig();
        var packageNameIfNotGenerated = bootfilesNaming.IsGeneratedBootfiles(PackageName) ? null : PackageName;
        eventHandler.ExtractingBootfiles(packageNameIfNotGenerated);

        innerInstall();

        if (modConfigs.CrdFileEntries.Count > 0)
        {
            eventHandler.PostProcessingVehicles();
            AppendCrdFileEntries(modConfigs.CrdFileEntries, backupStrategy, callbacks);
        }

        if (modConfigs.TrdFileEntries.Count > 0)
        {
            eventHandler.PostProcessingTracks();
            AppendTrdFileEntries(modConfigs.TrdFileEntries, backupStrategy, callbacks);
        }

        if (modConfigs.DrivelineRecords.Count > 0)
        {
            eventHandler.PostProcessingDrivelines();
            InsertDrivelineRecords(modConfigs.DrivelineRecords, backupStrategy, callbacks);
        }
    }

    protected override RootedPath VehicleListDir { get; }

    protected override RootedPath TrackListDir { get; }

    protected override RootedPath DrivelineDir { get; }

    protected override string WrapConfigBlock(string configBlock) =>
        $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{configBlock}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";

    private ConfigEntries CollectModConfig()
    {
        var modsGamePath = gameRoot.Combine(GameSupportedModRelativeDir);
        var directoryInfo = FileSystem.DirectoryInfo.New(modsGamePath.Full);
        if (!directoryInfo.Exists)
            return ConfigEntries.Empty;

        return directoryInfo.GetDirectories("*").Select(modDir =>
            modDir.EnumerateFiles($"{modDir.Name}.xml").Any()
                ? ConfigEntries.Empty
                : new ConfigEntries
                (
                    FileLinesOrEmpty(modDir, VehicleListFileName),
                    FileLinesOrEmpty(modDir, TrackListFileName),
                    FileLinesOrEmpty(modDir, DrivelineFileName)
                )
        ).Aggregate(ConfigEntries.Empty, ConfigEntries.Combine);
    }

    private string[] FileLinesOrEmpty(IDirectoryInfo parent, string fileName)
    {
        var filePath = Path.Combine(parent.FullName, fileName);
        return FileSystem.File.Exists(filePath) ? FileSystem.File.ReadAllLines(filePath) : Array.Empty<string>();
    }
}
