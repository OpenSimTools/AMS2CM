using System.IO.Abstractions.TestingHelpers;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Tests.Packages.Installation.Installers;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class ModInstallerTest
{
    private const string GameSupportedModDirectory = "ModDirectory";
    private const string GameDirAtRoot = "DirAtRoot";
    private const string BootfilesPackageName = "BootFilesPackage";
    private const string VehicleListFile = "vehiclelist.lst";
    private const string TrackListFile = "tracklist.lst";
    private const string DrivelineFile = "driveline.rg";

    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<ModInstaller.IConfig> configMock = new();
    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly Mock<Action<RootedPath>> callbackMock = new();

    private readonly string destDir;
    private readonly string tempDir;

    public ModInstallerTest()
    {
        configMock.Setup(c => c.DirsAtRoot).Returns([GameDirAtRoot, GameSupportedModDirectory]);
        configMock.Setup(c => c.GameSupportedModDir).Returns(GameSupportedModDirectory);
        configMock.Setup(c => c.GenerateModDetails).Returns(true);
        configMock.Setup(c => c.VehicleListFileName).Returns(VehicleListFile);
        configMock.Setup(c => c.TrackListFileName).Returns(TrackListFile);
        configMock.Setup(c => c.DrivelineFileName).Returns(DrivelineFile);

        destDir = fs.Directory.CreateDirectory("Dest").FullName;
        tempDir = fs.Directory.CreateDirectory("Temp").FullName;
    }

    #endregion

    [Fact]
    public void AutomaticModConfigurationSkippedWhenNothingToConfigure()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "NotRequiringConfiguration"),
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        modInstaller.InstalledFiles.Should().BeEquivalentTo(ToDestRootedPath(packageFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(packageFiles);

        fs.AllFiles.Should().BeEquivalentTo(ToDestPath(packageFiles));
    }

    [Fact]
    public void AutomaticModConfigurationSkippedWhenConfiguredByPackage()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.crd"),
            // This disables configuration
            Path.Combine(GameSupportedModDirectory, "Anything")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        modInstaller.InstalledFiles.Should().BeEquivalentTo(ToDestRootedPath(packageFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(packageFiles);

        fs.AllFiles.Should().BeEquivalentTo(ToDestPath(packageFiles));
    }

    [Fact]
    public void AutomaticModConfigurationForVehicles()
    {
        var crdFile = Path.Combine(GameDirAtRoot, "File.crd");
        var drivelineRecord = $"RECORD Something{Environment.NewLine}";

        var packageFiles = new Dictionary<string, string>
        {
            [Path.Combine("SubDir", crdFile)] = "Anything",
            ["FileAtRoot.txt"] = drivelineRecord
        };

        var modInstaller = InstallWithModInstaller(InstallerOf("A", 0xbadcafe, packageFiles));

        string[] expectedFiles = {
            crdFile,
            Path.Combine(GameSupportedModDirectory, "A_badcafe", VehicleListFile),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", DrivelineFile),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", "A_badcafe.xml")
        };

        modInstaller.InstalledFiles.Should().BeEquivalentTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().BeEmpty();

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentTo(ToDestPath(expectedFiles));
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", VehicleListFile))
            .TextContents.Should().Be(crdFile);
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", DrivelineFile))
            .TextContents.Should().Be(drivelineRecord.Trim());
    }

    [Fact]
    public void AutomaticModConfigurationCanBeDisabled()
    {
        configMock.Setup(c => c.GenerateModDetails).Returns(false);

        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.crd")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("A", null, packageFiles));

        var expectedFiles = packageFiles.Concat([
            Path.Combine(GameSupportedModDirectory, "A_0", VehicleListFile)
            // No mod xml
        ]).ToArray();

        modInstaller.InstalledFiles.Should().BeEquivalentTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentTo(ToDestPath(expectedFiles));
    }

    [Fact]
    public void AutomaticModConfigurationNotPossibleForTracks()
    {
        string[] packageFiles =
        [
            Path.Combine(GameDirAtRoot, "File.trd")
        ];

        var modInstaller = InstallWithModInstaller(InstallerOf("Bee Cee", null, packageFiles));

        var expectedFiles = packageFiles.Concat([
            Path.Combine(GameSupportedModDirectory, "BeeCee_0", TrackListFile)
            // No mod xml
        ]).ToArray();

        modInstaller.InstalledFiles.Should().BeEquivalentTo(ToDestRootedPath(expectedFiles));
        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);

        VerifyCallbackCalledWith(expectedFiles);

        fs.AllFiles.Should().BeEquivalentTo(ToDestPath(expectedFiles));
    }


    #region Utility

    private ModInstaller InstallWithModInstaller(IInstaller inner)
    {
        var modInstaller = new ModInstaller(fs, inner, tempDir, configMock.Object, destDir, BootfilesPackageName);
        modInstaller.Install(packagePath => new RootedPath(destDir, packagePath),
            backupStrategyMock.Object, new ProcessingCallbacks<RootedPath>
            {
                Before = callbackMock.Object
            });
        fs.Directory.Delete(tempDir, recursive: true);
        return modInstaller;
    }

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files) =>
        InstallerOf(name, fsHash, files.ToDictionary(f => f, _ => Convert.ToString(fsHash) ?? string.Empty));

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyDictionary<string, string> fileContents) =>
        new StaticFilesInstaller(fs, name, fsHash, fileContents, Array.Empty<string>());

    private IReadOnlySet<string> ToDestPath(IReadOnlyCollection<string> relativePaths) =>
        relativePaths.Select(f => Path.Combine(destDir, f)).ToHashSet();

    private IReadOnlySet<RootedPath> ToDestRootedPath(IReadOnlyCollection<string> relativePaths) =>
        relativePaths.Select(f => new RootedPath(destDir, f)).ToHashSet();

    private void VerifyCallbackCalledWith(IReadOnlyCollection<string> relativePaths)
    {
        foreach (var rp in ToDestRootedPath(relativePaths))
        {
            callbackMock.Verify(a => a(rp), Times.Once);
        }
        callbackMock.VerifyNoOtherCalls();
    }

    #endregion
}
