using System.Collections.Immutable;
using System.IO.Abstractions;
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
        void PostProcessingNotRequired();
        void PostProcessingStart();
        void ExtractingBootfiles(string? packageName);
        void PostProcessingVehicles();
        void PostProcessingTracks();
        void PostProcessingDrivelines();
        void PostProcessingEnd();
    }

    private readonly RootedPath gameInstallationPath;
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly IEventHandler eventHandler;

    public BootfilesInstaller(IInstaller? bootfilesPackageInstaller, string tempDir, IConfig config,
        string gameInstallationDir, IBootfilesNaming bootfilesNaming, IEventHandler eventHandler) :
        this(new FileSystem(), bootfilesPackageInstaller, tempDir, config,
            gameInstallationDir, bootfilesNaming, eventHandler)
    {
    }

    public BootfilesInstaller(IFileSystem fileSystem, IInstaller? bootfilesPackageInstaller, string tempDir,
        IConfig config, string gameInstallationDir, IBootfilesNaming bootfilesNaming, IEventHandler eventHandler) :
        base(fileSystem, PackageOrGenerated(bootfilesPackageInstaller, gameInstallationDir, tempDir, bootfilesNaming),
            tempDir, config)
    {
        gameInstallationPath = new RootedPath(gameInstallationDir);
        VehicleListDir = gameInstallationPath.SubPath(config.BootfilesVehicleListDir);
        TrackListDir = gameInstallationPath.SubPath(config.BootfilesTrackListDir);
        DrivelineDir = gameInstallationPath.SubPath(config.BootfilesDrivelineDir);
        this.bootfilesNaming = bootfilesNaming;
        this.eventHandler = eventHandler;
    }

    private static IInstaller PackageOrGenerated(IInstaller? bootfilesPackageInstaller,
        string gameInstallationDirectory, string tempDir, IBootfilesNaming bootfilesNaming) =>
        bootfilesPackageInstaller ?? new GeneratedBootfilesInstaller(bootfilesNaming.GeneratedBootfilesName,
            gameInstallationDirectory, tempDir);

    // Bootfiles cannot have dependencies.
    public override IReadOnlySet<string> PackageDependencies => ImmutableHashSet<string>.Empty;

    protected override void Install(Action innerInstall, ProcessingCallbacks<RootedPath> callbacks)
    {
        var modConfigs = CollectModConfig();
        if (modConfigs.None())
        {
            eventHandler.PostProcessingNotRequired();
            return;
        }

        eventHandler.PostProcessingStart();
        var packageNameIfNotGenerated = bootfilesNaming.IsGeneratedBootfiles(PackageName) ? PackageName : null;
        eventHandler.ExtractingBootfiles(packageNameIfNotGenerated);

        innerInstall();

        if (modConfigs.CrdFileEntries.Count > 0)
        {
            eventHandler.PostProcessingVehicles();
            AppendCrdFileEntries(modConfigs.CrdFileEntries, callbacks);
        }
        if (modConfigs.TrdFileEntries.Count > 0)
        {
            eventHandler.PostProcessingTracks();
            AppendTrdFileEntries(modConfigs.TrdFileEntries, callbacks);
        }
        if (modConfigs.DrivelineRecords.Count > 0)
        {
            eventHandler.PostProcessingDrivelines();
            InsertDrivelineRecords(modConfigs.DrivelineRecords, callbacks);
        }
        eventHandler.PostProcessingEnd();
    }

    protected override RootedPath VehicleListDir { get; }

    protected override RootedPath TrackListDir { get; }

    protected override RootedPath DrivelineDir { get; }

    protected override string WrapConfigBlock(string configBlock) =>
        $"{Environment.NewLine}### BEGIN AMS2CM{Environment.NewLine}{configBlock}{Environment.NewLine}### END AMS2CM{Environment.NewLine}";

    private ConfigEntries CollectModConfig()
    {
        var modsGamePath = gameInstallationPath.SubPath(GameSupportedModRelativeDir);
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
