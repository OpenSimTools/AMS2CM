using System.IO.Abstractions.TestingHelpers;
using Core.Mods;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class BootfilesInstallerTest
{
    private const string BootfilesPackageName = "notused";
    private static readonly string FileInBootfilesPackage = Path.Combine(GameDirAtRoot, "Something");

    private const string GameDirAtRoot = "DirAtRoot";
    private const string GameSupportedModDirectory = "ModDirectory";
    private const string BootfilesVehicleListDir = "BootfilesVehicleListDir";
    private const string VehicleListFileName = "VehicleList";
    private const string BootfilesTrackListDir = "BootfilesTrackListDir";
    private const string TrackListFileName = "TrackList";
    private const string BootfilesDrivelineDir = "BootfilesDrivelineDir";
    private const string DrivelineFileName = "Driveline";

    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<BootfilesInstaller.IConfig> configMock = new();
    private readonly Mock<IBootfilesNaming> bootfilesNamingMock = new();
    private readonly Mock<BootfilesInstaller.IEventHandler> eventHandlerMock = new();
    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<Action<RootedPath>> callbackMock = new();

    private readonly string destDir;
    private readonly string tempDir;

    public BootfilesInstallerTest()
    {
        configMock.Setup(c => c.DirsAtRoot).Returns([GameDirAtRoot, GameSupportedModDirectory]);
        configMock.Setup(c => c.GameSupportedModDir).Returns(GameSupportedModDirectory);
        configMock.Setup(c => c.BootfilesVehicleListDir).Returns(BootfilesVehicleListDir);
        configMock.Setup(c => c.VehicleListFileName).Returns(VehicleListFileName);
        configMock.Setup(c => c.BootfilesTrackListDir).Returns(BootfilesTrackListDir);
        configMock.Setup(c => c.TrackListFileName).Returns(TrackListFileName);
        configMock.Setup(c => c.BootfilesDrivelineDir).Returns(BootfilesDrivelineDir);
        configMock.Setup(c => c.DrivelineFileName).Returns(DrivelineFileName);

        destDir = fs.Directory.CreateDirectory("Dest").FullName;
        tempDir = fs.Directory.CreateDirectory("Temp").FullName;
    }

    #endregion

    [Fact]
    public void BootfilesAreNotInstalledWhenNoMods()
    {
        InstallBootfiles().InstalledFiles.Should().BeEmpty();

        eventHandlerMock.Verify(m => m.PostProcessingNotRequired(), Times.Once);

        callbackMock.VerifyNoOtherCalls();

        fs.AllFiles.Should().BeEmpty();
    }

    [Fact]
    public void BootfilesAreNotInstalledForModsWithManifests()
    {
        var contents = MultiLineContents();

        fs.AddEmptyFile(Path.Combine(destDir, GameSupportedModDirectory, "ModName", "ModName.xml"));
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "ModName", VehicleListFileName), contents);
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "ModName", TrackListFileName), contents);
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "ModName", DrivelineFileName), contents);

        InstallBootfiles().InstalledFiles.Should().BeEmpty();

        eventHandlerMock.Verify(m => m.PostProcessingNotRequired(), Times.Once);

        callbackMock.VerifyNoOtherCalls();

        fs.File.Exists(Path.Combine(destDir, BootfilesVehicleListDir, VehicleListFileName)).Should().BeFalse();
        fs.File.Exists(Path.Combine(destDir, BootfilesTrackListDir, TrackListFileName)).Should().BeFalse();
        fs.File.Exists(Path.Combine(destDir, BootfilesDrivelineDir, DrivelineFileName)).Should().BeFalse();
    }

    [Fact]
    public void BootfilesAreInstalledForVehicleListWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod1", VehicleListFileName), mod1Config);
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod2", VehicleListFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesVehicleListDir, VehicleListFileName)
        );

        InstallBootfiles().InstalledFiles.Should().BeEquivalentTo(expected);

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingVehicles(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destDir, BootfilesVehicleListDir, VehicleListFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }

    [Fact]
    public void BootfilesAreInstalledForTrackListWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod1", TrackListFileName), mod1Config);
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod2", TrackListFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesTrackListDir, TrackListFileName)
        );

        InstallBootfiles().InstalledFiles.Should().BeEquivalentTo(expected);

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingTracks(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destDir, BootfilesTrackListDir, TrackListFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }

    [Fact]
    public void BootfilesAreInstalledForDrivelineWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod1", DrivelineFileName), mod1Config);
        fs.AddFile(Path.Combine(destDir, GameSupportedModDirectory, "Mod2", DrivelineFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesDrivelineDir, DrivelineFileName)
        );

        InstallBootfiles().InstalledFiles.Should().BeEquivalentTo(expected);

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingDrivelines(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destDir, BootfilesDrivelineDir, DrivelineFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }


    #region Utility

    private BootfilesInstaller InstallBootfiles()
    {
        var emptyPackage = new StaticFilesInstaller(fs, BootfilesPackageName, null,
            new Dictionary<string, string>
            {
                [FileInBootfilesPackage] = ""
            }, Array.Empty<string>());
        var bootfilesInstaller = new BootfilesInstaller(fs, emptyPackage, tempDir, configMock.Object,
            destDir, bootfilesNamingMock.Object, eventHandlerMock.Object);
        bootfilesInstaller.Install(packagePath => new RootedPath(destDir, packagePath),
            backupStrategyMock.Object, new ProcessingCallbacks<RootedPath>
            {
                Before = callbackMock.Object
            });
        fs.Directory.Delete(tempDir, recursive: true);
        return bootfilesInstaller;
    }

    private static string MultiLineContents()
    {
        var rnd = new Random();
        var options = Enumerable.Range(0, 128).Select(i => (char)i)
            .Where(char.IsAsciiLetterOrDigit).Select(c => c.ToString())
            .Append(Environment.NewLine).ToArray();
        var length = rnd.Next(10, 100);
        var randomConfig = string.Concat(Enumerable.Range(0, length).Select(_ => options[rnd.Next(options.Length)]));
        return TrimConfig(randomConfig);
    }

    private static string TrimConfig(string config) =>
        string.Join(Environment.NewLine,
            config.Split(Environment.NewLine)
                .Select(line => line.Split('#')[0].Trim())
                .Where(line => !string.IsNullOrEmpty(line)));

    private IReadOnlySet<RootedPath> DestRootedPaths(params string[] relativePaths) =>
        relativePaths.Select(f => new RootedPath(destDir, f)).ToHashSet();

    private void VerifyCallbackCalledWith(IReadOnlySet<RootedPath> relativePaths)
    {
        foreach (var rp in relativePaths)
        {
            callbackMock.Verify(a => a(rp), Times.Once);
        }
        callbackMock.VerifyNoOtherCalls();
    }

    #endregion
}
