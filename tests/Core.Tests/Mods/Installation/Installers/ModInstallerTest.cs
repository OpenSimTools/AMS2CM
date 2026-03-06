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

    #region Setup

    private readonly MockFileSystem fs = new();
    private readonly Mock<ModInstaller.IConfig> configMock = new();
    private readonly Mock<IBackupStrategy> backupStrategyMock = new();
    private readonly string destDir;
    private readonly string tempDir;

    public ModInstallerTest()
    {
        configMock.Setup(c => c.DirsAtRoot).Returns([GameDirAtRoot, GameSupportedModDirectory]);
        configMock.Setup(c => c.GameSupportedModDirectory).Returns(GameSupportedModDirectory);
        configMock.Setup(c => c.GenerateModDetails).Returns(true);

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

        fs.AllFiles.Should().BeEquivalentTo(
            packageFiles.Select(f => Path.Combine(destDir, f))
        );

        modInstaller.InstalledFiles.Should().BeEquivalentTo(
            packageFiles.Select(f => new RootedPath(destDir, f))
        );

        modInstaller.PackageDependencies.Should().BeEmpty();
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

        fs.AllFiles.Should().BeEquivalentTo(
            packageFiles.Select(f => Path.Combine(destDir, f))
        );

        modInstaller.InstalledFiles.Should().BeEquivalentTo(
            packageFiles.Select(f => new RootedPath(destDir, f))
        );

        modInstaller.PackageDependencies.Should().BeEmpty();
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
            Path.Combine(GameSupportedModDirectory, "A_badcafe", BaseModInstaller.VehicleListFileName),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", BaseModInstaller.DrivelineFileName),
            Path.Combine(GameSupportedModDirectory, "A_badcafe", "A_badcafe.xml")
        };

        modInstaller.InstalledFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => new RootedPath(destDir, f))
        );

        modInstaller.PackageDependencies.Should().BeEmpty();

        fs.AllFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => Path.Combine(destDir, f))
        );
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", BaseModInstaller.VehicleListFileName))
            .TextContents.Should().Be(crdFile);
        fs.GetFile(Path.Combine(destDir, GameSupportedModDirectory, "A_badcafe", BaseModInstaller.DrivelineFileName))
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
            Path.Combine(GameSupportedModDirectory, "A_0", "vehiclelist.lst")
            // No mod xml
        ]).ToHashSet();

        fs.AllFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => Path.Combine(destDir, f))
        );

        modInstaller.InstalledFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => new RootedPath(destDir, f))
        );

        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);
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
            Path.Combine(GameSupportedModDirectory, "BeeCee_0", "tracklist.lst")
            // No mod xml
        ]).ToHashSet();

        fs.AllFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => Path.Combine(destDir, f))
        );

        modInstaller.InstalledFiles.Should().BeEquivalentTo(
            expectedFiles.Select(f => new RootedPath(destDir, f))
        );

        modInstaller.PackageDependencies.Should().ContainSingle(BootfilesPackageName);
    }

    private ModInstaller InstallWithModInstaller(IInstaller inner)
    {
        var modInstaller = new ModInstaller(fs, inner, tempDir, configMock.Object, destDir, BootfilesPackageName);
        modInstaller.Install(packagePath => new RootedPath(destDir, packagePath),
            backupStrategyMock.Object, new ProcessingCallbacks<RootedPath>());
        fs.Directory.Delete(tempDir, recursive: true);
        return modInstaller;
    }

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyCollection<string> files) =>
        InstallerOf(name, fsHash, files.ToDictionary(f => f, _ => Convert.ToString(fsHash) ?? string.Empty));

    private IInstaller InstallerOf(string name, int? fsHash, IReadOnlyDictionary<string, string> fileContents) =>
        new StaticFilesInstaller(fs, name, fsHash, fileContents, Array.Empty<string>());
}
