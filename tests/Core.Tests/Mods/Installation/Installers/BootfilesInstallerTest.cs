using System.IO.Abstractions.TestingHelpers;
using Core.Mods;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Tests.Utils;
using Core.Utils;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

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
    private readonly FakeTimeProvider fakeTimeProvider = new();
    private readonly Mock<BootfilesInstaller.IConfig> configMock = new();
    private readonly Mock<IBootfilesNaming> bootfilesNamingMock = new();
    private readonly Mock<BootfilesInstaller.IEventHandler> eventHandlerMock = new();
    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<Action<RootedPath>> callbackMock = new();

    private readonly string destinationPath;
    private readonly PathRoot destinationRoot;

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

        var destDir = fs.Directory.CreateDirectory("Dest");
        destinationPath = destDir.FullName;
        destinationRoot = new PathRoot(destDir);
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

        fs.AddEmptyFile(Path.Combine(destinationPath, GameSupportedModDirectory, "ModName", "ModName.xml"));
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "ModName", VehicleListFileName), contents);
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "ModName", TrackListFileName), contents);
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "ModName", DrivelineFileName), contents);

        InstallBootfiles().InstalledFiles.Should().BeEmpty();

        eventHandlerMock.Verify(m => m.PostProcessingNotRequired(), Times.Once);

        callbackMock.VerifyNoOtherCalls();

        fs.File.Exists(Path.Combine(destinationPath, BootfilesVehicleListDir, VehicleListFileName)).Should().BeFalse();
        fs.File.Exists(Path.Combine(destinationPath, BootfilesTrackListDir, TrackListFileName)).Should().BeFalse();
        fs.File.Exists(Path.Combine(destinationPath, BootfilesDrivelineDir, DrivelineFileName)).Should().BeFalse();
    }

    [Fact]
    public void BootfilesAreInstalledForVehicleListWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod1", VehicleListFileName), mod1Config);
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod2", VehicleListFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesVehicleListDir, VehicleListFileName)
        );

        var bootfiles = InstallBootfiles();
        bootfiles.InstalledFiles.Should().BeEquivalentPathTo(expected);
        bootfiles.PackageVersionHash.Should().BeNull();

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingVehicles(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destinationPath, BootfilesVehicleListDir, VehicleListFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }

    [Fact]
    public void BootfilesAreInstalledForTrackListWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod1", TrackListFileName), mod1Config);
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod2", TrackListFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesTrackListDir, TrackListFileName)
        );

        InstallBootfiles().InstalledFiles.Should().BeEquivalentPathTo(expected);

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingTracks(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destinationPath, BootfilesTrackListDir, TrackListFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }

    [Fact]
    public void BootfilesAreInstalledForDrivelineWithoutModManifest()
    {
        var mod1Config = MultiLineContents();
        var mod2Config = MultiLineContents();

        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod1", DrivelineFileName), mod1Config);
        fs.AddFile(Path.Combine(destinationPath, GameSupportedModDirectory, "Mod2", DrivelineFileName), mod2Config);

        var expected = DestRootedPaths(
            FileInBootfilesPackage,
            Path.Combine(BootfilesDrivelineDir, DrivelineFileName)
        );

        InstallBootfiles().InstalledFiles.Should().BeEquivalentPathTo(expected);

        eventHandlerMock.Verify(m => m.PostProcessingStart(), Times.Once);
        eventHandlerMock.Verify(m => m.ExtractingBootfiles(null), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingDrivelines(), Times.Once);
        eventHandlerMock.Verify(m => m.PostProcessingEnd(), Times.Once);
        eventHandlerMock.VerifyNoOtherCalls();

        VerifyCallbackCalledWith(expected);

        TrimConfig(fs.File.ReadAllText(Path.Combine(destinationPath, BootfilesDrivelineDir, DrivelineFileName)))
            .Should().Be(mod1Config + Environment.NewLine + mod2Config);
    }


    #region Utility Methods

    private BootfilesInstaller InstallBootfiles()
    {
        var tempDir = fs.Directory.CreateDirectory("Temp");
        var emptyPackage = new StaticFilesInstaller(fs, fakeTimeProvider, BootfilesPackageName, 42,
            new Dictionary<string, string> { [FileInBootfilesPackage] = "" }, Array.Empty<string>());
        var bootfilesInstaller = new BootfilesInstaller(emptyPackage, tempDir, configMock.Object,
            fs.DirectoryInfo.New(destinationPath), bootfilesNamingMock.Object, eventHandlerMock.Object);
        bootfilesInstaller.Install(destinationRoot.Combine,
            backupStrategyMock.Object, new ProcessingCallbacks<RootedPath> { Before = callbackMock.Object });
        tempDir.Delete(recursive: true);
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
        relativePaths.Select(destinationRoot.Combine).ToHashSet();

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
